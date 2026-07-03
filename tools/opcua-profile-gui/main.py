"""OPCUAProfilexmlGUI — PySide6 GUI for picking OPC UA nodes and generating
an MTConnect profile YAML + device.xml.

Phase 1 scope (this build):
- Connection dialog (endpoint URL + optional root NodeId, anonymous auth).
- Eager browse of the address space on connect, including a one-shot read of
  each Variable's current value + StatusCode + friendly DataType name.
- Tree view: BrowseName | NodeClass | DataType | Status | Value, with tri-state
  checkboxes; ticking a folder cascades to its leaves.
- "Refresh values" button — re-reads every Variable's current value without
  re-walking the address space.
- Filter box that hides non-matching rows as you type.
- Selected panel: a QTableWidget showing every ticked Variable, with a live
  Value column updated via an OPC UA subscription opened when the row is
  added and closed when it's removed.
- Description (when populated) is surfaced as a tooltip on the BrowseName cell.
- Generate button writes <name>.yaml + <name>.device.xml to a chosen folder.

Client lifetime: connect once on user action and stay connected for the life
of the window (closes on closeEvent). The subscription used for live values is
created on first need and reused.
"""

from __future__ import annotations

import asyncio
import socket
import sys
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any

import qasync
from asyncua import Client, ua
from PySide6.QtCore import QSortFilterProxyModel, Qt, QTimer, Signal
from PySide6.QtGui import QBrush, QColor, QStandardItem, QStandardItemModel
from PySide6.QtWidgets import (
    QApplication,
    QCheckBox,
    QComboBox,
    QCompleter,
    QDialog,
    QDialogButtonBox,
    QFileDialog,
    QFormLayout,
    QHBoxLayout,
    QHeaderView,
    QLabel,
    QLineEdit,
    QMainWindow,
    QMessageBox,
    QProgressDialog,
    QPushButton,
    QSplitter,
    QTableWidget,
    QTableWidgetItem,
    QTreeView,
    QVBoxLayout,
    QWidget,
)

from generators import (
    SelectedVariable,
    build_device_xml,
    build_profile_yaml,
    infer_kind,
    kind_to_category,
    slugify,
    suggest_data_item_key,
)
from mtconnect_vocab import (
    domain_for_type,
    lookup_type,
    suggest_type,
    suggest_units,
    types_for_category,
)
from mtconnect_archetypes import (
    ARCHETYPES,
    GENERIC,
    MachineArchetype,
    archetype_by_name,
)

# ----------------------------------------------------------------------------
# Data model
# ----------------------------------------------------------------------------


@dataclass
class BrowsedNode:
    """One node discovered during the address-space walk."""

    node_id: str
    browse_name: str
    node_class: ua.NodeClass
    parent_browse_name: str
    data_type_name: str       # friendly name when resolvable, else NodeId string
    description: str          # may be empty
    current_value: str        # stringified snapshot; "" for non-Variables
    current_status: str       # "Good" / "Bad: <message>" / "" if not read
    children: list["BrowsedNode"]


# Custom item-data roles for stashing BrowsedNode on QStandardItems
ROLE_NODE = Qt.UserRole + 1
ROLE_KIND = Qt.UserRole + 2  # "sample" or "event"; only set on Variable rows


# Column indices in the tree model
COL_NAME = 0
COL_NODECLASS = 1
COL_DATATYPE = 2
COL_STATUS = 3
COL_VALUE = 4

# Column indices in the Selected panel table
SEL_COL_KIND = 0
SEL_COL_PARENT = 1
SEL_COL_NAME = 2
SEL_COL_VALUE = 3


# ----------------------------------------------------------------------------
# OPC UA browse + value read
# ----------------------------------------------------------------------------


def _format_value(value: Any) -> str:
    if value is None:
        return ""
    if isinstance(value, (list, tuple)):
        return f"[array len={len(value)}]"
    return str(value)


def _stringify_status(status: ua.StatusCode | None) -> str:
    if status is None:
        return ""
    try:
        if status.is_good():
            return "Good"
        return f"Bad: {status.name}"
    except Exception:
        return str(status)


async def _read_friendly_datatype(client: Client, datatype_node_id: ua.NodeId) -> str:
    """Resolve a DataType NodeId to its BrowseName (e.g. 'Int16', 'Boolean')."""
    try:
        dt_node = client.get_node(datatype_node_id)
        bn = await dt_node.read_browse_name()
        return bn.Name
    except Exception:
        return datatype_node_id.to_string()


async def browse_subtree(
    client: Client,
    root_node_id: str | None,
    max_depth: int,
    progress: QProgressDialog | None = None,
) -> BrowsedNode:
    """Eager-walk the address space from root to max_depth, capturing values
    and friendly DataType names for every Variable.
    """
    root = client.get_node(root_node_id) if root_node_id else client.get_objects_node()
    root_bn = (await root.read_browse_name()).Name
    root_nc = await root.read_node_class()
    discovered = BrowsedNode(
        node_id=root.nodeid.to_string(),
        browse_name=root_bn,
        node_class=root_nc,
        parent_browse_name="",
        data_type_name="",
        description="",
        current_value="",
        current_status="",
        children=[],
    )

    visited: set[str] = {discovered.node_id}
    count_ref = [0]
    await _walk(client, root, discovered, depth=1, max_depth=max_depth, visited=visited, progress=progress, count_ref=count_ref)
    return discovered


async def _walk(
    client: Client,
    node,
    sink: BrowsedNode,
    depth: int,
    max_depth: int,
    visited: set[str],
    progress: QProgressDialog | None,
    count_ref: list[int],
) -> None:
    if depth > max_depth:
        return
    try:
        children = await node.get_children()
    except Exception:
        return

    new_children = []
    for child in children:
        child_id = child.nodeid.to_string()
        if child_id in visited:
            continue
        visited.add(child_id)
        new_children.append((child_id, child))

    # Fetch every child's attributes concurrently — one level at a time — instead
    # of ~5 sequential round-trips per node (which made large PLCs take minutes).
    described = await asyncio.gather(
        *(_describe_node(client, child) for _, child in new_children)
    )

    for (child_id, child), desc in zip(new_children, described):
        if desc is None:
            continue
        child_bn, child_nc, data_type_name, current_value, current_status, description = desc

        bn = BrowsedNode(
            node_id=child_id,
            browse_name=child_bn or child_id,
            node_class=child_nc,
            parent_browse_name=sink.browse_name,
            data_type_name=data_type_name,
            description=description,
            current_value=current_value,
            current_status=current_status,
            children=[],
        )
        sink.children.append(bn)
        count_ref[0] += 1
        if progress is not None and count_ref[0] % 25 == 0:
            progress.setLabelText(f"Walked {count_ref[0]} nodes...")

        if child_nc in (ua.NodeClass.Object, ua.NodeClass.Variable, ua.NodeClass.ObjectType, ua.NodeClass.VariableType):
            await _walk(client, child, bn, depth + 1, max_depth, visited, progress, count_ref)


async def _describe_node(client: Client, child) -> tuple | None:
    """Read one node's identity/value attributes; returns None if unreadable."""
    try:
        child_bn = (await child.read_browse_name()).Name
        child_nc = await child.read_node_class()
    except Exception:
        return None

    data_type_name = ""
    current_value = ""
    current_status = ""
    description = ""

    if child_nc == ua.NodeClass.Variable:
        try:
            dt_nodeid = await child.read_data_type()
            data_type_name = await _read_friendly_datatype(client, dt_nodeid)
        except Exception:
            pass
        try:
            dv = await child.read_data_value()
            current_value = _format_value(dv.Value.Value if dv.Value is not None else None)
            current_status = _stringify_status(dv.StatusCode)
        except Exception as exc:
            current_status = f"Bad: read failed ({type(exc).__name__})"

    try:
        d = await child.read_description()
        if d and d.Text:
            description = d.Text
    except Exception:
        pass

    return child_bn, child_nc, data_type_name, current_value, current_status, description


# ----------------------------------------------------------------------------
# Connection dialog
# ----------------------------------------------------------------------------


_ENDPOINT_TIP = (
    "Full opc.tcp:// URL of the OPC UA server (host + port + optional path).\n\n"
    "Examples:\n"
    "  opc.tcp://192.168.0.1:4840                        — Siemens S7-1500 (default port)\n"
    "  opc.tcp://192.168.1.50:4840                       — Sinumerik 840D\n"
    "  opc.tcp://localhost:53530/OPCUA/SimulationServer  — Prosys sim server\n\n"
    "Port 4840 is the OPC UA standard; vendors sometimes use a different port — check the controller's docs."
)

_ROOT_TIP = (
    "Optional starting point for the browse. Leave blank to start at the Objects folder (server root).\n\n"
    "Use a specific NodeId to skip server-infrastructure nodes (Server, ServerCapabilities, ServerDiagnostics)\n"
    "and target a subtree directly:\n\n"
    "  ns=3;s=PLC               — S7-1500 PLC root (Siemens namespace)\n"
    "  ns=3;s=DataBlocksGlobal  — just the user data blocks on an S7\n"
    "  ns=2;s=DeviceSet         — Device Integration namespace (most vendors)\n\n"
    "If you don't know the right NodeId, leave blank — you'll see the full tree and can copy a NodeId from\n"
    "any interesting parent to use next time."
)

_DEPTH_TIP = (
    "How deep to recurse when walking the address space.\n\n"
    "6 is enough for most servers:\n"
    "  - S7-1500 DBs sit 3–4 levels under PLC_1\n"
    "  - Sinumerik channels are similar\n"
    "  - Prosys sim server is shallow (2–3)\n\n"
    "Increase to 8–10 if you can't see expected nodes (deeply nested structs).\n"
    "Decrease to 3–4 for very large servers if the initial browse is taking too long."
)

_CONNECT_HELP = (
    "After connect, the tool eagerly walks the address space and reads each Variable's current value "
    "once. For a typical PLC with ~1,500 nodes this takes 5–15 seconds. The Refresh values button "
    "re-reads without re-walking; ticking an item opens a live subscription for that single node."
)

_SECURITY_POLICY_TIP = (
    "OPC UA channel encryption + signing policy.\n\n"
    "  None              — no encryption, no signing. Use only on trusted networks (factory VLANs).\n"
    "  Basic256Sha256    — AES-256 channel encryption + SHA-256 signing. Required by most production\n"
    "                       servers (Sinumerik with security enabled, properly-configured Fanuc/Heidenhain).\n\n"
    "When set to a non-None policy, the tool generates a self-signed client certificate on first use\n"
    "(stored under .venv/pki/ in this project folder). The server will reject this cert the first time —\n"
    "you must accept it on the server side (in TIA Portal: PLC Properties → OPC UA → Server → Certificate\n"
    "management; move from rejected store to trusted store)."
)

_SECURITY_MODE_TIP = (
    "How much protection is applied to messages over the channel.\n\n"
    "  Sign              — messages signed but not encrypted (integrity, no confidentiality).\n"
    "  SignAndEncrypt    — both signed and encrypted. Default and the common choice.\n\n"
    "Ignored when security policy is None."
)

_IDENTITY_TIP = (
    "How this client authenticates to the server.\n\n"
    "  Anonymous         — no credentials. Works only if the server allows it.\n"
    "  Username/Password — most common in production. Credentials are sent over the channel; use\n"
    "                       a non-None security policy to encrypt them in transit."
)


@dataclass
class ConnectParams:
    endpoint: str
    root: str | None
    depth: int
    security_policy: str        # "None" or "Basic256Sha256"
    security_mode: str          # "Sign" or "SignAndEncrypt" (ignored if policy=None)
    identity_mode: str          # "Anonymous" or "Username"
    username: str
    password: str
    archetype_name: str         # name from mtconnect_archetypes.ARCHETYPES; "Generic (no filter)" if not narrowed


_ARCHETYPE_TIP = (
    "Equipment class this server is. Used to:\n\n"
    "  - Filter the MTConnect type dropdown to the ~30–60 types typical for this class\n"
    "    (toggle 'Show all spec types' to override).\n"
    "  - Bias the BrowseName → type autocomplete toward whitelisted types so e.g.\n"
    "    'TEMPERATURE' on a Coolant Unit goes to TEMPERATURE rather than PROCESS_TIMER.\n\n"
    "Pick Generic if you don't know yet, want to see all 259 spec types, or are\n"
    "forwarding from a mixed-equipment server."
)


class ConnectDialog(QDialog):
    def __init__(self, parent=None) -> None:
        super().__init__(parent)
        self.setWindowTitle("Connect to OPC UA Server")
        self.setMinimumWidth(620)

        self.endpoint_edit = QLineEdit("opc.tcp://192.168.0.1:4840")
        self.endpoint_edit.setToolTip(_ENDPOINT_TIP)
        self.root_edit = QLineEdit("ns=3;s=PLC")
        self.root_edit.setPlaceholderText("(leave blank to start at Objects folder)")
        self.root_edit.setToolTip(_ROOT_TIP)
        self.depth_edit = QLineEdit("6")
        self.depth_edit.setToolTip(_DEPTH_TIP)

        self.security_policy_combo = QComboBox()
        self.security_policy_combo.addItems(["None", "Basic256Sha256"])
        self.security_policy_combo.setToolTip(_SECURITY_POLICY_TIP)
        self.security_policy_combo.currentTextChanged.connect(self._on_security_policy_changed)

        self.security_mode_combo = QComboBox()
        self.security_mode_combo.addItems(["SignAndEncrypt", "Sign"])
        self.security_mode_combo.setToolTip(_SECURITY_MODE_TIP)

        self.identity_combo = QComboBox()
        self.identity_combo.addItems(["Anonymous", "Username/Password"])
        self.identity_combo.setToolTip(_IDENTITY_TIP)
        self.identity_combo.currentTextChanged.connect(self._on_identity_changed)

        self.username_edit = QLineEdit()
        self.username_edit.setPlaceholderText("(server username)")
        self.password_edit = QLineEdit()
        self.password_edit.setEchoMode(QLineEdit.Password)
        self.password_edit.setPlaceholderText("(server password)")

        self.archetype_combo = QComboBox()
        for a in ARCHETYPES:
            self.archetype_combo.addItem(a.name, a.description)
        self.archetype_combo.setToolTip(_ARCHETYPE_TIP)
        # Default to Generic so first-time users aren't surprised by a filtered dropdown.
        self.archetype_combo.setCurrentText(GENERIC.name)

        form = QFormLayout()
        endpoint_label = QLabel("Endpoint URL:")
        endpoint_label.setToolTip(_ENDPOINT_TIP)
        root_label = QLabel("Root NodeId (optional):")
        root_label.setToolTip(_ROOT_TIP)
        depth_label = QLabel("Max browse depth:")
        depth_label.setToolTip(_DEPTH_TIP)
        sec_policy_label = QLabel("Security policy:")
        sec_policy_label.setToolTip(_SECURITY_POLICY_TIP)
        sec_mode_label = QLabel("Security mode:")
        sec_mode_label.setToolTip(_SECURITY_MODE_TIP)
        identity_label = QLabel("Identity:")
        identity_label.setToolTip(_IDENTITY_TIP)
        archetype_label = QLabel("Machine type:")
        archetype_label.setToolTip(_ARCHETYPE_TIP)

        form.addRow(endpoint_label, self.endpoint_edit)
        form.addRow(root_label, self.root_edit)
        form.addRow(depth_label, self.depth_edit)
        form.addRow(sec_policy_label, self.security_policy_combo)
        form.addRow(sec_mode_label, self.security_mode_combo)
        form.addRow(identity_label, self.identity_combo)
        self._username_label = QLabel("Username:")
        self._password_label = QLabel("Password:")
        form.addRow(self._username_label, self.username_edit)
        form.addRow(self._password_label, self.password_edit)
        form.addRow(archetype_label, self.archetype_combo)

        # Defaults match what the original anonymous + no-security flow did.
        self._on_security_policy_changed("None")
        self._on_identity_changed("Anonymous")

        help_label = QLabel(_CONNECT_HELP)
        help_label.setWordWrap(True)
        help_label.setStyleSheet(
            "QLabel { background-color: palette(alternate-base); padding: 8px; "
            "border: 1px solid palette(mid); border-radius: 4px; color: palette(window-text); }"
        )

        buttons = QDialogButtonBox(QDialogButtonBox.Ok | QDialogButtonBox.Cancel)
        buttons.accepted.connect(self.accept)
        buttons.rejected.connect(self.reject)

        layout = QVBoxLayout(self)
        layout.addLayout(form)
        layout.addWidget(help_label)
        layout.addStretch()
        layout.addWidget(buttons)

    def _on_security_policy_changed(self, policy: str) -> None:
        secured = policy != "None"
        self.security_mode_combo.setEnabled(secured)

    def _on_identity_changed(self, mode: str) -> None:
        is_user = mode == "Username/Password"
        self._username_label.setVisible(is_user)
        self.username_edit.setVisible(is_user)
        self._password_label.setVisible(is_user)
        self.password_edit.setVisible(is_user)

    def values(self) -> ConnectParams:
        try:
            depth = int(self.depth_edit.text().strip())
        except ValueError:
            depth = 6
        identity_raw = self.identity_combo.currentText()
        identity_mode = "Username" if identity_raw.startswith("Username") else "Anonymous"
        return ConnectParams(
            endpoint=self.endpoint_edit.text().strip(),
            root=self.root_edit.text().strip() or None,
            depth=depth,
            security_policy=self.security_policy_combo.currentText(),
            security_mode=self.security_mode_combo.currentText(),
            identity_mode=identity_mode,
            username=self.username_edit.text(),
            password=self.password_edit.text(),
            archetype_name=self.archetype_combo.currentText(),
        )


# ----------------------------------------------------------------------------
# Certificates management dialog
# ----------------------------------------------------------------------------


_CERT_HELP = (
    "OPC UA channel security uses an X.509 client certificate. The first time you connect with a "
    "non-None security policy, the server will reject our certificate — that's expected. Find the "
    "rejected cert on the server side and move it to the trusted store, then reconnect.\n\n"
    "  Siemens S7-1500 (TIA Portal):  PLC properties → OPC UA → Server → Security → Certificates →\n"
    "                                  move from rejected store to trusted clients store.\n"
    "  Sinumerik (NCU):               OPC UA service config → Certificate management → trust client.\n"
    "  Prosys Simulation Server:      pops up a dialog on first connect — click Accept.\n\n"
    "Regenerating the cert (e.g. after a hostname change) means the server has to re-accept the new "
    "one. Import an existing cert + key if you have a corporate-PKI-issued client identity."
)


class CertificatesDialog(QDialog):
    def __init__(self, parent=None) -> None:
        super().__init__(parent)
        self.setWindowTitle("OPC UA Client Certificate")
        self.setMinimumWidth(700)

        self._location_label = QLabel()
        self._location_label.setWordWrap(True)
        self._location_label.setTextInteractionFlags(Qt.TextSelectableByMouse)
        self._subject_label = QLabel()
        self._subject_label.setWordWrap(True)
        self._subject_label.setTextInteractionFlags(Qt.TextSelectableByMouse)
        self._issuer_label = QLabel()
        self._issuer_label.setWordWrap(True)
        self._issuer_label.setTextInteractionFlags(Qt.TextSelectableByMouse)
        self._validity_label = QLabel()
        self._validity_label.setTextInteractionFlags(Qt.TextSelectableByMouse)

        info_form = QFormLayout()
        info_form.addRow("File:", self._location_label)
        info_form.addRow("Subject:", self._subject_label)
        info_form.addRow("Issuer:", self._issuer_label)
        info_form.addRow("Validity:", self._validity_label)

        open_folder_button = QPushButton("Open folder in Explorer")
        open_folder_button.clicked.connect(self._on_open_folder)
        regen_button = QPushButton("Regenerate (auto-signed)")
        regen_button.clicked.connect(self._on_regenerate)
        import_button = QPushButton("Import existing cert + key...")
        import_button.clicked.connect(self._on_import)

        actions = QHBoxLayout()
        actions.addWidget(open_folder_button)
        actions.addWidget(regen_button)
        actions.addWidget(import_button)
        actions.addStretch()

        help_label = QLabel(_CERT_HELP)
        help_label.setWordWrap(True)
        help_label.setStyleSheet(
            "QLabel { background-color: palette(alternate-base); padding: 8px; "
            "border: 1px solid palette(mid); border-radius: 4px; color: palette(window-text); }"
        )

        buttons = QDialogButtonBox(QDialogButtonBox.Close)
        buttons.rejected.connect(self.reject)
        buttons.accepted.connect(self.accept)

        layout = QVBoxLayout(self)
        layout.addLayout(info_form)
        layout.addLayout(actions)
        layout.addWidget(help_label)
        layout.addStretch()
        layout.addWidget(buttons)

        self._refresh()

    def _refresh(self) -> None:
        cert_path, key_path = pki_cert_paths()
        self._location_label.setText(str(cert_path))
        if not cert_path.exists():
            self._subject_label.setText("[no certificate yet — will be generated on first secured connect]")
            self._issuer_label.setText("—")
            self._validity_label.setText("—")
            return
        info = read_cert_info(cert_path)
        if "error" in info:
            self._subject_label.setText(f"(failed to read: {info['error']})")
            self._issuer_label.setText("—")
            self._validity_label.setText("—")
            return
        self._subject_label.setText(info.get("subject", "—"))
        self._issuer_label.setText(info.get("issuer", "—"))
        days = info.get("days_remaining", "?")
        self._validity_label.setText(
            f"{info.get('valid_from', '?')}  →  {info.get('valid_to', '?')}  ({days} days remaining)"
        )

    def _on_open_folder(self) -> None:
        import subprocess
        path = pki_dir()
        try:
            if sys.platform == "win32":
                subprocess.Popen(["explorer", str(path)])
            else:
                subprocess.Popen(["xdg-open", str(path)])
        except Exception as exc:
            QMessageBox.warning(self, "Couldn't open folder", str(exc))

    def _on_regenerate(self) -> None:
        cert_path, key_path = pki_cert_paths()
        if cert_path.exists():
            reply = QMessageBox.question(
                self,
                "Regenerate client certificate?",
                "A certificate already exists. Regenerating creates a new one — any server that "
                "previously accepted this client will reject the new cert and you'll need to "
                "re-accept it on the server side.\n\nProceed?",
                QMessageBox.Yes | QMessageBox.No,
                QMessageBox.No,
            )
            if reply != QMessageBox.Yes:
                return
        try:
            _generate_cert_sync(cert_path, key_path)
            self._refresh()
            QMessageBox.information(self, "Regenerated", f"New cert + key written to:\n\n  {pki_dir()}")
        except Exception as exc:
            QMessageBox.critical(self, "Regeneration failed", str(exc))

    def _on_import(self) -> None:
        cert_src, _ = QFileDialog.getOpenFileName(
            self, "Choose client certificate (DER)", "",
            "DER certificate (*.der *.crt *.cer);;All files (*)",
        )
        if not cert_src:
            return
        key_src, _ = QFileDialog.getOpenFileName(
            self, "Choose private key (PEM)", "",
            "PEM private key (*.pem *.key);;All files (*)",
        )
        if not key_src:
            return
        cert_dest, key_dest = pki_cert_paths()
        try:
            cert_dest.write_bytes(Path(cert_src).read_bytes())
            key_dest.write_bytes(Path(key_src).read_bytes())
            self._refresh()
            QMessageBox.information(
                self,
                "Imported",
                f"Cert copied to:\n  {cert_dest}\nKey copied to:\n  {key_dest}\n\n"
                "These will be used on the next secured connect.",
            )
        except Exception as exc:
            QMessageBox.critical(self, "Import failed", str(exc))


# ----------------------------------------------------------------------------
# Tree population + collection
# ----------------------------------------------------------------------------


def _status_brush(status: str) -> QBrush | None:
    if status.startswith("Bad"):
        return QBrush(QColor(180, 80, 80))
    if status == "Good":
        return QBrush(QColor(80, 140, 80))
    return None


def populate_tree(model: QStandardItemModel, root: BrowsedNode) -> None:
    """Fill the QStandardItemModel from a BrowsedNode tree, attaching checkboxes."""
    model.clear()
    model.setHorizontalHeaderLabels(["BrowseName", "NodeClass", "DataType", "Status", "Value"])

    def add(parent_item: QStandardItem | None, node: BrowsedNode) -> None:
        bn_item = QStandardItem(node.browse_name)
        bn_item.setData(node, ROLE_NODE)
        bn_item.setEditable(False)
        if node.description:
            bn_item.setToolTip(node.description)
        if node.node_class == ua.NodeClass.Variable:
            bn_item.setCheckable(True)
            bn_item.setCheckState(Qt.Unchecked)
            bn_item.setData(infer_kind(node.data_type_name), ROLE_KIND)
        else:
            bn_item.setCheckable(True)
            bn_item.setAutoTristate(True)
            bn_item.setCheckState(Qt.Unchecked)

        nc_item = QStandardItem(node.node_class.name)
        nc_item.setEditable(False)
        dt_item = QStandardItem(node.data_type_name)
        dt_item.setEditable(False)
        status_item = QStandardItem(node.current_status)
        status_item.setEditable(False)
        brush = _status_brush(node.current_status)
        if brush is not None:
            status_item.setForeground(brush)
        value_item = QStandardItem(node.current_value)
        value_item.setEditable(False)

        row = [bn_item, nc_item, dt_item, status_item, value_item]
        if parent_item is None:
            model.appendRow(row)
        else:
            parent_item.appendRow(row)

        for child in node.children:
            add(bn_item, child)

    add(None, root)


def collect_checked(model: QStandardItemModel) -> list[SelectedVariable]:
    """Walk the model, return SelectedVariable for every Variable row whose
    checkbox is Checked."""
    out: list[SelectedVariable] = []

    def visit(item: QStandardItem) -> None:
        if item.checkState() == Qt.Checked and item.data(ROLE_KIND) is not None:
            node: BrowsedNode = item.data(ROLE_NODE)
            kind = item.data(ROLE_KIND) or infer_kind(node.data_type_name)
            out.append(
                SelectedVariable(
                    node_id=node.node_id,
                    browse_name=node.browse_name,
                    parent_browse_name=node.parent_browse_name,
                    data_type_name=node.data_type_name,
                    kind=kind,
                    data_item_key=suggest_data_item_key(node.browse_name),
                )
            )
        for r in range(item.rowCount()):
            visit(item.child(r, 0))

    for r in range(model.rowCount()):
        visit(model.item(r, 0))

    return out


def iter_variable_items(model: QStandardItemModel):
    """Yield (item, BrowsedNode) tuples for every Variable row in the model."""
    def visit(item: QStandardItem):
        node = item.data(ROLE_NODE)
        if isinstance(node, BrowsedNode) and node.node_class == ua.NodeClass.Variable:
            yield item, node
        for r in range(item.rowCount()):
            yield from visit(item.child(r, 0))

    for r in range(model.rowCount()):
        yield from visit(model.item(r, 0))


# ----------------------------------------------------------------------------
# Filter proxy
# ----------------------------------------------------------------------------


class TreeFilterProxy(QSortFilterProxyModel):
    def __init__(self, parent=None) -> None:
        super().__init__(parent)
        self.setRecursiveFilteringEnabled(True)
        self.setFilterCaseSensitivity(Qt.CaseInsensitive)


# ----------------------------------------------------------------------------
# Data-change handler for live subscriptions
# ----------------------------------------------------------------------------


class _DataChangeHandler:
    """asyncua subscription handler. With qasync the callback runs on the Qt main
    thread, so direct widget updates are safe."""

    def __init__(self, signal_emitter):
        self._signal_emitter = signal_emitter

    def datachange_notification(self, node, val, data):
        try:
            nid = node.nodeid.to_string()
        except Exception:
            return
        status = ""
        try:
            status = _stringify_status(data.monitored_item.Value.StatusCode)
        except Exception:
            pass
        self._signal_emitter.value_changed.emit(nid, _format_value(val), status)


class _SignalEmitter(QWidget):
    """Tiny QObject wrapping a single signal so the data handler can post to Qt safely."""
    value_changed = Signal(str, str, str)  # node_id, value_str, status_str


# ----------------------------------------------------------------------------
# Self-signed certificate generation (needed for non-None security policies)
# ----------------------------------------------------------------------------


def _generate_cert_sync(cert_path: Path, key_path: Path) -> None:
    """Generate a self-signed x509 cert + RSA key for the OPC UA client.

    Synchronous (runs on a thread-pool executor). RSA key generation can take
    a few seconds; keeps the Qt event loop responsive while it runs.
    """
    from cryptography import x509
    from cryptography.hazmat.primitives import hashes, serialization
    from cryptography.hazmat.primitives.asymmetric import rsa
    from cryptography.x509.oid import ExtendedKeyUsageOID, NameOID

    key = rsa.generate_private_key(public_exponent=65537, key_size=2048)

    hostname = socket.gethostname()
    app_uri = f"urn:{hostname}:OPCUAProfilexmlGUI"
    subject = issuer = x509.Name([
        x509.NameAttribute(NameOID.COMMON_NAME, "OPCUAProfilexmlGUI"),
        x509.NameAttribute(NameOID.ORGANIZATION_NAME, "OPCUAProfilexmlGUI"),
    ])

    cert = (
        x509.CertificateBuilder()
        .subject_name(subject)
        .issuer_name(issuer)
        .public_key(key.public_key())
        .serial_number(x509.random_serial_number())
        .not_valid_before(datetime.now(timezone.utc))
        .not_valid_after(datetime.now(timezone.utc) + timedelta(days=3650))
        .add_extension(
            x509.SubjectAlternativeName([
                x509.UniformResourceIdentifier(app_uri),
                x509.DNSName(hostname),
            ]),
            critical=False,
        )
        .add_extension(x509.BasicConstraints(ca=False, path_length=None), critical=True)
        .add_extension(
            x509.KeyUsage(
                digital_signature=True,
                content_commitment=False,
                key_encipherment=True,
                data_encipherment=True,
                key_agreement=False,
                key_cert_sign=False,
                crl_sign=False,
                encipher_only=False,
                decipher_only=False,
            ),
            critical=True,
        )
        .add_extension(x509.ExtendedKeyUsage([ExtendedKeyUsageOID.CLIENT_AUTH]), critical=False)
        .sign(key, hashes.SHA256())
    )

    cert_path.write_bytes(cert.public_bytes(serialization.Encoding.DER))
    key_path.write_bytes(
        key.private_bytes(
            encoding=serialization.Encoding.PEM,
            format=serialization.PrivateFormat.TraditionalOpenSSL,
            encryption_algorithm=serialization.NoEncryption(),
        )
    )


async def _generate_self_signed_cert(cert_path: Path, key_path: Path) -> None:
    """Async wrapper that runs the cert generation on a thread-pool executor."""
    loop = asyncio.get_event_loop()
    await loop.run_in_executor(None, _generate_cert_sync, cert_path, key_path)


# ----------------------------------------------------------------------------
# Cert location + introspection
# ----------------------------------------------------------------------------


def _app_dir() -> Path:
    """Where 'alongside the program' resolves at runtime.

    When running from source (dev): the folder containing this file.
    When running from a PyInstaller-frozen bundle: the folder containing the
    .exe (not the temporary _internal / _MEIPASS extraction dir). This means
    pki/, generated/, etc. sit next to the exe wherever the operator dropped
    it — no hardcoded paths, no permissions-in-Program-Files headaches.
    """
    if getattr(sys, "frozen", False):
        return Path(sys.executable).parent
    return Path(__file__).parent


def pki_dir() -> Path:
    """Local pki/ folder for the client cert + key. Sits alongside the
    program (main.py in dev, the .exe when frozen) so re-creating the venv
    or moving the built folder never wipes the cert and forces a re-accept
    on the server side."""
    d = _app_dir() / "pki"
    d.mkdir(parents=True, exist_ok=True)
    return d


def pki_cert_paths() -> tuple[Path, Path]:
    base = pki_dir()
    return base / "client_cert.der", base / "client_priv.pem"


def read_cert_info(cert_path: Path) -> dict[str, str]:
    """Best-effort: return {subject, valid_from, valid_to, days_remaining} for a DER cert.
    Returns {} on any error."""
    try:
        from cryptography import x509
        cert_bytes = cert_path.read_bytes()
        cert = x509.load_der_x509_certificate(cert_bytes)
        days = (cert.not_valid_after_utc - datetime.now(timezone.utc)).days
        return {
            "subject": cert.subject.rfc4514_string(),
            "issuer": cert.issuer.rfc4514_string(),
            "valid_from": cert.not_valid_before_utc.strftime("%Y-%m-%d %H:%M UTC"),
            "valid_to": cert.not_valid_after_utc.strftime("%Y-%m-%d %H:%M UTC"),
            "days_remaining": str(days),
        }
    except Exception as exc:
        return {"error": f"{type(exc).__name__}: {exc}"}


# ----------------------------------------------------------------------------
# Main window
# ----------------------------------------------------------------------------


class MainWindow(QMainWindow):
    def __init__(self) -> None:
        super().__init__()
        self.setWindowTitle("OPCUA Profile XML — MTConnect profile generator")
        self.resize(1400, 850)

        self.endpoint_url: str = ""
        self.root_node_id: str = ""
        self.client: Client | None = None
        self.subscription = None
        self.live_handles: dict[str, Any] = {}            # node_id -> subscribe handle
        self.selected_rows: dict[str, int] = {}           # node_id -> row index in selected_table
        self.selected_vars: dict[str, SelectedVariable] = {}  # node_id -> persisted edits
        self.archetype: MachineArchetype = GENERIC        # picked on Connect; narrows the type editor
        self.show_all_types: bool = False                 # bypass archetype filter
        self.group_by_domain: bool = False                # restructure type dropdown
        self.value_signal = _SignalEmitter(self)
        self.value_signal.value_changed.connect(self._on_live_value)
        self.handler = _DataChangeHandler(self.value_signal)
        self._editor_loading = False  # suppress write-back while populating editor

        # ---- top bar ----
        self.status_label = QLabel("Not connected.")
        self.filter_edit = QLineEdit()
        self.filter_edit.setPlaceholderText("Filter (browse-name contains, case-insensitive)...")
        self.filter_edit.textChanged.connect(self._on_filter_changed)

        connect_button = QPushButton("Connect...")
        connect_button.clicked.connect(self._open_connect_dialog)
        self.refresh_button = QPushButton("Refresh values")
        self.refresh_button.setEnabled(False)
        self.refresh_button.clicked.connect(self._on_refresh_values)
        certs_button = QPushButton("Certificates...")
        certs_button.clicked.connect(self._open_certificates_dialog)
        generate_button = QPushButton("Generate YAML + device.xml...")
        generate_button.clicked.connect(self._on_generate)

        top_bar = QHBoxLayout()
        top_bar.addWidget(connect_button)
        top_bar.addWidget(self.refresh_button)
        top_bar.addWidget(certs_button)
        top_bar.addWidget(self.status_label, stretch=1)
        top_bar.addWidget(QLabel("Filter:"))
        top_bar.addWidget(self.filter_edit, stretch=2)
        top_bar.addWidget(generate_button)

        # ---- tree ----
        self.tree_model = QStandardItemModel(self)
        self.tree_model.itemChanged.connect(self._on_item_changed)
        self.proxy_model = TreeFilterProxy(self)
        self.proxy_model.setSourceModel(self.tree_model)

        self.tree_view = QTreeView()
        self.tree_view.setModel(self.proxy_model)
        self.tree_view.setSortingEnabled(False)
        self.tree_view.setAlternatingRowColors(True)
        header = self.tree_view.header()
        header.setSectionResizeMode(COL_NAME, QHeaderView.Stretch)
        header.setSectionResizeMode(COL_NODECLASS, QHeaderView.ResizeToContents)
        header.setSectionResizeMode(COL_DATATYPE, QHeaderView.ResizeToContents)
        header.setSectionResizeMode(COL_STATUS, QHeaderView.ResizeToContents)
        header.setSectionResizeMode(COL_VALUE, QHeaderView.ResizeToContents)

        # ---- selected table ----
        self.selected_table = QTableWidget()
        self.selected_table.setColumnCount(4)
        self.selected_table.setHorizontalHeaderLabels(["Kind", "Parent", "BrowseName", "Live Value"])
        self.selected_table.horizontalHeader().setSectionResizeMode(SEL_COL_KIND, QHeaderView.ResizeToContents)
        self.selected_table.horizontalHeader().setSectionResizeMode(SEL_COL_PARENT, QHeaderView.ResizeToContents)
        self.selected_table.horizontalHeader().setSectionResizeMode(SEL_COL_NAME, QHeaderView.Stretch)
        self.selected_table.horizontalHeader().setSectionResizeMode(SEL_COL_VALUE, QHeaderView.ResizeToContents)
        self.selected_table.verticalHeader().setVisible(False)
        self.selected_table.setEditTriggers(QTableWidget.NoEditTriggers)
        self.selected_table.setAlternatingRowColors(True)

        self.selected_count_label = QLabel("0 selected")
        self.selected_table.currentItemChanged.connect(self._on_selected_row_changed)

        # ---- MTConnect type editor panel ----
        editor_panel = QWidget()
        editor_layout = QFormLayout(editor_panel)
        editor_layout.setContentsMargins(0, 4, 0, 0)
        self._editor_label = QLabel("(select a row above to edit MTConnect metadata)")
        self._editor_label.setWordWrap(True)
        self._editor_label.setStyleSheet("QLabel { font-style: italic; color: palette(mid); }")
        editor_layout.addRow(self._editor_label)

        self.kind_combo = QComboBox()
        self.kind_combo.addItems(["sample", "event", "condition"])
        self.kind_combo.currentTextChanged.connect(self._on_editor_kind_changed)
        self.kind_combo.setToolTip(
            "MTConnect category:\n"
            "  sample    — continuous numeric (POSITION, LOAD, TEMPERATURE, ...)\n"
            "  event     — discrete value or string (PROGRAM, EXECUTION, AVAILABILITY, ...)\n"
            "  condition — diagnostic state (Normal / Warning / Fault / Unavailable). Heads up:\n"
            "              v1 of MTConnect.NET-AgentModule-OpcUaAdapter doesn't currently drive\n"
            "              CONDITION states from OPC UA values — picked condition items will land\n"
            "              in the device.xml correctly but stay UNAVAILABLE at runtime."
        )

        self.type_combo = QComboBox()
        self.type_combo.setEditable(True)
        self.type_combo.currentTextChanged.connect(self._on_editor_type_changed)
        self.type_combo.setToolTip(
            "MTConnect data-item type from the spec vocabulary (PATH_FEEDRATE_OVERRIDE, POSITION, "
            "LOAD, etc.). Auto-suggested from the BrowseName. Start typing to filter (search-as-you-type "
            "via QCompleter); pick VARIABLE as a generic fallback if nothing fits."
        )
        # Substring filter via QCompleter
        self.type_completer = QCompleter(self.type_combo)
        self.type_completer.setCaseSensitivity(Qt.CaseInsensitive)
        self.type_completer.setFilterMode(Qt.MatchContains)
        self.type_completer.setCompletionMode(QCompleter.PopupCompletion)
        self.type_combo.setCompleter(self.type_completer)

        self.units_combo = QComboBox()
        self.units_combo.setEditable(True)
        self.units_combo.currentTextChanged.connect(self._on_editor_units_changed)
        self.units_combo.setToolTip(
            "Units for SAMPLE types (MILLIMETER, DEGREE, PERCENT, etc.). Required by the MTConnect "
            "spec on SAMPLE data items. Auto-suggested per type; override if your source already "
            "converts to a different unit."
        )

        self.subtype_combo = QComboBox()
        self.subtype_combo.setEditable(True)
        self.subtype_combo.currentTextChanged.connect(self._on_editor_subtype_changed)
        self.subtype_combo.setToolTip(
            "Optional subType (ACTUAL / COMMANDED / TARGET / etc.). Leave blank if the type doesn't "
            "define one or you don't care."
        )

        self.data_item_key_edit = QLineEdit()
        self.data_item_key_edit.textChanged.connect(self._on_editor_data_item_key_changed)
        self.data_item_key_edit.setToolTip(
            "MTConnect DataItem id used in the generated device.xml and as the routing key when the "
            "OpcUaAdapter pushes observations into the agent buffer."
        )

        editor_layout.addRow("Kind:", self.kind_combo)
        editor_layout.addRow("MTConnect type:", self.type_combo)
        editor_layout.addRow("Units:", self.units_combo)
        editor_layout.addRow("subType:", self.subtype_combo)
        editor_layout.addRow("DataItem id:", self.data_item_key_edit)

        # Two toggles affecting what the type dropdown shows.
        self.show_all_types_check = QCheckBox("Show all spec types (override archetype filter)")
        self.show_all_types_check.setToolTip(
            "When unchecked, the type dropdown shows only the types whitelisted by the machine "
            "archetype picked on Connect. Check to see all 259 MTConnect spec types regardless."
        )
        self.show_all_types_check.toggled.connect(self._on_show_all_toggled)
        editor_layout.addRow(self.show_all_types_check)

        self.group_by_domain_check = QCheckBox("Group by domain (Motion / Process / Electrical / ...)")
        self.group_by_domain_check.setToolTip(
            "Restructure the type dropdown into named domain sections (Motion, Process variables, "
            "Electrical, Controller, Tool, Production, etc.). Headers are disabled markers; pick a "
            "type underneath. Useful when scrolling through dozens of types."
        )
        self.group_by_domain_check.toggled.connect(self._on_group_by_domain_toggled)
        editor_layout.addRow(self.group_by_domain_check)

        reset_button = QPushButton("Reset to auto-suggested")
        reset_button.clicked.connect(self._on_editor_reset)
        editor_layout.addRow(reset_button)

        self._set_editor_enabled(False)

        right_panel = QWidget()
        right_layout = QVBoxLayout(right_panel)
        right_layout.setContentsMargins(0, 0, 0, 0)
        right_layout.addWidget(QLabel("Selected (live):"))
        right_layout.addWidget(self.selected_table)
        right_layout.addWidget(self.selected_count_label)
        right_layout.addWidget(editor_panel)

        splitter = QSplitter(Qt.Horizontal)
        splitter.addWidget(self.tree_view)
        splitter.addWidget(right_panel)
        splitter.setStretchFactor(0, 3)
        splitter.setStretchFactor(1, 2)

        central = QWidget()
        central_layout = QVBoxLayout(central)
        central_layout.addLayout(top_bar)
        central_layout.addWidget(splitter, stretch=1)
        self.setCentralWidget(central)

        # debounce timer for selection diffing
        self._diff_timer = QTimer(self)
        self._diff_timer.setSingleShot(True)
        self._diff_timer.setInterval(120)
        self._diff_timer.timeout.connect(self._diff_selection)

    # ------------------------------------------------------------------
    # Connection / browse
    # ------------------------------------------------------------------
    def _open_connect_dialog(self) -> None:
        dlg = ConnectDialog(self)
        if dlg.exec() == QDialog.Accepted:
            params = dlg.values()
            # If the user picked a secured policy AND we don't have a cert yet,
            # show the Certificates dialog first so they know what's happening
            # and where the cert lives.
            if params.security_policy != "None":
                cert_path, _ = pki_cert_paths()
                if not cert_path.exists():
                    QMessageBox.information(
                        self,
                        "Client certificate will be generated",
                        "This is the first secured connect, so a self-signed client certificate is "
                        "about to be generated.\n\n"
                        "After it's created, the server will reject our cert the first time — "
                        "open Certificates... to see exactly where the cert is stored, then accept "
                        "it on the server side and reconnect.",
                    )
            asyncio.ensure_future(self._connect_and_browse(params))

    def _open_certificates_dialog(self) -> None:
        dlg = CertificatesDialog(self)
        dlg.exec()

    async def _connect_and_browse(self, params: ConnectParams) -> None:
        # If we have an existing client, disconnect it cleanly first.
        await self._disconnect_client_if_any()

        progress = QProgressDialog("Connecting...", "Cancel", 0, 0, self)
        progress.setWindowTitle("OPC UA browse")
        progress.setWindowModality(Qt.WindowModal)
        progress.show()
        client = Client(url=params.endpoint)
        client.session_timeout = 30000

        # Identity (must be set before .connect())
        if params.identity_mode == "Username":
            client.set_user(params.username)
            client.set_password(params.password)

        try:
            # Security policy
            if params.security_policy != "None":
                cert_path, key_path = pki_cert_paths()
                if not cert_path.exists() or not key_path.exists():
                    await _generate_self_signed_cert(cert_path, key_path)
                sec_string = f"{params.security_policy},{params.security_mode},{cert_path},{key_path}"
                await client.set_security_string(sec_string)

            await client.connect()
            progress.setLabelText("Browsing address space...")
            tree = await browse_subtree(client, params.root, max_depth=params.depth, progress=progress)
            self.client = client
            self.endpoint_url = params.endpoint
            self.root_node_id = params.root or "Objects"
            self.archetype = archetype_by_name(params.archetype_name)
            populate_tree(self.tree_model, tree)
            self.tree_view.expandToDepth(1)
            secdesc = "no security" if params.security_policy == "None" else f"{params.security_policy} / {params.security_mode}"
            iddesc = "anonymous" if params.identity_mode == "Anonymous" else f"user '{params.username}'"
            arch_desc = "" if self.archetype is GENERIC else f", archetype '{self.archetype.name}'"
            self.status_label.setText(
                f"Connected to {params.endpoint} — browsed under {self.root_node_id} ({secdesc}, {iddesc}{arch_desc})."
            )
            self.refresh_button.setEnabled(True)
            # Create the subscription used for live values.
            self.subscription = await client.create_subscription(period=500, handler=self.handler)
            self._diff_selection()
        except Exception as exc:
            try:
                await client.disconnect()
            except Exception:
                pass
            self.client = None
            self.refresh_button.setEnabled(False)
            QMessageBox.critical(
                self,
                "Connect failed",
                f"{exc}\n\nIf this is the first connect with a security policy, the server probably "
                "rejected our client certificate — accept it on the server side (e.g. in TIA Portal: "
                "PLC → OPC UA → Server → Certificate management → move from rejected to trusted) and "
                "try again.",
            )
            self.status_label.setText("Not connected.")
        finally:
            progress.close()

    async def _disconnect_client_if_any(self) -> None:
        # Cancel all live subscriptions, then drop the session.
        if self.subscription is not None and self.live_handles:
            try:
                await self.subscription.unsubscribe(list(self.live_handles.values()))
            except Exception:
                pass
            self.live_handles.clear()
        if self.subscription is not None:
            try:
                await self.subscription.delete()
            except Exception:
                pass
            self.subscription = None
        if self.client is not None:
            try:
                await self.client.disconnect()
            except Exception:
                pass
            self.client = None

    def closeEvent(self, event) -> None:
        if self.client is not None:
            asyncio.ensure_future(self._disconnect_client_if_any())
        event.accept()

    # ------------------------------------------------------------------
    # Refresh values
    # ------------------------------------------------------------------
    def _on_refresh_values(self) -> None:
        if self.client is None:
            return
        asyncio.ensure_future(self._refresh_values())

    async def _refresh_values(self) -> None:
        items_and_nodes = list(iter_variable_items(self.tree_model))
        if not items_and_nodes:
            return
        self.status_label.setText(f"Re-reading {len(items_and_nodes)} values...")
        try:
            opc_nodes = [self.client.get_node(n.node_id) for _, n in items_and_nodes]
            try:
                data_values = await self.client.read_values(opc_nodes)
                statuses = ["Good"] * len(data_values)
                values = [_format_value(v) for v in data_values]
            except Exception:
                # Fall back to one-by-one if batch read isn't supported by server.
                values, statuses = [], []
                for opc_node in opc_nodes:
                    try:
                        dv = await opc_node.read_data_value()
                        values.append(_format_value(dv.Value.Value if dv.Value is not None else None))
                        statuses.append(_stringify_status(dv.StatusCode))
                    except Exception as exc:
                        values.append("")
                        statuses.append(f"Bad: {type(exc).__name__}")
            for (item, node), val, status in zip(items_and_nodes, values, statuses):
                parent = item.parent() or self.tree_model.invisibleRootItem()
                row = item.row()
                node.current_value = val
                node.current_status = status
                status_item = parent.child(row, COL_STATUS)
                value_item = parent.child(row, COL_VALUE)
                if status_item is not None:
                    status_item.setText(status)
                    brush = _status_brush(status)
                    if brush is not None:
                        status_item.setForeground(brush)
                if value_item is not None:
                    value_item.setText(val)
            self.status_label.setText(
                f"Connected to {self.endpoint_url} — re-read {len(items_and_nodes)} values."
            )
        except Exception as exc:
            self.status_label.setText(f"Refresh failed: {exc}")

    # ------------------------------------------------------------------
    # Filter
    # ------------------------------------------------------------------
    def _on_filter_changed(self, text: str) -> None:
        self.proxy_model.setFilterFixedString(text)
        if text:
            self.tree_view.expandAll()
        else:
            self.tree_view.collapseAll()
            self.tree_view.expandToDepth(1)

    # ------------------------------------------------------------------
    # Selection diffing → Selected table + subscriptions
    # ------------------------------------------------------------------
    def _on_item_changed(self, _item: QStandardItem) -> None:
        # Debounce: tristate cascades fire many itemChanged signals back-to-back.
        self._diff_timer.start()

    def _diff_selection(self) -> None:
        current_freshly_collected = collect_checked(self.tree_model)
        current_ids = {s.node_id for s in current_freshly_collected}
        prev_ids = set(self.selected_vars.keys())

        added = [s for s in current_freshly_collected if s.node_id not in prev_ids]
        removed_ids = prev_ids - current_ids

        for node_id in removed_ids:
            self.selected_vars.pop(node_id, None)
            self._remove_selected_row(node_id)
            asyncio.ensure_future(self._unsubscribe(node_id))
        for sel in added:
            self._auto_fill_mtconnect_fields(sel)
            self.selected_vars[sel.node_id] = sel
            self._add_selected_row(sel)
            asyncio.ensure_future(self._subscribe(sel.node_id))

        self.selected_count_label.setText(f"{len(self.selected_vars)} selected")

    def _auto_fill_mtconnect_fields(self, sel: SelectedVariable) -> None:
        """Run the MTConnect type/units autocomplete heuristics on a freshly-added selection.
        Honours the active archetype's whitelist when present (bias toward common-for-class types)."""
        category = kind_to_category(sel.kind)
        suggested = self._suggest_type_with_archetype_bias(sel.browse_name, category)
        sel.mtconnect_type = suggested.name
        sel.mtconnect_units = suggest_units(suggested, sel.browse_name) if category == "SAMPLE" else ""
        sel.mtconnect_subtype = ""

    def _suggest_type_with_archetype_bias(self, browse_name: str, category: str):
        """Like suggest_type() but prefers types in the active archetype's whitelist.
        Falls back to the general suggestion if no whitelisted type's keywords match."""
        if self.archetype is GENERIC or not self.archetype.type_whitelist:
            return suggest_type(browse_name, category)

        bn = browse_name.lower()
        # First pass: try only whitelisted types' keywords
        for t in types_for_category(category):
            if t.name not in self.archetype.type_whitelist:
                continue
            for kw in t.autocomplete_keywords:
                if kw and kw in bn:
                    return t
        # Fallback: general suggestion (may return a non-whitelisted type — the editor
        # will still show it because _refresh_type_combo_for_kind keeps the saved type visible).
        return suggest_type(browse_name, category)

    def _add_selected_row(self, sel: SelectedVariable) -> None:
        row = self.selected_table.rowCount()
        self.selected_table.insertRow(row)
        kind_item = QTableWidgetItem(sel.kind)
        # Stash node_id on the row's first cell so the editor can look up SelectedVariable.
        kind_item.setData(Qt.UserRole, sel.node_id)
        self.selected_table.setItem(row, SEL_COL_KIND, kind_item)
        self.selected_table.setItem(row, SEL_COL_PARENT, QTableWidgetItem(sel.parent_browse_name))
        self.selected_table.setItem(row, SEL_COL_NAME, QTableWidgetItem(sel.browse_name))
        self.selected_table.setItem(row, SEL_COL_VALUE, QTableWidgetItem("(awaiting first update)"))
        self.selected_rows[sel.node_id] = row

    def _remove_selected_row(self, node_id: str) -> None:
        row = self.selected_rows.pop(node_id, None)
        if row is None:
            return
        self.selected_table.removeRow(row)
        # Reindex remaining rows (Qt collapses indices on removeRow).
        for nid, r in list(self.selected_rows.items()):
            if r > row:
                self.selected_rows[nid] = r - 1

    # ------------------------------------------------------------------
    # MTConnect editor panel
    # ------------------------------------------------------------------
    def _set_editor_enabled(self, enabled: bool) -> None:
        for widget in (
            self.kind_combo, self.type_combo, self.units_combo,
            self.subtype_combo, self.data_item_key_edit,
        ):
            widget.setEnabled(enabled)
        if enabled:
            self._editor_label.setStyleSheet("QLabel { font-weight: bold; }")
        else:
            self._editor_label.setText("(select a row above to edit MTConnect metadata)")
            self._editor_label.setStyleSheet("QLabel { font-style: italic; color: palette(mid); }")

    def _current_editor_node_id(self) -> str | None:
        row = self.selected_table.currentRow()
        if row < 0:
            return None
        item = self.selected_table.item(row, SEL_COL_KIND)
        if item is None:
            return None
        return item.data(Qt.UserRole)

    def _on_selected_row_changed(self, _current, _previous) -> None:
        node_id = self._current_editor_node_id()
        if node_id is None or node_id not in self.selected_vars:
            self._set_editor_enabled(False)
            return
        self._populate_editor(self.selected_vars[node_id])

    def _populate_editor(self, sel: SelectedVariable) -> None:
        self._editor_loading = True
        try:
            self._editor_label.setText(f"Editing: {sel.parent_browse_name} / {sel.browse_name}")
            self._set_editor_enabled(True)

            self.kind_combo.setCurrentText(sel.kind)
            self._refresh_type_combo_for_kind(sel.kind, current_type=sel.mtconnect_type)
            self._refresh_units_combo_for_type(sel.mtconnect_type, current_units=sel.mtconnect_units)
            self._refresh_subtype_combo_for_type(sel.mtconnect_type, current_subtype=sel.mtconnect_subtype)
            self.data_item_key_edit.setText(sel.data_item_key)
        finally:
            self._editor_loading = False

    def _types_visible_for(self, category: str) -> list:
        """All spec types for the given category, optionally narrowed by the
        active archetype unless 'Show all spec types' is on."""
        all_types = types_for_category(category)
        if self.show_all_types or self.archetype is GENERIC or not self.archetype.type_whitelist:
            return all_types
        return [t for t in all_types if t.name in self.archetype.type_whitelist]

    def _refresh_type_combo_for_kind(self, kind: str, current_type: str = "") -> None:
        category = kind_to_category(kind)
        visible = self._types_visible_for(category)

        # If the user's saved type isn't in the visible list, surface it anyway so we
        # don't silently switch their choice. Happens when the archetype filter
        # excludes a previously-picked type.
        names = [t.name for t in visible]
        if current_type and current_type not in names:
            names.append(current_type)

        self.type_combo.blockSignals(True)
        self.type_combo.clear()

        if self.group_by_domain:
            self._populate_type_combo_grouped(names)
        else:
            self.type_combo.addItems(sorted(names))

        # Keep the QCompleter in sync with whatever's now in the model
        self.type_completer.setModel(self.type_combo.model())

        if current_type:
            idx = self.type_combo.findText(current_type)
            if idx >= 0:
                self.type_combo.setCurrentIndex(idx)
            else:
                self.type_combo.setEditText(current_type)
        self.type_combo.blockSignals(False)

    def _populate_type_combo_grouped(self, names: list[str]) -> None:
        """Insert types under domain-named, non-selectable header rows."""
        # Group + sort within each group
        groups: dict[str, list[str]] = {}
        for n in names:
            groups.setdefault(domain_for_type(n), []).append(n)
        # Header order: Motion, Process, Electrical, ..., Other last
        preferred = [
            "Motion / Geometry", "Process variables", "Electrical", "Mechanical",
            "Controller", "Tool", "Production", "State / Safety",
            "Interface (machine-to-machine)", "Asset / Metadata",
            "Connectivity / System", "Measurement", "Environment", "Conditions",
            "Other / Generic",
        ]
        ordered = [g for g in preferred if g in groups] + [g for g in groups if g not in preferred]

        for group in ordered:
            header_item = QStandardItem(f"── {group} ──")
            # Disable selection on the header rows so users can't pick a header.
            header_item.setFlags(Qt.NoItemFlags)
            self.type_combo.model().appendRow(header_item)
            for n in sorted(groups[group]):
                self.type_combo.model().appendRow(QStandardItem(n))

    def _on_show_all_toggled(self, checked: bool) -> None:
        self.show_all_types = checked
        node_id = self._current_editor_node_id()
        sel = self.selected_vars.get(node_id) if node_id else None
        if sel is not None:
            self._refresh_type_combo_for_kind(sel.kind, current_type=sel.mtconnect_type)

    def _on_group_by_domain_toggled(self, checked: bool) -> None:
        self.group_by_domain = checked
        node_id = self._current_editor_node_id()
        sel = self.selected_vars.get(node_id) if node_id else None
        if sel is not None:
            self._refresh_type_combo_for_kind(sel.kind, current_type=sel.mtconnect_type)

    def _refresh_units_combo_for_type(self, type_name: str, current_units: str = "") -> None:
        t = lookup_type(type_name)
        units = list(t.allowed_units) if t else []
        self.units_combo.blockSignals(True)
        self.units_combo.clear()
        self.units_combo.addItems(units)
        if current_units:
            idx = self.units_combo.findText(current_units)
            if idx >= 0:
                self.units_combo.setCurrentIndex(idx)
            else:
                self.units_combo.setEditText(current_units)
        self.units_combo.blockSignals(False)

    def _refresh_subtype_combo_for_type(self, type_name: str, current_subtype: str = "") -> None:
        t = lookup_type(type_name)
        subtypes = [""] + list(t.common_subtypes) if t else [""]
        self.subtype_combo.blockSignals(True)
        self.subtype_combo.clear()
        self.subtype_combo.addItems(subtypes)
        if current_subtype:
            idx = self.subtype_combo.findText(current_subtype)
            if idx >= 0:
                self.subtype_combo.setCurrentIndex(idx)
            else:
                self.subtype_combo.setEditText(current_subtype)
        self.subtype_combo.blockSignals(False)

    def _on_editor_kind_changed(self, text: str) -> None:
        if self._editor_loading:
            return
        node_id = self._current_editor_node_id()
        if node_id is None:
            return
        sel = self.selected_vars.get(node_id)
        if sel is None:
            return
        sel.kind = text
        # Update the table cell
        row = self.selected_rows.get(node_id)
        if row is not None:
            self.selected_table.item(row, SEL_COL_KIND).setText(text)
        # Refresh type combo because the category-relevant types changed
        category = kind_to_category(text)
        suggested = self._suggest_type_with_archetype_bias(sel.browse_name, category)
        sel.mtconnect_type = suggested.name
        sel.mtconnect_units = suggest_units(suggested, sel.browse_name) if category == "SAMPLE" else ""
        sel.mtconnect_subtype = ""
        self._populate_editor(sel)

    def _on_editor_type_changed(self, text: str) -> None:
        if self._editor_loading:
            return
        node_id = self._current_editor_node_id()
        if node_id is None:
            return
        sel = self.selected_vars.get(node_id)
        if sel is None:
            return
        sel.mtconnect_type = text
        # When the type changes, suggest units that match the new type
        category = kind_to_category(sel.kind)
        fallback_type = lookup_type(text) or self._suggest_type_with_archetype_bias(sel.browse_name, category)
        suggested_units = suggest_units(fallback_type, sel.browse_name)
        sel.mtconnect_units = suggested_units if category == "SAMPLE" else ""
        sel.mtconnect_subtype = ""
        self._refresh_units_combo_for_type(text, current_units=sel.mtconnect_units)
        self._refresh_subtype_combo_for_type(text, current_subtype="")

    def _on_editor_units_changed(self, text: str) -> None:
        if self._editor_loading:
            return
        node_id = self._current_editor_node_id()
        if node_id is None:
            return
        sel = self.selected_vars.get(node_id)
        if sel is None:
            return
        sel.mtconnect_units = text

    def _on_editor_subtype_changed(self, text: str) -> None:
        if self._editor_loading:
            return
        node_id = self._current_editor_node_id()
        if node_id is None:
            return
        sel = self.selected_vars.get(node_id)
        if sel is None:
            return
        sel.mtconnect_subtype = text

    def _on_editor_data_item_key_changed(self, text: str) -> None:
        if self._editor_loading:
            return
        node_id = self._current_editor_node_id()
        if node_id is None:
            return
        sel = self.selected_vars.get(node_id)
        if sel is None:
            return
        sel.data_item_key = text

    def _on_editor_reset(self) -> None:
        node_id = self._current_editor_node_id()
        if node_id is None:
            return
        sel = self.selected_vars.get(node_id)
        if sel is None:
            return
        sel.data_item_key = suggest_data_item_key(sel.browse_name)
        self._auto_fill_mtconnect_fields(sel)
        self._populate_editor(sel)

    def _on_live_value(self, node_id: str, value_str: str, status_str: str) -> None:
        row = self.selected_rows.get(node_id)
        if row is None:
            return
        cell_text = value_str if not status_str or status_str == "Good" else f"{value_str} ({status_str})"
        item = self.selected_table.item(row, SEL_COL_VALUE)
        if item is None:
            self.selected_table.setItem(row, SEL_COL_VALUE, QTableWidgetItem(cell_text))
        else:
            item.setText(cell_text)
            if status_str.startswith("Bad"):
                item.setForeground(QBrush(QColor(180, 80, 80)))
            else:
                item.setForeground(QBrush(QColor(80, 140, 80)))

    async def _subscribe(self, node_id: str) -> None:
        if self.client is None or self.subscription is None:
            return
        if node_id in self.live_handles:
            return
        try:
            node = self.client.get_node(node_id)
            handle = await self.subscription.subscribe_data_change(node)
            self.live_handles[node_id] = handle
        except Exception as exc:
            self.value_signal.value_changed.emit(node_id, "", f"Bad: subscribe failed ({type(exc).__name__})")

    async def _unsubscribe(self, node_id: str) -> None:
        if self.subscription is None:
            return
        handle = self.live_handles.pop(node_id, None)
        if handle is None:
            return
        try:
            await self.subscription.unsubscribe(handle)
        except Exception:
            pass

    # ------------------------------------------------------------------
    # Generate
    # ------------------------------------------------------------------
    def _on_generate(self) -> None:
        # Read from the persistent SelectedVariable map so user edits (type/units/etc.) are emitted.
        selections = list(self.selected_vars.values())
        if not selections:
            QMessageBox.information(self, "Nothing selected", "Tick at least one Variable in the tree first.")
            return
        name, ok = self._ask_profile_name()
        if not ok or not name:
            return
        # Default save dir sits alongside the running program (source folder in
        # dev, .exe folder when frozen). Auto-create it so the dialog opens
        # somewhere sensible even on first run.
        default_out = _app_dir() / "generated"
        default_out.mkdir(parents=True, exist_ok=True)
        out_dir = QFileDialog.getExistingDirectory(
            self,
            "Choose folder for generated files",
            str(default_out),
        )
        if not out_dir:
            return

        Path(out_dir).mkdir(parents=True, exist_ok=True)
        yaml_path = Path(out_dir) / f"{name}.yaml"
        template_path = Path(out_dir) / f"{name}.template.xml"
        yaml_path.write_text(build_profile_yaml(selections, self.endpoint_url, self.root_node_id), encoding="utf-8")
        template_path.write_text(build_device_xml(selections, name), encoding="utf-8")

        QMessageBox.information(
            self,
            "Generated — read this before copying!",
            "Two files written:\n\n"
            f"  {yaml_path.name}\n"
            f"  {template_path.name}\n\n"
            "Output folder: " + str(out_dir) + "\n\n"
            "Both files belong in your deployment's profiles/ folder (the one the\n"
            "docker-compose file mounts to /app/profiles).\n\n"
            "Do NOT put either file in the devices/ folder. The OpcUaAdapter module loads "
            "the .template.xml from profiles/ and rewrites it with your deviceKey at startup; "
            "the agent's devices/ loader would treat it as a literal device file with id='placeholder'.\n\n"
            "After copying, set 'profile: " + name + "' in agent.config.yaml's opcua-adapter block "
            "and restart the agent.",
        )

    def _ask_profile_name(self) -> tuple[str, bool]:
        dlg = QDialog(self)
        dlg.setWindowTitle("Profile name")
        edit = QLineEdit()
        edit.setPlaceholderText("e.g. coolant_filtration")
        buttons = QDialogButtonBox(QDialogButtonBox.Ok | QDialogButtonBox.Cancel)
        buttons.accepted.connect(dlg.accept)
        buttons.rejected.connect(dlg.reject)
        layout = QVBoxLayout(dlg)
        layout.addWidget(QLabel("Filename (without extension):"))
        layout.addWidget(edit)
        layout.addWidget(buttons)
        if dlg.exec() == QDialog.Accepted:
            return slugify(edit.text().strip(), keep_underscores=True), True
        return "", False


# ----------------------------------------------------------------------------
# Entry point
# ----------------------------------------------------------------------------


def main() -> int:
    app = QApplication(sys.argv)
    loop = qasync.QEventLoop(app)
    asyncio.set_event_loop(loop)
    window = MainWindow()
    window.show()
    with loop:
        return loop.run_forever() or 0


if __name__ == "__main__":
    sys.exit(main())

# OPCUAProfilexmlGUI

PySide6 desktop app for selecting OPC UA Variables from a server and generating an MTConnect profile YAML + device template for use with [`MTConnect.NET-AgentModule-OpcUaAdapter`](../../agent/Modules/MTConnect.NET-AgentModule-OpcUaAdapter/).

This is the canonical source location. A built `.exe` for everyday use is produced via PyInstaller (see [Packaging](#packaging) below) and conventionally lives outside the repo (e.g. `C:\Tools\OPCUAProfilexmlGUI\dist\`) so the compiled binary isn't committed.

## Run from source

First time only:

```powershell
cd c:\dev\MtConnect.net\tools\opcua-profile-gui
python -m venv .venv
.venv\Scripts\pip install -r requirements.txt
```

Subsequent runs:

```powershell
.venv\Scripts\python.exe main.py
```

(Requires Python 3.11+. Tested on 3.14.2 with PySide6 6.11 / asyncua 2.0 / qasync 0.28.)

## Workflow

1. Click **Connect...**. Enter the OPC UA endpoint (defaults to `opc.tcp://192.168.0.1:4840`), an optional root NodeId to start the browse from (e.g. `ns=3;s=PLC` for an S7-1500 to skip server diagnostics), and a max browse depth.
2. App connects, walks the address space, and shows the result as a tree with checkboxes.
3. **Filter** by typing in the search box (matches on BrowseName, case-insensitive, keeps ancestors visible).
4. **Tick** Variables you want to subscribe to. Ticking a folder cascades to its children.
5. **Selected** panel on the right shows what's ticked, with inferred kind (sample/event) per row.
6. Click **Generate YAML + device.xml**. Pick a name and folder; two files appear:
   - `<name>.yaml` — NodeId → DataItem mappings
   - `<name>.template.xml` — MTConnect device template, components grouped by parent Object

7. Copy the two files into `c:\dev\deployment\profiles\`, reference the profile name in `agent.config.yaml`'s `opcua-adapter:` block, and `docker compose restart mtconnect-agent-spb`.

## Current capabilities (latest)

- **Vocabulary verified against MTConnect.org** — 259 DataItem types from the official MTConnectDevices_2.7.xsd (`schemas.mtconnect.org`), including 12 MTConnect Interface types (`MATERIAL_FEED`, `OPEN_DOOR`, etc.) that the .NET fork's generated code hadn't yet caught up with.
- **Three categories** — `sample` / `event` / **`condition`**. Note: v1 of `MTConnect.NET-AgentModule-OpcUaAdapter` doesn't drive CONDITION states from OPC UA values; condition-categorised items will appear correctly in the generated `device.xml` but stay `UNAVAILABLE` at runtime until alarm-tree mapping ships.
- **Machine archetype on Connect** — pick the equipment class (Generic / CNC Mill / CNC Lathe / Robot / Coolant Unit / Packaging / Generic PLC). The archetype:
  - Filters the MTConnect type dropdown to ~50-90 commonly-relevant types per class (toggle "Show all spec types" in the editor to bypass).
  - Biases the BrowseName → type autocomplete toward whitelisted types, so "TEMPERATURE" on a Coolant Unit resolves to TEMPERATURE rather than PROCESS_TIMER.
- **Search-as-you-type on the type dropdown** — `QCompleter` with case-insensitive substring matching. Start typing in the type combo to filter the popup.
- **Group by domain toggle** — restructures the type dropdown into named domain sections (Motion / Process variables / Electrical / Mechanical / Controller / Tool / Production / State / Interfaces / Asset / Connectivity / Measurement / Conditions / Other). Headers are non-selectable; pick a type underneath. Curated domain assignments in `mtconnect_vocab.py`.

## Previous capabilities

- **Connect dialog with tooltips + inline help** — every field has hover-text with concrete examples; a help paragraph at the bottom explains the connect/browse/subscribe flow.
- **Auth + security policy** — anonymous or username/password; security policy `None` or `Basic256Sha256` with `Sign` / `SignAndEncrypt`. The client cert is auto-generated on first use of a non-`None` policy and stored under `.venv/pki/`. First connect to a secured server: the server will reject our cert; accept it manually (TIA Portal: PLC → OPC UA → Server → Certificate management → move from rejected to trusted) and reconnect.
- **Snapshot Value + StatusCode + friendly DataType in the tree** — each Variable row shows its current value at browse time, a Good/Bad status (colored), and its DataType as a friendly name (`Int16`, `Boolean`, `String`). Refresh values button re-reads without re-walking.
- **Live Value subscriptions in the Selected panel** — ticking a Variable opens a subscription on just that node; the Live Value cell updates as the PLC publishes changes. Untick → unsubscribe.
- **MTConnect type / units / subType editor** — clicking a row in the Selected panel opens an editor below the table with category-filtered type dropdown, type-filtered units dropdown, and subType picker. Auto-suggested from BrowseName (e.g. "PRESSURE 0-145 PSIG" → type=`PRESSURE`, units=`PSI`); override per row. A vocabulary of ~30 commonly-used MTConnect types is shipped in `mtconnect_vocab.py` — extend that file to add more.
- **Description tooltips** on tree rows when populated.

## Known limits

- **Eager browse on connect** — the whole subtree is walked up front. For your ~1500-node S7 PLC this is fast. For servers with 100k+ nodes the connect would feel slow. Could add lazy expand later.
- **No save/load of partial selections** — close the app and your tick state is gone. Add this if you find yourself wanting it.
- **No `ValueRank` flagging** — array/struct variables show as scalars; subscribing to one may produce ugly output. Filter them out of selections for now.

## Packaging

Built `.exe` is produced via PyInstaller. `--onedir` is preferred over `--onefile` (instant launch instead of 3-5s extract-on-every-run; trade-off is "ship a folder" instead of "ship one file"):

```powershell
.venv\Scripts\pip install pyinstaller
.venv\Scripts\pyinstaller --noconfirm --onedir --windowed --name OPCUAProfilexmlGUI main.py
```

Output lands in `dist/OPCUAProfilexmlGUI/`. Either zip the folder for distribution, or copy the whole folder to a deployment location (e.g. `C:\Tools\OPCUAProfilexmlGUI\` by convention — kept outside the repo so the ~100MB Qt bundle isn't committed; see `.gitignore`).

Folder size is large (Qt is large) but startup is instant after first extract.

## Files

- `main.py` — entry point, connect dialog, certificates dialog, main window, tree view, generate flow
- `generators.py` — YAML + device.xml emitters (mirrors the CLI tool's output format)
- `mtconnect_vocab.py` — runtime vocabulary API: `types_for_category`, `lookup_type`, `suggest_type`, `suggest_units`. Layers spec data + curated unit alternatives + curated keyword hints.
- `mtconnect_types_spec.py` — **auto-generated** data module with all 247 MTConnect v2.7 spec types (95 SAMPLE + 146 EVENT + 6 CONDITION), with each type's category, default units, description, and SubType enums. Do not edit by hand.
- `tools/extract_mtconnect_types.py` — one-shot extractor. Parses `c:/dev/MtConnect.net/libraries/MTConnect.NET-Common/Devices/DataItems/*.g.cs` and emits `mtconnect_types_spec.py`. Re-run after upstream MTConnect spec bumps.
- `requirements.txt` — pinned deps
- `pki/` — client cert + private key for secured OPC UA connects. Lives at the project root (not inside `.venv/`) so re-creating the venv doesn't wipe the cert. View / regenerate / import via the **Certificates...** button in the main window.
- `.venv/` — virtual environment (excluded from version control)
- `generated/` — default output folder for generated profile + device.xml

## Refreshing the MTConnect type vocabulary

When the spec version in the MTConnect.NET fork bumps (new types added in v2.8 etc.):

```powershell
cd C:\Tools\OPCUAProfilexmlGUI
.venv\Scripts\python.exe tools\extract_mtconnect_types.py
```

This overwrites `mtconnect_types_spec.py`. The extractor parses 247 generated `*DataItem.g.cs` files in the fork to pull the canonical type name, category, default units, description, and SubType enum members per type. The autocomplete keyword hints in `mtconnect_vocab.py` are hand-curated and persist across re-runs — extend them when you start seeing unfamiliar BrowseNames that should map cleanly to a spec type.

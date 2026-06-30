"""One-shot extractor that parses the MTConnect.NET fork's generated DataItem
classes into a Python data module the GUI loads at runtime.

Run when the MTConnect spec version in the fork bumps:

    cd C:\\Tools\\OPCUAProfilexmlGUI
    .venv\\Scripts\\python.exe tools\\extract_mtconnect_types.py

The script writes mtconnect_types_spec.py in the project root with one
SpecType entry per recognized DataItem .g.cs file, plus the SubType
enumerations.

Source of truth: c:/dev/MtConnect.net/libraries/MTConnect.NET-Common/Devices/

Each .g.cs file exposes:
  - CategoryId           DataItemCategory.{SAMPLE|EVENT|CONDITION}
  - TypeId               canonical MTConnect type string, e.g. "PRESSURE"
  - NameId               default DataItem name
  - DefaultUnits         Devices.Units.<CONST>  (we resolve to the string value)
  - DescriptionText      from the spec
  - SubTypes enum        (optional) with members ACTUAL, COMMANDED, etc.
"""

from __future__ import annotations

import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

REPO_DEVICES_DIR = Path(r"c:/dev/MtConnect.net/libraries/MTConnect.NET-Common/Devices")
DATAITEMS_DIR = REPO_DEVICES_DIR / "DataItems"
UNITS_GCS = REPO_DEVICES_DIR / "Units.g.cs"
OUTPUT = Path(__file__).resolve().parent.parent / "mtconnect_types_spec.py"


# Types listed in the official MTConnect XSD (schemas.mtconnect.org/schemas/
# MTConnectDevices_2.7.xsd, DataItemEnumEnum) but NOT yet present as generated
# *.g.cs classes in the TrakHound MTConnect.NET fork. All are MTConnect Interface
# events (machine-to-machine handshake protocol for material transfer, door/chuck
# coordination). Drop these once the .NET fork catches up.
MISSING_FROM_DOTNET_FORK: list[tuple[str, str, str]] = [
    # (type_id, category, description)
    ("CLOSE_CHUCK",             "EVENT", "MTConnect Interface event for the chuck close request handshake."),
    ("CLOSE_DOOR",              "EVENT", "MTConnect Interface event for the door close request handshake."),
    ("FEATURE_PERSISTENT_ID",   "EVENT", "Stable identifier carried across measurement sessions for a measured feature."),
    ("INTERFACE_STATE",         "EVENT", "Current state of an MTConnect Interface handshake (ENABLED / DISABLED)."),
    ("MATERIAL_CHANGE",         "EVENT", "MTConnect Interface event signaling a material-change request between equipment."),
    ("MATERIAL_FEED",           "EVENT", "MTConnect Interface event signaling that material is being fed."),
    ("MATERIAL_LOAD",           "EVENT", "MTConnect Interface event signaling a material-load request between equipment."),
    ("MATERIAL_RETRACT",        "EVENT", "MTConnect Interface event signaling that material is being retracted."),
    ("MATERIAL_UNLOAD",         "EVENT", "MTConnect Interface event signaling a material-unload request between equipment."),
    ("OPEN_CHUCK",              "EVENT", "MTConnect Interface event for the chuck open request handshake."),
    ("OPEN_DOOR",               "EVENT", "MTConnect Interface event for the door open request handshake."),
    ("PART_CHANGE",             "EVENT", "MTConnect Interface event signaling a part-change request between equipment."),
]


@dataclass
class SpecType:
    type_id: str
    category: str
    name_id: str
    default_units: str
    description: str
    common_subtypes: list[str] = field(default_factory=list)


def parse_units_table() -> dict[str, str]:
    """Build a {C# const name → canonical units string} map from Units.g.cs."""
    text = UNITS_GCS.read_text(encoding="utf-8-sig")
    out: dict[str, str] = {}
    for m in re.finditer(r'public const string ([A-Z_][A-Z0-9_]*)\s*=\s*"([^"]*)"', text):
        out[m.group(1)] = m.group(2)
    return out


def extract_one(path: Path, units_table: dict[str, str]) -> SpecType | None:
    text = path.read_text(encoding="utf-8-sig")

    cat_match = re.search(r"CategoryId\s*=\s*DataItemCategory\.(SAMPLE|EVENT|CONDITION)", text)
    type_match = re.search(r'TypeId\s*=\s*"([^"]+)"', text)
    name_match = re.search(r'NameId\s*=\s*"([^"]+)"', text)
    if not (cat_match and type_match):
        return None

    # DefaultUnits may reference Devices.Units.CONST or be a literal "STRING" or absent.
    default_units = ""
    du_match = re.search(r"DefaultUnits\s*=\s*([^;]+);", text)
    if du_match:
        rhs = du_match.group(1).strip()
        const_match = re.match(r"(?:Devices\.)?Units\.([A-Z_][A-Z0-9_]*)", rhs)
        if const_match:
            default_units = units_table.get(const_match.group(1), "")
        else:
            literal_match = re.match(r'"([^"]+)"', rhs)
            if literal_match:
                default_units = literal_match.group(1)

    desc_match = re.search(r'DescriptionText\s*=\s*"((?:[^"\\]|\\.)*)"', text)
    description = desc_match.group(1).encode().decode("unicode_escape") if desc_match else ""

    # SubTypes enum members
    subtypes: list[str] = []
    enum_match = re.search(r"public enum SubTypes\s*\{([^}]+)\}", text, re.DOTALL)
    if enum_match:
        for m in re.finditer(r"\b([A-Z][A-Z0-9_]+)\b", enum_match.group(1)):
            name = m.group(1)
            if name not in subtypes:
                subtypes.append(name)

    return SpecType(
        type_id=type_match.group(1),
        category=cat_match.group(1),
        name_id=name_match.group(1) if name_match else type_match.group(1).lower(),
        default_units=default_units,
        description=description.strip(),
        common_subtypes=subtypes,
    )


def emit(types: list[SpecType]) -> str:
    types_sorted = sorted(types, key=lambda t: (t.category, t.type_id))
    lines = [
        '"""Auto-generated by tools/extract_mtconnect_types.py — DO NOT EDIT BY HAND.',
        "",
        "Source: c:/dev/MtConnect.net/libraries/MTConnect.NET-Common/Devices/DataItems/*.g.cs",
        "(which is itself generated from the MTConnect SysML model).",
        '"""',
        "",
        "from __future__ import annotations",
        "",
        "from dataclasses import dataclass, field",
        "",
        "",
        "@dataclass(frozen=True)",
        "class SpecType:",
        "    type_id: str",
        "    category: str  # SAMPLE | EVENT | CONDITION",
        "    name_id: str",
        "    default_units: str  # canonical units string, or '' if N/A",
        "    description: str",
        "    common_subtypes: tuple[str, ...] = ()",
        "",
        "",
        "SPEC_TYPES: list[SpecType] = [",
    ]
    for t in types_sorted:
        subtypes_literal = "()" if not t.common_subtypes else "(" + ", ".join(repr(s) for s in t.common_subtypes) + ",)"
        # Quote-safe description (single-line; collapse newlines)
        desc = " ".join(t.description.split())
        lines.append(
            f"    SpecType(type_id={t.type_id!r}, category={t.category!r}, name_id={t.name_id!r}, "
            f"default_units={t.default_units!r}, description={desc!r}, common_subtypes={subtypes_literal}),"
        )
    lines.append("]")
    lines.append("")
    return "\n".join(lines)


def main() -> int:
    if not DATAITEMS_DIR.is_dir():
        print(f"DataItems directory not found: {DATAITEMS_DIR}", file=sys.stderr)
        return 1
    units_table = parse_units_table()
    print(f"Parsed {len(units_table)} units constants from {UNITS_GCS.name}")

    types: list[SpecType] = []
    skipped: list[str] = []
    for path in sorted(DATAITEMS_DIR.glob("*DataItem.g.cs")):
        t = extract_one(path, units_table)
        if t is None:
            skipped.append(path.name)
            continue
        types.append(t)

    print(f"Extracted {len(types)} SpecType entries; skipped {len(skipped)} non-matching files.")

    # Inject spec types missing from the .NET fork (verified against schemas.mtconnect.org)
    known_ids = {t.type_id for t in types}
    injected = 0
    for tid, cat, desc in MISSING_FROM_DOTNET_FORK:
        if tid in known_ids:
            continue
        types.append(SpecType(
            type_id=tid, category=cat, name_id=tid.lower(),
            default_units="", description=desc, common_subtypes=[],
        ))
        injected += 1
    if injected:
        print(f"Injected {injected} types from the official XSD that are missing from the .NET fork.")
    if skipped:
        print("  Skipped:", ", ".join(skipped[:10]) + (" ..." if len(skipped) > 10 else ""))

    OUTPUT.write_text(emit(types), encoding="utf-8")
    print(f"Wrote {OUTPUT}")

    # Quick sanity stats
    by_cat = {"SAMPLE": 0, "EVENT": 0, "CONDITION": 0}
    for t in types:
        by_cat[t.category] = by_cat.get(t.category, 0) + 1
    print(f"  SAMPLE: {by_cat['SAMPLE']}  EVENT: {by_cat['EVENT']}  CONDITION: {by_cat['CONDITION']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

"""MTConnect data-item type vocabulary, units, and BrowseName → type heuristics.

Three layers:

  1. SPEC base — auto-generated from the MTConnect.NET fork's *.g.cs files
     (see tools/extract_mtconnect_types.py). Comprehensive — 247 types across
     SAMPLE/EVENT/CONDITION as of MTConnect v2.7. Source of truth for type
     names, default units, descriptions, and SubType enums.

  2. UNIT_ALTERNATIVES — hand-curated map of additional unit strings that are
     commonly chosen for a given type (e.g. POSITION default is MILLIMETER but
     INCH is a frequent override). The dropdown shows default first, alternatives
     after. The combo is editable, so users can still type any unit value.

  3. KEYWORD_HINTS — hand-curated BrowseName-token → type-id mapping for the
     autocomplete heuristic. Order matters — first match wins. Hand-tune for
     the controllers you commonly meet (S7 DBs, Sinumerik, etc.).
"""

from __future__ import annotations

from dataclasses import dataclass

from mtconnect_types_spec import SPEC_TYPES, SpecType


@dataclass(frozen=True)
class MTConnectType:
    """Runtime view of a spec type with extra metadata for the UI."""

    name: str
    category: str                          # SAMPLE | EVENT | CONDITION
    allowed_units: tuple[str, ...]         # first element is the spec default
    common_subtypes: tuple[str, ...]
    description: str
    autocomplete_keywords: tuple[str, ...]


# ----------------------------------------------------------------------------
# Layer 2: Unit alternatives beyond the spec default (curated, common-only)
# ----------------------------------------------------------------------------

UNIT_ALTERNATIVES: dict[str, tuple[str, ...]] = {
    "POSITION":                   ("INCH",),
    "PATH_POSITION":              ("INCH",),
    "TOOL_LENGTH":                ("INCH",),
    "TEMPERATURE":                ("FAHRENHEIT",),
    "PRESSURE":                   ("BAR", "PSI"),
    "PATH_FEEDRATE":              ("MILLIMETER/MINUTE", "INCH/MINUTE"),
    "AXIS_FEEDRATE":              ("MILLIMETER/MINUTE", "INCH/MINUTE"),
    "CUTTING_SPEED":              ("MILLIMETER/MINUTE", "INCH/MINUTE"),
    "ROTARY_VELOCITY":            ("REVOLUTION/MINUTE", "DEGREE/SECOND"),
    "ANGULAR_VELOCITY":           ("DEGREE/SECOND",),
    "FLOW":                       ("GALLON/MINUTE",),
    "MASS":                       ("KILOGRAM",),
    "VOLUME_FLUID":               ("MILLILITER",),
    "ACCUMULATED_TIME":           ("MINUTE", "HOUR"),
    "PROCESS_TIMER":              ("MINUTE", "HOUR"),
    "ACTIVE_AXES":                (),
}


# ----------------------------------------------------------------------------
# Layer 3: BrowseName → type autocomplete hints (curated, order-sensitive)
# ----------------------------------------------------------------------------

KEYWORD_HINTS: dict[str, tuple[str, ...]] = {
    # SAMPLE
    "PATH_FEEDRATE_OVERRIDE":   ("path feedrate override", "feedrate override", "pfo", "feed override", "feedrateoverride"),
    "PATH_FEEDRATE":            ("path feedrate", "feedrate", "pfr", "feed_rate"),
    "ROTARY_VELOCITY_OVERRIDE": ("spindle override", "sso", "rotary velocity override"),
    "ROTARY_VELOCITY":          ("rpm", "rotary velocity", "spindle speed", "srpm"),
    "POSITION":                 ("position", "xabs", "yabs", "zabs", "aabs", "babs", "cabs", "xpos", "ypos", "zpos"),
    "ANGLE":                    ("angle", "rotation"),
    "LOAD":                     ("load", "axis load", "servo load"),
    "TEMPERATURE":              ("temperature", "temp", "deg c", "deg f", "celsius", "fahrenheit"),
    "PRESSURE":                 ("pressure", "psi", "bar", "kpa", "mpa"),
    "LEVEL":                    ("level", "tank level", "fluid level"),
    "FLOW":                     ("flow", "flow rate", "gpm", "lpm"),
    "VOLTAGE_DC":               ("vdc", "voltage dc", "volts dc"),
    "VOLTAGE_AC":               ("vac", "voltage ac", "volts ac"),
    "AMPERAGE_DC":              ("amperage", "current", "amps"),
    "FREQUENCY":                ("frequency", "hertz"),
    "ACCUMULATED_TIME":         ("running hours", "runtime", "uptime", "elapsed time", "running time"),
    "LINE_NUMBER":              ("line number", "line", "linenum"),
    "PART_COUNT":               ("part count", "parts", "production count"),
    "TOOL_LENGTH":              ("tool length",),
    "TOOL_DIAMETER":            ("tool diameter",),

    # EVENT
    "AVAILABILITY":             ("availability", "avail", "available"),
    "EXECUTION":                ("execution", "exec", "running state"),
    "CONTROLLER_MODE":          ("controller mode", "operating mode", "operatingmode"),
    "PROGRAM":                  ("program", "program name", "active program", "nc program"),
    "PROGRAM_COMMENT":          ("program comment",),
    "BLOCK":                    ("block", "nc block"),
    "LINE_LABEL":               ("line label",),
    "TOOL_NUMBER":              ("tool number", "tool id", "active tool"),
    "TOOL_OFFSET":              ("tool offset", "d number", "tool comp"),
    "PROCESS_STATE":            ("process state",),
    "EMERGENCY_STOP":           ("emergency stop", "estop", "e stop", "e-stop"),
    "POWER_STATE":              ("power state", "power on", "power_on"),
    "MOTION_TYPE":              ("motion type",),
    "PART_STATUS":              ("part status",),
    "EQUIPMENT_MODE":           ("equipment mode", "loaded", "working", "operating"),
    "CONNECTION_STATUS":        ("connection", "connected", "comms status"),
    "MESSAGE":                  ("message", "alarm message"),

    # CONDITION
    "SYSTEM_CONDITION":         ("system",),
    "ACTUATOR":                 ("actuator", "servo"),
    "COMMUNICATIONS":           ("communications", "comms"),
    "HARDWARE":                 ("hardware",),
    "LOGIC_PROGRAM":            ("logic", "logic program"),
    "MOTION_PROGRAM":           ("motion program",),
    "DATA_RANGE":               ("data range",),
}


# Priority list for type matching — when multiple keywords could match, this
# order decides. More specific types first; generic ones last.
_TYPE_MATCH_PRIORITY: list[str] = [
    "PATH_FEEDRATE_OVERRIDE", "ROTARY_VELOCITY_OVERRIDE",
    "PATH_FEEDRATE", "ROTARY_VELOCITY", "POSITION", "ANGLE", "LOAD",
    "TEMPERATURE", "PRESSURE", "LEVEL", "FLOW", "VOLTAGE_DC", "VOLTAGE_AC",
    "AMPERAGE_DC", "FREQUENCY", "ACCUMULATED_TIME", "LINE_NUMBER",
    "PART_COUNT", "TOOL_LENGTH", "TOOL_DIAMETER",
    "AVAILABILITY", "EXECUTION", "CONTROLLER_MODE", "PROGRAM",
    "TOOL_NUMBER", "TOOL_OFFSET", "EMERGENCY_STOP", "POWER_STATE",
    "EQUIPMENT_MODE", "CONNECTION_STATUS", "PROCESS_STATE",
    "PART_STATUS", "MESSAGE", "BLOCK", "LINE_LABEL", "PROGRAM_COMMENT",
    "SYSTEM", "ACTUATOR", "COMMUNICATIONS", "HARDWARE",
    "LOGIC_PROGRAM", "MOTION_PROGRAM", "DATA_RANGE",
]


# ----------------------------------------------------------------------------
# Domain assignments — used by the "Group by domain" toggle in the editor.
# Types not listed here fall under the catch-all "Other" group.
# ----------------------------------------------------------------------------

_DOMAIN_MAP: dict[str, str] = {
    # Motion / Geometry
    **{t: "Motion / Geometry" for t in (
        "POSITION", "ANGLE", "ORIENTATION", "DISPLACEMENT", "DISPLACEMENT_LINEAR",
        "DISPLACEMENT_ANGULAR", "GLOBAL_POSITION", "POSITION_CARTESIAN",
        "TILT", "X_DIMENSION", "Y_DIMENSION", "Z_DIMENSION", "DIAMETER",
        "DEPTH", "SWING_ANGLE", "SWING_RADIUS", "SWING_DIAMETER",
        "LENGTH", "THICKNESS", "ROTATION", "TRANSLATION",
        "VELOCITY", "ACCELERATION", "DECELERATION",
        "ANGULAR_VELOCITY", "ANGULAR_ACCELERATION", "ANGULAR_DECELERATION",
        "AXIS_FEEDRATE", "AXIS_FEEDRATE_OVERRIDE", "PATH_FEEDRATE",
        "PATH_FEEDRATE_OVERRIDE", "PATH_FEEDRATE_PER_REVOLUTION",
        "PATH_POSITION", "ROTARY_VELOCITY", "ROTARY_VELOCITY_OVERRIDE",
        "SPINDLE_SPEED", "CUTTING_SPEED",
        "FOLLOWING_ERROR", "SETTLING_ERROR", "FOLLOWING_ERROR_LINEAR",
        "SETTLING_ERROR_LINEAR", "FOLLOWING_ERROR_ANGULAR", "SETTLING_ERROR_ANGULAR",
    )},
    # Process variables
    **{t: "Process variables" for t in (
        "PRESSURE", "PRESSURE_ABSOLUTE", "PRESSURIZATION_RATE",
        "TEMPERATURE", "DEW_POINT", "HUMIDITY_RELATIVE", "HUMIDITY_ABSOLUTE",
        "HUMIDITY_SPECIFIC", "FLOW", "LEVEL", "FILL_LEVEL", "FILL_HEIGHT",
        "CONCENTRATION", "CONDUCTIVITY", "RESISTIVITY", "PH",
        "DENSITY", "VISCOSITY", "WATER_HARDNESS", "PARTICLE_COUNT",
        "PARTICLE_SIZE", "SOUND_LEVEL", "OPENNESS",
        "VOLUME_FLUID", "CAPACITY_FLUID", "VOLUME_SPATIAL", "CAPACITY_SPATIAL",
    )},
    # Electrical
    **{t: "Electrical" for t in (
        "AMPERAGE", "AMPERAGE_AC", "AMPERAGE_DC",
        "VOLTAGE", "VOLTAGE_AC", "VOLTAGE_DC",
        "VOLT_AMPERE", "VOLT_AMPERE_REACTIVE", "WATTAGE",
        "POWER_FACTOR", "FREQUENCY", "RESISTANCE", "ELECTRICAL_ENERGY",
        "BATTERY_CAPACITY", "BATTERY_CHARGE", "CHARGE_RATE",
        "DISCHARGE_RATE",
    )},
    # Mechanical
    **{t: "Mechanical" for t in (
        "LOAD", "TORQUE", "LINEAR_FORCE", "STRAIN", "TENSION", "MASS",
        "GRAVITATIONAL_FORCE", "GRAVITATIONAL_ACCELERATION",
    )},
    # Environment (rare in factory, common in HVAC / outdoor)
    **{t: "Environment" for t in (
        "LOCATION_ADDRESS", "LOCATION_NARRATIVE", "LOCATION_SPATIAL_GEOGRAPHIC",
        "CLOCK_TIME",
    )},
    # Controller / program
    **{t: "Controller" for t in (
        "EXECUTION", "CONTROLLER_MODE", "CONTROLLER_MODE_OVERRIDE",
        "FUNCTIONAL_MODE", "OPERATING_MODE", "PATH_MODE", "ROTARY_MODE",
        "DIRECTION", "WAIT_STATE", "AXIS_STATE", "AXIS_COUPLING",
        "AXIS_INTERLOCK", "ACTIVE_AXES", "COUPLED_AXES",
        "PROGRAM", "PROGRAM_COMMENT", "PROGRAM_EDIT", "PROGRAM_EDIT_NAME",
        "PROGRAM_HEADER", "PROGRAM_LOCATION", "PROGRAM_LOCATION_TYPE",
        "PROGRAM_NEST_LEVEL", "BLOCK", "BLOCK_COUNT", "LINE", "LINE_LABEL",
        "LINE_NUMBER", "CODE", "MOTION_PROGRAM", "LOGIC_PROGRAM",
    )},
    # Tool
    **{t: "Tool" for t in (
        "TOOL_ASSET_ID", "TOOL_GROUP", "TOOL_ID", "TOOL_NUMBER",
        "TOOL_OFFSET", "TOOL_OFFSETS", "TOOL_CUTTING_ITEM",
        "WORK_OFFSET", "WORK_OFFSETS",
    )},
    # Production / lifecycle
    **{t: "Production" for t in (
        "PART_COUNT", "PART_COUNT_TYPE", "CYCLE_COUNT", "LOAD_COUNT",
        "UNLOAD_COUNT", "TRANSFER_COUNT", "ACTIVATION_COUNT",
        "DEACTIVATION_COUNT", "ASSET_COUNT", "PART_DETECT", "PART_ID",
        "PART_NUMBER", "PART_GROUP_ID", "PART_KIND_ID", "PART_UNIQUE_ID",
        "PART_PROCESSING_STATE", "PART_STATUS", "PART_INDEX",
        "PALLET_ID", "WORKHOLDING_ID", "FIXTURE_ID", "FIXTURE_ASSET_ID",
        "MATERIAL", "MATERIAL_LAYER", "END_OF_BAR",
        "ACCUMULATED_TIME", "PROCESS_TIMER", "EQUIPMENT_TIMER",
        "PROCESS_TIME", "PROCESS_STATE", "PROCESS_OCCURRENCE_ID",
        "PROCESS_AGGREGATE_ID", "PROCESS_KIND_ID", "WIRE",
        "EQUIPMENT_MODE", "OPERATOR_ID", "USER",
    )},
    # State / safety
    **{t: "State / Safety" for t in (
        "AVAILABILITY", "EMERGENCY_STOP", "POWER_STATE", "POWER_STATUS",
        "ACTIVE_POWER_SOURCE", "ACTUATOR_STATE", "VALVE_STATE", "LOCK_STATE",
        "CHUCK_STATE", "CHUCK_INTERLOCK", "SPINDLE_INTERLOCK",
        "DOOR_STATE", "COMPOSITION_STATE", "MESSAGE", "ALARM",
        "BATTERY_STATE", "SENSOR_STATE", "LEAK_DETECT",
        "BINDING_STATE", "MAINTENANCE_LIST",
    )},
    # MTConnect Interfaces (machine-to-machine handshakes)
    **{t: "Interface (machine-to-machine)" for t in (
        "INTERFACE_STATE", "MATERIAL_FEED", "MATERIAL_CHANGE",
        "MATERIAL_RETRACT", "MATERIAL_LOAD", "MATERIAL_UNLOAD",
        "PART_CHANGE", "OPEN_CHUCK", "CLOSE_CHUCK", "OPEN_DOOR", "CLOSE_DOOR",
    )},
    # Asset / metadata
    **{t: "Asset / Metadata" for t in (
        "ASSET_ADDED", "ASSET_CHANGED", "ASSET_REMOVED", "ASSET_COUNT",
        "ASSET_UPDATE_RATE", "ASSOCIATED_ASSET_ID", "TASK_ASSET_ID",
        "DEVICE_ADDED", "DEVICE_REMOVED", "DEVICE_CHANGED", "DEVICE_UUID",
        "DATE_CODE", "SERIAL_NUMBER", "HARDNESS",
    )},
    # Connectivity / system
    **{t: "Connectivity / System" for t in (
        "CONNECTION_STATUS", "ADAPTER_SOFTWARE_VERSION", "ADAPTER_URI",
        "MTCONNECT_VERSION", "OBSERVATION_UPDATE_RATE",
        "HOST_NAME", "NETWORK_PORT", "OPERATING_SYSTEM",
        "FIRMWARE", "APPLICATION", "LIBRARY", "NETWORK",
        "SENSOR_ATTACHMENT", "COMPONENT_DATA",
    )},
    # Measurement / characterisation
    **{t: "Measurement" for t in (
        "FEATURE_PERSISTENT_ID", "FEATURE_MEASUREMENT",
        "CHARACTERISTIC_PERSISTENT_ID", "CHARACTERISTIC_STATUS",
        "MEASUREMENT_TYPE", "MEASUREMENT_VALUE", "MEASUREMENT_UNITS",
        "UNCERTAINTY", "UNCERTAINTY_TYPE",
        "CONTROL_LIMIT", "CONTROL_LIMITS",
        "SPECIFICATION_LIMIT", "SPECIFICATION_LIMITS",
        "ALARM_LIMIT", "ALARM_LIMITS",
    )},
    # Conditions
    **{t: "Conditions" for t in (
        "SYSTEM", "ACTUATOR", "COMMUNICATIONS", "HARDWARE",
        "DATA_RANGE",
    )},
    # Generic
    "VARIABLE": "Other / Generic",
}


def domain_for_type(type_id: str) -> str:
    return _DOMAIN_MAP.get(type_id, "Other / Generic")


# ----------------------------------------------------------------------------
# Compose runtime MTConnectType objects from the three layers
# ----------------------------------------------------------------------------


def _build_runtime_type(spec: SpecType) -> MTConnectType:
    units: list[str] = []
    if spec.default_units:
        units.append(spec.default_units)
    for alt in UNIT_ALTERNATIVES.get(spec.type_id, ()):
        if alt not in units:
            units.append(alt)
    return MTConnectType(
        name=spec.type_id,
        category=spec.category,
        allowed_units=tuple(units),
        common_subtypes=tuple(spec.common_subtypes),
        description=spec.description,
        autocomplete_keywords=KEYWORD_HINTS.get(spec.type_id, ()),
    )


_BY_NAME: dict[str, MTConnectType] = {}
_BY_CATEGORY: dict[str, list[MTConnectType]] = {"SAMPLE": [], "EVENT": [], "CONDITION": []}

for _spec in SPEC_TYPES:
    _rt = _build_runtime_type(_spec)
    _BY_NAME[_rt.name] = _rt
    _BY_CATEGORY.setdefault(_rt.category, []).append(_rt)

# Sort each category alphabetically for the dropdown ordering
for _cat, _items in _BY_CATEGORY.items():
    _items.sort(key=lambda t: t.name)


# ----------------------------------------------------------------------------
# Public API (back-compat with the previous module)
# ----------------------------------------------------------------------------


def types_for_category(category: str) -> list[MTConnectType]:
    return list(_BY_CATEGORY.get(category.upper(), ()))


def lookup_type(name: str) -> MTConnectType | None:
    return _BY_NAME.get(name)


def suggest_type(browse_name: str, category: str) -> MTConnectType:
    bn = browse_name.lower()
    cat = category.upper()

    # Try priority-ordered matches first (curated keyword hints)
    for type_id in _TYPE_MATCH_PRIORITY:
        t = _BY_NAME.get(type_id)
        if t is None or t.category != cat:
            continue
        for kw in t.autocomplete_keywords:
            if kw and kw in bn:
                return t

    # Then any keyword hint in any matching-category type
    for t in _BY_CATEGORY.get(cat, ()):
        for kw in t.autocomplete_keywords:
            if kw and kw in bn:
                return t

    # Fallback: VARIABLE (generic placeholder) if present in that category
    fallback = _BY_NAME.get("VARIABLE")
    if fallback is not None and fallback.category == cat:
        return fallback
    # Last resort: synthesize a stub
    return MTConnectType(
        name="VARIABLE",
        category=cat,
        allowed_units=(),
        common_subtypes=(),
        description="Generic placeholder; no spec match found.",
        autocomplete_keywords=(),
    )


def suggest_units(t: MTConnectType, browse_name: str) -> str:
    bn = browse_name.lower()
    for unit in t.allowed_units:
        if unit and unit.lower() in bn:
            return unit
    return t.allowed_units[0] if t.allowed_units else ""

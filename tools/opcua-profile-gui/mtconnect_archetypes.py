"""Machine archetypes for biasing the type autocomplete and filtering the dropdown.

An archetype is a hand-curated whitelist of MTConnect type names that are
commonly relevant for a class of equipment (CNC mill, coolant unit, packaging
machine, etc.). The Connect dialog lets you pick one per connection; the GUI
then:

  - Filters the type dropdown to the archetype's whitelist by default (toggle
    "Show all spec types" to override).
  - Biases the BrowseName → type autocomplete toward whitelisted types.

Add new archetypes here as new equipment classes are encountered. Keep lists
focused — too broad and the filter loses value; too narrow and users hit "Show
all" constantly. Aim for ~30–60 types per archetype.

Domain assignments (for the "Group by domain" toggle) live in mtconnect_vocab.py.
"""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class MachineArchetype:
    """Curated equipment-class profile that scopes the relevant MTConnect types."""

    name: str
    description: str
    type_whitelist: frozenset[str]


# Generic = no filter; whitelist is empty so all types show through.
GENERIC = MachineArchetype(
    name="Generic (no filter)",
    description="Show every MTConnect spec type. Use when you don't know what kind of equipment "
                "you're forwarding from, or for mixed devices.",
    type_whitelist=frozenset(),
)


CNC_MILL = MachineArchetype(
    name="CNC Mill (3 / 5-axis)",
    description="Milling machine — Sinumerik 840D, Fanuc, Heidenhain, Mazak SMooth, etc. Includes "
                "linear/rotary axes, spindle, controller program/execution, tooling.",
    type_whitelist=frozenset({
        # Motion
        "POSITION", "ANGLE", "AXIS_FEEDRATE", "AXIS_FEEDRATE_OVERRIDE",
        "PATH_FEEDRATE", "PATH_FEEDRATE_OVERRIDE", "PATH_FEEDRATE_PER_REVOLUTION",
        "PATH_POSITION", "VELOCITY", "ACCELERATION", "ANGULAR_VELOCITY",
        "ANGULAR_ACCELERATION", "DECELERATION", "ANGULAR_DECELERATION",
        "FOLLOWING_ERROR", "SETTLING_ERROR", "DISPLACEMENT",
        "ROTARY_VELOCITY", "ROTARY_VELOCITY_OVERRIDE", "SPINDLE_SPEED",
        "CUTTING_SPEED",
        # Loads / measurements
        "LOAD", "TEMPERATURE", "AMPERAGE", "AMPERAGE_AC", "AMPERAGE_DC",
        "VOLTAGE", "VOLTAGE_AC", "VOLTAGE_DC", "WATTAGE", "TORQUE",
        "STRAIN", "LINEAR_FORCE",
        # Controller
        "EXECUTION", "CONTROLLER_MODE", "CONTROLLER_MODE_OVERRIDE",
        "PROGRAM", "PROGRAM_COMMENT", "PROGRAM_HEADER", "PROGRAM_LOCATION",
        "PROGRAM_LOCATION_TYPE", "PROGRAM_NEST_LEVEL", "BLOCK", "BLOCK_COUNT",
        "LINE", "LINE_LABEL", "LINE_NUMBER", "CODE", "PATH_MODE",
        "ROTARY_MODE", "FUNCTIONAL_MODE", "DIRECTION", "WAIT_STATE",
        # Tool
        "TOOL_ASSET_ID", "TOOL_GROUP", "TOOL_ID", "TOOL_NUMBER",
        "TOOL_OFFSET", "WORK_OFFSET",
        # State / safety
        "AVAILABILITY", "EMERGENCY_STOP", "POWER_STATE", "POWER_STATUS",
        "EQUIPMENT_MODE", "OPERATING_MODE", "AXIS_STATE", "AXIS_COUPLING",
        "AXIS_INTERLOCK", "ACTUATOR_STATE", "CHUCK_STATE", "DOOR_STATE",
        "ACTIVE_AXES", "COUPLED_AXES", "MESSAGE",
        # Production
        "PART_COUNT", "PART_COUNT_TYPE", "CYCLE_COUNT", "PROCESS_TIMER",
        "PROCESS_TIME", "ACCUMULATED_TIME",
        # Connection / metadata
        "CONNECTION_STATUS", "ADAPTER_SOFTWARE_VERSION", "OPERATING_SYSTEM",
        "FIRMWARE", "APPLICATION", "SERIAL_NUMBER",
        # Conditions
        "SYSTEM", "ACTUATOR", "COMMUNICATIONS", "HARDWARE",
        "LOGIC_PROGRAM", "MOTION_PROGRAM", "DATA_RANGE",
        # Generic fallback
        "VARIABLE",
    }),
)


CNC_LATHE = MachineArchetype(
    name="CNC Lathe / Turning",
    description="Turning machine — Mazak, Okuma, Hardinge, etc. Emphasises spindle, chuck, "
                "part transfer between turret stations.",
    type_whitelist=frozenset({
        # Motion (lathes typically have X, Z; sometimes C and Y)
        "POSITION", "AXIS_FEEDRATE", "AXIS_FEEDRATE_OVERRIDE",
        "PATH_FEEDRATE", "PATH_FEEDRATE_OVERRIDE", "PATH_FEEDRATE_PER_REVOLUTION",
        "CUTTING_SPEED", "VELOCITY", "DECELERATION",
        # Spindle (the centerpiece of a lathe)
        "ROTARY_VELOCITY", "ROTARY_VELOCITY_OVERRIDE", "SPINDLE_SPEED",
        "ROTARY_MODE",
        # Loads
        "LOAD", "TEMPERATURE", "AMPERAGE", "AMPERAGE_AC", "VOLTAGE", "TORQUE",
        # Controller
        "EXECUTION", "CONTROLLER_MODE", "PROGRAM", "PROGRAM_COMMENT",
        "BLOCK", "LINE_NUMBER", "PATH_MODE", "DIRECTION", "WAIT_STATE",
        # Tool / turret
        "TOOL_ASSET_ID", "TOOL_GROUP", "TOOL_ID", "TOOL_NUMBER", "TOOL_OFFSET",
        "TOOL_CUTTING_ITEM", "WORK_OFFSET",
        # Chuck / part handling (key for lathes)
        "CHUCK_STATE", "CHUCK_INTERLOCK", "SPINDLE_INTERLOCK",
        "PART_DETECT", "END_OF_BAR",
        # State / safety
        "AVAILABILITY", "EMERGENCY_STOP", "POWER_STATE", "EQUIPMENT_MODE",
        "OPERATING_MODE", "AXIS_STATE", "ACTUATOR_STATE", "DOOR_STATE",
        "MESSAGE",
        # Production
        "PART_COUNT", "CYCLE_COUNT", "ACCUMULATED_TIME", "PROCESS_TIME",
        # Conditions
        "SYSTEM", "ACTUATOR", "COMMUNICATIONS", "HARDWARE",
        "LOGIC_PROGRAM", "MOTION_PROGRAM",
        "VARIABLE",
    }),
)


ROBOT = MachineArchetype(
    name="Robot (industrial / SCARA / 6-axis)",
    description="Industrial robot arm — ABB, Fanuc Robotics, KUKA, Yaskawa, UR. Includes joint "
                "positions, end-effector orientation, MTConnect Interface handshakes for material "
                "transfer between cells.",
    type_whitelist=frozenset({
        # Joints + end-effector
        "POSITION", "ANGLE", "ORIENTATION", "GLOBAL_POSITION", "POSITION_CARTESIAN",
        "VELOCITY", "ANGULAR_VELOCITY", "ACCELERATION", "ANGULAR_ACCELERATION",
        "AXIS_FEEDRATE", "TORQUE", "LINEAR_FORCE", "STRAIN", "TENSION",
        "FOLLOWING_ERROR", "SETTLING_ERROR", "DISPLACEMENT",
        # Loads / state
        "LOAD", "AMPERAGE", "VOLTAGE", "WATTAGE", "TEMPERATURE",
        # Controller / program
        "EXECUTION", "CONTROLLER_MODE", "PROGRAM", "PROGRAM_COMMENT",
        "BLOCK", "LINE_NUMBER", "WAIT_STATE", "ACTIVE_AXES",
        # State / safety
        "AVAILABILITY", "EMERGENCY_STOP", "POWER_STATE", "EQUIPMENT_MODE",
        "OPERATING_MODE", "AXIS_STATE", "ACTUATOR_STATE", "MESSAGE",
        # MTConnect Interface (handshakes with adjacent equipment)
        "INTERFACE_STATE", "MATERIAL_FEED", "MATERIAL_CHANGE", "MATERIAL_RETRACT",
        "MATERIAL_LOAD", "MATERIAL_UNLOAD", "PART_CHANGE",
        "OPEN_CHUCK", "CLOSE_CHUCK", "OPEN_DOOR", "CLOSE_DOOR",
        "PART_DETECT", "GRAVITATIONAL_FORCE",
        # Production
        "CYCLE_COUNT", "ACCUMULATED_TIME", "PROCESS_TIME", "PART_COUNT",
        # Battery (mobile / cobots)
        "BATTERY_CHARGE", "BATTERY_STATE", "BATTERY_CAPACITY",
        "CHARGE_RATE", "DISCHARGE_RATE",
        # Conditions
        "SYSTEM", "ACTUATOR", "COMMUNICATIONS", "HARDWARE",
        "LOGIC_PROGRAM", "MOTION_PROGRAM",
        "VARIABLE",
    }),
)


COOLANT_UNIT = MachineArchetype(
    name="Coolant / Filtration unit",
    description="Process auxiliary equipment — coolant filtration, chillers, hydraulic pumps, "
                "tank-level monitoring. Heavy on pressures, temperatures, levels, and runtime "
                "hours per pump.",
    type_whitelist=frozenset({
        # Process variables
        "PRESSURE", "PRESSURE_ABSOLUTE", "PRESSURIZATION_RATE",
        "TEMPERATURE", "DEW_POINT", "HUMIDITY_RELATIVE", "HUMIDITY_ABSOLUTE",
        "FLOW", "LEVEL", "FILL_LEVEL", "FILL_HEIGHT",
        "CONCENTRATION", "CONDUCTIVITY", "RESISTIVITY", "PH",
        "DENSITY", "VISCOSITY", "WATER_HARDNESS",
        "PARTICLE_COUNT", "PARTICLE_SIZE",
        "VOLUME_FLUID", "CAPACITY_FLUID", "MASS",
        # Pump / motor measurements
        "LOAD", "AMPERAGE", "AMPERAGE_AC", "VOLTAGE", "VOLTAGE_AC",
        "WATTAGE", "ROTARY_VELOCITY", "FREQUENCY", "POWER_FACTOR",
        # State
        "AVAILABILITY", "EMERGENCY_STOP", "POWER_STATE", "POWER_STATUS",
        "EQUIPMENT_MODE", "OPERATING_MODE", "ACTUATOR_STATE",
        "VALVE_STATE", "LEAK_DETECT", "MESSAGE",
        # Runtime
        "ACCUMULATED_TIME", "PROCESS_TIMER", "EQUIPMENT_TIMER",
        "MAINTENANCE_LIST", "PROCESS_STATE",
        # Conditions
        "SYSTEM", "ACTUATOR", "COMMUNICATIONS", "HARDWARE", "DATA_RANGE",
        "VARIABLE",
    }),
)


PACKAGING_ASSEMBLY = MachineArchetype(
    name="Packaging / Assembly cell",
    description="Discrete production equipment — packaging lines, assembly cells, palletisers. "
                "Heavy on cycle counts, part counts, equipment-mode transitions, and Interface "
                "handshakes between stations.",
    type_whitelist=frozenset({
        # Production
        "PART_COUNT", "PART_COUNT_TYPE", "CYCLE_COUNT", "LOAD_COUNT",
        "UNLOAD_COUNT", "TRANSFER_COUNT", "ACTIVATION_COUNT", "DEACTIVATION_COUNT",
        "PART_DETECT", "PART_ID", "PART_NUMBER", "PART_GROUP_ID",
        "PART_KIND_ID", "PART_UNIQUE_ID", "PART_PROCESSING_STATE",
        "PART_STATUS", "PART_INDEX",
        # State / lifecycle
        "AVAILABILITY", "EMERGENCY_STOP", "POWER_STATE", "EQUIPMENT_MODE",
        "OPERATING_MODE", "PROCESS_STATE", "FUNCTIONAL_MODE",
        "WAIT_STATE", "ACTUATOR_STATE", "VALVE_STATE", "DOOR_STATE",
        "MESSAGE",
        # MTConnect Interface (station-to-station coordination)
        "INTERFACE_STATE", "MATERIAL_FEED", "MATERIAL_CHANGE", "MATERIAL_RETRACT",
        "MATERIAL_LOAD", "MATERIAL_UNLOAD", "PART_CHANGE",
        "OPEN_CHUCK", "CLOSE_CHUCK", "OPEN_DOOR", "CLOSE_DOOR",
        # Tool / consumable
        "TOOL_ID", "TOOL_NUMBER", "MAINTENANCE_LIST",
        # Runtime
        "ACCUMULATED_TIME", "PROCESS_TIMER", "EQUIPMENT_TIMER", "PROCESS_TIME",
        # Motion (some packaging cells have axes)
        "POSITION", "VELOCITY", "LOAD", "TEMPERATURE",
        # Controller (when present)
        "EXECUTION", "CONTROLLER_MODE", "PROGRAM", "BLOCK", "LINE_NUMBER",
        # Conditions
        "SYSTEM", "ACTUATOR", "COMMUNICATIONS", "HARDWARE", "LOGIC_PROGRAM",
        "VARIABLE",
    }),
)


GENERIC_PLC = MachineArchetype(
    name="Generic PLC / Auxiliary",
    description="S7-1500, AB ControlLogix, or any generic PLC forwarding mixed tags. Broad "
                "coverage of measurement + state types, biased toward the common ones.",
    type_whitelist=frozenset({
        # Measurements
        "PRESSURE", "TEMPERATURE", "FLOW", "LEVEL", "FILL_LEVEL",
        "AMPERAGE", "AMPERAGE_AC", "AMPERAGE_DC",
        "VOLTAGE", "VOLTAGE_AC", "VOLTAGE_DC", "WATTAGE", "FREQUENCY",
        "POWER_FACTOR", "POSITION", "ANGLE", "VELOCITY", "ROTARY_VELOCITY",
        "ACCELERATION", "LOAD", "TORQUE", "MASS", "HUMIDITY_RELATIVE",
        "DEW_POINT", "SOUND_LEVEL", "PH", "CONCENTRATION", "VISCOSITY",
        # State
        "AVAILABILITY", "EMERGENCY_STOP", "POWER_STATE", "POWER_STATUS",
        "EQUIPMENT_MODE", "OPERATING_MODE", "PROCESS_STATE",
        "VALVE_STATE", "DOOR_STATE", "ACTUATOR_STATE", "LEAK_DETECT",
        "MESSAGE", "PART_DETECT", "CONNECTION_STATUS",
        # Counters / runtime
        "PART_COUNT", "CYCLE_COUNT", "ACCUMULATED_TIME", "PROCESS_TIMER",
        "EQUIPMENT_TIMER", "MAINTENANCE_LIST",
        # Conditions
        "SYSTEM", "ACTUATOR", "COMMUNICATIONS", "HARDWARE", "DATA_RANGE",
        "VARIABLE",
    }),
)


ARCHETYPES: list[MachineArchetype] = [
    GENERIC,
    CNC_MILL,
    CNC_LATHE,
    ROBOT,
    COOLANT_UNIT,
    PACKAGING_ASSEMBLY,
    GENERIC_PLC,
]


def archetype_by_name(name: str) -> MachineArchetype:
    for a in ARCHETYPES:
        if a.name == name:
            return a
    return GENERIC


def archetype_filters(archetype: MachineArchetype, type_id: str) -> bool:
    """True if a type should be hidden by this archetype's filter (returns False
    for Generic which keeps everything visible)."""
    if not archetype.type_whitelist:
        return False
    return type_id not in archetype.type_whitelist

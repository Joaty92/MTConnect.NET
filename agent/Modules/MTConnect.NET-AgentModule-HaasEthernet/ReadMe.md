# MTConnect.NET-AgentModule-HaasEthernet

> Part of the [fork additions](../../../FORK.md) to MTConnect.NET.

In-process agent module that polls a **Haas CNC** over TCP with Q-code and macro-variable commands and pushes the parsed values into the agent's observation buffer. The usual wiring is a **Moxa NPort** serial-to-Ethernet converter on the Haas HMI's RS-232 port; a direct-Ethernet Haas HMI works the same way.

The polling engine lives in [`libraries/MTConnect.NET-Haas`](../../../libraries/MTConnect.NET-Haas/); this module wires it to the agent under a configured `deviceKey`.

## Configuration

Add a `- haas-ethernet:` block under `modules:` in `agent.config.yaml`. One block per machine.

```yaml
- haas-ethernet:
    deviceKey: HAAS-01             # must match a device the agent knows (devices/<key>.xml)
    server: 192.168.127.254        # Moxa NPort IP (or direct-Ethernet Haas HMI)
    port: 4001                     # Moxa NPort port 1 = 4001, port 2 = 4002, …
    pollInterval: 1.0              # seconds between full poll cycles
    commands:                      # optional extra Q-codes beyond the 11 built-ins
    - { name: air_pressure, command: "?Q999", pollEvery: 30 }
    macroVariables:                # optional extra macros beyond the 8 built-ins
    - { name: feedrate, variable: 3003 }
```

| Key              | Required | Default     | Notes                                                                      |
| ---------------- | -------- | ----------- | --------------------------------------------------------------------------- |
| `deviceKey`      | yes      | —           | Device the observations belong to; DataItem keys below must exist in its device model (matched by Name, Id, or Source). |
| `server`         | yes      | `localhost` | Moxa NPort or Haas HMI address.                                              |
| `port`           | no       | `4001`      | Moxa NPort maps serial port 1 → TCP 4001, port 2 → 4002, …                   |
| `pollInterval`   | no       | `1.0`       | Seconds between poll cycles (all commands + macros, respecting throttles).   |
| `commands`       | no       | —           | Extra Q-codes. Each: `name` (DataItem key), `command` (e.g. `"?Q999"`), optional `pollOnce: true`, optional `pollEvery: <seconds>`. Unknown Q-codes publish their raw response string under `name`. |
| `macroVariables` | no       | —           | Extra macro registers, read via `?Q600 <variable>`. Each: `name`, `variable`, optional `pollEvery`. |

## Built-in polling set

These are always polled — user entries are additions, not replacements.

**Q-code commands** (11):

| DataItem key       | Q-code  | Cadence   | Also derives                                              |
| ------------------ | ------- | --------- | ---------------------------------------------------------- |
| `serial_number`    | `?Q100` | once/session | `avail` (`AVAILABLE` when Q100 answers)                  |
| `software_version` | `?Q101` | once/session |                                                          |
| `model`            | `?Q102` | once/session |                                                          |
| `mode`             | `?Q104` | every cycle | `controller_mode` (MTConnect enum), `zero_ret` condition |
| `power_on_time`    | `?Q300` | every cycle |                                                          |
| `cutting_time`     | `?Q301` | every cycle |                                                          |
| `last_cycle_time`  | `?Q303` | every cycle |                                                          |
| `prev_cycle_time`  | `?Q304` | every cycle |                                                          |
| `m30_counter_1`    | `?Q402` | every cycle |                                                          |
| `m30_counter_2`    | `?Q403` | every cycle |                                                          |
| `program_status`   | `?Q500` | every cycle | `program`, `execution`, `estop`, `system`, `partCount`   |

**Macro variables** (8), read via `?Q600`:

| DataItem key    | Macro | Meaning                  |
| --------------- | ----- | ------------------------ |
| `x_pos`         | 5041  | X work position          |
| `y_pos`         | 5042  | Y work position          |
| `z_pos`         | 5043  | Z work position          |
| `x_machine_pos` | 5021  | X machine position       |
| `y_machine_pos` | 5022  | Y machine position       |
| `z_machine_pos` | 5023  | Z machine position       |
| `single_block`  | 3030  | Single-block mode        |
| `last_m_code`   | 4113  | Last M-code executed     |

Your `devices/<deviceKey>.xml` needs DataItems whose **names match these keys** (plus any extras you add). Keys with no matching DataItem are silently dropped by the agent.

## Availability and reconnect behaviour

- `avail` flips to `AVAILABLE` when `?Q100` answers. The moment any command times out or the connection drops, **every data item** (built-in, user-added, and derived keys alike) is flagged `UNAVAILABLE` — consumers never see hours-old values served as current during an outage — and the rest of that poll cycle is skipped.
- On recovery the in-progress cycle is aborted and restarted, so `avail=AVAILABLE` (from the re-polled `?Q100`) always precedes the first fresh data. The once-per-session values (serial number, software version, model) are re-polled automatically.
- **Time base:** Haas Q-codes carry no machine clock, so observations are stamped with the **agent host's clock at receipt**. Keep the agent host NTP-synced (see the starter-kit README's time-synchronization section) — and note this differs from the OPC UA adapter, which passes the controller's own source timestamps through.
- The transport uses a bounded TCP connect (5 s) and terminator-aware reads (end-of-line / `>` prompt, 150 ms quiet-period fallback), so a dead Moxa can't hang the poll thread and replies return in milliseconds — the practical cycle floor is the machine's own response latency, not fixed sleeps.
- Transport errors (wrong IP, refused connection, timeouts) surface in the module log — if you see `started polling` and then silence *with* errors, check the address; silence *without* errors means the Haas simply isn't answering Q-codes.

## Machine-side setup notes

- On the Moxa NPort: operation mode **TCP Server**, one TCP port per serial port (4001, 4002, …), serial parameters matched to the Haas RS-232 settings.
- On the Haas control: enable the serial Q-code interface (Settings 143 "Machine Data Collect" enables Q/E-code data collection on classic controls; baud/parity settings must match the Moxa).
- Nothing else runs on the machine — the module is a pure network client.

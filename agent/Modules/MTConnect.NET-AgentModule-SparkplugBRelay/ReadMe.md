# MTConnect.NET-AgentModule-SparkplugBRelay

In-process agent module that bridges every device on this agent to **Sparkplug B** over MQTT. Each MTConnect device becomes a Sparkplug "Device" under one shared Edge Node identity; observations are republished as `DDATA` as they land in the agent buffer.

Implements the full Sparkplug B lifecycle:

- **NBIRTH** on first broker connect — declares this Edge Node.
- **DBIRTH** per device on connect / `DeviceAdded` — full current-value snapshot for every DataItem in the device.
- **DDATA** per observation as values change.
- **DDEATH** when a device flips to `AVAILABILITY=UNAVAILABLE`, with a re-`DBIRTH` if it comes back.
- **NDEATH** is published as the broker's last-will if the agent process dies — every downstream consumer sees all devices on this node go offline simultaneously.
- **NCMD Node Control/Rebirth** is honoured: a primary host sending `Node Control/Rebirth=true` causes the module to republish NBIRTH + every DBIRTH.

## Configuration

Add a `- sparkplug-relay:` block under `modules:` in `agent.config.yaml`. One block per Sparkplug Edge Node identity (you can have multiple blocks publishing to different brokers or different group/node namespaces — each spawns its own MQTT session).

```yaml
- sparkplug-relay:
    server: host.docker.internal             # MQTT broker hostname
    port: 1883                               # broker port
    clientId: MTConnect-Trakhound            # optional; defaults to MTConnect-{guid}
    username: mtconnect                      # optional
    password: hunter2                        # optional
    groupId: Factory                            # Sparkplug Group ID (top-level namespace)
    nodeId: Machine                           # Sparkplug Edge Node ID (this agent's identity)
    reconnectInterval: 10000                 # ms between reconnect attempts
```

### Field reference

| Key                 | Required | Default                     | Notes                                                                                          |
| ------------------- | -------- | --------------------------- | ---------------------------------------------------------------------------------------------- |
| `server`            | yes      | —                           | MQTT broker hostname or IP. Use `host.docker.internal` from inside a container to reach the host. |
| `port`              | no       | `1883`                      | Standard MQTT port. Use `8883` for TLS (TLS support inherited from SparkplugNet client).         |
| `clientId`          | no       | `MTConnect-{guid}`           | MQTT client identifier. Override to make broker logs readable.                                  |
| `username`          | no       | —                           | Plaintext over the connection unless TLS is enabled.                                            |
| `password`          | no       | —                           | Plaintext over the connection unless TLS is enabled.                                            |
| `groupId`           | yes      | `MTConnect`                  | Sparkplug Group ID (first path segment in topics: `spBv1.0/{groupId}/...`).                     |
| `nodeId`            | yes      | `Agent`                     | Sparkplug Edge Node ID (`spBv1.0/{groupId}/DBIRTH/{nodeId}/{deviceKey}`).                       |
| `reconnectInterval` | no       | `10000`                     | Milliseconds between reconnect attempts after the broker connection drops.                      |

## Resulting topic structure

For an agent with `groupId: Factory`, `nodeId: Machine`, and devices `Machine-191` + `coolant1`:

```
spBv1.0/Factory/NBIRTH/Machine                    ← edge node birth (once per connect)
spBv1.0/Factory/DBIRTH/Machine/Machine-191           ← device birth (per device, on connect or DeviceAdded)
spBv1.0/Factory/DBIRTH/Machine/coolant1
spBv1.0/Factory/DDATA/Machine/Machine-191            ← per-observation updates after birth
spBv1.0/Factory/DDATA/Machine/coolant1
spBv1.0/Factory/DDEATH/Machine/Machine-191           ← device offline (AVAILABILITY=UNAVAILABLE)
spBv1.0/Factory/NDEATH/Machine                    ← last-will if the agent process dies
spBv1.0/Factory/NCMD/Machine                      ← consumed: Rebirth requests from primary host
```

## How observations flow

1. Any input module (SHDR adapter, OPC UA adapter, HTTP adapter) writes an observation into the agent's shared buffer via `AddObservation(...)`.
2. The relay subscribes to the buffer's `ObservationAdded` event.
3. For each observation the relay:
   - Looks up the device by `DeviceUuid` in its `_deviceNames` map (populated on `DeviceAdded` / agent connect).
   - If the device is in the `Connected` state, builds a Sparkplug `Metric` from the observation and publishes it as `DDATA` on the device's topic.
   - If the device is in the `Initial` (waiting for first AVAILABILITY) or `Disconnected` state, suppresses the publish — the spec requires no `DDATA` before `DBIRTH` re-establishes the device.

### Per-device state machine

Each device is tracked in one of three lifecycle states, driven by `AVAILABILITY`:

| State          | Set when                                                                | Effect                                                                               |
| -------------- | ----------------------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| `Initial`      | After `DBIRTH` is published, before the first AVAILABILITY observation. | DDATA suppressed; published DBIRTH already declared current values.                  |
| `Connected`    | AVAILABILITY transitions to `AVAILABLE`.                                | DDATA flows freely on value change.                                                  |
| `Disconnected` | AVAILABILITY transitions to `UNAVAILABLE`. Atomic CAS prevents races.   | `DDEATH` published once on transition. DDATA suppressed until re-DBIRTH on recovery. |

DBIRTH is republished whenever a device transitions Disconnected → Connected, so subscribers always see a fresh value set before DDATA resumes.

## Value coercion

| MTConnect category   | Sparkplug type                                                              |
| -------------------- | --------------------------------------------------------------------------- |
| `SAMPLE`             | `Double` always (forced — Sparkplug DataType must match the DBIRTH declaration; coerces with `InvariantCulture` parsing, defaulting to `0.0` on parse failure or unavailable). |
| `EVENT` / `CONDITION` | `String` pass-through.                                                       |
| Unavailable          | `0.0` (SAMPLEs) or `"UNAVAILABLE"` (EVENT / CONDITION).                      |

The reason SAMPLEs are forced to `Double` even when unavailable: Sparkplug requires the data type of a metric to match what was declared in `DBIRTH`. Publishing a string "UNAVAILABLE" for a metric declared as `Double` is a protocol violation that most consumers reject.

## Sharing the Edge Node across all devices on this agent

All MTConnect devices on this agent share **one Sparkplug Edge Node identity** (`groupId` + `nodeId`). Consequences:

- **One NDEATH for all devices.** If the agent process dies, every device under this Edge Node flips to offline simultaneously from a Sparkplug consumer's perspective. Usually what you want; just be aware it's a fleet-wide event.
- **One MQTT session for all devices.** The Sparkplug client maintains a single connection — easier on the broker than N connections, but a connectivity blip affects every device's DDATA stream.
- **No per-device routing customisation** without splitting into multiple agent containers (each with its own `sparkplug-relay:` block under a different `nodeId` / `groupId`). See [`deployment/ARCHITECTURE.md`](../../../) for the deployment patterns at scale.

## Notes

- Built on top of [SparkplugNet](https://github.com/SeppPenner/SparkplugNet) (the most mature open .NET Sparkplug B library).
- TLS, x509 client-cert auth, and primary-host coordination are supported by the underlying library but not currently surfaced through this module's configuration — wire them in if needed.
- The relay does **not** consume DCMD or DBIRTH from other Sparkplug nodes (it's a pure outbound relay). MTConnect → Sparkplug only.

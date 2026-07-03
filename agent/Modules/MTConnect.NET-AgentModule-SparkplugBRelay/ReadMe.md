# MTConnect.NET-AgentModule-SparkplugBRelay

> Part of the [fork additions](../../../FORK.md) to MTConnect.NET.

In-process agent module that bridges every device on this agent to **Sparkplug B** over MQTT. Each MTConnect device becomes a Sparkplug "Device" under one shared Edge Node identity; observations are republished as `DDATA` as they land in the agent buffer.

Implements the full Sparkplug B lifecycle:

- **NBIRTH** on first broker connect — declares this Edge Node (includes the spec-required `Node Control/Rebirth` metric).
- **DBIRTH** per device on connect / `DeviceAdded` — full current-value snapshot for every DataItem in the device.
- **DDATA** batched per device as values change.
- **DDEATH** when a device flips to `AVAILABILITY=UNAVAILABLE`, with a re-`DBIRTH` if it comes back.
- **NDEATH** is registered as the broker's last-will — if the agent process dies, every downstream consumer sees all devices on this node go offline simultaneously.
- **NCMD Node Control/Rebirth** is honoured: a primary host sending `Node Control/Rebirth=true` causes the module to republish NBIRTH + every DBIRTH.

## Configuration

Add a `- sparkplug-relay:` block under `modules:` in `agent.config.yaml`. One block per Sparkplug Edge Node identity (you can have multiple blocks publishing to different brokers or different group/node namespaces — each spawns its own MQTT session).

```yaml
- sparkplug-relay:
    server: broker.example.com               # MQTT broker hostname
    port: 8883                               # broker port (8883 = conventional TLS port)
    clientId: MTConnect-Agent1               # optional; defaults to MTConnect-{guid}
    username: mtconnect                      # optional
    password: hunter2                        # optional
    groupId: Factory                         # Sparkplug Group ID (top-level namespace)
    nodeId: Agent1                           # Sparkplug Edge Node ID (this agent's identity)
    reconnectInterval: 10000                 # ms between reconnect attempts
    useTls: true                             # optional; see "TLS and client certificates"
    caCertificateFile: certs/broker-ca.crt   # optional; pin the broker's CA
    clientCertificateFile: certs/agent1.pfx  # optional; mutual-TLS client cert
    clientCertificatePassword: changeit      # optional; PFX password
```

### Field reference

| Key                         | Required | Default            | Notes                                                                                             |
| --------------------------- | -------- | ------------------ | ------------------------------------------------------------------------------------------------- |
| `server`                    | yes      | —                  | MQTT broker hostname or IP. Use `host.docker.internal` from inside a container to reach the host.  |
| `port`                      | no       | `1883`             | Standard MQTT port. Use `8883` (or your broker's TLS listener) together with `useTls: true`.       |
| `clientId`                  | no       | `MTConnect-{guid}` | MQTT client identifier. Override to make broker logs readable.                                     |
| `username`                  | no       | —                  | Plaintext over the connection unless TLS is enabled.                                               |
| `password`                  | no       | —                  | Plaintext over the connection unless TLS is enabled.                                               |
| `groupId`                   | yes      | `MTConnect`        | Sparkplug Group ID (first path segment in topics: `spBv1.0/{groupId}/...`).                        |
| `nodeId`                    | yes      | `Agent`            | Sparkplug Edge Node ID (`spBv1.0/{groupId}/DBIRTH/{nodeId}/{deviceKey}`).                          |
| `reconnectInterval`         | no       | `10000`            | Milliseconds between reconnect attempts after the broker connection drops.                         |
| `useTls`                    | no       | `false`            | Enables TLS on the MQTT connection.                                                                |
| `caCertificateFile`         | no       | —                  | Broker CA certificate (PEM or DER). Pins server-cert validation to this CA instead of the OS store.|
| `clientCertificateFile`     | no       | —                  | PKCS#12 (`.pfx`/`.p12`) bundle with the client certificate + private key → mutual TLS.             |
| `clientCertificatePassword` | no       | —                  | Password for the PKCS#12 bundle.                                                                   |
| `allowUntrustedCertificates`| no       | `false`            | Skips server-cert validation entirely. Test rigs only — never production.                          |

## TLS and client certificates

Three trust modes, from loosest to strictest:

1. **`useTls: true` alone** — the broker certificate is validated against the operating system trust store (the right choice when the broker uses a certificate from a public or corporate CA that the host already trusts).
2. **`useTls: true` + `caCertificateFile`** — the broker certificate must chain to exactly the CA in the file (custom root trust). Use this for self-managed / self-signed broker CAs; nothing needs to be installed into the OS store. The broker certificate's CN/SAN must still match `server` — hostname mismatches are rejected.
3. **`useTls: true` + `allowUntrustedCertificates: true`** — no server validation at all. Only for bench testing.

Adding `clientCertificateFile` to any of the above enables **mutual TLS**: the relay presents the client certificate during the handshake, so brokers configured with `require_certificate` (Mosquitto) or client-cert authentication (EMQX/HiveMQ) can authenticate the agent without a username/password.

Practical notes:

- Relative paths resolve against the **agent binary directory** (`/app` in the shipped container). In the starter-kit, drop certs in `config/certs/` and add a volume mount: `- ./config/certs:/app/certs`, then reference them as `certs/<file>`.
- The CA file may be PEM (`-----BEGIN CERTIFICATE-----`) or binary DER. The client certificate must be PKCS#12 — convert a PEM pair with:
  `openssl pkcs12 -export -in client.crt -inkey client.key -out agent1.pfx`
- For a quick bench setup (self-signed CA + broker + client certs), the Mosquitto docs' `mosquitto-tls(7)` page covers the full `openssl` sequence; the relay-side config is just the three file/password keys above.
- The relay logs the loaded client-cert subject and the pinned CA subject at startup, and logs a warning when `allowUntrustedCertificates` is active.

## Resulting topic structure

For an agent with `groupId: Factory`, `nodeId: Agent1`, and devices `Machine-01` + `coolant1`:

```
spBv1.0/Factory/NBIRTH/Agent1                    ← edge node birth (once per connect)
spBv1.0/Factory/DBIRTH/Agent1/Machine-01         ← device birth (per device, on connect or DeviceAdded)
spBv1.0/Factory/DBIRTH/Agent1/coolant1
spBv1.0/Factory/DDATA/Agent1/Machine-01          ← batched value updates after birth
spBv1.0/Factory/DDATA/Agent1/coolant1
spBv1.0/Factory/DDEATH/Agent1/Machine-01         ← device offline (AVAILABILITY=UNAVAILABLE)
spBv1.0/Factory/NDEATH/Agent1                    ← last-will if the agent process dies
spBv1.0/Factory/NCMD/Agent1                      ← consumed: Rebirth requests from primary host
```

## How observations flow

1. Any input module (SHDR adapter, OPC UA adapter, Haas adapter) writes an observation into the agent's shared buffer via `AddObservation(...)`.
2. The relay subscribes to the buffer's `ObservationAdded` event. The handler only does a **non-blocking enqueue** into a bounded channel (10,000 entries, drop-oldest), so a slow or disconnected broker can never stall the agent's observation path.
3. A single processing loop drains the channel in ~100 ms windows and publishes **one `DDATA` per device per window** containing all changed metrics — instead of one MQTT packet per observation.
4. Because all lifecycle work (births, deaths, data) runs on that one loop, ordering is guaranteed: no `DDATA` before `DBIRTH`, none after `DDEATH`, and nothing straddles a rebirth.

### Per-device lifecycle

Each device is either **born** (DBIRTH published and machine available → DDATA flows) or **not born** (DDATA suppressed), driven by `AVAILABILITY`:

| Transition                        | Action                                                                            |
| --------------------------------- | ---------------------------------------------------------------------------------- |
| `AVAILABILITY` → `UNAVAILABLE`    | `DDEATH` published once; queued and subsequent DDATA suppressed.                    |
| `AVAILABILITY` → `AVAILABLE`      | Fresh `DBIRTH` (full current-value snapshot from the agent buffer), then DDATA resumes. |
| Broker reconnect / NCMD Rebirth   | NBIRTH + fresh DBIRTH for every device; each device's born state is re-derived from its current `AVAILABILITY`. |

## Value coercion

| MTConnect category    | Sparkplug type                                                              |
| --------------------- | --------------------------------------------------------------------------- |
| `SAMPLE`              | `Double` always (forced — Sparkplug DataType must match the DBIRTH declaration; coerces with `InvariantCulture` parsing, defaulting to `0.0` on parse failure or unavailable). |
| `EVENT` / `CONDITION` | `String` pass-through.                                                       |
| Unavailable           | `0.0` (SAMPLEs) or `"UNAVAILABLE"` (EVENT / CONDITION).                      |

The reason SAMPLEs are forced to `Double` even when unavailable: Sparkplug requires the data type of a metric to match what was declared in `DBIRTH`. Publishing a string "UNAVAILABLE" for a metric declared as `Double` is a protocol violation that most consumers reject.

## Sharing the Edge Node across all devices on this agent

All MTConnect devices on this agent share **one Sparkplug Edge Node identity** (`groupId` + `nodeId`). Consequences:

- **One NDEATH for all devices.** If the agent process dies, every device under this Edge Node flips to offline simultaneously from a Sparkplug consumer's perspective. Usually what you want; just be aware it's a fleet-wide event.
- **One MQTT session for all devices.** The Sparkplug client maintains a single connection — easier on the broker than N connections, but a connectivity blip affects every device's DDATA stream.
- **No per-device routing customisation** without splitting into multiple agent containers (each with its own `sparkplug-relay:` block under a different `nodeId` / `groupId`).

## Notes

- Built on top of [SparkplugNet](https://github.com/SeppPenner/SparkplugNet) (the most mature open .NET Sparkplug B library).
- Primary-host (`STATE`) coordination is supported by the underlying library but not currently surfaced through this module's configuration — wire it in if needed.
- The relay does **not** consume DCMD or DBIRTH from other Sparkplug nodes (it's a pure outbound relay). MTConnect → Sparkplug only.

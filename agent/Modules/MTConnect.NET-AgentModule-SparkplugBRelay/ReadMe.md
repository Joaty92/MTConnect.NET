# MTConnect.NET-AgentModule-SparkplugBRelay

> Part of the [fork additions](../../../FORK.md) to MTConnect.NET.

In-process agent module that bridges every device on this agent to **Sparkplug B** over MQTT. Each MTConnect device becomes a Sparkplug "Device" under one shared Edge Node identity; observations are republished as `DDATA` as they land in the agent buffer.

## Birth ordering

Sparkplug 3.0 `tck-id-message-flow-device-birth-publish-nbirth-wait` requires that "the NBIRTH message MUST have been sent within the current MQTT session prior to a DBIRTH being published", and `...nbirth-payload-seq` makes each following message's `seq` exactly one more than the last. Getting this wrong is not cosmetic: a Host Application has no session to attach pre-NBIRTH messages to, so every device born early is invisible until a reorder timeout triggers a rebirth — and that timeout is only a SHOULD in the spec.

The relay therefore publishes **no DBIRTH until it has observed an NBIRTH on the wire for the current session**. That gate is driven by a small loopback MQTT subscriber on `spBv1.0/{groupId}/NBIRTH/{nodeId}`, because SparkplugNet provides no way to learn when NBIRTH was published:

- `Start()` runs `ConnectInternal()` → `SubscribeInternal()` → `PublishNodeAndDeviceBirthsInternal()`, and the `Connected` event fires inside the first step. It means "the socket is up", not "NBIRTH is out" — using it as a birth trigger loses the race against the SUBSCRIBE round-trip.
- There are `DeviceBirthPublishing` and `DeviceDeathPublishing` events but no `NodeBirthPublishing`, and `SparkplugNode` is `sealed` with those three methods `private`, so the event cannot be added by subclassing either.

Observing the broker's echo of our own NBIRTH is the one deterministic signal the public API allows, and it covers all three session starts — first connect, reconnect, and NCMD rebirth — through a single path.

Two supporting details, both load-bearing:

- **The observer maintains its own connection** (checked every 500 ms, far below the node's `reconnectInterval`). MQTTnet's `IMqttClient` does not reconnect on its own; without this a single broker restart leaves the gate shut and silences the relay permanently.
- **A watchdog forces a rebirth** if the node is connected but no NBIRTH has arrived within 15 s. NBIRTH is published at QoS 0 and must not be retained, so a broker cannot replay one the observer was offline for — forcing a fresh NBIRTH is the only recovery, and a rebirth is a legal Sparkplug event.

`PublishKnownDeviceMetricsOnReconnect` is explicitly set to `false`. It defaults to `true` and is not a constructor parameter, so it was silently on; combined with `PublishDeviceBirthMessage` registering devices via `KnownDevices.TryAdd` — an add, never an update — the library replayed each device's *first* birth on every reconnect, which for a machine that had not yet reported is a permanently stale all-null snapshot. This relay owns the birth round because only it can source current values from the agent buffer.

Implements the full Sparkplug B lifecycle:

- **NBIRTH** on first broker connect — declares this Edge Node (includes the spec-required `Node Control/Rebirth` metric).
- **DBIRTH** per device on connect / `DeviceAdded` — full current-value snapshot for every DataItem in the device, with each metric carrying its observation's **real timestamp** (not the rebirth time), so historians don't record phantom updates at every reconnect.
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
    primaryHostId: SCADA-1                   # optional; wait for this host's STATE before NBIRTH
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
| `primaryHostId`             | no       | —                  | Sparkplug Primary Host Application ID. When set, NBIRTH waits for that host's `STATE` to report online (`tck-id-message-flow-edge-node-birth-publish-phid-wait`). Omit when no primary host is present. |
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
| Device model declares **no** `AVAILABILITY` DataItem | Treated as born (DDATA flows) and a warning is logged once per device. |

A device that has been configured but never contacted — the machine is switched off, or its adapter block is not enabled yet — births normally and is then immediately DDEATH-ed, because its seeded `AVAILABILITY` is `UNAVAILABLE`. That is intentional: downstream consumers learn the device's full metric set up front (so tags, screens, and alarms can be provisioned before the machine is wired) while DDEATH correctly marks it offline. It re-births with real values the moment the machine first reports `AVAILABLE`.

The last row is a guard, not a supported configuration. The MTConnect standard requires a `Device` to carry `AVAILABILITY`, so a model without one is a malformed device file — but a device that cannot report `AVAILABLE` can never become born, and the previous behaviour was to drop every one of its observations forever with nothing logged to explain the silence. Availability is simply unobservable for such a device, so the relay treats it as available and says so, rather than asserting a machine is dead on no evidence.

## Value coercion

| MTConnect category    | Sparkplug type                                                              |
| --------------------- | --------------------------------------------------------------------------- |
| `SAMPLE`              | `Double` always (forced — Sparkplug DataType must match the DBIRTH declaration; coerced with `InvariantCulture` parsing). |
| `EVENT` / `CONDITION` | `String` pass-through.                                                       |
| Unavailable           | `is_null` (SAMPLEs) or `"UNAVAILABLE"` (EVENT / CONDITION).                  |
| SAMPLE that won't parse | `is_null` — a non-numeric value on a SAMPLE is a modelling error, and reporting it as missing data surfaces the problem instead of hiding it behind a plausible number. |

The reason SAMPLEs are forced to `Double` even when unavailable: Sparkplug requires the data type of a metric to match what was declared in `DBIRTH`. Publishing a string "UNAVAILABLE" for a metric declared as `Double` is a protocol violation that most consumers reject.

### Why unavailable numerics publish `is_null` rather than `0.0`

Sparkplug carries "no value" **out of band**, in each metric's `is_null` flag, so a numeric metric can say "no reading" while keeping the `Double` type its DBIRTH declared. Sending `0.0` instead would be indistinguishable from real data — an axis position, a spindle load, and a feedrate all reach a genuine zero in normal operation — so a historian would trend the placeholder as a measurement.

This matters most on **disconnect**, not at birth. When an adapter drops its connection it sets its DataItems unavailable, and those arrive as ordinary observations while the device is still born, so they publish as DDATA *before* `AVAILABILITY` flips and DDEATH lands. Under the old behaviour every numeric metric on the device wrote a convincing zero into its trend on every dropout.

String-valued metrics keep MTConnect's own `UNAVAILABLE` literal instead: a `String` metric can carry it without misrepresenting its type, and it is a state operators recognise and dashboards key on. The asymmetry is deliberate.

`SparkplugNet` has no public setter for `is_null`; passing a null value to the `Metric` constructor is the supported way to set it, and the declared `DataType` is still preserved on the wire.

## Sharing the Edge Node across all devices on this agent

All MTConnect devices on this agent share **one Sparkplug Edge Node identity** (`groupId` + `nodeId`). Consequences:

- **One NDEATH for all devices.** If the agent process dies, every device under this Edge Node flips to offline simultaneously from a Sparkplug consumer's perspective. Usually what you want; just be aware it's a fleet-wide event.
- **One MQTT session for all devices.** The Sparkplug client maintains a single connection — easier on the broker than N connections, but a connectivity blip affects every device's DDATA stream.
- **No per-device routing customisation** without splitting into multiple agent containers (each with its own `sparkplug-relay:` block under a different `nodeId` / `groupId`).

## Notes

- Built on top of [SparkplugNet](https://github.com/SeppPenner/SparkplugNet) (the most mature open .NET Sparkplug B library).
- Primary-host (`STATE`) coordination is supported by the underlying library but not currently surfaced through this module's configuration — wire it in if needed.
- The relay does **not** consume DCMD or DBIRTH from other Sparkplug nodes (it's a pure outbound relay). MTConnect → Sparkplug only.

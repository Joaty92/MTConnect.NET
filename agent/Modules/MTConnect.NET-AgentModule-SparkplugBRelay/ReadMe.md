# MTConnect.NET-AgentModule-SparkplugBRelay

> Part of the [fork additions](../../../FORK.md) to MTConnect.NET.

In-process agent module that bridges every device on this agent to **Sparkplug B** over MQTT. Each MTConnect device becomes a Sparkplug "Device" under one shared Edge Node identity; observations are republished as `DDATA` as they land in the agent buffer.

## Sparkplug transport

This module owns its Sparkplug B implementation rather than wrapping a client library. The MQTT session, topic construction, both sequence counters, the Will Message and the payload encoding all live in `MTConnect.NET-SparkplugB` under `Transport/` and `Payload/`.

The payload contract is hand-written from the specification's normative payload section rather than generated. Eclipse publishes `sparkplug_b.proto` under EPL-2.0, and generating from it would pull an EPL file and its output into this MIT repository. Field numbers were checked one by one against that `.proto` without copying it, and a test encodes with our contract and decodes with an independently generated model — two independently written contracts agreeing catches a wrong field number, which round-tripping against ourselves cannot.

**Birth ordering is structural.** Sparkplug 3.0 `tck-id-message-flow-device-birth-publish-nbirth-wait` requires that "the NBIRTH message MUST have been sent within the current MQTT session prior to a DBIRTH being published", and `...nbirth-payload-seq` makes each following message's `seq` exactly one more than the last. Getting this wrong is not cosmetic: a Host Application has no session to attach pre-NBIRTH messages to, so every device born early is invisible until a reorder timeout triggers a rebirth — and that timeout is only a SHOULD in the spec. The edge node publishes NBIRTH itself and awaits it before raising the event that drives device births, so a DBIRTH cannot precede its NBIRTH by construction.

Per the specification, and confirmed against the Eclipse sources rather than assumed:

| Message | QoS | Retain | Sequence number |
| --- | --- | --- | --- |
| NBIRTH, DBIRTH, NDATA, DDATA, DDEATH | 0 | false | yes, +1 each, wrapping 255 → 0 |
| NDEATH (registered as the Will Message) | 1 | false | **none** — `tck-id-payloads-ndeath-seq` |
| NCMD / DCMD subscriptions | 1 | — | — |

**`bdSeq`** is what lets a Host Application tell a live session from a stale NDEATH the broker delivered late: the Will registered on a connection and the NBIRTH published on that same connection carry the same value, and it advances on every CONNECT. It is persisted, because a restarted node beginning again at zero would replay values the host has already seen — see [Persisting bdSeq](#persisting-bdseq), which matters in a container.

**Primary Host awareness.** With `primaryHostId` set, the node subscribes to `spBv1.0/STATE/{hostId}` and withholds NBIRTH until that host reports online, ending the session if it goes offline (`tck-id-message-flow-edge-node-birth-publish-phid-wait`).

**Shutdown is intentional.** On stop the node publishes NDEATH and then sends a clean MQTT DISCONNECT. On MQTT 3.1.1 a clean DISCONNECT suppresses the Will, so a node that merely disconnects announces nothing, and one that is killed announces its shutdown as a crash — neither lets a consumer tell a planned stop from a failure.

**DCMD** is subscribed for every device, but this relay is outbound-only: MTConnect offers no write path back to the machine here, so an arriving command is logged and explicitly ignored rather than silently dropped.

## Persisting bdSeq

`bdSeq` has to keep advancing across restarts, so the agent writes it to a `state` directory beside its binary. Inside a container that is lost on recreation, so **mount a volume there** — one line, and nothing to declare per relay block:

```yaml
volumes:
  - ./state:/app/state
```

One directory serves any number of `sparkplug-relay` blocks: each file is named after its Edge Node identity (`bdseq-<groupId>-<nodeId>`), so two Edge Nodes never share a counter. Override the location with `stateDirectory` only if the default does not suit.

Without the mount the relay still runs — the counter simply restarts at zero on each deployment, replaying values a Host Application has already seen.

## Conformance

Validated against the Eclipse Sparkplug TCK 3.0.0, driven over MQTT (the web console is optional — it only publishes `NEW_TEST` to `SPARKPLUG_TCK/TEST_CONTROL`):

| Test | Result |
| --- | --- |
| `SessionEstablishmentTest` | 82 PASS, 0 FAIL |
| `SendDataTest` (live machine data) | 23 PASS, 0 FAIL |
| `SessionTerminationTest` | 23 PASS, 0 FAIL |

Assertions reported NOT EXECUTED are inapplicable rather than unmet: metric aliases, which this relay does not use; MQTT 5 clean-session and disconnect semantics, where it speaks 3.1.1; and the `ddeath` assertions, which require an individual Device to go offline rather than the whole Edge Node.

Implements the full Sparkplug B lifecycle:

- **NBIRTH** on first broker connect — declares this Edge Node (includes the spec-required `Node Control/Rebirth` metric).
- **DBIRTH** per device on connect / `DeviceAdded` — full current-value snapshot for every DataItem in the device, with each metric carrying its observation's **real timestamp** (not the rebirth time), so historians don't record phantom updates at every reconnect.
- **DDATA** batched per device as values change.
- **DDEATH** when a device flips to `AVAILABILITY=UNAVAILABLE`, with a re-`DBIRTH` if it comes back.
- **NDEATH** is registered as the broker's last-will for an ungraceful exit, and published deliberately before a clean DISCONNECT on an intentional stop — so consumers can tell a planned shutdown from a crash. Either way every device on this node goes offline at once.
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
| `stateDirectory`            | no       | `state`            | Overrides where persisted `bdSeq` lives. Rarely needed — see [Persisting bdSeq](#persisting-bdseq). |
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

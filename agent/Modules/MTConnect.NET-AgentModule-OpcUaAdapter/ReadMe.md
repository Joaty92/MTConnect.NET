# MTConnect.NET-AgentModule-OpcUaAdapter

In-process agent module that opens an OPC UA `Session` to a server (typically a CNC controller), subscribes to a list of NodeIds defined in a YAML profile, and forwards each value notification to the agent's observation buffer via `IMTConnectAgentBroker.AddObservation(...)`.

One block per device. Downstream relays (MQTT, SparkplugB, HTTP) pick the observations up from the buffer automatically — there is no protocol-specific publishing code here.

## Device model resolution

A profile is a bundle of up to two files in `profiles/`:

| File                          | Contents                                                                                                |
| ----------------------------- | ------------------------------------------------------------------------------------------------------- |
| `<name>.yaml` *(required)*    | NodeId → MTConnect DataItem key mappings + value kind (sample/event).                                    |
| `<name>.template.xml` *(opt.)*  | Pre-built MTConnect device model for this controller — components, units, condition items, the lot. Module rewrites Device.id/uuid/name to the configured `deviceKey`, then prefixes every DataItem.id with `{deviceKey}_` so /probe ids are globally unique across all devices. |

Back-compat: `<name>.device.xml` is also accepted as a template extension (older naming, kept working for one release). New profiles use `.template.xml` to avoid confusion with the literal `devices/` folder convention.

On `OnStartAfterLoad` the module decides where the agent's device model for `deviceKey` comes from in this priority order:

1. **User-authored `devices/<deviceKey>.xml`** — if the agent's startup device loader already produced a device matching `deviceKey`, the module uses it as-is and only attaches observations. Full control, used for the SHDR-style hand-tuned device model.
2. **Shipped `profiles/<name>.template.xml` template** — if the profile bundle includes a `.template.xml`, the module parses it, rewrites the `id`/`uuid`/`name` to the configured `deviceKey`, and pushes it via `Agent.AddDevice(...)`. Used for well-documented controllers where the structural template is known up front (Sinumerik 840D is the shipped example).
3. **Auto-generated flat device** — if neither of the above applies, the module builds a flat `Device` with one `DataItem` per YAML mapping (`category` from `kind:`, `type="VARIABLE"`). Valid MTConnect document, no component hierarchy, no units. Used for one-off / S7 / generic-PLC scenarios where the goal is just to forward values.

Which tier ran is logged at `Information` on module startup — look for `using existing device from devices/ folder`, `created device from template …`, or `created flat device from profile mappings (N data items)`.

## Shipped profile bundles

| Profile               | YAML                      | Device template            | Notes                                                                                  |
| --------------------- | ------------------------- | -------------------------- | -------------------------------------------------------------------------------------- |
| `generic`             | `generic.yaml`            | *(none)*                    | Smoke-test bundle for Prosys OPC UA Simulation Server. Falls through to auto-generated. |
| `sinumerik_840d`      | `sinumerik_840d.yaml`     | `sinumerik_840d.template.xml` | 5-axis trunnion mill (X, Y, Z + A, C). NodeIds are `TODO` — fill in via UAExpert.       |

The `sinumerik_840d.template.xml` is adapted structurally from the TrakHound reference `mazak.xml` and follows the MTConnect OPC UA Companion Specification V2.00 vocabulary for data item types. The DataItem `id` values are the same as the `dataItemKey` values in `sinumerik_840d.yaml` so observations route correctly without further configuration.

For 3-axis machines using a Sinumerik 840D: copy the bundle to a new pair (e.g. `sinumerik_840d_3axis.yaml` + `sinumerik_840d_3axis.template.xml`), delete the four rotary-axis entries from the YAML and the two `<Rotary id="a" …>` / `<Rotary id="c" …>` blocks from the XML, then set `profile: sinumerik_840d_3axis` in `agent.config.yaml`.

## When the Companion Spec helps vs. when it doesn't

MTConnect OPC UA Companion Specification V2.00 (canonical link [here](https://opcfoundation.org/developer-tools/specifications-opc-ua-information-models/opc-unified-architecture-for-mtconnect/)) defines:

- The mapping rules between MTConnect data items and OPC UA NodeIds (which `type` values to use, how categories map).
- A standard OPC UA information model that wraps MTConnect concepts (for servers that implement it).

What it does **not** provide:

- Downloadable `device.xml` templates per controller — the structural XML is yours to write.
- Per-vendor NodeId lists — Sinumerik / Fanuc / Heidenhain expose their own information models and their NodeIds come from vendor docs + UAExpert browsing.

So the spec is your reference for **MTConnect modelling** (the `.template.xml` side); UAExpert is your reference for **NodeId discovery** (the `.yaml` side). Don't expect to find a pre-mapped Sinumerik bundle anywhere on the public internet — that work happens once per controller model and lives in this `profiles/` folder going forward.

## Configuration

Add an entry under `modules:` in `agent.config.yaml`:

```yaml
- opcua-adapter:
    deviceKey: Sinu-001                 # matches devices/Sinu-001.xml
    endpoint: opc.tcp://192.168.1.50:4840
    securityPolicy: Basic256Sha256      # or "None" for sim servers
    identity:
      mode: username                    # or "anonymous"
      username: mtconnect
      password: hunter2
    samplingInterval: 250               # ms — how often the server samples the source
    publishingInterval: 1000            # ms — how often the server pushes notifications
    reconnectInterval: 5000             # ms — delay between reconnect attempts after keep-alive failure
    sessionTimeout: 60000               # ms — session timeout requested from server
    profile: sinumerik_840d             # loads profiles/sinumerik_840d.yaml
```

Repeat the block (with a different `deviceKey`, `endpoint`, and `profile`) once per machine.

### Field reference

| Key                  | Required | Default      | Notes                                                                |
| -------------------- | -------- | ------------ | -------------------------------------------------------------------- |
| `deviceKey`          | yes      | —            | Must match a device declared in `devices/<key>.xml`.                  |
| `endpoint`           | yes      | —            | Full `opc.tcp://host:port[/path]` URL.                                |
| `securityPolicy`     | no       | `None`       | `None` or `Basic256Sha256`. Use `None` only on trusted networks.      |
| `identity.mode`      | no       | `anonymous`  | `anonymous` or `username`.                                            |
| `identity.username`  | no       | —            | Required when `mode: username`.                                       |
| `identity.password`  | no       | —            | Required when `mode: username`. UTF-8 plain text.                     |
| `samplingInterval`   | no       | `1000`       | OPC UA server-side sample rate, milliseconds.                         |
| `publishingInterval` | no       | `1000`       | OPC UA push interval to this client, milliseconds.                    |
| `reconnectInterval`  | no       | `5000`       | Delay before reconnect after the keep-alive declares the link dead.   |
| `sessionTimeout`     | no       | `60000`      | Session timeout requested from the server.                            |
| `profile`            | yes      | —            | Name (without `.yaml`) of a file in this module's `profiles/` folder. |

## Profile file format

```yaml
mappings:
  - nodeId: "ns=2;s=/Channel/State/feedRateOvr"
    dataItemKey: pfo               # must match the DataItem id in devices/<key>.xml
    kind: sample                   # sample → coerced to double; event → string pass-through
```

`kind: condition` is not supported in v1 — alarm-tree mapping needs vendor-specific work and is tracked as a follow-up. CONDITION data items can still be populated by other modules (SHDR adapter, etc.) for the same device.

The shipped profiles:

- **`generic.yaml`** — minimal mapping for the Prosys OPC UA Simulation Server's built-in Counter/Random/Sawtooth/Sinusoid signals. Use for end-to-end smoke testing.
- **`sinumerik_840d.yaml`** — skeleton listing the MTConnect data item keys for a Sinumerik 840D, with each `nodeId:` left as `TODO`. Fill these in from a real machine using UAExpert (see below).

## Discovery workflow

The module is purely subscription-based at runtime. NodeId discovery happens once per machine type, at dev time.

1. **Browse the server.** Connect [UAExpert](https://www.unified-automation.com/products/development-tools/uaexpert.html) (the standard OPC UA client) to the controller's OPC UA endpoint. As an alternative for quick checks, this repo ships [`tools/opcua-browse`](../../../tools/opcua-browse/) which prints the address space tree to stdout.
2. **Find the NodeId for each MTConnect data item.** For the Sinumerik profile, that means finding the controller's nodes for `pfo`, `srpm`, `execution`, axis positions, etc. The MTConnect [OPC UA Companion Specification V2.00](https://opcfoundation.org/developer-tools/specifications-opc-ua-information-models/opc-unified-architecture-for-mtconnect/) defines the canonical mapping between MTConnect data items and OPC UA NodeIds, but vendor-specific address spaces still vary.
3. **Update the profile YAML.** Replace each `TODO: browse with UAExpert` with the discovered NodeId. Delete entries the machine doesn't expose.
4. **Restart the agent.** The module loads the profile on `OnStartAfterLoad`, opens the subscription, and observations begin to flow.

There is no auto-discovery at runtime. Vendor address spaces are inconsistent enough that an auto-mapper fails confusingly more often than it works; the YAML profile is the canonical contract, owned by a human.

## How observations are pushed

- `ValueReceived` fires once per MonitoredItem notification.
- The value is coerced based on the mapping's `kind`:
  - `sample` → `double` (numeric types cast; strings parsed with `InvariantCulture`; bools become `1.0`/`0.0`; on parse failure, `0.0` is pushed).
  - `event` → `ToString()`.
- An OPC UA `StatusCode` other than `Good` causes the literal string `"UNAVAILABLE"` to be pushed for that data item — matches MTConnect's UNAVAILABLE semantics so downstream consumers see the data item flip correctly.
- The observation is forwarded via `Agent.AddObservation(deviceKey, dataItemKey, value, sourceTimestampUtc)`.

## Reconnect behaviour

The module relies on the SDK's `SessionReconnectHandler`:

- `Session.KeepAlive` faulting flips the state to disconnected and starts the reconnect handler.
- The handler retries every `reconnectInterval` ms until the session re-establishes.
- On reconnect, the subscription and all MonitoredItems are restored automatically by the SDK (the same handler swaps the underlying session reference under the covers).
- During the disconnected window, `ValueReceived` does not fire and no observations are pushed.

## Security notes

`securityPolicy: None` disables transport encryption — only acceptable on networks where the OPC UA traffic is already isolated (private VLAN, segmented control network behind a firewall).

`securityPolicy: Basic256Sha256` requires the client and server to trust each other's certificates. The module generates a self-signed client certificate on first run and stores it under `pki/own/` next to the agent binary. The first time you connect to a new server you will need to **manually accept the client certificate on the server side** (typically by moving it from the server's `rejected/` store to `trusted/`).

Untrusted server certificates are auto-accepted on the client side. This is a deliberate trade-off for ease of deployment — a hardened deployment should remove the `BadCertificateUntrusted → e.Accept = true` handler in `OpcUaSubscriptionClient.BuildApplicationConfiguration()` and manage the trust list explicitly.

## Troubleshooting

| Symptom in logs                                                       | Likely cause                                                                                                |
| --------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| `OpcUa adapter (...): profile '...' not found under profiles/`        | `profile:` value doesn't match a file in `profiles/`; check spelling, ensure the file ships with the image. |
| `OpcUa client failed to start (...): BadSecureChannelClosed`          | Wrong endpoint URL, firewall blocking, or server requires a security policy you haven't enabled.            |
| `OpcUa client failed to start (...): BadIdentityTokenRejected`        | Anonymous identity not allowed by the server, or wrong username/password.                                   |
| Subscription opens, no `ValueReceived` events fire                    | NodeIds in profile don't exist on the server — browse with UAExpert to confirm. Or `MonitoringMode` is off. |
| Repeated reconnects with `BadSessionClosed`                           | `sessionTimeout` shorter than `publishingInterval` × keep-alive count; raise `sessionTimeout`.              |

## Out of scope (deliberate; follow-up commits)

- x509 certificate identity (only anonymous + username/password supported in v1).
- CONDITION/alarm-tree mapping (Sinumerik-specific; needs a dedicated translator).
- Method calls / writes (MTConnect is one-way; this module is read-only).
- Multi-server-per-block (one device per block is consistent with `shdr-adapter:`).

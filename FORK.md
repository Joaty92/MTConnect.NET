# Fork additions

This is a personal fork of [TrakHound/MTConnect.NET](https://github.com/TrakHound/MTConnect.NET) focused on one job: **collecting live data from factory machines and relaying it to MQTT / Sparkplug B consumers**, shipped to factory servers as a single Docker image.

Everything fork-specific is mapped on this page. Everything else is upstream — start at the main [README](README.md), or browse the full documentation site: `cd docs && npm ci && npm run dev` → <http://localhost:5173> (VitePress, with sidebar and search).

## How the pieces fit

```
Haas CNC ────(Q-codes / macros over TCP via Moxa NPort)───► haas-ethernet module ──┐
OPC UA server (Siemens 840D, PLCs, …) ──(subscriptions)───► opcua-adapter module ──┤
Fanuc CNC ──(FOCAS)──► ladder99 fanuc-driver ──(SHDR)─────► shdr-adapter module ───┤  (upstream)
any SHDR-speaking adapter ────────────(SHDR over TCP)─────► shdr-adapter module ───┤  (upstream)
                                                                                   ▼
                                                              agent observation buffer
                                                                                   │
                    ┌──────────────────────────────┬───────────────────────────────┤
                    ▼                              ▼                               ▼
             http-server                     mqtt-relay                    sparkplug-relay
       /probe /current /sample            (upstream module)          NBIRTH/DBIRTH/DDATA/DDEATH
                                                                                   │
                                                                                   ▼
                                                                    MQTT broker → SCADA / IIoT platform
```

## What this fork adds

| Addition | What it does | Docs |
| --- | --- | --- |
| **Sparkplug B relay** (module + `MTConnect.NET-SparkplugB` library) | Publishes every device as a Sparkplug B edge node: NBIRTH/DBIRTH on connect, batched DDATA, DDEATH on availability loss, NCMD rebirth, TLS / mutual-TLS client-cert auth. Owns its Sparkplug B implementation — MQTT session, topics, `seq`/`bdSeq`, Will Message and payload encoding — rather than wrapping a client library. Unavailable numeric metrics publish `is_null` instead of a fabricated `0.0`; NBIRTH precedes every DBIRTH by construction; `primaryHostId` gives Primary Host `STATE` coordination; shutdown publishes NDEATH then disconnects cleanly; and a device model missing its `AVAILABILITY` DataItem warns rather than going silently dark. Validated against the Eclipse Sparkplug TCK 3.0.0: **128 assertions PASS, 0 FAIL**. | [Module ReadMe](agent/Modules/MTConnect.NET-AgentModule-SparkplugBRelay/ReadMe.md) |
| **OPC UA adapter** (module + `MTConnect.NET-OpcUa` library) | OPC UA subscription client driven by YAML NodeId→DataItem profiles; three-tier device-model resolution (user XML → shipped template → auto-generated); passes controller source timestamps through, with an optional per-adapter `ignoreTimestamps` override; server-certificate trust is configurable (`acceptUntrustedServerCertificate`) and the negotiated security policy is verified at startup. | [Module ReadMe](agent/Modules/MTConnect.NET-AgentModule-OpcUaAdapter/ReadMe.md) |
| **Haas Ethernet adapter** (module + `MTConnect.NET-Haas` library) | YAML-driven Q-code / macro-variable polling engine for Haas CNCs, usually via a Moxa NPort serial-to-Ethernet converter. | [Module ReadMe](agent/Modules/MTConnect.NET-AgentModule-HaasEthernet/ReadMe.md) |
| **OPC UA profile GUI** | PySide6 desktop app: browse an OPC UA server, tick Variables, get the `<name>.yaml` + `<name>.template.xml` profile bundle the OPC UA adapter consumes. | [Tool README](tools/opcua-profile-gui/README.md) |
| **Deployment tooling** | Dockerfile, image-shipping workflow, and a zip-and-go starter kit for factory servers (no source tree on the target). | [deploy/README](deploy/README.md) · [starter-kit README](deploy/starter-kit/README.md) |
| **Docs additions** | The three fork modules and the profile GUI are documented alongside upstream's modules in the docs site; building/serving the site (including via docker-compose) is covered in the docs README. | [docs/configure/module-config.md](docs/configure/module-config.md) · [docs/README.md](docs/README.md) |

## Which path for which machine?

| You have… | Use | You'll need |
| --- | --- | --- |
| A Haas CNC | `haas-ethernet` module | Moxa NPort (or direct-Ethernet HMI) reachable over TCP; Q-code support enabled |
| A Siemens 840D / anything with an OPC UA server | `opcua-adapter` module + a profile from the [GUI](tools/opcua-profile-gui/README.md) | OPC UA server option enabled on the controller |
| A Fanuc CNC | [ladder99 fanuc-driver](https://github.com/Ladder99/fanuc) (SHDR transport, generates the device XML) → upstream `shdr-adapter` module | FOCAS Ethernet option; Fanuc FOCAS libraries |
| Anything already speaking SHDR | upstream `shdr-adapter` module | `devices/<key>.xml` whose DataItem names match the SHDR keys |

## Deploying to a factory server

The 5-minute version: build the image with the root [Dockerfile](Dockerfile), `docker save` it to a `.tar`, ship it with the [starter kit](deploy/starter-kit/) (config + devices + profiles + compose file), then on the target: `docker load`, edit `config/agent.config.yaml`, `docker compose up -d`. Full walkthrough in [deploy/README.md](deploy/README.md).

## Keeping the fork in sync with upstream

```
git fetch upstream
git checkout master && git merge --ff-only upstream/master   # master mirrors upstream
git checkout fork/main && git rebase master                  # replay fork work on top
```

Fork content lives in fork-owned files/folders wherever possible, so upstream merges stay conflict-free. The deliberate exceptions are the short fork-note in [README.md](README.md), the fork-module sections in [docs/configure/module-config.md](docs/configure/module-config.md), and two targeted edits to the upstream `MTConnect.NET-SHDR` library: the `IgnoreHeartbeatOnChange` adapter setting in `ShdrClient.cs`, and a case-insensitive condition-level parse in `ShdrFaultState.cs` (`Enum.Parse(…, ignoreCase: true)`). Without the latter, an adapter that sends a lower-case level such as `fault` instead of `FAULT` hits a case-sensitive `Enum.Parse`, the surrounding `catch` swallows the exception, `FromLine` returns null, and the condition line is dropped **silently** — the condition never leaves `UNAVAILABLE`. A third upstream exception is the `mqtt-relay` module (`agent/Modules/MTConnect.NET-AgentModule-MqttRelay/Module.cs`), where TLS and client authentication were entangled: all TLS was gated on a **client** certificate loading, so a CA-only (server-authenticated) block silently connected in plaintext; `useTls` was only read inside the credentials branch, making it a no-op on its own; and that branch rebuilt the TLS options, discarding the client certificate and pinned CA whenever credentials were also set. The fork decouples the two axes. The same pattern still exists upstream in the MqttAdapter module and the MTConnect.NET-MQTT clients, which this fork does not use. A fourth upstream exception is `examples/MTConnect.NET-Agent-Embedded/Program.cs`, the Docker image's entrypoint, which now registers a SIGTERM handler that calls `StopAgent()`. Nothing in `MTConnectAgentApplication` listens for a termination signal — `StopAgent()` is reached only from the config-file reload path and the explicit CLI/service stop commands — so `docker stop` killed the process with its modules still running. The Sparkplug relay never published its NDEATH or sent a clean DISCONNECT, and the `mqtt-relay`'s availability last-will was mis-reported the same way, meaning every planned shutdown announced itself to consumers as a crash. The fix is deliberately confined to the entrypoint rather than reaching into the upstream application class. A fifth upstream exception is a cross-device DataItem Id collision, fixed in three places. `libraries/MTConnect.NET-Common/Agents/MTConnectAgent.cs` cached resolved DataItems in `_dataItems` keyed by `DataItemId` alone, while the field's own comment documented the key as `DeviceUuid:DataItemId`; because DataItem Ids are only unique within a Device, the first Device to observe a given Id claimed the entry for every Device, and every other Device then resolved to that Device's `IDataItem` — including its `Device` back-reference. The same file now also drops a Device's cache entries when its model is replaced, which it never did, so a reloaded Device no longer resolves against the model it just replaced. `libraries/MTConnect.NET-MQTT/MTConnectMqttEntityServer.cs` built the entity topic's Device segment from `observation.DataItem.Device`, so a mis-resolved DataItem published one Device's observations under another Device's topic — while the HTTP Probe and Current responses stayed correct, because they rebuild from each Device's own component tree. It now prefers `observation.DeviceUuid`, which the Agent stamps on every observation, falling back to the back-reference only when that is empty. `agent/MTConnect.NET-Applications-Agents/MTConnectAgentApplication.cs` gained a startup warning naming any Id declared by more than one Device file: Component and DataItem Ids are `xs:ID`, which XML Schema scopes to the whole document, and a Probe response returns every Device in a single MTConnectDevices document, so duplicates are both schema-invalid and silently mis-resolving — yet nothing in the load path detected them. It warns rather than refusing to start, so an agent already running in production is not taken down by a pre-existing duplicate it has been tolerating. All of these are candidates to upstream as PRs.

## Contributor guard rails

A pre-commit hook blocks site-identifying terms from entering the repo. Enable it once per clone:

```
git config core.hooksPath .githooks
```

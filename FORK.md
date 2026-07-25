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
| **Sparkplug B relay** (module + `MTConnect.NET-SparkplugB` library) | Publishes every device as a Sparkplug B edge node: NBIRTH/DBIRTH on connect, batched DDATA, DDEATH on availability loss, NCMD rebirth, TLS / mutual-TLS client-cert auth. | [Module ReadMe](agent/Modules/MTConnect.NET-AgentModule-SparkplugBRelay/ReadMe.md) |
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
git checkout master && git merge --ff-only upstream/master
git checkout <feature-branch> && git merge master
```

Fork content lives in fork-owned files/folders wherever possible, so upstream merges stay conflict-free. The deliberate exceptions are the short fork-note in [README.md](README.md), the fork-module sections in [docs/configure/module-config.md](docs/configure/module-config.md), and two targeted edits to the upstream `MTConnect.NET-SHDR` library: the `IgnoreHeartbeatOnChange` adapter setting in `ShdrClient.cs`, and a case-insensitive condition-level parse in `ShdrFaultState.cs` (`Enum.Parse(…, ignoreCase: true)`). Without the latter, an adapter that sends a lower-case level such as `fault` instead of `FAULT` hits a case-sensitive `Enum.Parse`, the surrounding `catch` swallows the exception, `FromLine` returns null, and the condition line is dropped **silently** — the condition never leaves `UNAVAILABLE`. Both are candidates to upstream as PRs.

## Contributor guard rails

A pre-commit hook blocks site-identifying terms from entering the repo. Enable it once per clone:

```
git config core.hooksPath .githooks
```

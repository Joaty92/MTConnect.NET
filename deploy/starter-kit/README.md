# MTConnect Agent — Starter Kit

> Part of the [fork additions](../../FORK.md) to MTConnect.NET.

Everything a factory server needs to run the agent, minus the compiled image itself. Unzip anywhere, load the `.tar`, edit config, start.

## What's in this folder

```
starter-kit/
├── docker-compose.yml            ← already points at the image by tag; no source needed on target
├── config/
│   ├── agent.config.yaml         ← starter with every module commented; enable + edit for this site
│   └── NLog.config
├── devices/                       ← drop <deviceKey>.xml files here, one per SHDR-fed machine
├── profiles/                      ← drop OPC UA profile bundles here (.yaml + optional .template.xml)
└── logs/                          ← the agent writes here at runtime
```

## First run on a target machine

```powershell
# 1. Unzip the starter-kit somewhere on the target, e.g. C:\Factories\SiteA\
#    Also copy the shipped mtconnect-agent-spb-<version>.tar to a convenient location.

# 2. Load the Docker image (only needed once per version).
docker load -i mtconnect-agent-spb-1.1.0.tar
# → "Loaded image: mtconnect-agent-spb:1.1.0"

# 3. Edit config\agent.config.yaml for this site — uncomment the module blocks you need
#    (mqtt-relay, sparkplug-relay, opcua-adapter, shdr-adapter, haas-ethernet) and fill in
#    the site's IP addresses, broker hostnames, deviceKeys, etc.

# 4. For every SHDR-fed machine: drop a devices\<deviceKey>.xml file describing the device.
# 5. For every OPC UA-fed machine: drop a profiles\<name>.yaml (+ optional .template.xml) bundle.

# 6. Start the agent.
docker compose up -d

# 7. Verify.
docker logs --follow mtconnect-agent-spb
# Browse http://<target-host>:5003/probe
```

## Time synchronization (required)

Every observation this stack produces carries a timestamp, and different adapters use **different clocks**:

| Data | Time base |
| --- | --- |
| Haas Ethernet adapter observations | **Agent host clock** (values are stamped on receipt — Q-codes carry no machine time) |
| SHDR adapter observations + all availability events | **Agent host clock** |
| OPC UA adapter observations | **Controller's clock** (OPC UA source timestamps are passed through) |
| Agent logs | UTC on the agent host |

For downtime analytics, cycle-time KPIs, and cross-machine event ordering to mean anything:

- **Run NTP on the agent host** (the container inherits the host clock). Prefer *slewing* corrections (chrony default; `w32time` with small offsets) over hard steps — the polling throttles and SHDR heartbeat assume a steady clock.
- **Sync the CNC controllers' clocks too** where the adapter passes machine time through (OPC UA). The agent logs a warning when a controller's timestamps drift more than ~5 s from the agent clock.
- If a source's clock can't be disciplined, the agent-wide `ignoreTimestamps: true` (or the per-adapter override) re-stamps everything with agent time — you lose source-time accuracy but gain a single consistent time base.

## Updating to a newer image

```powershell
docker compose down
docker load -i mtconnect-agent-spb-1.0.1.tar
# Bump the `image:` tag in docker-compose.yml to 1.0.1 if you're pinning versions
docker compose up -d
```

## What's NOT in this starter-kit

- **The compiled Docker image** (`mtconnect-agent-spb-<version>.tar`). Ships alongside this folder — see the parent [`deploy/README.md`](../README.md) for the build/save workflow. The `.tar` is a large binary that lives outside version control.
- **Site-specific device XMLs and OPC UA profiles.** Those are authored per-machine using the OPC UA profile generator at [`tools/opcua-profile-gui/`](../../tools/opcua-profile-gui/) or hand-written for SHDR / Haas Ethernet.

## Ship it

To send a new site their complete deployment bundle:

1. Copy this `starter-kit/` folder to a staging area, rename per site, edit the config.
2. Zip the copied folder: `Compress-Archive -Path <staging>/SiteA -DestinationPath <staging>/SiteA.zip`
3. Also send the current `.tar` (from the parent `deploy/` folder): `mtconnect-agent-spb-<version>.tar`
4. Target-side steps: unzip, `docker load -i <tar>`, edit config, `docker compose up -d`. No git clone, no build, no source code required.

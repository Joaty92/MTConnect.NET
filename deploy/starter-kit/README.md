# MTConnect Agent — Starter Kit

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
docker load -i mtconnect-agent-spb-1.0.0.tar
# → "Loaded image: mtconnect-agent-spb:1.0.0"

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

## Updating to a newer image

```powershell
docker compose down
docker load -i mtconnect-agent-spb-1.0.1.tar
# Bump the `image:` tag in docker-compose.yml to 1.0.1 if you're pinning versions
docker compose up -d
```

## What's NOT in this starter-kit

- **The compiled Docker image** (`mtconnect-agent-spb-<version>.tar`). Ships alongside this folder — see the parent `deploy/README.md` for the build/save workflow. The `.tar` is a large binary that lives outside version control.
- **Site-specific device XMLs and OPC UA profiles.** Those are authored per-machine using the tools in `../../tools/opcua-profile-gui/` (for OPC UA) or hand-written from a template like `haas-agent/devices/device.xml` (for SHDR / Haas Ethernet).

## Ship it

To send a new site their complete deployment bundle:

1. Zip this folder: `Compress-Archive -Path deploy\starter-kit -DestinationPath siteA-starter.zip`
2. Also send the current `.tar`: `deploy\mtconnect-agent-spb-1.0.1.tar`
3. Target-side steps: unzip, `docker load -i <tar>`, edit config, `docker compose up -d`. No git clone, no build, no source code required.

# MTConnect.NET — Deploying the Agent to a Factory Server

This folder holds the tools you need to ship the MTConnect agent to a factory server **without giving that server the source code**. This README walks through it plainly, no assumed Docker expertise.

---

## The mental model (read this first)

Think of a Docker image like a **flatpack furniture box**:

- On the dev machine you **assemble** the box from source code (`docker compose build`).
- You **pack** the box into a shipping-friendly form (`docker save` → a `.tar` file).
- You **drive the box to the factory** (email, USB stick, SFTP — however).
- On the target machine you **unpack** the box into Docker's cupboard (`docker load`).
- After unpacking, the box (the `.tar` file) is empty and can be thrown away or kept as a backup — Docker now knows about the image and refers to it by **name + version tag**, not by file path.

The compose file at the target says "give me the box called `mtconnect-agent-spb:1.0.0`" — Docker looks in its cupboard, finds it, starts a container from it. **The compose file never mentions the `.tar` file path.** Once loaded, the `.tar` is irrelevant.

Now the actual steps.

---

## Phase 1 — Build the image (on your dev machine)

You only do this **once per version**. Do it again when the source code changes and you want to ship an update.

```powershell
# 1. Build the image from source.
#    This uses the docker-compose.yml at deployment, which
#    references the Dockerfile at c:\dev\MtConnect.net\Dockerfile.
cd c:\dev\deployment
docker compose build --no-cache

# 2. Give it a distributable name.
#    Compose auto-names the image with an ugly folder-based name.
#    Retag it as "mtconnect-agent-spb" with a version.
docker tag deployment-mtconnect-agent-spb:latest mtconnect-agent-spb:1.0.0
docker tag deployment-mtconnect-agent-spb:latest mtconnect-agent-spb:latest

# 3. Pack the image into a shippable .tar file.
#    Both tags travel inside the .tar.
cd c:\dev\MtConnect.net\deploy
docker save -o mtconnect-agent-spb-1.0.0.tar mtconnect-agent-spb:1.0.0 mtconnect-agent-spb:latest

# 4. Confirm size (typically ~40-60 MB — Docker compresses well).
ls -lh mtconnect-agent-spb-1.0.0.tar
```

After this, `c:\dev\MtConnect.net\deploy\mtconnect-agent-spb-1.0.0.tar` is the **one artefact from the source repo** that goes to the target.

---

## Phase 2 — Prepare the site folder (still on your dev machine)

Every factory site needs a **config folder**. This holds the per-site YAML/XML — which machines are on this network, what IPs, what profile per machine. It's small (a few KB).

You have one already at `c:\dev\deployment\`. To prepare a **new** site (say, "Site B"):

```powershell
# 1. Copy the site folder as a starting point.
cp -r c:\dev\deployment c:\dev\deployment-SiteB
cd c:\dev\deployment-SiteB

# 2. Swap the compose file for the deployment version.
#    The dev one builds from source (needs C:\dev\MtConnect.net on disk).
#    The deploy one just references the loaded image (no source needed).
copy c:\dev\MtConnect.net\deploy\docker-compose.deploy.template.yml docker-compose.yml

# 3. Edit config\agent.config.yaml for this site:
#    - The Sparkplug groupId / nodeId (unique per site if you want separate namespaces)
#    - The OPC UA endpoints, SHDR adapter IPs, MQTT broker for this site's network
#    - The deviceKeys and devices/*.xml files for machines here
```

The site folder is now ready to ship. It contains:

```
deployment-SiteB/
├── docker-compose.yml            ← deploy template (references image, not build)
├── config/
│   ├── agent.config.yaml         ← site-specific edits made
│   └── NLog.config
├── devices/                       ← per-machine XMLs
├── profiles/                      ← OPC UA profile bundles
└── logs/                          ← empty until first run
```

---

## Phase 3 — Ship to the target factory server

**Two things travel to the target:**

1. `c:\dev\MtConnect.net\deploy\mtconnect-agent-spb-1.0.0.tar` (the packed image — ~46 MB)
2. `c:\dev\deployment-SiteB\` (the site config folder — a few KB, zip it up)

Ship however you like — USB drive, SFTP, email, S3 bucket, shared drive. Nothing about them is Docker-specific during transit; they're just files.

---

## Phase 4 — Install on the target factory server

The target machine has **Docker installed** but no source code and no images yet. You (or a site engineer) do this once:

```powershell
# 1. Put the shipped files somewhere on disk. Convention (any location works):
#    C:\Factories\SiteB\
#    ├── deployment-SiteB\    ← the unzipped site config folder
#    │   ├── docker-compose.yml
#    │   ├── config\ devices\ profiles\ logs\
#    └── mtconnect-agent-spb-1.0.0.tar    ← put it next to the site folder, or anywhere reachable

# 2. Unpack the image into Docker's internal storage.
#    You can be in ANY folder for this step — the -i flag takes the .tar path.
cd C:\Factories\SiteB
docker load -i mtconnect-agent-spb-1.0.0.tar
# You'll see:
#   Loaded image: mtconnect-agent-spb:1.0.0
#   Loaded image: mtconnect-agent-spb:latest
#
# Docker has now copied the image into its own store. The .tar file is no
# longer needed. You can delete it, keep it as a backup, whatever — Docker
# doesn't care.

# 3. Verify Docker knows about the image.
docker images mtconnect-agent-spb
# You should see BOTH tags (1.0.0 and latest) pointing at the same image ID.

# 4. Start the agent — from INSIDE the site config folder.
#    The compose file references the image by name (mtconnect-agent-spb:1.0.0),
#    and mounts the config/devices/profiles folders from THIS folder.
cd C:\Factories\SiteB\deployment-SiteB
docker compose up -d

# 5. Watch it come up.
docker logs --follow mtconnect-agent-spb
# Ctrl+C to stop tailing (the container keeps running).
```

Once it's up, browse `http://<target-server>:5003/probe` to confirm the MTConnect endpoint is live.

**The `.tar` file is now optional.** You can delete `C:\Factories\SiteB\mtconnect-agent-spb-1.0.0.tar` and everything keeps working. Docker only cares about what's in its internal store now. Keep it around only if you want an offline backup for a rainy day.

---

## Concrete worked example — shipping to "Site B"

Say Site B has:
- One Siemens S7-1500 PLC controlling a coolant system (like your `304cu` machine)
- No CNCs
- MQTT broker at `192.168.20.10`

### On the dev machine

```powershell
# Build + pack the image (only if you haven't already for this version)
cd c:\dev\deployment && docker compose build --no-cache
docker tag deployment-mtconnect-agent-spb:latest mtconnect-agent-spb:1.0.0
cd c:\dev\MtConnect.net\deploy
docker save -o mtconnect-agent-spb-1.0.0.tar mtconnect-agent-spb:1.0.0 mtconnect-agent-spb:latest

# Prepare the site folder
cp -r c:\dev\deployment c:\dev\deployment-SiteB
copy c:\dev\MtConnect.net\deploy\docker-compose.deploy.template.yml c:\dev\deployment-SiteB\docker-compose.yml

# Edit the site config
# (open c:\dev\deployment-SiteB\config\agent.config.yaml in an editor)
#  - Change groupId/nodeId in sparkplug-relay to something like SiteB / Coolant
#  - Change MQTT broker to 192.168.20.10
#  - Remove the shdr-adapter blocks (no CNCs at Site B)
#  - Keep the opcua-adapter block, update endpoint to Site B's S7 IP

# Zip the site folder for shipping
Compress-Archive -Path c:\dev\deployment-SiteB -DestinationPath c:\Ship\deployment-SiteB.zip
```

Now `c:\Ship\` contains:
- `mtconnect-agent-spb-1.0.0.tar`  (from `c:\dev\MtConnect.net\deploy\`)
- `deployment-SiteB.zip`  (the config folder, zipped)

Ship both to Site B via SFTP / USB / whatever.

### On Site B's target server

```powershell
# Someone puts the shipped files at C:\Factories\
# C:\Factories\
#   ├── mtconnect-agent-spb-1.0.0.tar
#   └── deployment-SiteB.zip

# Unzip the config folder
cd C:\Factories
Expand-Archive deployment-SiteB.zip .

# Load the image
docker load -i mtconnect-agent-spb-1.0.0.tar

# Start the agent
cd deployment-SiteB
docker compose up -d

# Check it's running
docker ps
docker logs mtconnect-agent-spb
```

Done. Agent is up at `http://siteb-server:5003/probe`. Sparkplug B data flowing to the site's MQTT broker.

---

## Phase 5 — Updating a site to a newer image version

Same process, minus preparing the site folder from scratch:

```powershell
# On the target — ship the new .tar to the target machine first, then:
cd C:\Factories
docker compose -f deployment-SiteB\docker-compose.yml down    # stop old container
docker load -i mtconnect-agent-spb-1.0.1.tar                             # load new image
# Edit deployment-SiteB\docker-compose.yml, change the image tag:
#   image: mtconnect-agent-spb:1.0.1     (was 1.0.0)
docker compose -f deployment-SiteB\docker-compose.yml up -d   # start new container
```

Optionally clean up the old image afterwards:

```powershell
docker image rm mtconnect-agent-spb:1.0.0
```

---

## FAQ

**Q: Do I need to keep the `.tar` file on the target after loading?**
No. Once `docker load` finishes, Docker has copied the image into its internal storage. The `.tar` is just a transport format. Delete it, back it up, whatever — the container will keep running.

**Q: What does the `image:` field in `docker-compose.yml` actually point at?**
The image's **name and tag** in Docker's internal store (e.g. `mtconnect-agent-spb:1.0.0`). Not a file path. When compose starts a container, Docker looks up that name+tag in its internal store and uses the image it finds there. If it doesn't find it, you get "image not found" — that's when you go back and run `docker load`.

**Q: What if I lose the source code but still have the `.tar`?**
You can still deploy from the `.tar` forever. It's fully self-contained — the compiled agent, .NET runtime, OS layer, all of it. Losing the source means you can't build a **new** version, but existing images keep working.

**Q: How do I check which version is currently running on a target?**
```powershell
docker ps --format "table {{.Names}}\t{{.Image}}"
```
Look at the `Image` column. If it says `mtconnect-agent-spb:1.0.0`, that's the version.

**Q: Where do the container's logs go?**
Two places:
- The **MTConnect log files** are written to `logs/` inside the site config folder (via the `logs:` volume mount).
- The **stdout/stderr** of the .NET process is captured by Docker and shown by `docker logs mtconnect-agent-spb`.

**Q: Can I have multiple site configs on the same target machine?**
Yes — as long as they don't collide on host ports. Each site config folder is independent; each `docker compose up -d` from a different folder creates a differently-named container. Change the port mapping (`5003:5000` → `5004:5000` for the second one) so they don't fight.

**Q: The `image:` tag in my compose file is `1.0.0`, but the `.tar` I loaded contained `1.0.1`. What happens?**
`docker compose up -d` will fail with "no such image" because the tag `1.0.0` isn't in Docker's store — only `1.0.1` is. Fix: either edit the compose file to say `1.0.1`, or ship the correct `.tar` for `1.0.0`.

---

## Troubleshooting

| Symptom | Cause | Fix |
| --- | --- | --- |
| `docker load` errors with "invalid tar header" | `.tar` corrupted in transit | Re-ship, verify file size matches, load again |
| `docker compose up` says "no such image" | The image tag isn't in Docker's store | Run `docker images` — if the image isn't listed, run `docker load -i <tar>` |
| Container starts then immediately exits (code 1 in logs) | Config parse error | `docker logs mtconnect-agent-spb` — usually a YAML indentation issue in `agent.config.yaml` |
| Port already in use on target | Something else on the host is using port 5003 | Change host port in `docker-compose.yml` (e.g. `"5004:5000"`), `up -d` again |
| Cannot connect to Docker daemon | Docker Desktop / Docker Engine isn't running on the target | Start the Docker service |
| Windows volume mounts fail | The drive isn't shared with Docker Desktop | Docker Desktop → Settings → Resources → File Sharing → add the drive (usually `C:`) |

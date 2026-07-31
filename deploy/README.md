# MTConnect.NET — Deploying the Agent to a Factory Server

> Part of the [fork additions](../FORK.md) to MTConnect.NET.

This folder holds the tools you need to ship the MTConnect agent to a factory server **without giving that server the source code**. Layman-friendly walkthrough, no assumed Docker expertise.

Paths in this doc are written relative to **this repo's root** (where the `Dockerfile` and `README.md` at the top live). Adjust for your local checkout.

---

## The mental model (read this first)

Think of a Docker image like a **flatpack furniture box**:

- On the dev machine you **assemble** the box from source code (`docker compose build`).
- You **pack** the box into a shipping-friendly form (`docker save` → a `.tar` file).
- You **drive the box to the factory** (email, USB stick, SFTP — however).
- On the target machine you **unpack** the box into Docker's cupboard (`docker load`).
- After unpacking, the box (the `.tar` file) is empty and can be thrown away or kept as a backup — Docker now knows about the image and refers to it by **name + version tag**, not by file path.

The compose file at the target says "give me the box called `mtconnect-agent-spb:1.1.0`" — Docker looks in its cupboard, finds it, starts a container from it. **The compose file never mentions the `.tar` file path.** Once loaded, the `.tar` is irrelevant.

Now the actual steps.

---

## Phase 1 — Build the image (on your dev machine)

You only do this **once per version**. Do it again when the source code changes and you want to ship an update.

**Prerequisite:** you have this repo cloned somewhere, and a "site config folder" alongside it (see [Phase 2](#phase-2--prepare-the-site-folder-still-on-your-dev-machine)) — a folder containing a `docker-compose.yml` with `build: context: <path-to-this-repo>` and `dockerfile: Dockerfile`. If you don't have one yet, use [`deploy/starter-kit/`](starter-kit/) as the template.

Give that dev-machine compose file an `image:` key alongside its `build:` block, naming the version it produces:

```yaml
    build:
      context: <path-to-this-repo>
      dockerfile: Dockerfile
    image: mtconnect-agent-spb:1.1.0
```

With both keys present, `docker compose build` tags the result directly. Without the `image:` key Compose invents a name from the folder (`<folder-name>-mtconnect-agent-spb:latest`) and you have to retag by hand — and guess what the folder-derived name was. Bumping a release is then a one-line edit here.

```powershell
# 1. Build the image from source, from your site config folder.
#    That folder's docker-compose.yml references the Dockerfile in this repo,
#    and its image: key names the output.
cd <path-to-your-site-config-folder>
docker compose build --no-cache

# 2. Confirm it built under the versioned name.
docker images mtconnect-agent-spb

# 3. Pack the image into a shippable .tar file, into this repo's deploy/ folder.
cd <path-to-this-repo>/deploy
docker save -o mtconnect-agent-spb-1.1.0.tar mtconnect-agent-spb:1.1.0

# 4. Confirm size (typically ~40-60 MB — Docker compresses well).
ls -lh mtconnect-agent-spb-1.1.0.tar
```

**Save the versioned tag only — never `:latest`.** A `.tar` containing `:latest` moves the target's `latest` to whatever you happened to ship, which makes "which version is running here?" unanswerable without inspecting image IDs, and leaves nothing to roll back to.

After this, `<repo>/deploy/mtconnect-agent-spb-1.1.0.tar` is the **one artefact from the source repo** that goes to the target. It's gitignored — ship it out-of-band (SFTP / USB / private artefact store).

---

## Phase 2 — Prepare the site folder (still on your dev machine)

**Easiest path — use the starter-kit.** This folder contains [`starter-kit/`](starter-kit/) — a ready-to-ship bundle with the compose file, a commented `agent.config.yaml`, empty `devices/` and `profiles/` folders, `logs/`, and a target-side README. To prep a new site, just copy it:

```powershell
# Copy the starter-kit as-is, rename for the site (paths are examples — put it wherever you like)
cp -r <path-to-this-repo>/deploy/starter-kit <ship-staging>/SiteA
```

Then edit `<ship-staging>/SiteA/config/agent.config.yaml` — uncomment the module blocks you need (mqtt-relay, sparkplug-relay, opcua-adapter, shdr-adapter, haas-ethernet) and fill in the site's IPs, broker hostnames, deviceKeys. Drop any per-machine device XMLs into `devices/` and OPC UA profile bundles into `profiles/`.

That's it. Zip `<ship-staging>/SiteA/`, ship alongside the `.tar`, done.

**Alternative (legacy) path — build the site folder from scratch:**

If for some reason you want to assemble the site folder manually rather than starting from the kit:

```powershell
# 1. Make an empty site folder anywhere
mkdir <ship-staging>/SiteA
cd <ship-staging>/SiteA

# 2. Grab the deploy-mode compose template (references image:, not build:).
copy <path-to-this-repo>/deploy/docker-compose.deploy.template.yml docker-compose.yml

# 3. Create the mount folders + drop in a starter config
mkdir config devices profiles logs
# then hand-populate config/agent.config.yaml and config/NLog.config
```

Whichever route, the resulting site folder needs to look like this:

```
SiteA/
├── docker-compose.yml            ← references image: mtconnect-agent-spb:1.1.0, no build: block
├── config/
│   ├── agent.config.yaml         ← edited for this site
│   └── NLog.config
├── devices/                       ← per-machine XMLs (SHDR / hand-authored)
├── profiles/                      ← OPC UA profile bundles
└── logs/                          ← empty until first run
```

---

## Phase 3 — Ship to the target factory server

**Two things travel to the target:**

1. `<repo>/deploy/mtconnect-agent-spb-1.1.0.tar` (the packed image — ~46 MB)
2. `<ship-staging>/SiteA/` (the site config folder — a few KB; zip it up first)

Ship however you like — USB drive, SFTP, email, S3 bucket, shared drive. Nothing about them is Docker-specific during transit; they're just files.

---

## Phase 4 — Install on the target factory server

The target machine has **Docker installed** but no source code and no images yet. You (or a site engineer) do this once:

```powershell
# 1. Put the shipped files somewhere on disk. Convention (any location works):
#    <install-root>/
#    ├── SiteA/                              ← the unzipped site config folder
#    │   ├── docker-compose.yml
#    │   ├── config/  devices/  profiles/  logs/
#    └── mtconnect-agent-spb-1.1.0.tar        ← put it wherever's reachable

# 2. Unpack the image into Docker's internal storage.
#    You can be in ANY folder for this step — the -i flag takes the .tar path.
cd <install-root>
docker load -i mtconnect-agent-spb-1.1.0.tar
# You'll see:
#   Loaded image: mtconnect-agent-spb:1.1.0
#
# Docker has now copied the image into its own store. The .tar file is no
# longer needed. You can delete it, keep it as a backup, whatever — Docker
# doesn't care.

# 3. Verify Docker knows about the image.
docker images mtconnect-agent-spb

# 4. Start the agent — from INSIDE the site config folder.
#    The compose file references the image by name (mtconnect-agent-spb:1.1.0),
#    and mounts the config/devices/profiles folders from THIS folder.
cd <install-root>/SiteA
docker compose up -d

# 5. Watch it come up.
docker logs --follow mtconnect-agent-spb
# Ctrl+C to stop tailing (the container keeps running).
```

Once it's up, browse `http://<target-server>:5003/probe` to confirm the MTConnect endpoint is live.

**The `.tar` file is now optional.** You can delete `<install-root>/mtconnect-agent-spb-1.1.0.tar` and everything keeps working. Docker only cares about what's in its internal store now. Keep it around only if you want an offline backup for a rainy day.

---

## Concrete worked example — shipping to "Site B"

Say Site B has:
- One Siemens S7-1500 PLC controlling a coolant system
- No CNCs
- MQTT broker at `192.168.20.10`

### On the dev machine

```powershell
# Assume this repo is cloned at <repo>, and you have a working site folder <site>.

# Build + pack the image (only if you haven't already for this version)
# <site>/docker-compose.yml carries image: mtconnect-agent-spb:1.1.0, so the
# build comes out already named — no retag step.
cd <site>
docker compose build --no-cache

cd <repo>/deploy
docker save -o mtconnect-agent-spb-1.1.0.tar mtconnect-agent-spb:1.1.0

# Prepare Site B's folder from the starter-kit
cp -r <repo>/deploy/starter-kit <ship>/SiteB

# Edit <ship>/SiteB/config/agent.config.yaml:
#  - Uncomment sparkplug-relay, set groupId/nodeId to something like SiteB / Coolant
#  - Uncomment mqtt-relay, set server to 192.168.20.10
#  - Uncomment opcua-adapter, set endpoint to Site B's S7 IP, deviceKey, profile

# Zip the site folder for shipping
Compress-Archive -Path <ship>/SiteB -DestinationPath <ship>/SiteB.zip
```

Now `<ship>/` contains:
- `mtconnect-agent-spb-1.1.0.tar`  (from `<repo>/deploy/`)
- `SiteB.zip`                     (the site config folder, zipped)

Ship both to Site B via SFTP / USB / whatever.

### On Site B's target server

```powershell
# Someone puts the shipped files at <install-root>\, e.g. C:\Factories\
# C:\Factories\
#   ├── mtconnect-agent-spb-1.1.0.tar
#   └── SiteB.zip

cd C:\Factories
Expand-Archive SiteB.zip .

# Load the image
docker load -i mtconnect-agent-spb-1.1.0.tar

# Start the agent
cd SiteB
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
cd <install-root>
docker compose -f SiteB\docker-compose.yml down    # stop old container
docker load -i mtconnect-agent-spb-1.1.1.tar        # load new image
# Edit SiteB\docker-compose.yml, change the image tag:
#   image: mtconnect-agent-spb:1.1.1     (was 1.1.0)
docker compose -f SiteB\docker-compose.yml up -d   # start new container
```

Optionally clean up the old image afterwards:

```powershell
docker image rm mtconnect-agent-spb:1.1.0
```

---

## Persisting the Sparkplug bdSeq counter

Sparkplug's `bdSeq` is how a Host Application tells a live session from a stale NDEATH the broker delivered late: the Will Message registered on a connection and the NBIRTH published on that same connection carry the same value, and it must advance on every connect.

The agent persists it, but by default to a file beside the binary — which **inside a container is lost when the container is recreated**. Every `docker compose down && up` would therefore restart the counter at zero and replay values the host has already seen, so a genuinely new session can be mistaken for the stale one it replaces. The Eclipse Sparkplug TCK catches this as `tck-id-topics-nbirth-bdseq-increment`.

Give it a mounted directory — that is the whole change, and it serves every relay block:

```yaml
    volumes:
      - ./state:/app/state          # add alongside the existing mounts
```

Create `state/` next to `config/` and `devices/` when preparing the site folder. No `agent.config.yaml` change is needed: `/app/state` is where the agent already writes, and each file is named after its Edge Node identity (`bdseq-<groupId>-<nodeId>`), so several `sparkplug-relay` blocks share the directory without sharing a counter. Each file holds a single number and is safe to delete; doing so simply restarts that node's sequence.

## Splitting a larger fleet across several Sparkplug Edge Nodes

One agent process publishes **every device it loads** under a single Edge Node — the `groupId` + `nodeId` of its `sparkplug-relay` block. That is the right shape for a handful of machines. Past that, three properties of Sparkplug start to couple machines together that have nothing to do with each other:

- **`seq` is per Edge Node, not per device.** Sparkplug messages are QoS 0, so one dropped publish anywhere leaves a gap the Host Application detects — and its prescribed recovery is a `Node Control/Rebirth` for the whole node, meaning every device's DBIRTH at once. On a large node that burst is itself more likely to drop something.
- **NDEATH is per Edge Node.** If the process dies, every machine under it goes offline simultaneously from a consumer's point of view.
- **A config edit restarts the agent**, which re-births every device it owns and bumps the MTConnect `InstanceId` for all of them. While a site is being commissioned this is the churn you notice most.

Splitting the fleet across containers gives each group its own Edge Node identity, its own MQTT session, and — the practical win — its own restart blast radius. Adding a machine to one group leaves the others untouched. Ten to fifteen machines per node is a reasonable starting point.

**The `devices/` folder is the partition.** An agent relays every device XML it can see, so a machine's XML must live in exactly one group's folder and never in two. The adapter blocks travel with it: whichever container holds a machine's device XML also holds the `shdr-adapter` / `opcua-adapter` / `haas-ethernet` block that feeds it.

```
<install-root>/
├─ docker-compose.yml
├─ config/     agent.config.yaml  → nodeId: Agent1
├─ devices/    Machine-01.xml, Machine-02.xml
├─ config-2/   agent.config.yaml  → nodeId: Agent2
├─ devices-2/  Machine-03.xml, Machine-04.xml
├─ profiles/   (shared — read-only, safe to mount into both)
├─ logs/
└─ logs-2/
```

A worked two-service compose file is in [docker-compose.deploy.template.yml](docker-compose.deploy.template.yml), commented out and ready to enable.

Two caveats before you split:

- **It splits the MTConnect HTTP view as well.** Each container serves only its own devices, on its own host port, and nothing aggregates `/probe` or `/current` across them. If anything consumes the HTTP API fleet-wide, stay on one container. If your consumers are Sparkplug-only, this costs you nothing.
- **Each container is a full agent**, so budget roughly 100–200 MB of memory per group. Comfortable at three or four groups; wasteful at twenty.

---

## FAQ

**Q: Do I need to keep the `.tar` file on the target after loading?**
No. Once `docker load` finishes, Docker has copied the image into its internal storage. The `.tar` is just a transport format. Delete it, back it up, whatever — the container will keep running.

**Q: What does the `image:` field in `docker-compose.yml` actually point at?**
The image's **name and tag** in Docker's internal store (e.g. `mtconnect-agent-spb:1.1.0`). Not a file path. When compose starts a container, Docker looks up that name+tag in its internal store and uses the image it finds there. If it doesn't find it, you get "image not found" — that's when you go back and run `docker load`.

**Q: What if I lose the source code but still have the `.tar`?**
You can still deploy from the `.tar` forever. It's fully self-contained — the compiled agent, .NET runtime, OS layer, all of it. Losing the source means you can't build a **new** version, but existing images keep working.

**Q: How do I check which version is currently running on a target?**
```powershell
docker ps --format "table {{.Names}}\t{{.Image}}"
```
Look at the `Image` column. If it says `mtconnect-agent-spb:1.1.0`, that's the version.

**Q: Where do the container's logs go?**
Two places:
- The **MTConnect log files** are written to `logs/` inside the site config folder (via the `logs:` volume mount).
- The **stdout/stderr** of the .NET process is captured by Docker and shown by `docker logs mtconnect-agent-spb`.

**Q: Can I have multiple site configs on the same target machine?**
Yes — as long as they don't collide on host ports. Each site config folder is independent; each `docker compose up -d` from a different folder creates a differently-named container. Change the port mapping (`5003:5000` → `5004:5000` for the second one) so they don't fight.

**Q: The `image:` tag in my compose file is `1.1.0`, but the `.tar` I loaded contained `1.1.1`. What happens?**
`docker compose up -d` will fail with "no such image" because the tag `1.1.0` isn't in Docker's store — only `1.1.1` is. Fix: either edit the compose file to say `1.1.1`, or ship the correct `.tar` for `1.1.0`.

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

# Configure modules

Modules are the agent's pluggable extension points. Each module attaches a capability — HTTP serving, MQTT publishing, SHDR ingestion — and a single agent typically runs several modules at once. This page documents every shipped module's configuration keys.

The module set:

- [HTTP server](#http-server) — `/probe`, `/current`, `/sample`, `/asset` endpoints.
- [MQTT broker](#mqtt-broker) — embedded MQTT broker.
- [MQTT relay](#mqtt-relay) — outbound publisher to an external MQTT broker.
- [MQTT adapter](#mqtt-adapter) — inbound consumer from an external MQTT broker.
- [SHDR adapter](#shdr-adapter) — inbound TCP-SHDR adapter.
- [HTTP adapter](#http-adapter) — inbound HTTP-poll adapter.
- [Sparkplug B relay](#sparkplug-b-relay) — outbound Sparkplug B edge node *(fork addition)*.
- [OPC UA adapter](#opc-ua-adapter) — inbound OPC UA subscription client *(fork addition)*.
- [Haas Ethernet adapter](#haas-ethernet-adapter) — inbound Haas Q-code poller *(fork addition)*.

Every module is declared in `agent.config.yaml` under `modules:` as a single-key map. The key is the module's name; the value is the module's config object.

## HTTP server

Hosts the MTConnect endpoints: `/probe`, `/current`, `/sample`, `/asset`, plus optional file-serving for stylesheets and schemas. Module key: `http-server`. Implementation: `MTConnect.NET-AgentModule-HttpServer`.

### Keys

```yaml
- http-server:
    hostname: 0.0.0.0          # interface to bind. Default: all interfaces.
    port: 5000                 # listen port. Default: 5000.
    allowPut: false            # whether /put is allowed for ingest. Default: false.
    indentOutput: true         # pretty-print responses. Default: false.
    documentFormat: xml        # default response format. xml | json | json-cppAgent
    accept:                    # MIME-to-format dispatch.
      text/xml: xml
      application/json: json
      application/mtconnect+json: json-cppAgent
    responseCompression:       # which compression encodings to advertise.
    - gzip
    - br
    files:                     # static-file mounts under /styles, /schemas, etc.
    - path: schemas
      location: schemas
    - path: styles
      location: styles
    tls:                       # optional TLS settings.
      pem:
        certificateAuthority: /path/to/rootCA.crt
        certificatePath: /path/to/server.crt
        privateKeyPath: /path/to/server.key
        privateKeyPassword: <secret>
```

Multiple `http-server` instances are supported and common — one on port `5000` plain, another on port `5001` with TLS, and a third on `5002` with `allowPut: true` for an internal ingest endpoint.

### `tls`

The `tls:` block configures TLS termination. Two shapes are supported:

- `pem:` — PEM-encoded certificate chain and private key.
- `pfx:` — PKCS#12 bundle.

When TLS is enabled, the HTTP server accepts only HTTPS connections. The `tlsConfiguration` class is shared with the MQTT modules — see [`MTConnect.Tls.TlsConfiguration`](/api/MTConnect.Tls.TlsConfiguration).

## MQTT broker

Hosts an embedded MQTT broker that publishes one topic per Device / DataItem, formatted as JSON-CPPAGENT-MQTT. Consumers connect with any MQTT client. Module key: `mqtt-broker`. Implementation: `MTConnect.NET-AgentModule-MqttBroker`.

```yaml
- mqtt-broker:
    port: 1883
    currentInterval: 10000
    sampleInterval: 500
    documentFormat: JSON-CPPAGENT
    topicPrefix: MTConnect/Document
    topicStructure: Document    # Document | Entity
    tls:
      pem:
        certificateAuthority: /path/to/rootCA.crt
        certificatePath: /path/to/server.crt
        privateKeyPath: /path/to/server.key
        privateKeyPassword: <secret>
```

### Topic structures

- **`Document`** — one topic per response document. Topics are `<prefix>/Probe/<deviceUuid>`, `<prefix>/Current/<deviceUuid>`, `<prefix>/Sample/<deviceUuid>`, `<prefix>/Asset/<deviceUuid>/<assetId>`. The payload is the full envelope.
- **`Entity`** — one topic per Observation / Asset / Device. Topics are `<prefix>/Devices/<deviceUuid>`, `<prefix>/Observations/<deviceUuid>/<dataItemId>`, `<prefix>/Assets/<deviceUuid>/<assetId>`. The payload is a single entity.

The `Document` structure is the cppagent-MQTT parity shape; the `Entity` structure is the library's per-observation alternative for consumers that prefer one-topic-per-observation.

### Intervals

- **`currentInterval`** — milliseconds between successive `Current` publishes. Default: `10000` (10 s).
- **`sampleInterval`** — milliseconds between successive `Sample` publishes. Default: `500`.

## MQTT relay

Publishes outbound MQTT messages to an external broker. The agent acts as a client. Module key: `mqtt-relay`. Implementation: `MTConnect.NET-AgentModule-MqttRelay`. The configuration object is [`MqttRelayModuleConfiguration`](/api/MTConnect.Configurations.MqttRelayModuleConfiguration).

```yaml
- mqtt-relay:
    server: broker.example.com   # MQTT broker hostname.
    port: 1883                   # broker port.
    timeout: 5000                # connect / I/O timeout (ms).
    reconnectInterval: 10000     # delay between reconnect attempts (ms).

    username: agent-01           # optional broker auth.
    password: <secret>
    clientId: agent-01           # MQTT client id.
    cleanSession: true           # MQTT clean-session flag.
    qos: 1                       # 0 | 1 | 2.

    useTls: false                # whether to use TLS.
    tls:
      pem:
        certificateAuthority: /path/to/rootCA.crt
        certificatePath: /path/to/agent.crt
        privateKeyPath: /path/to/agent.key
        privateKeyPassword: <secret>

    topicPrefix: MTConnect        # topic prefix.
    topicStructure: Document      # Document | Entity (see MQTT broker).
    documentFormat: JSON-CPPAGENT-MQTT
    indentOutput: false

    currentInterval: 5000         # millis between Current publishes.
    sampleInterval: 500           # millis between Sample publishes.
    durableRelay: true            # buffer + replay on reconnect.
```

### `durableRelay`

When true, the relay buffers outbound messages while disconnected and replays them on reconnect, preserving ordering. Set false to drop in-flight messages and resume only the live stream.

### Default values

The shipped defaults match what the constructor sets:

| Key | Default |
|---|---|
| `server` | `localhost` |
| `port` | `1883` |
| `timeout` | `5000` |
| `reconnectInterval` | `10000` |
| `topicPrefix` | `MTConnect` |
| `topicStructure` | `Document` |
| `documentFormat` | `json-cppAgent` |
| `currentInterval` | `5000` |
| `sampleInterval` | `500` |
| `durableRelay` | `false` |

## MQTT adapter

Subscribes to an external MQTT broker and ingests Observations from incoming messages. Module key: `mqtt-adapter`. Implementation: `MTConnect.NET-AgentModule-MqttAdapter`.

```yaml
- mqtt-adapter:
    deviceKey: mill-01            # the Device this adapter feeds.
    server: broker.example.com
    port: 1883
    topic: MTConnect/Adapter/mill-01    # subscribe topic (supports + and # wildcards).
    username: agent-01
    password: <secret>
    useTls: false
    tls: { ... }                  # same TlsConfiguration shape as above.
```

The adapter expects each MQTT message to carry a JSON-CPPAGENT or JSON-v1 Observation payload (or an SHDR-formatted text line). The `documentFormat` field auto-detects when omitted.

## SHDR adapter

Connects to an SHDR-output adapter over TCP and ingests SHDR lines. Module key: `shdr-adapter`. Implementation: `MTConnect.NET-AgentModule-ShdrAdapter`.

```yaml
- shdr-adapter:
    deviceKey: mill-01             # the Device this adapter feeds.
    hostname: localhost            # remote SHDR adapter host.
    port: 7878                     # remote SHDR adapter port.
    heartbeat: 1000                # heartbeat interval (ms).
    reconnectInterval: 1000        # delay between reconnect attempts (ms).
    connectionTimeout: 1000        # connection timeout (ms).
```

Multiple instances are typical when more than one upstream adapter feeds the agent — one per Device, each on its own port.

## HTTP adapter

Polls an HTTP endpoint that returns MTConnect-shaped responses. Module key: `http-adapter`. Implementation: `MTConnect.NET-AgentModule-HttpAdapter`.

```yaml
- http-adapter:
    deviceKey: mill-01
    url: http://upstream-agent.example.com:5000/sample
    interval: 1000                 # poll interval (ms).
    documentFormat: xml            # expected response format.
```

The HTTP adapter is most often used to chain agents — a regional aggregator agent polling per-cell agents — but works against any HTTP endpoint that emits the right envelope.

## Sparkplug B relay

*Fork addition.* Publishes every device on the agent to an MQTT broker as a Sparkplug B edge node — NBIRTH/DBIRTH on connect, batched DDATA on observation changes, DDEATH driven by AVAILABILITY, NCMD rebirth honoured. Module key: `sparkplug-relay`. Implementation: `MTConnect.NET-AgentModule-SparkplugBRelay`. Full walk-through in the module's ReadMe (`agent/Modules/MTConnect.NET-AgentModule-SparkplugBRelay/ReadMe.md`).

```yaml
- sparkplug-relay:
    server: broker.example.com     # MQTT broker hostname.
    port: 8883                     # broker port (8883 = TLS convention).
    groupId: Factory               # Sparkplug Group ID (topic namespace).
    nodeId: Agent1                 # Sparkplug Edge Node ID.
    clientId: MTConnect-Agent1     # optional; defaults to MTConnect-{guid}.
    username: mtconnect            # optional broker auth.
    password: <secret>
    reconnectInterval: 10000       # delay between reconnect attempts (ms).
    primaryHostId: SCADA-1         # optional; wait for this host's STATE before NBIRTH.

    useTls: true                   # optional; enable TLS.
    caCertificateFile: certs/broker-ca.crt   # optional; pin the broker CA (PEM or DER).
    clientCertificateFile: certs/agent1.pfx  # optional; PKCS#12 bundle → mutual TLS.
    clientCertificatePassword: <secret>
```

All devices share one edge-node identity (`groupId` + `nodeId`). DDATA is batched per device over a ~100 ms window, and the agent's observation path only pays a non-blocking enqueue, so a slow broker never stalls ingestion. `allowUntrustedCertificates: true` disables server-certificate validation — bench testing only. Relative certificate paths resolve against the agent binary directory.

Numeric (`SAMPLE`) metrics with no reading — unavailable, or a value that will not parse — publish Sparkplug's `is_null` flag rather than a value, keeping the `Double` type their DBIRTH declared without fabricating a `0.0` that a historian would trend as real data. `EVENT` and `CONDITION` metrics keep MTConnect's `UNAVAILABLE` literal, which a `String` metric can carry honestly. A device model that declares no `AVAILABILITY` DataItem is treated as available with a warning logged once, rather than being left permanently unborn with its observations silently discarded.

This module owns its Sparkplug B implementation — MQTT session, topics, `seq`/`bdSeq`, Will Message and payload encoding — so NBIRTH precedes every DBIRTH by construction rather than by observation, satisfying `tck-id-message-flow-device-birth-publish-nbirth-wait`. `primaryHostId` defers NBIRTH until the named Primary Host Application reports online via `STATE`. Persisted `bdSeq` lives in a `state` directory beside the agent binary, one file per Edge Node identity, so mounting a volume at `/app/state` is all that is needed however many relay blocks are configured — there is nothing to declare per block. Without it every deployment restarts the counter at zero and replays values a Host Application has already seen. `stateDirectory` overrides the location if required. The module ReadMe covers the QoS and retain rules per message type, and the TCK results.

## OPC UA adapter

*Fork addition.* Opens an OPC UA session to a server (typically a CNC controller), subscribes to the NodeIds mapped in a YAML profile from `profiles/`, and feeds value notifications into the agent buffer. The device model resolves in priority order: user-authored `devices/<key>.xml` → shipped `profiles/<name>.template.xml` → auto-generated flat device. Module key: `opcua-adapter`. Implementation: `MTConnect.NET-AgentModule-OpcUaAdapter`.

```yaml
- opcua-adapter:
    deviceKey: Machine-01          # the Device this adapter feeds.
    endpoint: opc.tcp://192.168.1.50:4840
    profile: sinumerik_840d        # references profiles/sinumerik_840d.yaml.
    securityPolicy: None           # None | Basic256Sha256.
    # acceptUntrustedServerCertificate: false   # require the server cert in pki/trusted.
    identity:
      mode: anonymous              # anonymous | username.
    samplingInterval: 250          # server-side sampling interval (ms).
    publishingInterval: 1000       # notification push interval (ms).
    reconnectInterval: 5000        # reconnect delay after keep-alive failure (ms).
    sessionTimeout: 60000          # requested session timeout (ms).
    # ignoreTimestamps: false      # per-adapter override of the agent-wide ignoreTimestamps.
```

One block per machine. On session loss every mapped data item flips to `UNAVAILABLE`; after reconnect all mapped nodes are re-read so unchanged values recover immediately instead of waiting for their next change. The optional `ignoreTimestamps` key overrides the agent-wide setting per adapter - unset inherits it, `true` re-stamps this device with the agent clock (dropping the controller's source timestamps), `false` keeps source time even when the agent-wide flag is on.
 By default any server certificate is accepted (`acceptUntrustedServerCertificate: true`) - the channel is encrypted but the server is not authenticated; set it to `false` and place the server certificate in the agent's `pki/trusted` store to close that gap. Startup fails fast if a security policy is configured but the server offers no secured endpoint.

### Authoring profiles: the OPC UA profile GUI

Profiles don't have to be written by hand. `tools/opcua-profile-gui/` (fork addition) is a PySide6 desktop app that connects to the OPC UA server, walks the address space showing live values, and lets you tick the Variables to map — suggesting MTConnect types and units from the spec vocabulary as you pick. It emits the `<name>.yaml` mappings file **and** the matching `<name>.template.xml` device template this module consumes: drop both into `profiles/` and set `profile: <name>` in the block above.

Run it from source (`pip install -r requirements.txt`, then `python main.py`) or ship the PyInstaller-built `.exe` to any workstation — full walkthrough in `tools/opcua-profile-gui/README.md`.

## Haas Ethernet adapter

*Fork addition.* Polls a Haas CNC over TCP with Q-code and macro-variable commands — usually through a Moxa NPort serial-to-Ethernet converter on the HMI's RS-232 port. Eleven built-in Q-codes and eight macro variables cover identity, mode, program state, counters, and axis positions; extras are declared inline. Module key: `haas-ethernet`. Implementation: `MTConnect.NET-AgentModule-HaasEthernet`.

```yaml
- haas-ethernet:
    deviceKey: HAAS-01             # must match devices/<key>.xml.
    server: 192.168.127.254        # Moxa NPort IP (or direct-Ethernet Haas).
    port: 4001                     # Moxa port 1 = 4001, port 2 = 4002, …
    pollInterval: 1.0              # seconds between poll cycles.
    commands:                      # extra Q-codes beyond the built-ins.
    - { name: air_pressure, command: "?Q999", pollEvery: 30 }
    macroVariables:                # extra macro registers beyond the built-ins.
    - { name: feedrate, variable: 3003 }
```

`pollOnce: true` fetches a value once per connection session (identity values); `pollEvery: <seconds>` throttles slow-moving counters. On transport failure the module flags **every** data item `UNAVAILABLE` (not just `avail`) and skips the rest of the cycle; on recovery the poll cycle restarts so availability returns before fresh data, and the once-per-session identity values are re-polled. Observations are stamped with the agent host clock at receipt (Q-codes carry no machine time) — keep the agent host NTP-synced.

## Configuration objects

Each module's YAML keys deserialize into a configuration class:

| Module | Configuration class |
|---|---|
| `http-server` | [`MTConnect.Configurations.HttpServerModuleConfiguration`](/api/MTConnect.Configurations.HttpServerModuleConfiguration) |
| `mqtt-broker` | [`MTConnect.Configurations.MqttBrokerModuleConfiguration`](/api/MTConnect.Configurations.MqttBrokerModuleConfiguration) |
| `mqtt-relay` | [`MTConnect.Configurations.MqttRelayModuleConfiguration`](/api/MTConnect.Configurations.MqttRelayModuleConfiguration) |
| `mqtt-adapter` | [`MTConnect.Configurations.MqttAdapterModuleConfiguration`](/api/MTConnect.Configurations.MqttAdapterModuleConfiguration) |
| `shdr-adapter` | [`MTConnect.Configurations.ShdrAdapterModuleConfiguration`](/api/MTConnect.Configurations.ShdrAdapterModuleConfiguration) |
| `http-adapter` | [`MTConnect.Configurations.HttpAdapterModuleConfiguration`](/api/MTConnect.Configurations.HttpAdapterModuleConfiguration) |
| `sparkplug-relay` | `MTConnect.Configurations.SparkplugBRelayModuleConfiguration` *(fork addition — no generated API page)* |
| `opcua-adapter` | `MTConnect.Configurations.OpcUaAdapterModuleConfiguration` *(fork addition — no generated API page)* |
| `haas-ethernet` | `MTConnect.Configurations.HaasEthernetAdapterModuleConfiguration` *(fork addition — no generated API page)* |

Programmatic configuration uses the class directly:

```csharp
using MTConnect.Configurations;

var relay = new MqttRelayModuleConfiguration
{
    Server = "broker.example.com",
    Port = 8883,
    UseTls = true,
    DocumentFormat = "JSON-CPPAGENT-MQTT",
    TopicPrefix = "MTConnect/Document",
    CurrentInterval = 5000,
    SampleInterval = 500,
    DurableRelay = true,
};
```

## Where to next

- [Configure an agent](/configure/agent-config) — the agent-wide settings the modules live under.
- [Cookbook: Configure MQTT relay](/cookbook/configure-mqtt-relay) — a full MQTT-relay walk-through.
- [Cookbook: Write a JSON-MQTT consumer](/cookbook/write-a-json-mqtt-consumer) — the consumer side.
- [Troubleshooting: MQTT TLS handshake](/troubleshooting/mqtt-tls-handshake) — when TLS connections fail.

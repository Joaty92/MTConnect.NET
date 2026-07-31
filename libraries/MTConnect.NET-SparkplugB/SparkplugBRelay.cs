// Copyright (c) 2025 MTConnect.NET fork contributors, All Rights Reserved.
// Licensed to you under the MIT license; see LICENSE.md in the repository root.

using MQTTnet;
using MQTTnet.Client;
using MTConnect.Agents;
using MTConnect.Configurations;
using MTConnect.Devices;
using MTConnect.Devices.DataItems;
using MTConnect.Logging;
using MTConnect.Observations;
using MTConnect.Observations.Output;
using MTConnect.SparkplugB.Payload;
using MTConnect.SparkplugB.Transport;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MTConnect.SparkplugB
{
    /// <summary>Bridges an <see cref="IMTConnectAgentBroker"/> to a Sparkplug B edge node — publishes NBIRTH/DBIRTH on connect, batched DDATA on observation changes, and DDEATH/re-DBIRTH driven by AVAILABILITY transitions per the Sparkplug B specification. All lifecycle work runs on one processing loop fed by a bounded channel, so the agent's observation path only pays a non-blocking enqueue and device state needs no lock-free choreography.</summary>
    public class SparkplugBRelay
    {
        // How long the processing loop accumulates observations before publishing
        // one DDATA per device, instead of one MQTT packet per observation.
        private const int BatchWindowMs = 100;

        private readonly SparkplugBRelayConfiguration _config;
        private readonly IMTConnectAgentBroker _agent;
        private SparkplugEdgeNode _node;
        private CancellationTokenSource _cts;
        private Channel<WorkItem> _work;
        private Task _processTask;
        private volatile bool _isRunning;

        // Capacity of the work queue. Overflow discards the OLDEST entry, which is the
        // right policy for telemetry — shed stale readings, keep fresh ones — but it means
        // loss is invisible unless it is counted. See ReportQueueDropsIfDue.
        private const int WorkQueueCapacity = 10000;

        // How often to report queue overflow.
        private const int QueueDropReportMs = 30000;

        // Prefix of the file carrying bdSeq across restarts. The Edge Node identity is appended,
        // so a fleet split across several Edge Nodes can share one state directory — and one
        // mounted volume — without two relays clobbering each other's counter. Sharing one would
        // make bdSeq non-monotonic on both nodes, which is precisely the confusion it exists to
        // prevent.
        private const string BirthDeathSequencePrefix = "bdseq";

        // Default directory for persisted state, relative to the agent binary directory. A
        // convention rather than a required setting: mounting a volume at this path is enough to
        // make bdSeq survive, with nothing to declare per relay block. Without the mount the
        // directory is still created and used, it simply does not outlive the container.
        private const string DefaultStateDirectory = "state";

        // Queue-loss accounting. _enqueued is written from the agent's observation dispatch
        // path (Interlocked); _processed and the reporting state below are loop-owned.
        // Dropped is derived rather than observed because BoundedChannelFullMode.DropOldest
        // gives no callback and leaves TryWrite returning true even as it evicts.
        private long _enqueued;
        private long _processed;
        private long _reportedDrops;
        private int _lastDropReportTick = Environment.TickCount;

        // True once the edge node has published NBIRTH for the current session.
        // Sparkplug 3.0 tck-id-message-flow-device-birth-publish-nbirth-wait: "The NBIRTH
        // message MUST have been sent within the current MQTT session prior to a DBIRTH
        // being published." The edge node publishes NBIRTH itself and only then raises
        // SessionEstablished, so ordering is guaranteed by construction; this flag simply
        // stops queued work from leaking across a session boundary. Loop-owned.
        private bool _sessionBorn;

        // UUID → device Name (written by the processing loop, read on the agent's
        // observation dispatch path)
        private readonly ConcurrentDictionary<string, string> _deviceNames = new();

        // device Name → DBIRTH published and device currently available (DDATA allowed).
        // Touched only from the single processing loop — plain dictionary by design.
        private readonly Dictionary<string, bool> _deviceBorn = new();

        // Device names already warned about a missing AVAILABILITY DataItem, so a
        // flapping broker connection does not repeat the warning on every rebirth.
        // Loop-owned, like _deviceBorn.
        private readonly HashSet<string> _availabilityWarned = new();

        private enum WorkKind { Observation, DeviceAdded, PublishAllBirths, NodeRebirth, SessionEnded }

        private readonly struct WorkItem
        {
            public WorkKind Kind { get; }
            public IObservation Observation { get; }
            public IDevice Device { get; }

            private WorkItem(WorkKind kind, IObservation observation, IDevice device)
            {
                Kind = kind;
                Observation = observation;
                Device = device;
            }

            public static WorkItem ForObservation(IObservation observation) => new(WorkKind.Observation, observation, null);
            public static WorkItem ForDeviceAdded(IDevice device) => new(WorkKind.DeviceAdded, null, device);
            public static WorkItem ForPublishAllBirths() => new(WorkKind.PublishAllBirths, null, null);
            public static WorkItem ForNodeRebirth() => new(WorkKind.NodeRebirth, null, null);
            public static WorkItem ForSessionEnded() => new(WorkKind.SessionEnded, null, null);
        }

        /// <summary>Raised for relay diagnostic messages (connection state, publish failures, lifecycle events).</summary>
        public event MTConnectLogEventHandler LogReceived;

        /// <summary>Creates a relay bound to <paramref name="config"/> and the supplied <paramref name="agent"/>; call <see cref="Start"/> to connect to the broker.</summary>
        public SparkplugBRelay(SparkplugBRelayConfiguration config, IMTConnectAgentBroker agent)
        {
            _config = config;
            _agent = agent;
        }

        /// <summary>Connects the Sparkplug edge node to the configured MQTT broker, starts the processing loop, and wires up Connected / Disconnected / NCMD-Rebirth handlers.</summary>
        public async Task Start()
        {
            _cts = new CancellationTokenSource();
            _isRunning = true;

            _work = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(WorkQueueCapacity)
            {
                SingleReader = true,
                // A slow or disconnected broker must never block the agent's
                // observation path; prefer losing the oldest queued updates.
                FullMode = BoundedChannelFullMode.DropOldest,
            });

            _node = new SparkplugEdgeNode(new SparkplugEdgeNodeOptions
            {
                Server = _config.Server,
                Port = _config.Port,
                ClientId = _config.ClientId,
                Username = _config.Username,
                Password = _config.Password,
                GroupId = _config.GroupId,
                EdgeNodeId = _config.NodeId,
                PrimaryHostId = _config.PrimaryHostId,
                ReconnectIntervalMs = _config.ReconnectInterval,
                TlsOptions = BuildTlsOptions(),
                BirthDeathSequencePath = BuildBirthDeathSequencePath()
            });

            _node.LogReceived += Log;

            // Every NBIRTH metric this relay contributes. The edge node adds bdSeq and
            // Node Control/Rebirth itself, since both are protocol-level rather than ours.
            _node.NodeMetricsProvider = () => Array.Empty<SparkplugMetric>();

            // Raised only after NBIRTH has reached the wire, so device births cannot precede
            // it. That ordering is structural now — the edge node publishes NBIRTH and awaits
            // it before raising this — rather than something observed after the fact.
            _node.SessionEstablished += () =>
            {
                Log(MTConnectLogLevel.Information, "NBIRTH published — publishing DBIRTH for all devices");
                Enqueue(WorkItem.ForPublishAllBirths());
                return Task.CompletedTask;
            };

            _node.SessionLost += () =>
            {
                // The Sparkplug session is over: nothing may be published until the next
                // NBIRTH, and every device must be re-born into the new session.
                Enqueue(WorkItem.ForSessionEnded());
                return Task.CompletedTask;
            };

            // The edge node republishes NBIRTH itself on an NCMD Rebirth; this only has to
            // rebuild the device births that follow it.
            _node.RebirthRequested += () =>
            {
                Enqueue(WorkItem.ForSessionEnded());
                return Task.CompletedTask;
            };

            _processTask = Task.Run(() => ProcessWorkAsync(_cts.Token));

            await _node.StartAsync(_cts.Token);
        }


        /// <summary>Stops the processing loop, then ends the Sparkplug session intentionally — the edge node publishes NDEATH and disconnects cleanly, so consumers can tell a planned stop from a crash.</summary>
        public async Task Stop()
        {
            _isRunning = false;
            _work?.Writer.TryComplete();
            _cts?.Cancel();
            if (_processTask != null)
            {
                try { await _processTask; } catch { }
            }
            if (_node != null)
            {
                await _node.DisposeAsync();
                _node = null;
            }
        }

        /// <summary>Handler invoked by the agent when a device is added or rediscovered; queues a DBIRTH for the new device.</summary>
        public Task OnDeviceAdded(IDevice device)
        {
            if (_isRunning && device != null) Enqueue(WorkItem.ForDeviceAdded(device));
            return Task.CompletedTask;
        }

        /// <summary>Handler invoked by the agent on every new observation. Non-blocking: the observation is queued and the processing loop batches DDATA per device over a short window; AVAILABILITY observations drive DDEATH / re-DBIRTH transitions.</summary>
        public Task OnObservationAdded(IObservation observation)
        {
            if (_isRunning && observation != null) Enqueue(WorkItem.ForObservation(observation));
            return Task.CompletedTask;
        }

        // Single write path, so queue accounting cannot drift from what was actually queued.
        private void Enqueue(WorkItem item)
        {
            if (_work?.Writer.TryWrite(item) == true) Interlocked.Increment(ref _enqueued);
        }

        // Turns silent queue loss into an operational signal.
        //
        // DropOldest evicts without any notification and TryWrite still reports success, so
        // a relay that cannot keep up simply publishes less with nothing to say about it —
        // indistinguishable, downstream, from machines that went quiet. Loss is therefore
        // derived: everything written, minus everything drained, minus what is still queued.
        private void ReportQueueDropsIfDue()
        {
            if (unchecked((uint)(Environment.TickCount - _lastDropReportTick)) < QueueDropReportMs) return;
            _lastDropReportTick = Environment.TickCount;

            var reader = _work.Reader;
            var depth = reader.CanCount ? reader.Count : 0;
            var dropped = Interlocked.Read(ref _enqueued) - _processed - depth;
            if (dropped < 0) dropped = 0;

            var newDrops = dropped - _reportedDrops;
            if (newDrops <= 0) return;
            _reportedDrops = dropped;

            Log(MTConnectLogLevel.Warning,
                $"observation queue overflow: {newDrops:N0} dropped in the last {QueueDropReportMs / 1000}s " +
                $"({dropped:N0} total), queue depth {depth:N0}/{WorkQueueCapacity:N0} — " +
                "the broker or network is not keeping up and Sparkplug updates are being shed");
        }

        private async Task ProcessWorkAsync(CancellationToken cancellationToken)
        {
            var reader = _work.Reader;
            // deviceName → metrics accumulated during the current batch window
            var pending = new Dictionary<string, List<SparkplugMetric>>();

            try
            {
                while (await reader.WaitToReadAsync(cancellationToken))
                {
                    try { await Task.Delay(BatchWindowMs, cancellationToken); }
                    catch (OperationCanceledException) { }

                    while (reader.TryRead(out var item))
                    {
                        _processed++;
                        try
                        {
                            await HandleWorkItemAsync(item, pending);
                        }
                        catch (Exception ex)
                        {
                            Log(MTConnectLogLevel.Warning, $"Relay work item failed: {ex.Message}");
                        }
                    }

                    await FlushAllAsync(pending);
                    ReportQueueDropsIfDue();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log(MTConnectLogLevel.Error, $"Relay processing loop stopped unexpectedly: {ex.Message}");
            }
        }

        private async Task HandleWorkItemAsync(WorkItem item, Dictionary<string, List<SparkplugMetric>> pending)
        {
            switch (item.Kind)
            {
                case WorkKind.Observation:
                    if (!_deviceNames.TryGetValue(item.Observation.DeviceUuid, out var deviceName)) return;

                    if (item.Observation.Type == AvailabilityDataItem.TypeId)
                    {
                        // Flush queued DDATA first so data cannot straddle the
                        // DDEATH / re-DBIRTH boundary this transition may publish.
                        await FlushDeviceAsync(deviceName, pending);
                        await HandleAvailabilityChangeAsync(deviceName, item.Observation, pending);
                        return;
                    }

                    // SPB spec: no DDATA before DBIRTH or after DDEATH.
                    if (!_deviceBorn.GetValueOrDefault(deviceName)) return;

                    Append(pending, deviceName, SparkplugBMetricMapper.ToMetric(item.Observation));
                    return;

                case WorkKind.DeviceAdded:
                    await PublishDeviceBirthAsync(item.Device, pending);
                    return;

                case WorkKind.PublishAllBirths:
                    // Reached only after an NBIRTH was seen on the wire, so this is the
                    // point at which DBIRTHs become legal for the new session.
                    _sessionBorn = true;
                    _deviceBorn.Clear();
                    foreach (var device in _agent.GetDevices() ?? Enumerable.Empty<IDevice>())
                        await PublishDeviceBirthAsync(device, pending);
                    return;

                case WorkKind.NodeRebirth:
                    // Rebirth REPLACES the node's known-metrics storage — pass the same
                    // list as at startup or the Node Control/Rebirth metric disappears
                    // from the new NBIRTH and later NCMD rebirths get filtered out.
                    //
                    // The DBIRTH round is deliberately not published here: Rebirth()
                    // publishes a new NBIRTH, which starts a new Sparkplug session, and
                    // the observer turns that into a PublishAllBirths once the NBIRTH is
                    // actually on the wire. Publishing births inline would race it exactly
                    // as the old connect-time trigger did.
                    _sessionBorn = false;
                    pending.Clear();
                    await _node.RebirthAsync();
                    return;

                case WorkKind.SessionEnded:
                    // Nothing may be published until the next NBIRTH is observed, and any
                    // DDATA queued for the dead session must not straddle into the new one.
                    _sessionBorn = false;
                    _deviceBorn.Clear();
                    pending.Clear();
                    return;
            }
        }

        private async Task HandleAvailabilityChangeAsync(string deviceName, IObservation observation, Dictionary<string, List<SparkplugMetric>> pending)
        {
            var raw = observation.IsUnavailable ? null : observation.GetValue(ValueKeys.Result);
            var isAvailable = SparkplugBAvailability.IsAvailableValue(raw);

            if (!isAvailable)
            {
                if (_deviceBorn.GetValueOrDefault(deviceName))
                {
                    _deviceBorn[deviceName] = false;
                    await PublishDeviceDeathAsync(deviceName);
                }
                return;
            }

            // Machine came online. If DDEATH was sent (or DBIRTH never succeeded),
            // re-publish DBIRTH before any DDATA can flow.
            if (!_deviceBorn.GetValueOrDefault(deviceName))
            {
                var device = _agent.GetDevices()?.FirstOrDefault(d => d.Name == deviceName);
                if (device == null) return;
                await PublishDeviceBirthAsync(device, pending);
                if (!_deviceBorn.GetValueOrDefault(deviceName)) return;
            }

            // Queue the AVAILABILITY change as DDATA so subscribers see AVAILABLE
            // at the next flush.
            Append(pending, deviceName, SparkplugBMetricMapper.ToMetric(observation));
        }

        private async Task PublishDeviceBirthAsync(IDevice device, Dictionary<string, List<SparkplugMetric>> pending)
        {
            if (!_isRunning || _node == null || device == null) return;

            // tck-id-message-flow-device-birth-publish-nbirth-wait. Every caller reaches
            // here from the single processing loop, so this one check covers the birth
            // round, DeviceAdded, and the AVAILABILITY re-birth path alike.
            if (!_sessionBorn)
            {
                Log(MTConnectLogLevel.Debug,
                    $"DBIRTH for {device.Name} deferred — no NBIRTH published in this session yet");
                return;
            }

            _deviceNames[device.Uuid] = device.Name;

            // Discard DDATA queued before this (re)birth: publishing it now would
            // put data from the previous birth epoch after the new session's NBIRTH
            // but before this device's DBIRTH (spec violation), and the DBIRTH below
            // carries current values anyway.
            pending.Remove(device.Name);

            // SPB spec: DBIRTH values MUST reflect current device state.
            // Query the agent for current observations so subscribers see real values, not stale zeros.
            var currentObs = _agent.GetCurrentObservations(device.Name)?.ToList();
            var metrics = SparkplugBMetricMapper.BirthMetricsForDevice(device, currentObs);
            if (metrics.Count == 0) return;

            try
            {
                await _node.PublishDeviceBirthAsync(device.Name, metrics);
                Log(MTConnectLogLevel.Information, $"Published DBIRTH for {device.Name} ({metrics.Count} metrics)");

                // Resolve born state from current AVAILABILITY so DDATA suppression is
                // correct without waiting for the next AVAILABILITY observation.
                var isAvailable = ResolveBornState(device, currentObs);
                _deviceBorn[device.Name] = isAvailable;
                if (!isAvailable)
                {
                    // Machine is currently offline — publish DDEATH immediately after DBIRTH.
                    await PublishDeviceDeathAsync(device.Name);
                }
            }
            catch (Exception ex)
            {
                Log(MTConnectLogLevel.Warning, $"DBIRTH failed for {device.Name}: {ex.Message}");
                // Suppress DDATA until a successful DBIRTH re-establishes the device.
                _deviceBorn[device.Name] = false;
            }
        }

        // Decides whether a freshly-birthed device may stream DDATA. The MTConnect
        // standard requires a Device to carry AVAILABILITY, so a model without one is
        // a malformed device file rather than a supported configuration — but silently
        // discarding every observation from it is the wrong way to report that.
        // Availability is simply unobservable for such a device, so treat it as
        // available and warn, rather than asserting a machine is dead on no evidence.
        private bool ResolveBornState(IDevice device, IEnumerable<IObservationOutput> currentObservations)
        {
            var state = SparkplugBAvailability.Resolve(device, currentObservations);

            if (state == SparkplugBAvailabilityState.NotDeclared)
            {
                if (_availabilityWarned.Add(device.Name))
                {
                    Log(MTConnectLogLevel.Warning,
                        $"{device.Name} declares no {AvailabilityDataItem.TypeId} DataItem — Sparkplug device state cannot follow the machine; treating it as available so its observations are not discarded");
                }
                return true;
            }

            return state == SparkplugBAvailabilityState.Available;
        }

        private async Task PublishDeviceDeathAsync(string deviceName)
        {
            if (!_isRunning || _node == null) return;
            try
            {
                await _node.PublishDeviceDeathAsync(deviceName);
                Log(MTConnectLogLevel.Information, $"Published DDEATH for {deviceName}");
            }
            catch (Exception ex)
            {
                Log(MTConnectLogLevel.Warning, $"DDEATH failed for {deviceName}: {ex.Message}");
            }
        }

        private static void Append(Dictionary<string, List<SparkplugMetric>> pending, string deviceName, SparkplugMetric metric)
        {
            if (!pending.TryGetValue(deviceName, out var metrics))
            {
                metrics = new List<SparkplugMetric>();
                pending[deviceName] = metrics;
            }
            metrics.Add(metric);
        }

        private async Task FlushAllAsync(Dictionary<string, List<SparkplugMetric>> pending)
        {
            if (pending.Count == 0) return;
            foreach (var deviceName in pending.Keys.ToList())
            {
                await FlushDeviceAsync(deviceName, pending);
            }
        }

        private async Task FlushDeviceAsync(string deviceName, Dictionary<string, List<SparkplugMetric>> pending)
        {
            if (!pending.TryGetValue(deviceName, out var metrics)) return;
            pending.Remove(deviceName);
            if (metrics.Count == 0) return;

            try
            {
                await _node.PublishDeviceDataAsync(deviceName, metrics);
            }
            catch (Exception ex)
            {
                Log(MTConnectLogLevel.Warning, $"DDATA failed for {deviceName} ({metrics.Count} metrics): {ex.Message}");
            }
        }

        // Builds the MQTTnet TLS options from configuration, or null for plaintext.
        // Three trust modes: OS trust store (default), pinned CA file (custom root
        // trust), or allow-untrusted (test rigs). A PKCS#12 client certificate
        // enables mutual TLS.
        private MqttClientTlsOptions BuildTlsOptions()
        {
            if (!_config.UseTls) return null;

            var tls = new MqttClientTlsOptions { UseTls = true };

            if (!string.IsNullOrWhiteSpace(_config.ClientCertificateFile))
            {
                var clientCertificate = new X509Certificate2(
                    ResolvePath(_config.ClientCertificateFile),
                    _config.ClientCertificatePassword);
                tls.ClientCertificatesProvider = new DefaultMqttCertificatesProvider(
                    new X509Certificate2Collection(clientCertificate));
                Log(MTConnectLogLevel.Information, $"SparkplugB TLS: client certificate loaded ({clientCertificate.Subject})");
            }

            if (_config.AllowUntrustedCertificates)
            {
                tls.AllowUntrustedCertificates = true;
                tls.IgnoreCertificateChainErrors = true;
                tls.IgnoreCertificateRevocationErrors = true;
                tls.CertificateValidationHandler = _ => true;
                Log(MTConnectLogLevel.Warning, "SparkplugB TLS: allowUntrustedCertificates enabled — the broker certificate is NOT validated (test use only)");
            }
            else if (!string.IsNullOrWhiteSpace(_config.CaCertificateFile))
            {
                var caCertificate = LoadCaCertificate(ResolvePath(_config.CaCertificateFile));
                tls.CertificateValidationHandler = args => ValidateAgainstCa(args, caCertificate);
                Log(MTConnectLogLevel.Information, $"SparkplugB TLS: broker certificate pinned to CA '{caCertificate.Subject}'");
            }

            return tls;
        }

        // One file per Edge Node identity inside the configured state directory.
        private string BuildBirthDeathSequencePath()
        {
            var identity = Sanitise($"{_config.GroupId}-{_config.NodeId}");
            var fileName = $"{BirthDeathSequencePrefix}-{identity}";

            var directory = string.IsNullOrWhiteSpace(_config.StateDirectory)
                ? DefaultStateDirectory
                : _config.StateDirectory;

            return ResolvePath(Path.Combine(directory, fileName));
        }

        // Sparkplug identifiers are already free of '/', '+' and '#', but nothing stops a Group or
        // Edge Node ID containing characters a filesystem rejects.
        private static string Sanitise(string value)
        {
            var chars = value.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                var safe = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
                           || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-';
                if (!safe) chars[i] = '_';
            }
            return new string(chars);
        }

        private static string ResolvePath(string path) =>
            Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);

        // Accepts PEM ("-----BEGIN CERTIFICATE-----") or binary DER.
        private static X509Certificate2 LoadCaCertificate(string fullPath)
        {
            var bytes = File.ReadAllBytes(fullPath);
            if (bytes.Length > 0 && bytes[0] == (byte)'-')
            {
                return X509Certificate2.CreateFromPem(Encoding.ASCII.GetString(bytes));
            }
            return new X509Certificate2(bytes);
        }

        // Validates the broker certificate against the pinned CA using custom root
        // trust. Hostname mismatches are still rejected — the broker cert's CN/SAN
        // must match the configured server name.
        private static bool ValidateAgainstCa(MqttClientCertificateValidationEventArgs args, X509Certificate2 caCertificate)
        {
            if (args.SslPolicyErrors == SslPolicyErrors.None) return true;
            if ((args.SslPolicyErrors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0) return false;
            if ((args.SslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0) return false;
            if (args.Certificate == null) return false;

            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(caCertificate);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            if (args.Chain != null)
            {
                foreach (var element in args.Chain.ChainElements)
                {
                    chain.ChainPolicy.ExtraStore.Add(element.Certificate);
                }
            }

            using var serverCertificate = new X509Certificate2(args.Certificate);
            return chain.Build(serverCertificate);
        }

        private void Log(MTConnectLogLevel level, string message) =>
            LogReceived?.Invoke(this, level, message, null);
    }
}

// Copyright (c) 2025, All Rights Reserved.

using MTConnect.Agents;
using MTConnect.Configurations;
using MTConnect.Devices;
using MTConnect.Logging;
using MTConnect.Observations;
using SparkplugNet.Core.Enumerations;
using SparkplugNet.Core.Node;
using VersionBNode = SparkplugNet.VersionB.SparkplugNode;
using VersionBData = SparkplugNet.VersionB.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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

        // SPB spec: every NBIRTH MUST include a "Node Control/Rebirth" Boolean metric
        // (value false). SparkplugNet builds NBIRTH from the node's known-metrics list
        // AND filters incoming NCMD metrics against that same list — with an empty
        // list the metric is missing from NBIRTH and a host's Rebirth NCMD is
        // silently stripped before NodeCommandReceived fires.
        private const string NodeControlRebirthName = "Node Control/Rebirth";

        private static List<VersionBData.Metric> BuildNodeKnownMetrics() => new()
        {
            new VersionBData.Metric(NodeControlRebirthName, VersionBData.DataType.Boolean, false, DateTimeOffset.UtcNow),
        };

        private readonly SparkplugBRelayConfiguration _config;
        private readonly IMTConnectAgentBroker _agent;
        private VersionBNode _node;
        private CancellationTokenSource _cts;
        private Channel<WorkItem> _work;
        private Task _processTask;
        private volatile bool _isRunning;

        // UUID → device Name (written by the processing loop, read on the agent's
        // observation dispatch path)
        private readonly ConcurrentDictionary<string, string> _deviceNames = new();

        // device Name → DBIRTH published and device currently available (DDATA allowed).
        // Touched only from the single processing loop — plain dictionary by design.
        private readonly Dictionary<string, bool> _deviceBorn = new();

        private enum WorkKind { Observation, DeviceAdded, PublishAllBirths, NodeRebirth }

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

            _work = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(10000)
            {
                SingleReader = true,
                // A slow or disconnected broker must never block the agent's
                // observation path; prefer losing the oldest queued updates.
                FullMode = BoundedChannelFullMode.DropOldest,
            });

            _node = new VersionBNode(
                BuildNodeKnownMetrics(),
                SparkplugSpecificationVersion.Version30,
                logger: null);

            // SPB spec: on (re)connect publish NBIRTH then DBIRTH for all devices.
            // PublishDeviceBirthAsync re-derives each device's born state from its
            // current AVAILABILITY, so no state clearing is needed here.
            _node.Connected += (args) =>
            {
                Log(MTConnectLogLevel.Information, "SparkplugB node connected — publishing DBIRTH for all devices");
                _work.Writer.TryWrite(WorkItem.ForPublishAllBirths());
                return Task.CompletedTask;
            };

            _node.Disconnected += (args) =>
            {
                Log(MTConnectLogLevel.Information, "SparkplugB broker disconnected — will reconnect automatically");
                return Task.CompletedTask;
            };

            // SPB spec §6.4.3: NCMD with Node Control/Rebirth=true MUST cause the node to
            // republish NBIRTH followed by DBIRTH for every managed device.
            _node.NodeCommandReceived += (args) =>
            {
                if (!args.Metrics.Any(m => string.Equals(m.Name, NodeControlRebirthName, StringComparison.OrdinalIgnoreCase)))
                    return Task.CompletedTask;
                Log(MTConnectLogLevel.Information, "NCMD Rebirth received — republishing NBIRTH and all DBIRTHs");
                _work.Writer.TryWrite(WorkItem.ForNodeRebirth());
                return Task.CompletedTask;
            };

            _processTask = Task.Run(() => ProcessWorkAsync(_cts.Token));

            var options = new SparkplugNodeOptions(
                brokerAddress: _config.Server,
                port: _config.Port,
                clientId: _config.ClientId ?? $"MTConnect-{Guid.NewGuid():N}",
                userName: _config.Username,
                password: _config.Password,
                // null = no primary application required; node publishes NBIRTH immediately.
                // Set to a host ID string only if a Sparkplug primary application is present.
                scadaHostIdentifier: null,
                reconnectInterval: TimeSpan.FromMilliseconds(_config.ReconnectInterval),
                mqttProtocolVersion: null,
                mqttTlsOptions: null,
                mqttWebSocketOptions: null,
                groupIdentifier: _config.GroupId,
                edgeNodeIdentifier: _config.NodeId,
                cancellationToken: _cts.Token);

            await _node.Start(options, null!);
        }

        /// <summary>Stops the processing loop and the edge node; relies on the broker delivering the configured Sparkplug NDEATH will-message.</summary>
        public async Task Stop()
        {
            _isRunning = false;
            _work?.Writer.TryComplete();
            _cts?.Cancel();
            if (_processTask != null)
            {
                try { await _processTask; } catch { }
            }
            if (_node != null) await _node.Stop();
        }

        /// <summary>Handler invoked by the agent when a device is added or rediscovered; queues a DBIRTH for the new device.</summary>
        public Task OnDeviceAdded(IDevice device)
        {
            if (_isRunning && device != null) _work?.Writer.TryWrite(WorkItem.ForDeviceAdded(device));
            return Task.CompletedTask;
        }

        /// <summary>Handler invoked by the agent on every new observation. Non-blocking: the observation is queued and the processing loop batches DDATA per device over a short window; AVAILABILITY observations drive DDEATH / re-DBIRTH transitions.</summary>
        public Task OnObservationAdded(IObservation observation)
        {
            if (_isRunning && observation != null) _work?.Writer.TryWrite(WorkItem.ForObservation(observation));
            return Task.CompletedTask;
        }

        private async Task ProcessWorkAsync(CancellationToken cancellationToken)
        {
            var reader = _work.Reader;
            // deviceName → metrics accumulated during the current batch window
            var pending = new Dictionary<string, List<VersionBData.Metric>>();

            try
            {
                while (await reader.WaitToReadAsync(cancellationToken))
                {
                    try { await Task.Delay(BatchWindowMs, cancellationToken); }
                    catch (OperationCanceledException) { }

                    while (reader.TryRead(out var item))
                    {
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
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log(MTConnectLogLevel.Error, $"Relay processing loop stopped unexpectedly: {ex.Message}");
            }
        }

        private async Task HandleWorkItemAsync(WorkItem item, Dictionary<string, List<VersionBData.Metric>> pending)
        {
            switch (item.Kind)
            {
                case WorkKind.Observation:
                    if (!_deviceNames.TryGetValue(item.Observation.DeviceUuid, out var deviceName)) return;

                    if (item.Observation.Type == "AVAILABILITY")
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
                    foreach (var device in _agent.GetDevices() ?? Enumerable.Empty<IDevice>())
                        await PublishDeviceBirthAsync(device, pending);
                    return;

                case WorkKind.NodeRebirth:
                    // Rebirth REPLACES the node's known-metrics storage — pass the same
                    // list as at startup or the Node Control/Rebirth metric disappears
                    // from the new NBIRTH and later NCMD rebirths get filtered out.
                    await _node.Rebirth(BuildNodeKnownMetrics());
                    foreach (var device in _agent.GetDevices() ?? Enumerable.Empty<IDevice>())
                        await PublishDeviceBirthAsync(device, pending);
                    return;
            }
        }

        private async Task HandleAvailabilityChangeAsync(string deviceName, IObservation observation, Dictionary<string, List<VersionBData.Metric>> pending)
        {
            var raw = observation.IsUnavailable ? null : observation.GetValue(ValueKeys.Result);
            var isAvailable = string.Equals(raw, "AVAILABLE", StringComparison.OrdinalIgnoreCase);

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

        private async Task PublishDeviceBirthAsync(IDevice device, Dictionary<string, List<VersionBData.Metric>> pending)
        {
            if (!_isRunning || _node == null || device == null) return;

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
                await _node.PublishDeviceBirthMessage(metrics, device.Name, logger: null);
                Log(MTConnectLogLevel.Information, $"Published DBIRTH for {device.Name} ({metrics.Count} metrics)");

                // Resolve born state from current AVAILABILITY so DDATA suppression is
                // correct without waiting for the next AVAILABILITY observation.
                var availObs = currentObs?.FirstOrDefault(o => o.Type == "AVAILABILITY");
                var isAvailable = string.Equals(
                    availObs?.GetValue(ValueKeys.Result), "AVAILABLE", StringComparison.OrdinalIgnoreCase);
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

        private async Task PublishDeviceDeathAsync(string deviceName)
        {
            if (!_isRunning || _node == null) return;
            try
            {
                await _node.PublishDeviceDeathMessage(deviceName);
                Log(MTConnectLogLevel.Information, $"Published DDEATH for {deviceName}");
            }
            catch (Exception ex)
            {
                Log(MTConnectLogLevel.Warning, $"DDEATH failed for {deviceName}: {ex.Message}");
            }
        }

        private static void Append(Dictionary<string, List<VersionBData.Metric>> pending, string deviceName, VersionBData.Metric metric)
        {
            if (!pending.TryGetValue(deviceName, out var metrics))
            {
                metrics = new List<VersionBData.Metric>();
                pending[deviceName] = metrics;
            }
            metrics.Add(metric);
        }

        private async Task FlushAllAsync(Dictionary<string, List<VersionBData.Metric>> pending)
        {
            if (pending.Count == 0) return;
            foreach (var deviceName in pending.Keys.ToList())
            {
                await FlushDeviceAsync(deviceName, pending);
            }
        }

        private async Task FlushDeviceAsync(string deviceName, Dictionary<string, List<VersionBData.Metric>> pending)
        {
            if (!pending.TryGetValue(deviceName, out var metrics)) return;
            pending.Remove(deviceName);
            if (metrics.Count == 0) return;

            try
            {
                await _node.PublishDeviceData(metrics, deviceName);
            }
            catch (Exception ex)
            {
                Log(MTConnectLogLevel.Warning, $"DDATA failed for {deviceName} ({metrics.Count} metrics): {ex.Message}");
            }
        }

        private void Log(MTConnectLogLevel level, string message) =>
            LogReceived?.Invoke(this, level, message, null);
    }
}

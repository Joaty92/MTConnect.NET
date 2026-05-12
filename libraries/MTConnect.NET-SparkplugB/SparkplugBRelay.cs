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
using System.Threading.Tasks;

namespace MTConnect.SparkplugB
{
    /// <summary>Bridges an <see cref="IMTConnectAgentBroker"/> to a Sparkplug B edge node — publishes NBIRTH/DBIRTH on connect, DDATA on observation changes, and DDEATH/re-DBIRTH driven by AVAILABILITY transitions per the Sparkplug B specification.</summary>
    public class SparkplugBRelay
    {
        // Per SPB spec: DBIRTH published → Initial; AVAILABILITY=AVAILABLE → Connected;
        // AVAILABILITY=UNAVAILABLE → Disconnected (DDEATH sent); back to Connected on DBIRTH re-publish.
        private enum DeviceState { Initial, Connected, Disconnected }

        private readonly SparkplugBRelayConfiguration _config;
        private readonly IMTConnectAgentBroker _agent;
        private VersionBNode _node;
        private CancellationTokenSource _cts;
        private volatile bool _isRunning;

        // UUID → device Name (populated on DeviceAdded / Connected event)
        private readonly ConcurrentDictionary<string, string> _deviceNames = new();
        // device Name → lifecycle state — drives DDEATH / re-DBIRTH decisions
        private readonly ConcurrentDictionary<string, DeviceState> _deviceStates = new();

        /// <summary>Raised for relay diagnostic messages (connection state, publish failures, lifecycle events).</summary>
        public event MTConnectLogEventHandler LogReceived;

        /// <summary>Creates a relay bound to <paramref name="config"/> and the supplied <paramref name="agent"/>; call <see cref="Start"/> to connect to the broker.</summary>
        public SparkplugBRelay(SparkplugBRelayConfiguration config, IMTConnectAgentBroker agent)
        {
            _config = config;
            _agent = agent;
        }

        /// <summary>Connects the Sparkplug edge node to the configured MQTT broker and wires up Connected / Disconnected / NCMD-Rebirth handlers.</summary>
        public async Task Start()
        {
            _cts = new CancellationTokenSource();
            _isRunning = true;

            _node = new VersionBNode(
                new List<VersionBData.Metric>(),
                SparkplugSpecificationVersion.Version30,
                logger: null);

            // SPB spec: on (re)connect publish NBIRTH then DBIRTH for all devices.
            // State is cleared so the AVAILABILITY observations re-drive the Connected transitions.
            _node.Connected += async (args) =>
            {
                Log(MTConnectLogLevel.Information, "SparkplugB node connected — publishing DBIRTH for all devices");
                _deviceStates.Clear();
                foreach (var device in _agent.GetDevices())
                    await PublishDeviceBirth(device);
            };

            _node.Disconnected += (args) =>
            {
                Log(MTConnectLogLevel.Information, "SparkplugB broker disconnected — will reconnect automatically");
                return Task.CompletedTask;
            };

            // SPB spec §6.4.3: NCMD with Node Control/Rebirth=true MUST cause the node to
            // republish NBIRTH followed by DBIRTH for every managed device.
            _node.NodeCommandReceived += async (args) =>
            {
                if (!args.Metrics.Any(m => string.Equals(m.Name, "Node Control/Rebirth", StringComparison.OrdinalIgnoreCase)))
                    return;
                Log(MTConnectLogLevel.Information, "NCMD Rebirth received — republishing NBIRTH and all DBIRTHs");
                _deviceStates.Clear();
                await _node.Rebirth(new List<VersionBData.Metric>());
                foreach (var device in _agent.GetDevices())
                    await PublishDeviceBirth(device);
            };

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

        /// <summary>Stops the edge node and cancels the reconnect loop; relies on the broker delivering the configured Sparkplug NDEATH will-message.</summary>
        public async Task Stop()
        {
            _isRunning = false;
            _cts?.Cancel();
            if (_node != null) await _node.Stop();
        }

        /// <summary>Handler invoked by the agent when a device is added or rediscovered; publishes DBIRTH for the new device.</summary>
        public async Task OnDeviceAdded(IDevice device)
        {
            if (!_isRunning) return;
            _deviceNames[device.Uuid] = device.Name;
            await PublishDeviceBirth(device);
        }

        /// <summary>Handler invoked by the agent on every new observation; AVAILABILITY observations drive Connected/Disconnected transitions, all others are published as DDATA when the device is in Connected state.</summary>
        public async Task OnObservationAdded(IObservation observation)
        {
            if (!_isRunning || _node == null) return;
            if (!_deviceNames.TryGetValue(observation.DeviceUuid, out var deviceName)) return;

            if (observation.Type == "AVAILABILITY")
            {
                await HandleAvailabilityChange(deviceName, observation);
                return;
            }

            // SPB spec: DDATA must not be published for a device after DDEATH until DBIRTH re-establishes it.
            // Default to Disconnected for unknown devices to prevent DDATA before first DBIRTH.
            if (_deviceStates.GetValueOrDefault(deviceName, DeviceState.Disconnected) == DeviceState.Disconnected)
                return;

            var metric = SparkplugBMetricMapper.ToMetric(observation);
            try
            {
                await _node.PublishDeviceData(new List<VersionBData.Metric> { metric }, deviceName);
            }
            catch (Exception ex)
            {
                Log(MTConnectLogLevel.Warning, $"DDATA failed for {deviceName}: {ex.Message}");
            }
        }

        private async Task HandleAvailabilityChange(string deviceName, IObservation observation)
        {
            var raw = observation.IsUnavailable ? null : observation.GetValue(ValueKeys.Result);
            var isAvailable = string.Equals(raw, "AVAILABLE", StringComparison.OrdinalIgnoreCase);

            // Ensure the device has a state entry so TryUpdate works (ConcurrentDictionary
            // TryUpdate returns false if the key doesn't exist).
            _deviceStates.GetOrAdd(deviceName, DeviceState.Initial);

            if (!isAvailable)
            {
                // Atomic CAS: only the first thread transitioning from non-Disconnected wins.
                // This prevents duplicate DDEATHs if two UNAVAILABLE observations race.
                if (_deviceStates.TryUpdate(deviceName, DeviceState.Disconnected, DeviceState.Connected) ||
                    _deviceStates.TryUpdate(deviceName, DeviceState.Disconnected, DeviceState.Initial))
                {
                    await PublishDeviceDeath(deviceName);
                }
                // Regardless of whether we won the CAS, don't send DDATA for this observation.
                return;
            }

            // Machine came online.
            if (_deviceStates.TryUpdate(deviceName, DeviceState.Connected, DeviceState.Disconnected))
            {
                // Was disconnected (DDEATH was sent) — must re-publish DBIRTH before DDATA can flow.
                var device = _agent.GetDevices()?.FirstOrDefault(d => d.Name == deviceName);
                if (device != null)
                {
                    await PublishDeviceBirth(device);
                }
                else
                {
                    // Device no longer known — revert to Disconnected to suppress DDATA.
                    _deviceStates[deviceName] = DeviceState.Disconnected;
                }
                // If DBIRTH failed (or device not found), state is Disconnected — suppress DDATA.
                if (_deviceStates.GetValueOrDefault(deviceName, DeviceState.Disconnected) == DeviceState.Disconnected)
                    return;
            }
            else
            {
                // Initial → Connected: DBIRTH was already published at startup with current values.
                _deviceStates.TryUpdate(deviceName, DeviceState.Connected, DeviceState.Initial);
            }

            // Publish the AVAILABILITY change as DDATA so subscribers see AVAILABLE immediately.
            var metric = SparkplugBMetricMapper.ToMetric(observation);
            try
            {
                await _node.PublishDeviceData(new List<VersionBData.Metric> { metric }, deviceName);
            }
            catch (Exception ex)
            {
                Log(MTConnectLogLevel.Warning, $"DDATA (AVAILABILITY) failed for {deviceName}: {ex.Message}");
            }
        }

        private async Task PublishDeviceBirth(IDevice device)
        {
            if (!_isRunning || _node == null) return;

            _deviceNames[device.Uuid] = device.Name;

            // SPB spec: DBIRTH values MUST reflect current device state.
            // Query the agent for current observations so subscribers see real values, not stale zeros.
            var currentObs = _agent.GetCurrentObservations(device.Name)?.ToList();
            var metrics = SparkplugBMetricMapper.BirthMetricsForDevice(device, currentObs);
            if (metrics.Count == 0) return;

            try
            {
                await _node.PublishDeviceBirthMessage(metrics, device.Name, logger: null);
                Log(MTConnectLogLevel.Information, $"Published DBIRTH for {device.Name} ({metrics.Count} metrics)");
                // Mark as Initial only if no state entry exists yet — preserves Connected/Disconnected
                // state when re-DBIRTH is triggered from the reconnect path.
                _deviceStates.TryAdd(device.Name, DeviceState.Initial);

                // Immediately resolve state from current AVAILABILITY so DDATA suppression is correct
                // without waiting for the next AVAILABILITY observation to arrive.
                var availObs = currentObs?.FirstOrDefault(o => o.Type == "AVAILABILITY");
                var isAvailable = string.Equals(
                    availObs?.GetValue(ValueKeys.Result), "AVAILABLE", StringComparison.OrdinalIgnoreCase);
                if (isAvailable)
                {
                    _deviceStates.TryUpdate(device.Name, DeviceState.Connected, DeviceState.Initial);
                }
                else
                {
                    // Machine is currently offline — publish DDEATH immediately after DBIRTH.
                    if (_deviceStates.TryUpdate(device.Name, DeviceState.Disconnected, DeviceState.Initial))
                        await PublishDeviceDeath(device.Name);
                }
            }
            catch (Exception ex)
            {
                Log(MTConnectLogLevel.Warning, $"DBIRTH failed for {device.Name}: {ex.Message}");
                // On failure, set state to Disconnected so DDATA is suppressed
                // until a successful DBIRTH re-establishes the device.
                _deviceStates[device.Name] = DeviceState.Disconnected;
            }
        }

        private async Task PublishDeviceDeath(string deviceName)
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

        private void Log(MTConnectLogLevel level, string message) =>
            LogReceived?.Invoke(this, level, message, null);
    }
}

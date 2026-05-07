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
using System.Threading;
using System.Threading.Tasks;

namespace MTConnect.SparkplugB
{
    public class SparkplugBRelay
    {
        private readonly SparkplugBRelayConfiguration _config;
        private readonly IMTConnectAgentBroker _agent;
        private VersionBNode _node;
        private CancellationTokenSource _cts;
        private bool _isRunning;

        // UUID → device Name populated as devices are added/discovered
        private readonly ConcurrentDictionary<string, string> _deviceNames = new();

        public event MTConnectLogEventHandler LogReceived;

        public SparkplugBRelay(SparkplugBRelayConfiguration config, IMTConnectAgentBroker agent)
        {
            _config = config;
            _agent = agent;
        }

        public async Task Start()
        {
            _cts = new CancellationTokenSource();
            _isRunning = true;

            _node = new VersionBNode(
                new List<VersionBData.Metric>(),
                SparkplugSpecificationVersion.Version30,
                logger: null);

            _node.Connected += async (args) =>
            {
                Log(MTConnectLogLevel.Information, "SparkplugB node connected — publishing DBIRTH for all devices");
                foreach (var device in _agent.GetDevices())
                    await PublishDeviceBirth(device);
            };

            var options = new SparkplugNodeOptions(
                brokerAddress: _config.Server,
                port: _config.Port,
                clientId: _config.ClientId ?? $"MTConnect-{Guid.NewGuid():N}",
                userName: _config.Username,
                password: _config.Password,
                scadaHostIdentifier: "STATE",
                reconnectInterval: TimeSpan.FromMilliseconds(_config.ReconnectInterval),
                mqttProtocolVersion: null,
                mqttTlsOptions: null,
                mqttWebSocketOptions: null,
                groupIdentifier: _config.GroupId,
                edgeNodeIdentifier: _config.NodeId,
                cancellationToken: _cts.Token);

            await _node.Start(options, null!);
        }

        public async Task Stop()
        {
            _isRunning = false;
            _cts?.Cancel();
            if (_node != null) await _node.Stop();
        }

        public async Task OnDeviceAdded(IDevice device)
        {
            if (!_isRunning) return;
            _deviceNames[device.Uuid] = device.Name;
            await PublishDeviceBirth(device);
        }

        public async Task OnObservationAdded(IObservation observation)
        {
            if (!_isRunning) return;
            if (!_deviceNames.TryGetValue(observation.DeviceUuid, out var deviceName)) return;

            var metric = SparkplugBMetricMapper.ToMetric(observation);
            try
            {
                await _node.PublishDeviceData(new List<VersionBData.Metric> { metric }, deviceName);
            }
            catch (Exception ex)
            {
                Log(MTConnectLogLevel.Warning, $"Failed to publish DDATA for {deviceName}: {ex.Message}");
            }
        }

        private async Task PublishDeviceBirth(IDevice device)
        {
            if (_node == null) return;

            _deviceNames[device.Uuid] = device.Name;
            var metrics = SparkplugBMetricMapper.BirthMetricsForDevice(device);
            if (metrics.Count == 0) return;

            try
            {
                await _node.PublishDeviceBirthMessage(metrics, device.Name, logger: null);
                Log(MTConnectLogLevel.Information, $"Published DBIRTH for {device.Name} ({metrics.Count} metrics)");
            }
            catch (Exception ex)
            {
                Log(MTConnectLogLevel.Warning, $"Failed to publish DBIRTH for {device.Name}: {ex.Message}");
            }
        }

        private void Log(MTConnectLogLevel level, string message) =>
            LogReceived?.Invoke(this, level, message, null);
    }
}

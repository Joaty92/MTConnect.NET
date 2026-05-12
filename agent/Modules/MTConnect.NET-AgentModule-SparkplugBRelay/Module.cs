// Copyright (c) 2025, All Rights Reserved.

using MTConnect.Agents;
using MTConnect.Configurations;
using MTConnect.Devices;
using MTConnect.Logging;
using MTConnect.Observations;
using MTConnect.SparkplugB;
using System;

namespace MTConnect
{
    /// <summary>Agent module that hosts a <see cref="SparkplugBRelay"/>; activated by the <c>sparkplug-relay</c> configuration section in <c>agent.config.yaml</c>.</summary>
    public class Module : MTConnectAgentModule
    {
        /// <summary>YAML key under <c>modules:</c> that selects this module's configuration section.</summary>
        public const string ConfigurationTypeId = "sparkplug-relay";
        private const string ModuleId = "SparkplugB Relay";

        private readonly SparkplugBRelayModuleConfiguration _configuration;
        private SparkplugBRelay _relay;

        /// <summary>Constructed by the agent's module loader; <paramref name="configuration"/> is the raw YAML section bound to <see cref="SparkplugBRelayModuleConfiguration"/>.</summary>
        public Module(IMTConnectAgentBroker agent, object configuration) : base(agent)
        {
            Id = ModuleId;
            _configuration = AgentApplicationConfiguration.GetConfiguration<SparkplugBRelayModuleConfiguration>(configuration);
        }

        /// <summary>Wires up agent event handlers and starts the underlying Sparkplug relay; invoked by the host once devices have been loaded.</summary>
        protected override void OnStartAfterLoad(bool initializeDataItems)
        {
            if (_configuration == null) return;

            _relay = new SparkplugBRelay(_configuration, Agent);
            _relay.LogReceived += (sender, level, message, logId) => Log(level, message);

            Agent.DeviceAdded += AgentDeviceAdded;
            Agent.ObservationAdded += AgentObservationAdded;

            _ = _relay.Start().ContinueWith(
                t => Log(MTConnectLogLevel.Error, $"SparkplugB relay failed to start: {t.Exception?.GetBaseException()?.Message}"),
                System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>Detaches agent event handlers and stops the underlying Sparkplug relay; invoked by the host on shutdown.</summary>
        protected override void OnStop()
        {
            Agent.DeviceAdded -= AgentDeviceAdded;
            Agent.ObservationAdded -= AgentObservationAdded;

            if (_relay != null) _ = _relay.Stop();
        }

        private async void AgentDeviceAdded(object sender, IDevice device)
        {
            try
            {
                if (_relay != null) await _relay.OnDeviceAdded(device);
            }
            catch (Exception ex)
            {
                Log(MTConnectLogLevel.Warning, $"DeviceAdded handler error: {ex.Message}");
            }
        }

        private async void AgentObservationAdded(object sender, IObservation observation)
        {
            try
            {
                if (_relay != null) await _relay.OnObservationAdded(observation);
            }
            catch (Exception ex)
            {
                Log(MTConnectLogLevel.Warning, $"ObservationAdded handler error: {ex.Message}");
            }
        }
    }
}

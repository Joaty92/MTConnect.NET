// Copyright (c) 2025, All Rights Reserved.

using MTConnect.Agents;
using MTConnect.Configurations;
using MTConnect.Devices;
using MTConnect.Logging;
using MTConnect.Observations;
using MTConnect.SparkplugB;

namespace MTConnect
{
    public class Module : MTConnectAgentModule
    {
        public const string ConfigurationTypeId = "sparkplug-relay";
        private const string ModuleId = "SparkplugB Relay";

        private readonly SparkplugBRelayModuleConfiguration _configuration;
        private SparkplugBRelay _relay;

        public Module(IMTConnectAgentBroker agent, object configuration) : base(agent)
        {
            Id = ModuleId;
            _configuration = AgentApplicationConfiguration.GetConfiguration<SparkplugBRelayModuleConfiguration>(configuration);
        }

        protected override void OnStartAfterLoad(bool initializeDataItems)
        {
            if (_configuration == null) return;

            _relay = new SparkplugBRelay(_configuration, Agent);
            _relay.LogReceived += (sender, level, message, logId) => Log(level, message);

            Agent.DeviceAdded += AgentDeviceAdded;
            Agent.ObservationAdded += AgentObservationAdded;

            _ = _relay.Start();
        }

        protected override void OnStop()
        {
            Agent.DeviceAdded -= AgentDeviceAdded;
            Agent.ObservationAdded -= AgentObservationAdded;

            if (_relay != null) _ = _relay.Stop();
        }

        private async void AgentDeviceAdded(object sender, IDevice device)
        {
            if (_relay != null) await _relay.OnDeviceAdded(device);
        }

        private async void AgentObservationAdded(object sender, IObservation observation)
        {
            if (_relay != null) await _relay.OnObservationAdded(observation);
        }
    }
}

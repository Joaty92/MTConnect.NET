// Copyright (c) 2023 TrakHound Inc. + 2025 fork contributors, All Rights Reserved.
// Original TrakHound MTConnect.NET-Haas (MIT). Fork rewrites the Module to
// match the current MTConnect.NET agent-module API (OnStartAfterLoad(bool))
// and to wire the YAML-driven HaasDataSource into the agent buffer via
// AddObservation(deviceKey, dataItemKey, value, timestamp).

using MTConnect.Agents;
using MTConnect.Configurations;
using MTConnect.Haas;
using MTConnect.Logging;
using System;

namespace MTConnect
{
    /// <summary>Agent module that hosts a <see cref="HaasEthernetDataSource"/>; activated by the <c>haas-ethernet</c> configuration section in <c>agent.config.yaml</c>. One block per machine. Observations flow into the agent's shared buffer via <c>IMTConnectAgentBroker.AddObservation(...)</c>, then out to whatever relays are configured (MQTT, SparkplugB, HTTP).</summary>
    public class Module : MTConnectAgentModule
    {
        /// <summary>YAML key under <c>modules:</c> that selects this module's configuration section.</summary>
        public const string ConfigurationTypeId = "haas-ethernet";
        private const string ModuleId = "Haas Ethernet Adapter";

        private readonly HaasEthernetAdapterModuleConfiguration _configuration;
        private HaasEthernetDataSource _dataSource;

        /// <summary>Constructed by the agent's module loader; <paramref name="configuration"/> is the raw YAML section bound to <see cref="HaasEthernetAdapterModuleConfiguration"/>.</summary>
        public Module(IMTConnectAgentBroker agent, object configuration) : base(agent)
        {
            Id = ModuleId;
            _configuration = AgentApplicationConfiguration.GetConfiguration<HaasEthernetAdapterModuleConfiguration>(configuration);
        }

        /// <summary>Starts the polling engine. Values arriving via <see cref="HaasDataSource.ObservationAdded"/> are routed to <c>IMTConnectAgentBroker.AddObservation(deviceKey, dataItemKey, value, timestamp)</c> under the configured <c>deviceKey</c>.</summary>
        protected override void OnStartAfterLoad(bool initializeDataItems)
        {
            if (_configuration == null) return;
            if (string.IsNullOrWhiteSpace(_configuration.DeviceKey))
            {
                Log(MTConnectLogLevel.Warning, "Haas Ethernet adapter: deviceKey missing; skipping module start");
                return;
            }
            if (string.IsNullOrWhiteSpace(_configuration.Server))
            {
                Log(MTConnectLogLevel.Warning, $"Haas Ethernet adapter ({_configuration.DeviceKey}): server missing; skipping module start");
                return;
            }

            _dataSource = new HaasEthernetDataSource(_configuration);
            _dataSource.ObservationAdded += OnObservation;
            _dataSource.LogReceived += OnDataSourceLog;
            _dataSource.Start();
            Log(MTConnectLogLevel.Information,
                $"Haas Ethernet adapter ({_configuration.DeviceKey}): started polling {_configuration.Server}:{_configuration.Port} every {_configuration.PollInterval}s");
        }

        /// <summary>Stops the polling engine.</summary>
        protected override void OnStop()
        {
            if (_dataSource != null) _dataSource.Stop();
        }

        private void OnObservation(object sender, (string Key, string Value) evt)
        {
            Agent.AddObservation(_configuration.DeviceKey, evt.Key, (object)evt.Value, DateTime.UtcNow);
        }

        private void OnDataSourceLog(object sender, (int LogLevel, string Message) evt)
        {
            // Map int level to MTConnectLogLevel; loose mapping — anything >=2 is Warning.
            var mapped = evt.LogLevel switch
            {
                <= 0 => MTConnectLogLevel.Debug,
                1 => MTConnectLogLevel.Information,
                _ => MTConnectLogLevel.Warning,
            };
            Log(mapped, $"Haas ({_configuration.DeviceKey}): {evt.Message}");
        }
    }
}

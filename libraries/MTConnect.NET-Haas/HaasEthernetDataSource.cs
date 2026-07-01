// Copyright (c) 2023 TrakHound Inc. + 2025 fork contributors, All Rights Reserved.
// Original TrakHound MTConnect.NET-Haas (MIT).

namespace MTConnect.Haas
{
    /// <summary>TCP transport for <see cref="HaasDataSource"/>. Wraps <see cref="Ethernet"/> for a single Moxa NPort endpoint (or a direct-Ethernet Haas HMI).</summary>
    public class HaasEthernetDataSource : HaasDataSource
    {
        private readonly Ethernet _ethernet;

        /// <summary>Builds the polling engine bound to the endpoint declared in <paramref name="configuration"/>. User-added Commands and MacroVariables from the config are appended to <see cref="HaasDefaults.Commands"/> / <see cref="HaasDefaults.MacroVariables"/>.</summary>
        public HaasEthernetDataSource(HaasEthernetConfiguration configuration)
            : base(configuration.Commands, configuration.MacroVariables, configuration.PollInterval)
        {
            _ethernet = new Ethernet(configuration.Server, configuration.Port);
        }

        /// <inheritdoc/>
        protected override void OnStart()
        {
            if (_ethernet != null) _ethernet.Connect();
        }

        /// <inheritdoc/>
        protected override void OnStop()
        {
            if (_ethernet != null) _ethernet.Close();
        }

        /// <inheritdoc/>
        protected override string SendCommand(string command) => _ethernet?.SendCommand(command);
    }
}

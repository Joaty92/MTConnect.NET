// Copyright (c) 2023 TrakHound Inc. + 2025 fork contributors, All Rights Reserved.
// Original TrakHound MTConnect.NET-Haas (MIT).

namespace MTConnect.Haas
{
    /// <summary>RS-232 transport for <see cref="HaasDataSource"/>. Opens the COM port per command via the static <see cref="Serial.SendCommand(string, string)"/> helper — not suitable for high-frequency polling.</summary>
    public class HaasSerialDataSource : HaasDataSource
    {
        private readonly HaasSerialConfiguration _configuration;

        /// <summary>Builds the polling engine bound to the COM port declared in <paramref name="configuration"/>. User-added Commands and MacroVariables from the config are appended to <see cref="HaasDefaults.Commands"/> / <see cref="HaasDefaults.MacroVariables"/>.</summary>
        public HaasSerialDataSource(HaasSerialConfiguration configuration)
            : base(configuration.Commands, configuration.MacroVariables, configuration.PollInterval)
        {
            _configuration = configuration;
        }

        /// <inheritdoc/>
        protected override string SendCommand(string command)
        {
            var responses = Serial.SendCommand(command, _configuration.COMPort);
            if (responses != null && responses.Length > 1)
            {
                return responses[1];
            }
            return null;
        }
    }
}

// Copyright (c) 2023 TrakHound Inc. + 2025 fork contributors, All Rights Reserved.
// Original TrakHound MTConnect.NET-Haas (MIT). Fork extends the POCO with
// declarative Q-code and macro-variable lists.

using System.Collections.Generic;

namespace MTConnect.Haas
{
    /// <summary>YAML-deserialised configuration for one Haas machine reached over TCP (typically a Moxa NPort serial-to-Ethernet converter fronting the HMI's RS-232 port).</summary>
    public class HaasEthernetConfiguration
    {
        /// <summary>Hostname or IP address of the endpoint (Moxa, direct-Ethernet Haas, or similar).</summary>
        public string Server { get; set; }

        /// <summary>TCP port on the endpoint. Moxa NPort defaults: <c>4001</c> for port 1, <c>4002</c> for port 2, etc.</summary>
        public int Port { get; set; }

        /// <summary>Seconds between polling cycles across all commands and macro variables. Defaults to 1.0.</summary>
        public double PollInterval { get; set; } = 1.0;

        /// <summary>User-added Q-code commands beyond the <see cref="HaasDefaults.Commands"/> built-ins. Appended, not replacing — both sets are polled.</summary>
        public List<HaasCommand> Commands { get; set; } = new List<HaasCommand>();

        /// <summary>User-added macro variables beyond the <see cref="HaasDefaults.MacroVariables"/> built-ins. Appended, not replacing — both sets are polled.</summary>
        public List<HaasMacroVariable> MacroVariables { get; set; } = new List<HaasMacroVariable>();
    }
}

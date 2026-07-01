// Copyright (c) 2023 TrakHound Inc. + 2025 fork contributors, All Rights Reserved.
// Original TrakHound MTConnect.NET-Haas (MIT). Fork extends the POCO with
// declarative Q-code and macro-variable lists.

using System.Collections.Generic;

namespace MTConnect.Haas
{
    /// <summary>YAML-deserialised configuration for one Haas machine reached over RS-232 (via a local COM port on the host).</summary>
    public class HaasSerialConfiguration
    {
        /// <summary>COM port name, e.g. <c>COM3</c> on Windows or <c>/dev/ttyS0</c> on Linux.</summary>
        public string COMPort { get; set; }

        /// <summary>Seconds between polling cycles across all commands and macro variables. Defaults to 1.0.</summary>
        public double PollInterval { get; set; } = 1.0;

        /// <summary>User-added Q-code commands beyond the <see cref="HaasDefaults.Commands"/> built-ins. Appended, not replacing.</summary>
        public List<HaasCommand> Commands { get; set; } = new List<HaasCommand>();

        /// <summary>User-added macro variables beyond the <see cref="HaasDefaults.MacroVariables"/> built-ins. Appended, not replacing.</summary>
        public List<HaasMacroVariable> MacroVariables { get; set; } = new List<HaasMacroVariable>();
    }
}

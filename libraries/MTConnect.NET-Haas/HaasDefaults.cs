// Copyright (c) 2025 MTConnect.NET fork contributors, All Rights Reserved.
// Licensed to you under the MIT license; see LICENSE.md in the repository root.

using System.Collections.Generic;

namespace MTConnect.Haas
{
    /// <summary>Built-in Q-code commands and macro variables — polled for every Haas machine regardless of user config. User-declared entries in <c>agent.config.yaml</c> are treated as additions to these defaults, not replacements. Ported from the HAAS-MQTT Python poller's DEFAULT_COMMANDS / DEFAULT_MACRO_VARIABLES tables.</summary>
    public static class HaasDefaults
    {
        /// <summary>11 built-in Q-code commands covering machine identity (Q100/101/102), controller mode (Q104), uptime/cutting-time counters (Q300/301), cycle-time telemetry (Q303/304), M30 part counters (Q402/403), and program status (Q500).</summary>
        public static IReadOnlyList<HaasCommand> Commands { get; } = new List<HaasCommand>
        {
            new HaasCommand { Name = "serial_number",    Command = "?Q100", PollOnce = true },
            new HaasCommand { Name = "software_version", Command = "?Q101", PollOnce = true },
            new HaasCommand { Name = "model",            Command = "?Q102", PollOnce = true },
            new HaasCommand { Name = "mode",             Command = "?Q104" },
            new HaasCommand { Name = "power_on_time",    Command = "?Q300" },
            new HaasCommand { Name = "cutting_time",     Command = "?Q301" },
            new HaasCommand { Name = "last_cycle_time",  Command = "?Q303" },
            new HaasCommand { Name = "prev_cycle_time",  Command = "?Q304" },
            new HaasCommand { Name = "m30_counter_1",    Command = "?Q402" },
            new HaasCommand { Name = "m30_counter_2",    Command = "?Q403" },
            new HaasCommand { Name = "program_status",   Command = "?Q500" },
        };

        /// <summary>8 built-in macro variables covering axis work positions (5041-5043), axis machine positions (5021-5023), single-block mode (3030), and the last M-code executed (4113).</summary>
        public static IReadOnlyList<HaasMacroVariable> MacroVariables { get; } = new List<HaasMacroVariable>
        {
            new HaasMacroVariable { Name = "x_pos",         Variable = 5041 },
            new HaasMacroVariable { Name = "y_pos",         Variable = 5042 },
            new HaasMacroVariable { Name = "z_pos",         Variable = 5043 },
            new HaasMacroVariable { Name = "x_machine_pos", Variable = 5021 },
            new HaasMacroVariable { Name = "y_machine_pos", Variable = 5022 },
            new HaasMacroVariable { Name = "z_machine_pos", Variable = 5023 },
            new HaasMacroVariable { Name = "single_block",  Variable = 3030 },
            new HaasMacroVariable { Name = "last_m_code",   Variable = 4113 },
        };
    }
}

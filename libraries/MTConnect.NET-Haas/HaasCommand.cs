// Copyright (c) 2025 MTConnect.NET fork contributors, All Rights Reserved.
// Licensed to you under the MIT license; see LICENSE.md in the repository root.

namespace MTConnect.Haas
{
    /// <summary>One Q-code command definition — from either the built-in defaults or the user's YAML. Poll semantics: every cycle by default, throttled by <see cref="PollEvery"/> seconds if set, or one-shot per connection session if <see cref="PollOnce"/> is true.</summary>
    public class HaasCommand
    {
        /// <summary>Data-item key used when publishing observations for this command (also the MTConnect DataItem name in the auto-generated device model).</summary>
        public string Name { get; set; }

        /// <summary>Raw Q-code string sent over the wire, e.g. <c>?Q100</c> or <c>?Q500</c>.</summary>
        public string Command { get; set; }

        /// <summary>Skip after the first successful reply of a connection session; re-poll automatically after any reconnect. Use for values that never change during a session (serial number, software version, model).</summary>
        public bool PollOnce { get; set; }

        /// <summary>Minimum seconds between polls. Null = every cycle. Use for values that change infrequently (uptime counters, part counters) to reduce Q-code traffic.</summary>
        public int? PollEvery { get; set; }
    }
}

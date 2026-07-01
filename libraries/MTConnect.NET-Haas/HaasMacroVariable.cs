// Copyright (c) 2025, All Rights Reserved.

namespace MTConnect.Haas
{
    /// <summary>One macro-variable definition — from either the built-in defaults or the user's YAML. Polled via <c>?Q600 &lt;variable&gt;</c>; the numeric value from the <c>&gt;MACRO, &lt;value&gt;</c> response is published under <see cref="Name"/>.</summary>
    public class HaasMacroVariable
    {
        /// <summary>Data-item key used when publishing observations (also the MTConnect DataItem name in the auto-generated device model).</summary>
        public string Name { get; set; }

        /// <summary>Haas macro register number (e.g. 5041 = X axis actual position, 3027 = spindle speed).</summary>
        public int Variable { get; set; }

        /// <summary>Minimum seconds between polls. Null = every cycle. Use for values that change infrequently to reduce Q-code traffic.</summary>
        public int? PollEvery { get; set; }
    }
}

// Copyright (c) 2023 TrakHound Inc. + 2025 fork contributors, All Rights Reserved.
// Licensed to you under the MIT license; see LICENSE.md in the repository root.
// Regex parsing patterns adapted from the original TrakHound MTConnect.NET-Haas
// (MIT), extracted into a dedicated static class so the polling loop stays
// declarative.

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MTConnect.Haas
{
    /// <summary>Parses raw Q-code responses into structured observations. Every parser returns a list of (dataItemKey, value) pairs so the polling loop can emit them uniformly. Unknown responses fall through to a raw-string publish under the command's own name.</summary>
    public static class HaasResponseParsers
    {
        /// <summary>Parse a Q-code response. If <paramref name="command"/> matches a well-known Q-code (Q100, Q104, Q500, Q600), extract the specific fields; otherwise publish the raw response under the command's <c>Name</c>.</summary>
        public static IEnumerable<(string Key, string Value)> Parse(HaasCommand command, string response)
        {
            var results = new List<(string, string)>();
            if (string.IsNullOrWhiteSpace(response)) return results;

            switch (command.Command)
            {
                case "?Q100":
                    // ">SERIAL NUMBER, <serial>" — presence indicates availability; also emit the serial.
                    var q100 = Regex.Match(response, @"^>SERIAL NUMBER,\s*(.+)$", RegexOptions.Multiline);
                    if (q100.Success)
                    {
                        results.Add((command.Name, q100.Groups[1].Value.Trim()));
                        results.Add(("avail", "AVAILABLE"));
                    }
                    else
                    {
                        results.Add(("avail", "UNAVAILABLE"));
                    }
                    break;

                case "?Q104":
                    // ">MODE, (<mode>)" — extract raw mode string; also derive controller_mode enum + zero_ret condition.
                    var q104 = Regex.Match(response, @"^>MODE,\s*(.+)$", RegexOptions.Multiline);
                    if (q104.Success)
                    {
                        var raw = q104.Groups[1].Value.Trim();
                        results.Add((command.Name, raw));

                        // Map to MTConnect CONTROLLER_MODE enum
                        string controllerMode = raw switch
                        {
                            "(MDI)" => "MANUAL_DATA_INPUT",
                            "(JOG)" => "MANUAL",
                            "(ZERO RET)" => "MANUAL",
                            _ => "AUTOMATIC",
                        };
                        results.Add(("controller_mode", controllerMode));

                        // Zero-return condition
                        results.Add(("zero_ret", raw == "(ZERO RET)" ? "FAULT" : "NORMAL"));
                    }
                    break;

                case "?Q500":
                    // ">PROGRAM, <name>, <state>, PARTS, <count>" — split into program, execution, estop, partCount.
                    var q500 = Regex.Match(response,
                        @"^>PROGRAM,\s*(?<prog>[^,]*),\s*(?<state>[^,]*),\s*PARTS,\s*(?<parts>\d+)",
                        RegexOptions.Multiline);
                    if (q500.Success)
                    {
                        results.Add((command.Name, response.Trim()));

                        var prog = q500.Groups["prog"].Value.Trim();
                        var state = q500.Groups["state"].Value.Trim();
                        var parts = q500.Groups["parts"].Value.Trim();

                        // program: MDI → empty string per MTConnect convention
                        results.Add(("program", prog == "MDI" ? "" : prog));

                        // execution mapping
                        string execution = state switch
                        {
                            "IDLE" => "READY",
                            "FEED HOLD" => "INTERRUPTED",
                            "ALARM ON" => "STOPPED",
                            "BUSY" => "ACTIVE",
                            _ => "READY",
                        };
                        results.Add(("execution", execution));

                        // e-stop: ALARM ON => TRIGGERED + system FAULT; else ARMED + NORMAL
                        if (state == "ALARM ON")
                        {
                            results.Add(("estop", "TRIGGERED"));
                            results.Add(("system", "FAULT"));
                        }
                        else
                        {
                            results.Add(("estop", "ARMED"));
                            results.Add(("system", "NORMAL"));
                        }

                        results.Add(("partCount", parts));
                    }
                    break;

                default:
                    // Unknown Q-code — publish the raw response under the command's own name.
                    // Matches the HAAS-MQTT Python poller's "publish raw, parse downstream" ethos
                    // for custom user-added Q-codes.
                    results.Add((command.Name, response.Trim()));
                    break;
            }

            return results;
        }

        /// <summary>Extract the numeric value from a <c>?Q600 &lt;var&gt;</c> response of the form <c>&gt;MACRO, &lt;value&gt;</c>. Returns null if the pattern doesn't match.</summary>
        public static string ParseMacroValue(string response)
        {
            if (string.IsNullOrWhiteSpace(response)) return null;
            var m = Regex.Match(response, @"^>MACRO,\s*(.+)$", RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }
    }
}

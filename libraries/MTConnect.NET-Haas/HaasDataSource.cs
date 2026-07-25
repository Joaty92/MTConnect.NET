// Copyright (c) 2023 TrakHound Inc. + 2025 fork contributors, All Rights Reserved.
// Licensed to you under the MIT license; see LICENSE.md in the repository root.
// Original TrakHound MTConnect.NET-Haas (MIT). Fork rewrites the polling loop
// to be YAML-driven: built-in Q-codes/macros from HaasDefaults plus any extra
// entries the user declares under Commands / MacroVariables in agent.config.yaml.
//
// Design principle inherited from the HAAS-MQTT Python poller: known Q-codes
// (Q100/Q104/Q500) get typed parsing to feed spec-compliant MTConnect DataItems;
// unknown / user-added Q-codes publish their raw response string as a
// pass-through — parse downstream if needed.

using MTConnect.Logging;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MTConnect.Haas
{
    /// <summary>Abstract polling loop for a Haas machine. Concrete transports (<see cref="HaasEthernetDataSource"/> for TCP via Moxa) override <see cref="SendCommand"/>. Emits <see cref="ObservationAdded"/> for every value produced by the built-in defaults and the user-added commands / macros. On transport failure the loop flags <c>avail</c> and every other key <c>UNAVAILABLE</c> and skips the rest of the cycle; on recovery the cycle restarts so availability returns before fresh data, and the once-per-session values are re-polled.</summary>
    public abstract class HaasDataSource
    {
        // Keys the parsers derive beyond the command/macro names themselves — used to
        // flag the whole device stale on transport failure (parity with the OPC UA
        // module's source-down behaviour). "avail" is handled separately.
        private static readonly string[] DerivedKeys =
            { "controller_mode", "zero_ret", "program", "execution", "estop", "system", "partCount" };

        private readonly List<HaasCommand> _allCommands;
        private readonly List<HaasMacroVariable> _allMacros;
        private readonly List<string> _allKeys;
        // Monotonic ticks (Environment.TickCount64) — wall-clock deltas would let an
        // NTP step silently suppress or double-fire PollEvery throttles.
        private readonly Dictionary<string, long> _lastPollTicks = new Dictionary<string, long>();
        private readonly HashSet<string> _polledOnceKeys = new HashSet<string>();

        private Thread _pollThread;
        private volatile bool _running;
        private bool _transportDown; // poll-thread only

        /// <summary>Seconds between polling cycles (all commands + macros scanned once per cycle, respecting per-entry throttles).</summary>
        public double PollInterval { get; set; } = 1.0;

        /// <summary>Raised for every observation produced by the polling loop. Consumers (the agent module) wire this to <see cref="MTConnect.Agents.IMTConnectAgentBroker.AddObservation"/>.</summary>
        public event EventHandler<(string Key, string Value)> ObservationAdded;

        /// <summary>Raised when the polling loop or the transport wants to emit a log line (start/stop, transport errors, unknown responses).</summary>
        public event MTConnectLogEventHandler LogReceived;

        /// <summary>Builds the polling engine. User-added Commands/MacroVariables from configuration are appended to <see cref="HaasDefaults.Commands"/> / <see cref="HaasDefaults.MacroVariables"/> — both sets are polled.</summary>
        protected HaasDataSource(IEnumerable<HaasCommand> extraCommands, IEnumerable<HaasMacroVariable> extraMacros, double pollInterval)
        {
            _allCommands = new List<HaasCommand>(HaasDefaults.Commands);
            if (extraCommands != null) _allCommands.AddRange(extraCommands);

            _allMacros = new List<HaasMacroVariable>(HaasDefaults.MacroVariables);
            if (extraMacros != null) _allMacros.AddRange(extraMacros);

            _allKeys = new List<string>();
            foreach (var cmd in _allCommands) if (!string.IsNullOrEmpty(cmd.Name)) _allKeys.Add(cmd.Name);
            foreach (var mv in _allMacros) if (!string.IsNullOrEmpty(mv.Name)) _allKeys.Add(mv.Name);
            _allKeys.AddRange(DerivedKeys);

            PollInterval = pollInterval > 0 ? pollInterval : 1.0;
        }

        /// <summary>Opens the transport and starts the polling thread.</summary>
        public void Start()
        {
            _running = true;
            OnStart();
            _pollThread = new Thread(PollLoop) { IsBackground = true };
            _pollThread.Start();
        }

        /// <summary>Stops the polling loop and closes the transport. Closing the transport first also unblocks a poll thread waiting inside a read; then blocks briefly on the poll thread.</summary>
        public void Stop()
        {
            _running = false;
            OnStop();
            _pollThread?.Join(TimeSpan.FromSeconds(3));
        }

        /// <summary>Override in a transport subclass to send a Q-code command and return the raw ASCII response. Returning null signals a transport failure — the polling loop flags every data item <c>UNAVAILABLE</c> once per outage and skips this cycle's remaining commands.</summary>
        protected abstract string SendCommand(string command);

        /// <summary>Called on <see cref="Start"/> before the poll thread starts. Override to open transports, etc.</summary>
        protected virtual void OnStart() { }

        /// <summary>Called on <see cref="Stop"/> after the poll loop is asked to exit. Override to close transports.</summary>
        protected virtual void OnStop() { }

        private void PollLoop()
        {
            while (_running)
            {
                try
                {
                    // PollCommands returns false when the cycle was aborted — transport
                    // down, or the restart that follows recovery. The macro pass must be
                    // skipped in both cases: on recovery it would otherwise emit fresh
                    // macro values before Q100 has flipped avail back to AVAILABLE.
                    if (PollCommands()) PollMacroVariables();
                }
                catch (Exception ex)
                {
                    Log(MTConnectLogLevel.Error, "Poll loop exception: " + ex.Message);
                }

                Thread.Sleep(TimeSpan.FromSeconds(PollInterval));
            }
        }

        // Returns false when the remaining cycle (including the macro pass) must be
        // abandoned: the transport went down, went up again, or the source was stopped.
        private bool PollCommands()
        {
            foreach (var cmd in _allCommands)
            {
                if (!_running) return false;
                if (SkipDueToRateLimit(cmd.Name, cmd.PollEvery)) continue;
                if (cmd.PollOnce && _polledOnceKeys.Contains(cmd.Name)) continue;

                var response = SendCommand(cmd.Command);
                if (!NoteTransportResult(response != null)) return false;

                var parsedPrimary = false;
                foreach (var (key, value) in HaasResponseParsers.Parse(cmd, response))
                {
                    if (key == cmd.Name) parsedPrimary = true;
                    Emit(key, value);
                }

                // Latch PollOnce only when the primary value actually parsed, so a
                // garbled first reply (e.g. Q100 serial number) is retried next cycle
                // instead of being lost — and avail stuck UNAVAILABLE — for the whole
                // session.
                if (cmd.PollOnce && parsedPrimary) _polledOnceKeys.Add(cmd.Name);
                _lastPollTicks[cmd.Name] = Environment.TickCount64;
            }

            return true;
        }

        private void PollMacroVariables()
        {
            foreach (var mv in _allMacros)
            {
                if (!_running) return;
                if (SkipDueToRateLimit(mv.Name, mv.PollEvery)) continue;

                var response = SendCommand("?Q600 " + mv.Variable);
                if (!NoteTransportResult(response != null)) return;

                var value = HaasResponseParsers.ParseMacroValue(response);
                if (value != null)
                {
                    Emit(mv.Name, value);
                }

                _lastPollTicks[mv.Name] = Environment.TickCount64;
            }
        }

        // Tracks transport up/down transitions; returns false when the current poll
        // cycle should be aborted. On the first failure of an outage: flag avail and
        // every other key UNAVAILABLE — otherwise /current keeps serving hours-old
        // values with live-looking timestamps for the whole outage. On the first
        // success after an outage: clear the PollOnce/throttle state and abort the
        // cycle, so the NEXT cycle leads with Q100 and avail returns to AVAILABLE
        // before any fresh data is emitted (consumers never see live-stamped data
        // inside an 'unavailable' window).
        private bool NoteTransportResult(bool ok)
        {
            if (!ok)
            {
                if (!_transportDown)
                {
                    _transportDown = true;
                    Emit("avail", "UNAVAILABLE");
                    foreach (var key in _allKeys) Emit(key, "UNAVAILABLE");
                    Log(MTConnectLogLevel.Warning, "Transport failure — flagging all data items UNAVAILABLE and skipping the rest of this cycle");
                }
                return false;
            }

            if (_transportDown)
            {
                _transportDown = false;
                ResetSessionState();
                Log(MTConnectLogLevel.Information, "Transport recovered — restarting the poll cycle so availability leads the fresh data");
                return false;
            }
            return true;
        }

        private bool SkipDueToRateLimit(string key, int? pollEverySeconds)
        {
            if (!pollEverySeconds.HasValue) return false;
            if (!_lastPollTicks.TryGetValue(key, out var lastTicks)) return false;
            return (Environment.TickCount64 - lastTicks) < pollEverySeconds.Value * 1000L;
        }

        private void Emit(string key, string value)
        {
            ObservationAdded?.Invoke(this, (key, value));
        }

        // Clears the PollOnce tracking so the once-per-session values (serial
        // number, software version, model) re-emit after a transport recovery.
        // Driven by NoteTransportResult on the first success after an outage.
        private void ResetSessionState()
        {
            _polledOnceKeys.Clear();
            _lastPollTicks.Clear();
        }

        /// <summary>Emit a log line to the hosting module.</summary>
        protected void Log(MTConnectLogLevel level, string message)
        {
            LogReceived?.Invoke(this, level, message);
        }
    }
}

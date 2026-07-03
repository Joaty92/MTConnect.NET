// Copyright (c) 2023 TrakHound Inc. + 2025 fork contributors, All Rights Reserved.
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
    /// <summary>Abstract polling loop for a Haas machine. Concrete transports (<see cref="HaasEthernetDataSource"/> for TCP via Moxa) override <see cref="SendCommand"/>. Emits <see cref="ObservationAdded"/> for every value produced by the built-in defaults and the user-added commands / macros. On transport failure the loop emits <c>avail=UNAVAILABLE</c>, skips the rest of the cycle, and re-polls the once-per-session values after recovery.</summary>
    public abstract class HaasDataSource
    {
        private readonly List<HaasCommand> _allCommands;
        private readonly List<HaasMacroVariable> _allMacros;
        private readonly Dictionary<string, DateTime> _lastPollUtc = new Dictionary<string, DateTime>();
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

        /// <summary>Override in a transport subclass to send a Q-code command and return the raw ASCII response. Returning null signals a transport failure — the polling loop emits <c>avail=UNAVAILABLE</c> once per outage and skips this cycle's remaining commands.</summary>
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
                    PollCommands();
                    PollMacroVariables();
                }
                catch (Exception ex)
                {
                    Log(MTConnectLogLevel.Error, "Poll loop exception: " + ex.Message);
                }

                Thread.Sleep(TimeSpan.FromSeconds(PollInterval));
            }
        }

        private void PollCommands()
        {
            foreach (var cmd in _allCommands)
            {
                if (!_running) return;
                if (SkipDueToRateLimit(cmd.Name, cmd.PollEvery)) continue;
                if (cmd.PollOnce && _polledOnceKeys.Contains(cmd.Name)) continue;

                var response = SendCommand(cmd.Command);
                if (!NoteTransportResult(response != null)) return;

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
                _lastPollUtc[cmd.Name] = DateTime.UtcNow;
            }
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

                _lastPollUtc[mv.Name] = DateTime.UtcNow;
            }
        }

        // Tracks transport up/down transitions. On the first failure of an outage:
        // emit avail=UNAVAILABLE (Q100 only reports availability while the link is
        // up, so nothing else would) and abort the cycle. On the first success after
        // an outage: clear the PollOnce/throttle state so Q100 re-polls and flips
        // avail back to AVAILABLE along with the identity values.
        private bool NoteTransportResult(bool ok)
        {
            if (!ok)
            {
                if (!_transportDown)
                {
                    _transportDown = true;
                    Emit("avail", "UNAVAILABLE");
                    Log(MTConnectLogLevel.Warning, "Transport failure — marking avail UNAVAILABLE and skipping the rest of this cycle");
                }
                return false;
            }

            if (_transportDown)
            {
                _transportDown = false;
                ResetSessionState();
                Log(MTConnectLogLevel.Information, "Transport recovered — re-polling session values (serial number, model, availability)");
            }
            return true;
        }

        private bool SkipDueToRateLimit(string key, int? pollEverySeconds)
        {
            if (!pollEverySeconds.HasValue) return false;
            if (!_lastPollUtc.TryGetValue(key, out var last)) return false;
            return (DateTime.UtcNow - last).TotalSeconds < pollEverySeconds.Value;
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
            _lastPollUtc.Clear();
        }

        /// <summary>Emit a log line to the hosting module.</summary>
        protected void Log(MTConnectLogLevel level, string message)
        {
            LogReceived?.Invoke(this, level, message);
        }
    }
}

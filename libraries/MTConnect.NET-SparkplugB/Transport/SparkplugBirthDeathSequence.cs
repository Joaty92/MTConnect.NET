// Copyright (c) 2025 MTConnect.NET fork contributors, All Rights Reserved.
// Licensed to you under the MIT license; see LICENSE.md in the repository root.

using System;
using System.IO;

namespace MTConnect.SparkplugB.Transport
{
    /// <summary>
    /// The Sparkplug B birth/death sequence number (<c>bdSeq</c>).
    /// <para>
    /// This is what lets a Host Application decide whether an arriving NDEATH kills the session it
    /// currently believes in, or is a stale artefact of a previous one. The NDEATH is registered as
    /// the Will Message at CONNECT time, so the broker may publish it long after the Edge Node has
    /// already reconnected and issued a fresh NBIRTH. Without a discriminator the host would mark a
    /// live node offline. The bdSeq registered in a connection's will therefore has to equal the
    /// bdSeq in the NBIRTH published on that same connection, and it must advance on every new
    /// CONNECT — <c>tck-id-topics-nbirth-bdseq-increment</c>.
    /// </para>
    /// <para>
    /// The value survives process restarts. A restarted Edge Node that began again at zero would
    /// replay bdSeq values the host has already seen, so a genuinely new session could be mistaken
    /// for the stale one it is replacing. Persistence is best-effort: if the store cannot be read or
    /// written the counter still advances in memory and the relay keeps running, because refusing to
    /// publish would be a far worse failure than an imperfect discriminator.
    /// </para>
    /// </summary>
    public sealed class SparkplugBirthDeathSequence
    {
        /// <summary>One past the highest legal bdSeq value; the counter wraps here.</summary>
        public const long Wrap = 256;

        private readonly object _lock = new();
        private readonly string _statePath;
        private long _next;

        /// <summary>Raised when the persisted value cannot be read or written, carrying the reason.</summary>
        public event Action<string> PersistenceFailed;

        /// <summary>
        /// Creates the counter, resuming from <paramref name="statePath"/> when it holds a usable value.
        /// </summary>
        /// <param name="statePath">
        /// File used to carry the counter across restarts. Pass null to keep the counter in memory only,
        /// which is correct for tests but means a restart replays bdSeq values on the broker.
        /// </param>
        public SparkplugBirthDeathSequence(string statePath = null)
        {
            _statePath = statePath;
            _next = Load();
        }

        /// <summary>The bdSeq the next connection will use; also the value its Will Message must carry.</summary>
        public long Current
        {
            get { lock (_lock) return _next; }
        }

        /// <summary>
        /// Advances to the next connection's bdSeq and persists it. Call once per MQTT CONNECT, before
        /// building that connection's Will Message, so the will and the NBIRTH agree.
        /// </summary>
        public long Advance()
        {
            lock (_lock)
            {
                _next = (_next + 1) % Wrap;
                Save(_next);
                return _next;
            }
        }

        private long Load()
        {
            if (string.IsNullOrWhiteSpace(_statePath)) return 0;

            try
            {
                if (!File.Exists(_statePath)) return 0;

                var text = File.ReadAllText(_statePath).Trim();
                if (long.TryParse(text, out var value) && value >= 0 && value < Wrap) return value;

                // A corrupt store is not worth failing over: starting from zero replays at most one
                // bdSeq value, whereas refusing to start would take the whole relay down.
                PersistenceFailed?.Invoke($"bdSeq store '{_statePath}' held '{text}'; starting from 0");
                return 0;
            }
            catch (Exception ex)
            {
                PersistenceFailed?.Invoke($"could not read bdSeq store '{_statePath}': {ex.Message}");
                return 0;
            }
        }

        private void Save(long value)
        {
            if (string.IsNullOrWhiteSpace(_statePath)) return;

            try
            {
                var directory = Path.GetDirectoryName(_statePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                File.WriteAllText(_statePath, value.ToString());
            }
            catch (Exception ex)
            {
                PersistenceFailed?.Invoke($"could not persist bdSeq to '{_statePath}': {ex.Message}");
            }
        }
    }
}

// Copyright (c) 2025 MTConnect.NET fork contributors, All Rights Reserved.
// Licensed to you under the MIT license; see LICENSE.md in the repository root.

namespace MTConnect.SparkplugB.Transport
{
    /// <summary>
    /// The Sparkplug B message sequence number.
    /// <para>
    /// One counter per Edge Node, shared by every message it publishes. NBIRTH starts the sequence
    /// at zero and every following NDATA, DBIRTH, DDATA and DDEATH carries exactly one more than the
    /// message before it, wrapping 255 back to 0. A Host Application tracks this to detect loss, so
    /// a single skipped or duplicated value costs the whole Edge Node a rebirth — which is why the
    /// counter is owned here rather than incremented at call sites.
    /// </para>
    /// <para>
    /// NDEATH is deliberately excluded: <c>tck-id-payloads-ndeath-seq</c> requires that it carry no
    /// sequence number at all. It is registered as the Will Message before the session exists, so it
    /// has no place in the session's ordering.
    /// </para>
    /// </summary>
    public sealed class SparkplugSequence
    {
        /// <summary>One past the highest legal sequence number; the counter wraps here.</summary>
        public const ulong Wrap = 256;

        private readonly object _lock = new();
        private ulong _next;

        /// <summary>The value the next call to <see cref="Next"/> will return.</summary>
        public ulong Peek
        {
            get { lock (_lock) return _next; }
        }

        /// <summary>
        /// Restarts the sequence at zero for a new Sparkplug session. Called when publishing NBIRTH,
        /// so that the NBIRTH itself carries sequence 0.
        /// </summary>
        public void Reset()
        {
            lock (_lock) _next = 0;
        }

        /// <summary>Takes the next sequence number and advances the counter, wrapping 255 to 0.</summary>
        public ulong Next()
        {
            lock (_lock)
            {
                var value = _next;
                _next = (_next + 1) % Wrap;
                return value;
            }
        }
    }
}

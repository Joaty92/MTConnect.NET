// Copyright (c) 2025 MTConnect.NET fork contributors, All Rights Reserved.
// Licensed to you under the MIT license; see LICENSE.md in the repository root.

using MQTTnet.Client;

namespace MTConnect.SparkplugB.Transport
{
    /// <summary>Connection and identity settings for a <see cref="SparkplugEdgeNode"/>.</summary>
    public sealed class SparkplugEdgeNodeOptions
    {
        /// <summary>MQTT broker hostname or IP address.</summary>
        public string Server { get; set; } = "localhost";

        /// <summary>MQTT broker TCP port.</summary>
        public int Port { get; set; } = 1883;

        /// <summary>MQTT client identifier.</summary>
        public string ClientId { get; set; }

        /// <summary>Optional MQTT username.</summary>
        public string Username { get; set; }

        /// <summary>Optional MQTT password.</summary>
        public string Password { get; set; }

        /// <summary>Sparkplug Group ID; the first topic level after the namespace.</summary>
        public string GroupId { get; set; } = "MTConnect";

        /// <summary>Sparkplug Edge Node ID identifying this gateway.</summary>
        public string EdgeNodeId { get; set; } = "Agent";

        /// <summary>
        /// Sparkplug Primary Host Application ID. When set, NBIRTH is withheld until that host's
        /// <c>STATE</c> reports online, and the host going offline ends the session —
        /// <c>tck-id-message-flow-edge-node-birth-publish-phid-wait</c>. Null disables the behaviour
        /// and NBIRTH follows the connection immediately.
        /// </summary>
        public string PrimaryHostId { get; set; }

        /// <summary>Milliseconds to wait between reconnect attempts.</summary>
        public int ReconnectIntervalMs { get; set; } = 10000;

        /// <summary>TLS settings, or null for a plaintext connection.</summary>
        public MqttClientTlsOptions TlsOptions { get; set; }

        /// <summary>
        /// File used to carry <c>bdSeq</c> across process restarts. Null keeps the counter in memory,
        /// which means a restart replays bdSeq values a Host Application has already seen.
        /// </summary>
        public string BirthDeathSequencePath { get; set; }
    }
}

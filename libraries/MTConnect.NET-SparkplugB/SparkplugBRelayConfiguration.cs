// Copyright (c) 2025 MTConnect.NET fork contributors, All Rights Reserved.
// Licensed to you under the MIT license; see LICENSE.md in the repository root.

namespace MTConnect.Configurations
{
    /// <summary>Configuration for the SparkplugB relay's MQTT connection and node identity.</summary>
    public class SparkplugBRelayConfiguration
    {
        /// <summary>MQTT broker hostname or IP address.</summary>
        public string Server { get; set; } = "localhost";
        /// <summary>MQTT broker TCP port.</summary>
        public int Port { get; set; } = 1883;
        /// <summary>MQTT client identifier; when null the relay generates a unique <c>MTConnect-{guid}</c> id.</summary>
        public string ClientId { get; set; }
        /// <summary>Optional MQTT username for broker authentication.</summary>
        public string Username { get; set; }
        /// <summary>Optional MQTT password for broker authentication.</summary>
        public string Password { get; set; }
        /// <summary>Sparkplug Group ID (top-level namespace for this set of edge nodes).</summary>
        public string GroupId { get; set; } = "MTConnect";
        /// <summary>Sparkplug Edge Node ID identifying this relay within the group.</summary>
        public string NodeId { get; set; } = "Agent";
        /// <summary>Delay in milliseconds between reconnect attempts after the broker connection drops.</summary>
        public int ReconnectInterval { get; set; } = 10000;

        /// <summary>
        /// Sparkplug Primary Host Application ID this edge node coordinates with. When set, the node
        /// waits for that host's <c>STATE</c> to report online before publishing NBIRTH, and treats the
        /// host going offline as a reason to end its session — the behaviour Sparkplug calls Primary Host
        /// awareness. Leave unset (the default) when no primary host is present, in which case NBIRTH is
        /// published as soon as the broker connection is up.
        /// </summary>
        public string PrimaryHostId { get; set; }

        /// <summary>
        /// Overrides where this relay persists Sparkplug state — currently just the <c>bdSeq</c>
        /// counter, in a file named after the Edge Node identity so several relay blocks share one
        /// directory without sharing a counter.
        /// <para>
        /// Rarely needed. The default is a <c>state</c> directory beside the agent binary, so mounting
        /// a volume there is enough and nothing has to be declared per relay block. What matters is
        /// that the directory outlives the container: otherwise every deployment restarts the counter
        /// at zero and replays bdSeq values a Host Application has already seen, making a new session
        /// indistinguishable from the stale one it replaces
        /// (<c>tck-id-topics-nbirth-bdseq-increment</c>).
        /// </para>
        /// </summary>
        public string StateDirectory { get; set; }

        /// <summary>Enables TLS on the MQTT connection. Point <see cref="Port"/> at the broker's TLS listener (conventionally 8883).</summary>
        public bool UseTls { get; set; }
        /// <summary>Optional path to the broker's CA certificate (PEM or DER). When set, the server certificate is validated against this CA (custom root trust) instead of the OS trust store. Relative paths resolve against the agent binary directory.</summary>
        public string CaCertificateFile { get; set; }
        /// <summary>Optional path to a PKCS#12 (.pfx / .p12) bundle holding the client certificate + private key; enables mutual-TLS client-certificate authentication. Relative paths resolve against the agent binary directory.</summary>
        public string ClientCertificateFile { get; set; }
        /// <summary>Password for <see cref="ClientCertificateFile"/>; omit if the bundle is not password-protected.</summary>
        public string ClientCertificatePassword { get; set; }
        /// <summary>Accepts any server certificate without validation. Test rigs only — never production.</summary>
        public bool AllowUntrustedCertificates { get; set; }
    }
}

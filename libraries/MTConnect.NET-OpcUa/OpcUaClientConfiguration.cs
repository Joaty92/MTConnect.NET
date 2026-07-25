// Copyright (c) 2025 MTConnect.NET fork contributors, All Rights Reserved.
// Licensed to you under the MIT license; see LICENSE.md in the repository root.

namespace MTConnect.Configurations
{
    /// <summary>Connection settings for an OPC UA client session: endpoint, security policy, identity, and timing.</summary>
    public class OpcUaClientConfiguration
    {
        /// <summary>OPC UA endpoint URL (e.g. <c>opc.tcp://192.168.1.50:4840</c>).</summary>
        public string Endpoint { get; set; }

        /// <summary>Security policy name — currently <c>None</c> or <c>Basic256Sha256</c>; defaults to <c>None</c> for sim-server use.</summary>
        public string SecurityPolicy { get; set; } = "None";

        /// <summary>Whether an untrusted server certificate is accepted automatically. Defaults to <c>true</c>, which encrypts the channel but does <b>not</b> authenticate the server — any endpoint answering on <see cref="Endpoint"/> is trusted, so a username identity could be presented to an impostor. Set to <c>false</c> on hardened deployments: the server certificate must then be placed in the client's <c>pki/trusted</c> store.</summary>
        public bool AcceptUntrustedServerCertificate { get; set; } = true;

        /// <summary>User identity for the session; defaults to anonymous.</summary>
        public OpcUaIdentityConfiguration Identity { get; set; } = new();

        /// <summary>MonitoredItem sampling interval in milliseconds (how often the server samples the underlying source).</summary>
        public int SamplingInterval { get; set; } = 1000;

        /// <summary>Subscription publishing interval in milliseconds (how often the server pushes notifications).</summary>
        public int PublishingInterval { get; set; } = 1000;

        /// <summary>Delay in milliseconds between reconnect attempts after a session keep-alive failure.</summary>
        public int ReconnectInterval { get; set; } = 5000;

        /// <summary>Session timeout in milliseconds requested from the server.</summary>
        public int SessionTimeout { get; set; } = 60000;
    }

    /// <summary>Identity (authentication) configuration for an OPC UA session.</summary>
    public class OpcUaIdentityConfiguration
    {
        /// <summary>Identity mode — <c>anonymous</c> or <c>username</c>.</summary>
        public string Mode { get; set; } = "anonymous";

        /// <summary>Username when <see cref="Mode"/> is <c>username</c>; ignored otherwise.</summary>
        public string Username { get; set; }

        /// <summary>Password when <see cref="Mode"/> is <c>username</c>; ignored otherwise.</summary>
        public string Password { get; set; }
    }
}

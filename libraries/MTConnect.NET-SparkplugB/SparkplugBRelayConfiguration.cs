// Copyright (c) 2025, All Rights Reserved.

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
    }
}

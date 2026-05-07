// Copyright (c) 2025, All Rights Reserved.

namespace MTConnect.Configurations
{
    public class SparkplugBRelayConfiguration
    {
        public string Server { get; set; } = "localhost";
        public int Port { get; set; } = 1883;
        public string ClientId { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string GroupId { get; set; } = "MTConnect";
        public string NodeId { get; set; } = "Agent";
        public int ReconnectInterval { get; set; } = 10000;
    }
}

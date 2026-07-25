// Copyright (c) 2023 TrakHound Inc. + 2025 fork contributors, All Rights Reserved.
// Licensed to you under the MIT license; see LICENSE.md in the repository root.
// Original TrakHound MTConnect.NET-Haas (MIT).

using MTConnect.Haas;

namespace MTConnect.Configurations
{
    /// <summary>Agent-module configuration binding for the Haas Ethernet adapter; extends <see cref="HaasEthernetConfiguration"/> with the device key that ties this poller to an MTConnect device in the agent buffer.</summary>
    public class HaasEthernetAdapterModuleConfiguration : HaasEthernetConfiguration
    {
        /// <summary>Key of the MTConnect device that observations from this Haas machine belong to; must match a <c>device.Uuid</c> or <c>device.Name</c> the agent knows (from <c>devices/&lt;key&gt;.xml</c>, another module's <c>AddDevice</c>, or an upcoming auto-generated device model).</summary>
        public string DeviceKey { get; set; }

        /// <summary>Defaults suitable for a local Moxa NPort on the same host.</summary>
        public HaasEthernetAdapterModuleConfiguration()
        {
            Server = "localhost";
            Port = 4001;
        }
    }
}

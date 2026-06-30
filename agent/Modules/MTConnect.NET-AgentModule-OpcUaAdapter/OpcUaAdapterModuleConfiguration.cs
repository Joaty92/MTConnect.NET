// Copyright (c) 2025, All Rights Reserved.

namespace MTConnect.Configurations
{
    /// <summary>Agent-module configuration binding for the OPC UA adapter; extends <see cref="OpcUaClientConfiguration"/> with the device key and profile name selected from <c>profiles/</c>.</summary>
    public class OpcUaAdapterModuleConfiguration : OpcUaClientConfiguration
    {
        /// <summary>Key of the MTConnect device that observations from this OPC UA server belong to; must match a <c>device.Uuid</c> or <c>device.Name</c> loaded by the agent from <c>devices/</c>.</summary>
        public string DeviceKey { get; set; }

        /// <summary>Name of a profile file (without extension) under the module's <c>profiles/</c> folder; selects the NodeId → DataItem mapping for this server.</summary>
        public string Profile { get; set; }
    }
}

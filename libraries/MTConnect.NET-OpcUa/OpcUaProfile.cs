// Copyright (c) 2025, All Rights Reserved.

using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MTConnect.OpcUa
{
    /// <summary>MTConnect data category that an OPC UA value is coerced into; mirrors MTConnect's SAMPLE/EVENT categories for v1 (CONDITION is out of scope).</summary>
    public enum OpcUaValueKind
    {
        /// <summary>Numeric continuous value; the module coerces the OPC UA value to <see cref="double"/> before forwarding.</summary>
        Sample,
        /// <summary>Discrete value; passed through as the underlying string representation.</summary>
        Event
    }

    /// <summary>One NodeId → MTConnect data-item mapping inside an <see cref="OpcUaProfile"/>.</summary>
    public class OpcUaMapping
    {
        /// <summary>OPC UA NodeId string (e.g. <c>ns=2;s=/Channel/State/feedRateOvr</c>).</summary>
        public string NodeId { get; set; }

        /// <summary>Key of the MTConnect data item to push observations into; must match a <c>DataItem id</c> in the corresponding device XML.</summary>
        public string DataItemKey { get; set; }

        /// <summary>How the value should be coerced before forwarding; <see cref="OpcUaValueKind.Sample"/> forces <see cref="double"/> coercion.</summary>
        public OpcUaValueKind Kind { get; set; } = OpcUaValueKind.Event;
    }

    /// <summary>Deserialized representation of a NodeId → MTConnect mapping profile YAML file.</summary>
    public class OpcUaProfile
    {
        /// <summary>Ordered list of NodeId → MTConnect mappings to subscribe to.</summary>
        public List<OpcUaMapping> Mappings { get; set; } = new();

        /// <summary>Loads a profile from a YAML file on disk; throws <see cref="FileNotFoundException"/> if the file is missing.</summary>
        public static OpcUaProfile LoadFromFile(string path)
        {
            var yaml = File.ReadAllText(path);
            return LoadFromString(yaml);
        }

        /// <summary>Deserializes a profile from a YAML string; used by <see cref="LoadFromFile(string)"/> and for unit-testing round-trip behaviour.</summary>
        public static OpcUaProfile LoadFromString(string yaml)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            return deserializer.Deserialize<OpcUaProfile>(yaml) ?? new OpcUaProfile();
        }
    }
}

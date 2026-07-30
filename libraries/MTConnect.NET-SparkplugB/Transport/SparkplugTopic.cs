// Copyright (c) 2025 MTConnect.NET fork contributors, All Rights Reserved.
// Licensed to you under the MIT license; see LICENSE.md in the repository root.

using System;

namespace MTConnect.SparkplugB.Transport
{
    /// <summary>Sparkplug B message types, as they appear in the topic namespace.</summary>
    public enum SparkplugMessageType
    {
        /// <summary>Edge Node birth certificate.</summary>
        NBIRTH,
        /// <summary>Edge Node death certificate; registered as the MQTT Will Message.</summary>
        NDEATH,
        /// <summary>Edge Node data.</summary>
        NDATA,
        /// <summary>Edge Node command, inbound.</summary>
        NCMD,
        /// <summary>Device birth certificate.</summary>
        DBIRTH,
        /// <summary>Device death certificate.</summary>
        DDEATH,
        /// <summary>Device data.</summary>
        DDATA,
        /// <summary>Device command, inbound.</summary>
        DCMD
    }

    /// <summary>
    /// Builds and validates Sparkplug B topics.
    /// <para>
    /// The namespace is <c>spBv1.0/{group_id}/{message_type}/{edge_node_id}[/{device_id}]</c>, with
    /// one exception: a Host Application's <c>STATE</c> topic is <c>spBv1.0/STATE/{host_id}</c> and
    /// carries no group segment at all. Getting that wrong is silent — the edge node simply never
    /// hears the primary host — so it is built here rather than composed at call sites.
    /// </para>
    /// </summary>
    public static class SparkplugTopic
    {
        /// <summary>Topic namespace root for Sparkplug B.</summary>
        public const string Namespace = "spBv1.0";

        /// <summary>Fixed segment identifying a Host Application STATE topic.</summary>
        public const string StateSegment = "STATE";

        /// <summary>Builds an Edge Node topic: <c>spBv1.0/{group}/{type}/{node}</c>.</summary>
        /// <param name="groupId">Sparkplug Group ID.</param>
        /// <param name="messageType">Message type; must be one of the node-scoped types.</param>
        /// <param name="edgeNodeId">Sparkplug Edge Node ID.</param>
        public static string Node(string groupId, SparkplugMessageType messageType, string edgeNodeId) =>
            $"{Namespace}/{groupId}/{messageType}/{edgeNodeId}";

        /// <summary>Builds a Device topic: <c>spBv1.0/{group}/{type}/{node}/{device}</c>.</summary>
        /// <param name="groupId">Sparkplug Group ID.</param>
        /// <param name="messageType">Message type; must be one of the device-scoped types.</param>
        /// <param name="edgeNodeId">Sparkplug Edge Node ID.</param>
        /// <param name="deviceId">Sparkplug Device ID.</param>
        public static string Device(string groupId, SparkplugMessageType messageType, string edgeNodeId, string deviceId) =>
            $"{Namespace}/{groupId}/{messageType}/{edgeNodeId}/{deviceId}";

        /// <summary>
        /// Builds a Host Application STATE topic: <c>spBv1.0/STATE/{hostId}</c>. Note the absence of a
        /// group segment — STATE is scoped to the host, not to a group.
        /// </summary>
        /// <param name="hostId">Sparkplug Host Application ID.</param>
        public static string State(string hostId) => $"{Namespace}/{StateSegment}/{hostId}";

        /// <summary>
        /// Whether an identifier is usable as a topic segment. Group, Edge Node and Device IDs become
        /// topic levels verbatim, so an embedded separator or wildcard would silently reshape the
        /// namespace — an id containing <c>/</c> would fabricate a level, and one containing <c>+</c>
        /// or <c>#</c> would collide with subscription wildcards.
        /// </summary>
        /// <param name="identifier">The candidate Group, Edge Node or Device ID.</param>
        public static bool IsValidIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return false;

            foreach (var c in identifier)
            {
                if (c == '/' || c == '+' || c == '#') return false;
                // MQTT forbids the null character in topic names.
                if (c == '\0') return false;
            }

            return true;
        }

        /// <summary>
        /// Parses a Sparkplug topic. Returns false for anything outside the <c>spBv1.0</c> namespace,
        /// including STATE topics, which carry no group or node and are matched separately.
        /// </summary>
        /// <param name="topic">The MQTT topic to parse.</param>
        /// <param name="groupId">Parsed Group ID.</param>
        /// <param name="messageType">Parsed message type.</param>
        /// <param name="edgeNodeId">Parsed Edge Node ID.</param>
        /// <param name="deviceId">Parsed Device ID, or null for a node-scoped topic.</param>
        public static bool TryParse(
            string topic,
            out string groupId,
            out SparkplugMessageType messageType,
            out string edgeNodeId,
            out string deviceId)
        {
            groupId = null;
            edgeNodeId = null;
            deviceId = null;
            messageType = default;

            if (string.IsNullOrEmpty(topic)) return false;

            var levels = topic.Split('/');
            if (levels.Length < 4 || levels.Length > 5) return false;
            if (levels[0] != Namespace) return false;
            if (!Enum.TryParse(levels[2], ignoreCase: false, out messageType)) return false;

            groupId = levels[1];
            edgeNodeId = levels[3];
            if (levels.Length == 5) deviceId = levels[4];

            return true;
        }

        /// <summary>Extracts the Host Application ID from a STATE topic, or null if it is not one.</summary>
        /// <param name="topic">The MQTT topic to inspect.</param>
        public static string TryParseStateHostId(string topic)
        {
            if (string.IsNullOrEmpty(topic)) return null;

            var levels = topic.Split('/');
            if (levels.Length != 3) return null;
            if (levels[0] != Namespace || levels[1] != StateSegment) return null;

            return string.IsNullOrEmpty(levels[2]) ? null : levels[2];
        }
    }
}

// Copyright (c) 2025 MTConnect.NET fork contributors, All Rights Reserved.
// Licensed to you under the MIT license; see LICENSE.md in the repository root.

using ProtoBuf;
using System.Collections.Generic;

namespace MTConnect.SparkplugB.Payload
{
    /// <summary>
    /// Wire contract for a Sparkplug B payload.
    /// <para>
    /// Hand-written rather than generated. Eclipse publishes <c>sparkplug_b.proto</c> under
    /// EPL-2.0, and generating from it would pull an EPL file and its generated output into
    /// this MIT repository; the field numbers below are instead taken from the Sparkplug B
    /// specification's normative payload section, checked field-by-field against that
    /// <c>.proto</c>, and proven on the wire by the Eclipse Sparkplug TCK. Nothing here is
    /// copied from Eclipse sources — the numbers are the interoperability contract, not the
    /// expression of it.
    /// </para>
    /// <para>
    /// proto2 semantics: every field is optional, so every member is nullable and an unset
    /// field must serialise as absent rather than as a default. That distinction carries real
    /// meaning here — an absent <c>timestamp</c> fails a TCK assertion, and an absent value
    /// alongside <c>is_null</c> is how "no reading" is expressed.
    /// </para>
    /// </summary>
    [ProtoContract]
    public sealed class SparkplugPayload
    {
        /// <summary>Time the message was sent, in milliseconds since the Unix epoch.</summary>
        [ProtoMember(1, Name = "timestamp")]
        public ulong? Timestamp { get; set; }

        /// <summary>Metrics carried by this payload.</summary>
        [ProtoMember(2, Name = "metrics")]
        public List<SparkplugMetric> Metrics { get; set; } = new();

        /// <summary>
        /// Sequence number. Set on NBIRTH and incremented by exactly one on every subsequent
        /// message from the Edge Node, wrapping 255 to 0. NDEATH deliberately carries none.
        /// </summary>
        [ProtoMember(3, Name = "seq")]
        public ulong? Seq { get; set; }

        /// <summary>Optional schema-tracking UUID; unused by this relay.</summary>
        [ProtoMember(4, Name = "uuid")]
        public string Uuid { get; set; }

        /// <summary>Optional opaque body; unused by this relay.</summary>
        [ProtoMember(5, Name = "body")]
        public byte[] Body { get; set; }
    }

    /// <summary>
    /// A single Sparkplug B metric.
    /// <para>
    /// The value is a protobuf <c>oneof</c> spanning fields 10-19. protobuf-net models that as
    /// separate optional members with those numbers: on the wire a oneof is simply "at most one
    /// of these fields is present", so encoding one and leaving the rest null is byte-identical
    /// to a generated oneof. Only the scalar arms this relay emits are declared — the DataSet,
    /// Template and extension arms are deliberately absent, since set-valued MTConnect
    /// representations are serialised as strings by SparkplugBMetricMapper. protobuf-net skips
    /// unknown fields when decoding, so an inbound payload using them is still parsed safely.
    /// </para>
    /// </summary>
    [ProtoContract]
    public sealed class SparkplugMetric
    {
        /// <summary>Metric name. Required on birth messages; may be omitted later if an alias is used.</summary>
        [ProtoMember(1, Name = "name")]
        public string Name { get; set; }

        /// <summary>Alias bound to the name at birth. Not used by this relay — names are sent every time.</summary>
        [ProtoMember(2, Name = "alias")]
        public ulong? Alias { get; set; }

        /// <summary>
        /// Data acquisition time, milliseconds since the Unix epoch. The specification requires a
        /// timestamp on every metric in NBIRTH, DBIRTH, NDATA and DDATA, so this is never left unset
        /// on an outbound metric.
        /// </summary>
        [ProtoMember(3, Name = "timestamp")]
        public ulong? Timestamp { get; set; }

        /// <summary>Sparkplug <see cref="SparkplugDataType"/> of this metric.</summary>
        [ProtoMember(4, Name = "datatype")]
        public uint? DataType { get; set; }

        /// <summary>Marks historical data that must not update a consumer's live value.</summary>
        [ProtoMember(5, Name = "is_historical")]
        public bool? IsHistorical { get; set; }

        /// <summary>Marks data a consumer should not persist as a tag.</summary>
        [ProtoMember(6, Name = "is_transient")]
        public bool? IsTransient { get; set; }

        /// <summary>
        /// Declares the metric to have no value, while keeping its declared DataType. This is how a
        /// numeric metric says "no reading" without inventing a number that a historian would trend
        /// as a measurement.
        /// </summary>
        [ProtoMember(7, Name = "is_null")]
        public bool? IsNull { get; set; }

        // --- value oneof (fields 10-19) -------------------------------------------------

        /// <summary>Value arm for the 8/16/32-bit signed and unsigned integer types.</summary>
        [ProtoMember(10, Name = "int_value")]
        public uint? IntValue { get; set; }

        /// <summary>Value arm for the 64-bit integer types and DateTime.</summary>
        [ProtoMember(11, Name = "long_value")]
        public ulong? LongValue { get; set; }

        /// <summary>Value arm for <see cref="SparkplugDataType.Float"/>.</summary>
        [ProtoMember(12, Name = "float_value")]
        public float? FloatValue { get; set; }

        /// <summary>Value arm for <see cref="SparkplugDataType.Double"/>.</summary>
        [ProtoMember(13, Name = "double_value")]
        public double? DoubleValue { get; set; }

        /// <summary>Value arm for <see cref="SparkplugDataType.Boolean"/>.</summary>
        [ProtoMember(14, Name = "boolean_value")]
        public bool? BooleanValue { get; set; }

        /// <summary>Value arm for <see cref="SparkplugDataType.String"/>, Text and UUID.</summary>
        [ProtoMember(15, Name = "string_value")]
        public string StringValue { get; set; }

        /// <summary>Value arm for Bytes and File.</summary>
        [ProtoMember(16, Name = "bytes_value")]
        public byte[] BytesValue { get; set; }
    }

    /// <summary>
    /// Sparkplug B metric data types, as numbered by the specification. Only the members this
    /// relay emits or parses are declared; the numbering is the spec's, so adding more later
    /// needs no renumbering.
    /// </summary>
    public enum SparkplugDataType : uint
    {
        /// <summary>Unset or unrecognised.</summary>
        Unknown = 0,

        /// <summary>32-bit signed integer.</summary>
        Int32 = 3,

        /// <summary>64-bit signed integer.</summary>
        Int64 = 4,

        /// <summary>32-bit floating point.</summary>
        Float = 9,

        /// <summary>64-bit floating point; every MTConnect SAMPLE is published as this.</summary>
        Double = 10,

        /// <summary>Boolean; used by the Node Control/Rebirth metric.</summary>
        Boolean = 11,

        /// <summary>UTF-8 string; every MTConnect EVENT and CONDITION is published as this.</summary>
        String = 12
    }
}

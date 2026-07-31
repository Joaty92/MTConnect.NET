// Copyright (c) 2025 MTConnect.NET fork contributors, All Rights Reserved.
// Licensed to you under the MIT license; see LICENSE.md in the repository root.

using MTConnect.SparkplugB.Payload;
using NUnit.Framework;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MTConnect.SparkplugB.Tests
{
    /// <summary>
    /// Pins the hand-written Sparkplug payload contract against an independent implementation
    /// of the same schema.
    ///
    /// The contract in Payload/ is written from the specification rather than generated from
    /// Eclipse's EPL-2.0 sparkplug_b.proto, so that this MIT repository stays MIT. The risk
    /// that buys is transcription error — a wrong field number produces bytes that encode
    /// cleanly, decode cleanly against our own contract, and are meaningless to every other
    /// Sparkplug implementation on the broker.
    ///
    /// Round-tripping against our own types would not catch that. These tests therefore encode
    /// with our contract and decode with SparkplugNet's ProtoBuf model, which was generated
    /// from the official schema. Agreement across two independently written contracts is
    /// strong evidence the field numbers are right; the Eclipse TCK then confirms it on a real
    /// broker.
    ///
    /// SparkplugNet is reached reflectively because its ProtoBuf model is internal. When the
    /// dependency is eventually dropped these tests go with it, and the TCK becomes the sole
    /// wire-format check.
    /// </summary>
    [TestFixture]
    public class SparkplugPayloadWireCompatibilityTests
    {
        private static readonly Type _referencePayloadType =
            typeof(SparkplugNet.VersionB.Data.Metric).Assembly
                .GetType("SparkplugNet.VersionB.ProtoBuf.ProtoBufPayload");

        [OneTimeSetUp]
        public void ReferenceModelIsAvailable()
        {
            Assert.That(_referencePayloadType, Is.Not.Null,
                "reference ProtoBuf model not found — SparkplugNet's internals may have moved");
        }

        [Test]
        public void Payload_seq_and_timestamp_survive_the_wire()
        {
            var decoded = RoundTrip(new SparkplugPayload
            {
                Timestamp = 1_785_412_345_678,
                Seq = 42,
                Metrics = { Metric("AVAILABILITY[avail]", SparkplugDataType.String, s: "AVAILABLE") }
            });

            Assert.That(Read<ulong>(decoded, "Timestamp"), Is.EqualTo(1_785_412_345_678UL));
            Assert.That(Read<ulong>(decoded, "Seq"), Is.EqualTo(42UL));
        }

        [Test]
        public void Double_metric_survives_the_wire()
        {
            var metric = FirstMetric(RoundTrip(new SparkplugPayload
            {
                Seq = 1,
                Metrics = { Metric("POSITION/ACTUAL[x]", SparkplugDataType.Double, d: 119.64) }
            }));

            Assert.That(Read<string>(metric, "Name"), Is.EqualTo("POSITION/ACTUAL[x]"));
            Assert.That(Convert.ToUInt32(Read<object>(metric, "DataType")), Is.EqualTo((uint)SparkplugDataType.Double));
            Assert.That(Read<double>(metric, "DoubleValue"), Is.EqualTo(119.64).Within(0.0001));
            Assert.That(Read<bool>(metric, "IsNull"), Is.False);
        }

        [Test]
        public void String_metric_survives_the_wire()
        {
            var metric = FirstMetric(RoundTrip(new SparkplugPayload
            {
                Seq = 2,
                Metrics = { Metric("EXECUTION[exec]", SparkplugDataType.String, s: "ACTIVE") }
            }));

            Assert.That(Convert.ToUInt32(Read<object>(metric, "DataType")), Is.EqualTo((uint)SparkplugDataType.String));
            Assert.That(Read<string>(metric, "StringValue"), Is.EqualTo("ACTIVE"));
        }

        [Test]
        public void Boolean_metric_survives_the_wire()
        {
            // The Node Control/Rebirth metric an NBIRTH must carry.
            var metric = FirstMetric(RoundTrip(new SparkplugPayload
            {
                Seq = 0,
                Metrics = { Metric("Node Control/Rebirth", SparkplugDataType.Boolean, b: false) }
            }));

            Assert.That(Convert.ToUInt32(Read<object>(metric, "DataType")), Is.EqualTo((uint)SparkplugDataType.Boolean));
            Assert.That(Read<bool>(metric, "BooleanValue"), Is.False);
        }

        [Test]
        public void IsNull_survives_the_wire_with_its_datatype_intact()
        {
            // The whole point of is_null: no value, but the metric keeps the Double type its
            // DBIRTH declared, so a consumer sees "no reading" rather than a fabricated 0.0.
            var metric = FirstMetric(RoundTrip(new SparkplugPayload
            {
                Seq = 3,
                Metrics = { Metric("LOAD[z_thrust]", SparkplugDataType.Double, isNull: true) }
            }));

            Assert.That(Read<bool>(metric, "IsNull"), Is.True);
            Assert.That(Convert.ToUInt32(Read<object>(metric, "DataType")), Is.EqualTo((uint)SparkplugDataType.Double));
        }

        [Test]
        public void Every_metric_carries_a_timestamp()
        {
            // tck-id-payloads-name-birth-data-requirement.
            var metric = FirstMetric(RoundTrip(new SparkplugPayload
            {
                Seq = 4,
                Metrics = { Metric("POSITION/ACTUAL[y]", SparkplugDataType.Double, d: 0.0) }
            }));

            Assert.That(Read<ulong>(metric, "Timestamp"), Is.EqualTo(1_785_412_000_000UL));
        }

        [Test]
        public void Genuine_zero_is_distinguishable_from_no_reading_on_the_wire()
        {
            var payload = RoundTrip(new SparkplugPayload
            {
                Seq = 5,
                Metrics =
                {
                    Metric("POSITION/ACTUAL[at-origin]", SparkplugDataType.Double, d: 0.0),
                    Metric("POSITION/ACTUAL[no-reading]", SparkplugDataType.Double, isNull: true)
                }
            });

            var metrics = ((IEnumerable)_referencePayloadType.GetProperty("Metrics").GetValue(payload))
                .Cast<object>().ToList();

            Assert.That(Read<bool>(metrics[0], "IsNull"), Is.False, "a real zero must not be flagged null");
            Assert.That(Read<double>(metrics[0], "DoubleValue"), Is.EqualTo(0.0));
            Assert.That(Read<bool>(metrics[1], "IsNull"), Is.True, "a missing reading must be flagged null");
        }


        private static SparkplugMetric Metric(
            string name, SparkplugDataType type,
            double? d = null, string s = null, bool? b = null, bool isNull = false)
        {
            var metric = new SparkplugMetric
            {
                Name = name,
                Timestamp = 1_785_412_000_000,
                DataType = (uint)type,
                IsNull = isNull ? true : (bool?)false
            };

            if (!isNull)
            {
                metric.DoubleValue = d;
                metric.StringValue = s;
                metric.BooleanValue = b;
            }

            return metric;
        }

        // Encode with our contract, decode with the reference model.
        private static object RoundTrip(SparkplugPayload payload)
        {
            using var stream = new MemoryStream();
            ProtoBuf.Serializer.Serialize(stream, payload);
            stream.Position = 0;
            return ProtoBuf.Serializer.Deserialize(_referencePayloadType, stream);
        }

        private static object FirstMetric(object referencePayload) =>
            ((IEnumerable)_referencePayloadType.GetProperty("Metrics").GetValue(referencePayload))
                .Cast<object>().First();

        private static T Read<T>(object instance, string property)
        {
            var value = instance.GetType().GetProperty(property)?.GetValue(instance);
            if (value == null) return default;
            return typeof(T) == typeof(object) ? (T)value : (T)Convert.ChangeType(value, typeof(T));
        }
    }
}

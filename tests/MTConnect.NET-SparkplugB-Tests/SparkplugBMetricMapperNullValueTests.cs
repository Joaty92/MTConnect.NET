// Copyright (c) 2025 MTConnect.NET fork contributors, All Rights Reserved.
// Licensed to you under the MIT license; see LICENSE.md in the repository root.

using MTConnect.Devices;
using MTConnect.Devices.DataItems;
using MTConnect.Observations;
using MTConnect.Observations.Output;
using NUnit.Framework;
using MTConnect.SparkplugB.Payload;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MTConnect.SparkplugB.Tests
{
    /// <summary>
    /// Pins how the mapper represents "no reading" on a numeric metric.
    ///
    /// The mapper previously emitted <c>0.0</c> for any SAMPLE it could not
    /// produce a number for — an unavailable observation, an absent value, or
    /// a value that would not parse. Sparkplug requires a metric's DataType to
    /// match its DBIRTH declaration, so a String "UNAVAILABLE" is not an
    /// option on a Double metric, and 0.0 looked like the only choice.
    ///
    /// It is not: Sparkplug carries "no value" out of band in the metric's
    /// is_null flag, and that is what these tests pin. The distinction is not
    /// cosmetic. An axis position, a spindle load, and a feedrate all reach a
    /// genuine zero in normal operation, so a fabricated 0.0 is
    /// indistinguishable from real data once it lands in a historian — and an
    /// adapter dropping its connection sets every DataItem unavailable, which
    /// under the old behaviour wrote a plausible-looking zero into every
    /// numeric trend on every disconnect.
    ///
    /// String-valued metrics keep MTConnect's own UNAVAILABLE literal: a
    /// String metric can carry it without lying about its type, and it is a
    /// state operators recognise and dashboards key on.
    /// </summary>
    [TestFixture]
    public class SparkplugBMetricMapperNullValueTests
    {
        [Test]
        public void ToMetric_unavailable_sample_is_null_and_keeps_double_type()
        {
            var metric = SparkplugBMetricMapper.ToMetric(
                Observation("POSITION", "Xact", DataItemCategory.SAMPLE, isUnavailable: true));

            Assert.That(metric.IsNull, Is.EqualTo(true), "an unavailable SAMPLE must not publish a fabricated value");
            Assert.That(metric.DataType, Is.EqualTo((uint)SparkplugDataType.Double), "DBIRTH declared Double; DDATA must keep matching it");
        }

        [Test]
        public void ToMetric_unparseable_sample_is_null_rather_than_zero()
        {
            // A non-numeric value on a SAMPLE is a modelling error. Reporting it as
            // missing data surfaces the problem; reporting 0.0 hides it as plausible.
            var metric = SparkplugBMetricMapper.ToMetric(
                Observation("POSITION", "Xact", DataItemCategory.SAMPLE, value: "not-a-number"));

            Assert.That(metric.IsNull, Is.EqualTo(true));
            Assert.That(metric.DataType, Is.EqualTo((uint)SparkplugDataType.Double));
        }

        [Test]
        public void ToMetric_sample_with_real_value_is_not_null()
        {
            var metric = SparkplugBMetricMapper.ToMetric(
                Observation("POSITION", "Xact", DataItemCategory.SAMPLE, value: "12.5"));

            Assert.That(metric.IsNull, Is.EqualTo(false));
            Assert.That(metric.DataType, Is.EqualTo((uint)SparkplugDataType.Double));
            Assert.That(metric.DoubleValue, Is.EqualTo(12.5));
        }

        [Test]
        public void ToMetric_sample_with_genuine_zero_is_not_null()
        {
            // The whole point of the change: a real zero must stay distinguishable
            // from no reading at all.
            var metric = SparkplugBMetricMapper.ToMetric(
                Observation("POSITION", "Xact", DataItemCategory.SAMPLE, value: "0"));

            Assert.That(metric.IsNull, Is.EqualTo(false));
            Assert.That(metric.DoubleValue, Is.EqualTo(0.0));
        }

        [Test]
        public void ToMetric_unavailable_event_keeps_unavailable_literal()
        {
            var metric = SparkplugBMetricMapper.ToMetric(
                Observation("EXECUTION", null, DataItemCategory.EVENT, isUnavailable: true));

            Assert.That(metric.IsNull, Is.EqualTo(false), "a String metric can carry UNAVAILABLE without lying about its type");
            Assert.That(metric.DataType, Is.EqualTo((uint)SparkplugDataType.String));
            Assert.That(metric.StringValue, Is.EqualTo(Observations.Observation.Unavailable));
        }

        [Test]
        public void BirthMetrics_for_never_reported_device_null_the_samples_and_keep_event_literal()
        {
            // A machine that has never connected births every metric from seeded
            // UNAVAILABLE observations. Under the old behaviour that told a historian
            // every axis was parked at the origin.
            var device = DeviceWith(
                DataItem("pos", "POSITION", DataItemCategory.SAMPLE),
                DataItem("exec", "EXECUTION", DataItemCategory.EVENT));

            var metrics = SparkplugBMetricMapper.BirthMetricsForDevice(device, currentObservations: null);

            var position = metrics.Single(m => m.Name.StartsWith("POSITION"));
            Assert.That(position.IsNull, Is.EqualTo(true));
            Assert.That(position.DataType, Is.EqualTo((uint)SparkplugDataType.Double));

            var execution = metrics.Single(m => m.Name.StartsWith("EXECUTION"));
            Assert.That(execution.IsNull, Is.EqualTo(false));
            Assert.That(execution.StringValue, Is.EqualTo(Observations.Observation.Unavailable));
        }

        [Test]
        public void BirthMetrics_carry_real_values_when_the_device_has_reported()
        {
            var device = DeviceWith(DataItem("pos", "POSITION", DataItemCategory.SAMPLE));

            var metrics = SparkplugBMetricMapper.BirthMetricsForDevice(
                device,
                new[] { Output("pos", "POSITION", DataItemCategory.SAMPLE, "42.75") });

            var position = metrics.Single();
            Assert.That(position.IsNull, Is.EqualTo(false));
            Assert.That(position.DoubleValue, Is.EqualTo(42.75));
        }

        [Test]
        public void BirthMetrics_and_DDATA_agree_on_metric_names()
        {
            // Sparkplug correlates DDATA against DBIRTH by exact metric name, so the
            // two code paths must never drift apart.
            var dataItem = DataItem("Xact", "POSITION", DataItemCategory.SAMPLE, subType: "ACTUAL");
            var device = DeviceWith(dataItem);

            var birthName = SparkplugBMetricMapper.BirthMetricsForDevice(device, null).Single().Name;
            var dataName = SparkplugBMetricMapper.ToMetric(
                Observation("POSITION", "ACTUAL", DataItemCategory.SAMPLE, value: "1", dataItemId: "Xact")).Name;

            Assert.That(dataName, Is.EqualTo(birthName));
        }


        private static IDataItem DataItem(string id, string type, DataItemCategory category, string subType = null)
        {
            return new DataItem
            {
                Id = id,
                Type = type,
                SubType = subType,
                Category = category,
                Representation = DataItemRepresentation.VALUE
            };
        }

        private static IDevice DeviceWith(params IDataItem[] dataItems)
        {
            return new Device
            {
                Id = "Machine-01",
                Uuid = "Machine-01",
                Name = "Machine-01",
                DataItems = dataItems
            };
        }

        private static IObservation Observation(
            string type,
            string subType,
            DataItemCategory category,
            string value = null,
            bool isUnavailable = false,
            string dataItemId = "di-1")
        {
            return new ObservationStub(type, subType, category, value, isUnavailable, dataItemId);
        }

        private static IObservationOutput Output(
            string dataItemId, string type, DataItemCategory category, string value)
        {
            return new ObservationOutputStub(dataItemId, type, category, value);
        }


        private sealed class ObservationStub : IObservation
        {
            private readonly string _value;

            public ObservationStub(
                string type, string subType, DataItemCategory category,
                string value, bool isUnavailable, string dataItemId)
            {
                Type = type;
                SubType = subType;
                Category = category;
                _value = value;
                IsUnavailable = isUnavailable;
                DataItemId = dataItemId;
            }

            public string Uuid => DataItemId;
            public MTConnectEntityType EntityType => MTConnectEntityType.Observation;
            public string DeviceUuid => "Machine-01";
            public IDataItem DataItem => null;
            public DataItemCategory Category { get; }
            public string Type { get; }
            public string SubType { get; }
            public string DataItemId { get; }
            public string Name => null;
            public ulong InstanceId => 0UL;
            public ulong Sequence => 0UL;
            public DateTime Timestamp => new DateTime(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);
            public string CompositionId => null;
            public DataItemRepresentation Representation => DataItemRepresentation.VALUE;
            public Quality Quality => Quality.VALID;
            public bool Deprecated => false;
            public bool Extended => false;
            public bool IsUnavailable { get; }

            public IEnumerable<ObservationValue> Values => _value != null
                ? new[] { new ObservationValue(ValueKeys.Result, _value) }
                : Array.Empty<ObservationValue>();

            public string GetValue(string valueKey) =>
                valueKey == ValueKeys.Result ? _value : null;

            public ValidationResult Validate() => new ValidationResult(true);
        }

        private sealed class ObservationOutputStub : IObservationOutput
        {
            private readonly string _value;

            public ObservationOutputStub(
                string dataItemId, string type, DataItemCategory category, string value)
            {
                DataItemId = dataItemId;
                Type = type;
                Category = category;
                _value = value;
            }

            public string DeviceUuid => "Machine-01";
            public IDataItem DataItem => null;
            public string DataItemId { get; }
            public DataItemCategory Category { get; }
            public string Type { get; }
            public string SubType => null;
            public string Name => null;
            public ulong InstanceId => 0UL;
            public ulong Sequence => 0UL;
            public DateTime Timestamp => new DateTime(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);
            public DateTimeOffset TimeZoneTimestamp => DateTimeOffset.UtcNow;
            public string CompositionId => null;
            public DataItemRepresentation Representation => DataItemRepresentation.VALUE;
            public Quality Quality => Quality.VALID;
            public bool Deprecated => false;
            public bool Extended => false;

            public ObservationValue[] Values => _value != null
                ? new[] { new ObservationValue(ValueKeys.Result, _value) }
                : Array.Empty<ObservationValue>();

            public string GetValue(string valueKey) =>
                valueKey == ValueKeys.Result ? _value : null;
        }
    }
}

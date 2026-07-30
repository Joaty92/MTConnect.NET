// Copyright (c) 2025 MTConnect.NET fork contributors, All Rights Reserved.
// Licensed to you under the MIT license; see LICENSE.md in the repository root.

using MTConnect.Devices;
using MTConnect.Devices.DataItems;
using MTConnect.Observations;
using MTConnect.Observations.Output;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace MTConnect.SparkplugB.Tests
{
    /// <summary>
    /// Pins the three-way availability resolution the relay uses to decide
    /// whether a device may stream DDATA.
    ///
    /// The relay used to derive this as a boolean: find the AVAILABILITY
    /// observation, compare it to "AVAILABLE". A device model that declares no
    /// AVAILABILITY DataItem at all produced a null observation, which compared
    /// unequal, which meant "offline" — so such a device was born, immediately
    /// DDEATH-ed, and then had every one of its observations dropped forever,
    /// with nothing logged to explain the silence.
    ///
    /// "Not declared" and "declared but not AVAILABLE" need opposite handling,
    /// so the resolution has to be three-way. The MTConnect standard requires a
    /// Device to carry AVAILABILITY, so a missing one is a malformed device
    /// file — but the relay's job is to say so, not to silently discard data.
    /// </summary>
    [TestFixture]
    public class SparkplugBAvailabilityTests
    {
        [Test]
        public void Resolve_returns_Available_when_device_reports_AVAILABLE()
        {
            var state = SparkplugBAvailability.Resolve(
                DeviceWithAvailability(),
                new[] { AvailabilityObservation("AVAILABLE") });

            Assert.That(state, Is.EqualTo(SparkplugBAvailabilityState.Available));
        }

        [Test]
        public void Resolve_returns_Unavailable_when_device_reports_UNAVAILABLE()
        {
            var state = SparkplugBAvailability.Resolve(
                DeviceWithAvailability(),
                new[] { AvailabilityObservation("UNAVAILABLE") });

            Assert.That(state, Is.EqualTo(SparkplugBAvailabilityState.Unavailable));
        }

        [Test]
        public void Resolve_returns_Unavailable_when_a_declared_device_has_not_reported_yet()
        {
            // A machine that is wired up but switched off: the DataItem exists, so the
            // relay can legitimately say it is offline.
            var state = SparkplugBAvailability.Resolve(DeviceWithAvailability(), currentObservations: null);

            Assert.That(state, Is.EqualTo(SparkplugBAvailabilityState.Unavailable));
        }

        [Test]
        public void Resolve_returns_NotDeclared_when_the_device_model_omits_AVAILABILITY()
        {
            // The regression this guard exists for: previously indistinguishable from
            // Unavailable, which sent the device permanently dark.
            var state = SparkplugBAvailability.Resolve(
                DeviceWithoutAvailability(), currentObservations: null);

            Assert.That(state, Is.EqualTo(SparkplugBAvailabilityState.NotDeclared));
        }

        [Test]
        public void Resolve_returns_NotDeclared_even_when_other_observations_exist()
        {
            var state = SparkplugBAvailability.Resolve(
                DeviceWithoutAvailability(),
                new[] { Observation("exec", "EXECUTION", "ACTIVE") });

            Assert.That(state, Is.EqualTo(SparkplugBAvailabilityState.NotDeclared));
        }

        [Test]
        public void Resolve_is_case_insensitive_on_the_availability_value()
        {
            var state = SparkplugBAvailability.Resolve(
                DeviceWithAvailability(),
                new[] { AvailabilityObservation("available") });

            Assert.That(state, Is.EqualTo(SparkplugBAvailabilityState.Available));
        }

        [Test]
        public void Resolve_returns_NotDeclared_for_a_null_device()
        {
            Assert.That(
                SparkplugBAvailability.Resolve(null, null),
                Is.EqualTo(SparkplugBAvailabilityState.NotDeclared));
        }

        [Test]
        public void Resolve_finds_AVAILABILITY_declared_on_a_nested_component()
        {
            // GetDataItems() walks the whole component tree, so the scan must too.
            var device = new Device
            {
                Id = "Machine-01",
                Uuid = "Machine-01",
                Name = "Machine-01",
                Components = new IComponent[]
                {
                    new Devices.Components.ControllerComponent
                    {
                        Id = "Controller",
                        Name = "cn",
                        DataItems = new IDataItem[] { AvailabilityDataItemFor("nested_avail") }
                    }
                }
            };

            var state = SparkplugBAvailability.Resolve(
                device,
                new[] { AvailabilityObservation("AVAILABLE") });

            Assert.That(state, Is.EqualTo(SparkplugBAvailabilityState.Available));
        }

        [TestCase("AVAILABLE", true)]
        [TestCase("available", true)]
        [TestCase("UNAVAILABLE", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsAvailableValue_matches_only_AVAILABLE(string value, bool expected)
        {
            Assert.That(SparkplugBAvailability.IsAvailableValue(value), Is.EqualTo(expected));
        }


        private static IDataItem AvailabilityDataItemFor(string id) => new DataItem
        {
            Id = id,
            Type = AvailabilityDataItem.TypeId,
            Category = DataItemCategory.EVENT,
            Representation = DataItemRepresentation.VALUE
        };

        private static IDevice DeviceWithAvailability() => new Device
        {
            Id = "Machine-01",
            Uuid = "Machine-01",
            Name = "Machine-01",
            DataItems = new[] { AvailabilityDataItemFor("avail") }
        };

        private static IDevice DeviceWithoutAvailability() => new Device
        {
            Id = "Machine-01",
            Uuid = "Machine-01",
            Name = "Machine-01",
            DataItems = new IDataItem[]
            {
                new DataItem
                {
                    Id = "exec",
                    Type = "EXECUTION",
                    Category = DataItemCategory.EVENT,
                    Representation = DataItemRepresentation.VALUE
                }
            }
        };

        private static IObservationOutput AvailabilityObservation(string value) =>
            Observation("avail", AvailabilityDataItem.TypeId, value);

        private static IObservationOutput Observation(string dataItemId, string type, string value) =>
            new ObservationOutputStub(dataItemId, type, value);


        private sealed class ObservationOutputStub : IObservationOutput
        {
            private readonly string _value;

            public ObservationOutputStub(string dataItemId, string type, string value)
            {
                DataItemId = dataItemId;
                Type = type;
                _value = value;
            }

            public string DeviceUuid => "Machine-01";
            public IDataItem DataItem => null;
            public string DataItemId { get; }
            public DataItemCategory Category => DataItemCategory.EVENT;
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

            public ObservationValue[] Values => new[] { new ObservationValue(ValueKeys.Result, _value) };

            public string GetValue(string valueKey) =>
                valueKey == ValueKeys.Result ? _value : null;
        }
    }
}

// Copyright (c) 2026 TrakHound Inc., All Rights Reserved.
// TrakHound Inc. licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using MTConnect.Agents;
using MTConnect.Devices;
using MTConnect.Input;
using NUnit.Framework;

namespace MTConnect.Tests.Common.Regressions
{
    /// <summary>
    /// Pins that a DataItem Id reused by two Devices resolves to the DataItem belonging to the
    /// Device being asked about, rather than to whichever Device happened to be looked up first.
    ///
    /// <see cref="MTConnectAgent"/> caches resolved DataItems so repeat observations skip the
    /// per-Device tree walk. That cache was keyed by DataItemId alone while its contract was
    /// documented as DeviceUuid:DataItemId, so the first Device to observe a given Id claimed the
    /// entry for every Device. The second Device then resolved to the first Device's IDataItem —
    /// including its Device back-reference, which consumers such as the MQTT entity topic builder
    /// use to decide which Device an observation belongs to. The result was one Device's values
    /// being published under another Device's identity.
    ///
    /// The first lookup for a key takes the uncached path and was always correct, so every
    /// assertion here resolves at least twice: the regression only appears on the cached path.
    /// </summary>
    [TestFixture]
    public class CrossDeviceDataItemIdRegressionTests
    {
        private const string SharedDataItemId = "shared_id";

        private static Device CreateDevice(string uuid, string dataItemId, string dataItemType = "LENGTH")
        {
            return new Device
            {
                Id = uuid,
                Uuid = uuid,
                Name = uuid,
                Type = Device.TypeId,
                DataItems = new List<IDataItem>
                {
                    new DataItem(DataItemCategory.SAMPLE, dataItemType, null, dataItemId),
                },
            };
        }

        private static MTConnectAgent CreateAgentWithBothDevices()
        {
            var agent = new MTConnectAgent(uuid: "test-agent", initializeAgentDevice: false);
            agent.AddDevice(CreateDevice("device-1", SharedDataItemId), initializeDataItems: false);
            agent.AddDevice(CreateDevice("device-2", SharedDataItemId), initializeDataItems: false);
            return agent;
        }

        /// <summary>Pins that a shared DataItem Id resolves per Device on the cached lookup path.</summary>
        /// <param name="deviceUuid">The Device whose DataItem should be returned.</param>
        [TestCase("device-1")]
        [TestCase("device-2")]
        public void GetDataItem_returns_the_requested_devices_dataItem_when_the_id_is_shared(string deviceUuid)
        {
            using var agent = CreateAgentWithBothDevices();

            // Warm both Devices so the cache is populated by the other Device first.
            agent.GetDataItem("device-1", SharedDataItemId);
            agent.GetDataItem("device-2", SharedDataItemId);

            var dataItem = agent.GetDataItem(deviceUuid, SharedDataItemId);

            Assert.That(dataItem, Is.Not.Null, "A shared DataItem Id must still resolve for each Device.");
            Assert.That(dataItem!.Id, Is.EqualTo(SharedDataItemId));
            Assert.That(dataItem.Device, Is.Not.Null, "The resolved DataItem must carry its Device back-reference.");
            Assert.That(dataItem.Device!.Uuid, Is.EqualTo(deviceUuid),
                "The cached lookup returned a DataItem owned by a different Device, so consumers keying off " +
                "DataItem.Device would attribute this observation to the wrong Device.");
        }

        /// <summary>Pins that the two Devices resolve to genuinely distinct DataItem instances.</summary>
        [Test]
        public void GetDataItem_returns_distinct_instances_for_each_device_sharing_an_id()
        {
            using var agent = CreateAgentWithBothDevices();

            // Resolve twice each: the first call per key bypasses the cache.
            agent.GetDataItem("device-1", SharedDataItemId);
            agent.GetDataItem("device-2", SharedDataItemId);

            var first = agent.GetDataItem("device-1", SharedDataItemId);
            var second = agent.GetDataItem("device-2", SharedDataItemId);

            Assert.That(first, Is.Not.SameAs(second),
                "Each Device must resolve to its own DataItem instance, not a single shared one.");
        }

        /// <summary>Pins that observations on a shared Id stay attributed to the reporting Device.</summary>
        [Test]
        public void AddObservation_attributes_a_shared_dataItem_id_to_the_reporting_device()
        {
            // A broker rather than a bare agent: MTConnectAgent.OnAddObservation is a no-op
            // stub returning 0, so only the broker's observation buffer raises ObservationAdded.
            using var agent = new MTConnectAgentBroker(uuid: "test-agent", initializeAgentDevice: false);
            agent.AddDevice(CreateDevice("device-1", SharedDataItemId), initializeDataItems: false);
            agent.AddDevice(CreateDevice("device-2", SharedDataItemId), initializeDataItems: false);

            var observed = new List<(string DeviceUuid, string? DataItemDeviceUuid)>();
            agent.ObservationAdded += (_, observation) =>
                observed.Add((observation.DeviceUuid, observation.DataItem?.Device?.Uuid));

            // Two observations per Device: the second travels the cached path.
            foreach (var deviceUuid in new[] { "device-1", "device-2", "device-1", "device-2" })
            {
                agent.AddObservation(deviceUuid, new ObservationInput(SharedDataItemId, "1.0"), forceUpdate: true);
            }

            Assert.That(observed, Is.Not.Empty, "Observations must reach the ObservationAdded subscribers.");
            foreach (var entry in observed)
            {
                Assert.That(entry.DataItemDeviceUuid, Is.EqualTo(entry.DeviceUuid),
                    "The observation's DataItem must belong to the Device the observation was reported for.");
            }

            Assert.That(observed.Select(o => o.DeviceUuid).Distinct().Count(), Is.EqualTo(2),
                "Both Devices must produce observations under their own identity.");
        }

        /// <summary>Pins that replacing a Device model drops its stale cached DataItem instances.</summary>
        [Test]
        public void GetDataItem_resolves_against_the_new_model_after_a_device_is_replaced()
        {
            using var agent = new MTConnectAgent(uuid: "test-agent", initializeAgentDevice: false);
            agent.AddDevice(CreateDevice("device-1", SharedDataItemId), initializeDataItems: false);

            // Populate the cache against the original model.
            var original = agent.GetDataItem("device-1", SharedDataItemId);
            agent.GetDataItem("device-1", SharedDataItemId);
            Assert.That(original, Is.Not.Null);

            Assert.That(original!.Type, Is.EqualTo("LENGTH"));

            // Replace the Device with a model that redefines the same DataItem Id.
            agent.AddDevice(CreateDevice("device-1", SharedDataItemId, "TEMPERATURE"), initializeDataItems: false);

            var resolved = agent.GetDataItem("device-1", SharedDataItemId);

            Assert.That(resolved, Is.Not.Null, "The DataItem must still resolve after the Device is replaced.");
            Assert.That(resolved!.Type, Is.EqualTo("TEMPERATURE"),
                "A stale cache entry kept the DataItem pointing at the previous Device model.");
        }
    }
}
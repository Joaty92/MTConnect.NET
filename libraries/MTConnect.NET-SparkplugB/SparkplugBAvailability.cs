// Copyright (c) 2025 MTConnect.NET fork contributors, All Rights Reserved.
// Licensed to you under the MIT license; see LICENSE.md in the repository root.

using MTConnect.Devices;
using MTConnect.Devices.DataItems;
using MTConnect.Observations;
using MTConnect.Observations.Events;
using MTConnect.Observations.Output;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MTConnect.SparkplugB
{
    /// <summary>How a device's MTConnect <c>AVAILABILITY</c> resolves into a Sparkplug device state.</summary>
    public enum SparkplugBAvailabilityState
    {
        /// <summary>The device declares <c>AVAILABILITY</c> and currently reports <c>AVAILABLE</c>; DDATA may flow.</summary>
        Available,

        /// <summary>The device declares <c>AVAILABILITY</c> and does not currently report <c>AVAILABLE</c>; DDEATH is published and DDATA suppressed.</summary>
        Unavailable,

        /// <summary>The device model declares no <c>AVAILABILITY</c> DataItem at all, so availability cannot be observed either way.</summary>
        NotDeclared
    }

    /// <summary>Resolves the Sparkplug device state a device should be in, from its device model and current observations.</summary>
    public static class SparkplugBAvailability
    {
        /// <summary>
        /// Classifies <paramref name="device"/> as <see cref="SparkplugBAvailabilityState.Available"/>,
        /// <see cref="SparkplugBAvailabilityState.Unavailable"/>, or
        /// <see cref="SparkplugBAvailabilityState.NotDeclared"/>.
        /// <para>
        /// The three-way result exists because "not declared" and "declared but not AVAILABLE"
        /// call for opposite handling. Collapsing them to a boolean is what made a device model
        /// missing its <c>AVAILABILITY</c> DataItem go permanently dark: it can never report
        /// <c>AVAILABLE</c>, so it stayed unborn, emitting a DDEATH after every DBIRTH and never
        /// a single DDATA, with nothing logged to explain it.
        /// </para>
        /// </summary>
        /// <param name="device">Device whose model is inspected for an <c>AVAILABILITY</c> DataItem.</param>
        /// <param name="currentObservations">Current observations for the device, as returned by the agent broker; may be null.</param>
        public static SparkplugBAvailabilityState Resolve(
            IDevice device,
            IEnumerable<IObservationOutput> currentObservations)
        {
            var declaresAvailability = device?.GetDataItems()?
                .Any(dataItem => dataItem?.Type == AvailabilityDataItem.TypeId) ?? false;

            if (!declaresAvailability) return SparkplugBAvailabilityState.NotDeclared;

            var availabilityObservation = currentObservations?
                .FirstOrDefault(observation => observation?.Type == AvailabilityDataItem.TypeId);

            return IsAvailableValue(availabilityObservation?.GetValue(ValueKeys.Result))
                ? SparkplugBAvailabilityState.Available
                : SparkplugBAvailabilityState.Unavailable;
        }

        // nameof keeps this a compile-time constant — no per-call enum formatting —
        // while still failing the build if the enum member is ever renamed.
        private const string AvailableValue = nameof(Availability.AVAILABLE);

        /// <summary>Returns whether an <c>AVAILABILITY</c> observation value means the machine is available.</summary>
        /// <param name="value">Raw observation result, or null when unavailable or absent.</param>
        public static bool IsAvailableValue(string value) =>
            string.Equals(value, AvailableValue, StringComparison.OrdinalIgnoreCase);
    }
}

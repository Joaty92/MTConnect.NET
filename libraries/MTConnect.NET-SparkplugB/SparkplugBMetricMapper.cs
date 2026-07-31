// Copyright (c) 2025 MTConnect.NET fork contributors, All Rights Reserved.
// Licensed to you under the MIT license; see LICENSE.md in the repository root.

using MTConnect.Devices;
using MTConnect.Devices.DataItems;
using MTConnect.Observations;
using MTConnect.Observations.Output;
using MTConnect.SparkplugB.Payload;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MTConnect.SparkplugB
{
    /// <summary>Translates MTConnect <see cref="IObservation"/> and <see cref="IDataItem"/> instances into Sparkplug B metrics.</summary>
    public static class SparkplugBMetricMapper
    {
        /// <summary>Converts an MTConnect observation into a Sparkplug B metric; scalar SAMPLEs are coerced to <see cref="SparkplugDataType.Double"/> to match the DBIRTH declaration — publishing <c>is_null</c> rather than a value when there is no reading — while EVENTs, CONDITIONs (emitted as their level) and the set-valued representations (serialised as <c>[key]=value</c> pairs) are emitted as <see cref="SparkplugDataType.String"/>.</summary>
        /// <param name="observation">The observation to convert.</param>
        public static SparkplugMetric ToMetric(IObservation observation)
        {
            var name = BuildObservationMetricName(observation);
            // Observation.Timestamp is Kind=Utc (the agent buffer round-trips it through unix
            // ticks), so treating it as a zero-offset instant is always correct here.
            var timestamp = ToUnixMilliseconds(observation.Timestamp);

            var isDouble = IsDoubleMetric(observation.Category, observation.Representation);

            if (observation.IsUnavailable) return UnavailableMetric(name, isDouble, timestamp);

            var raw = ExtractValue(observation.Category, observation.Representation, observation.Values);
            if (raw == null) return UnavailableMetric(name, isDouble, timestamp);

            if (isDouble)
            {
                // Always Double for scalar SAMPLEs to match the DBIRTH declaration. A value that
                // will not parse is a modelling error, not a reading of zero — publish is_null so
                // it surfaces as missing data instead of masquerading as a plausible 0.0.
                return double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
                    ? DoubleMetric(name, d, timestamp)
                    : NullMetric(name, SparkplugDataType.Double, timestamp);
            }

            return StringMetric(name, raw, timestamp);
        }

        // DBIRTH declares a DataType per metric and DDATA must keep matching it, so this single
        // rule decides Double-vs-String for both paths. Only scalar SAMPLEs are numeric:
        // CONDITIONs carry a level string, and the set-valued representations are serialised as text.
        private static bool IsDoubleMetric(DataItemCategory category, DataItemRepresentation representation) =>
            category == DataItemCategory.SAMPLE
            && representation != DataItemRepresentation.DATA_SET
            && representation != DataItemRepresentation.TABLE
            && representation != DataItemRepresentation.TIME_SERIES;

        // Numeric and string metrics express "no reading" differently, and the asymmetry is
        // deliberate. A Double metric has no in-band way to say "unavailable", so it publishes
        // Sparkplug's out-of-band is_null flag — sending 0.0 instead would land in a historian as a
        // genuine zero, which an axis position or a spindle load reaching zero is indistinguishable
        // from. A String metric can carry MTConnect's own UNAVAILABLE literal without lying, and
        // that literal is a state operators recognise and dashboards key on, so it is kept.
        private static SparkplugMetric UnavailableMetric(string name, bool isDouble, ulong timestamp) =>
            isDouble
                ? NullMetric(name, SparkplugDataType.Double, timestamp)
                : StringMetric(name, Observation.Unavailable, timestamp);

        private static SparkplugMetric DoubleMetric(string name, double value, ulong timestamp) => new()
        {
            Name = name,
            Timestamp = timestamp,
            DataType = (uint)SparkplugDataType.Double,
            IsNull = false,
            DoubleValue = value
        };

        private static SparkplugMetric StringMetric(string name, string value, ulong timestamp) => new()
        {
            Name = name,
            Timestamp = timestamp,
            DataType = (uint)SparkplugDataType.String,
            IsNull = false,
            StringValue = value
        };

        // Sparkplug carries "no value" out of band in the metric's is_null flag, leaving the
        // declared DataType intact so the DBIRTH type contract still holds.
        private static SparkplugMetric NullMetric(string name, SparkplugDataType dataType, ulong timestamp) => new()
        {
            Name = name,
            Timestamp = timestamp,
            DataType = (uint)dataType,
            IsNull = true
        };

        // Each Category/Representation stores its state under a different value key. Reading
        // ValueKeys.Result unconditionally returns null for CONDITIONs — whose level lives in
        // ValueKeys.Level — and for the set-valued representations, which collapsed every alarm and
        // data set to a permanent "UNAVAILABLE" downstream.
        private static string ExtractValue(
            DataItemCategory category,
            DataItemRepresentation representation,
            IEnumerable<ObservationValue> values)
        {
            if (category == DataItemCategory.CONDITION) return GetValue(values, ValueKeys.Level);

            switch (representation)
            {
                case DataItemRepresentation.DATA_SET: return FormatEntries(values, ValueKeys.DataSetPrefix);
                case DataItemRepresentation.TABLE: return FormatEntries(values, ValueKeys.TablePrefix);
                case DataItemRepresentation.TIME_SERIES: return FormatEntries(values, ValueKeys.TimeSeriesPrefix);
                default: return GetValue(values, ValueKeys.Result);
            }
        }

        private static string GetValue(IEnumerable<ObservationValue> values, string valueKey)
        {
            if (values == null) return null;
            foreach (var value in values)
            {
                if (value.Key == valueKey) return value.Value;
            }
            return null;
        }

        // Serialises the keyed entries of a set-valued observation as "[key]=value" pairs. Ordered
        // by key so identical content always produces an identical string — downstream consumers
        // compare metric values to detect change.
        private static string FormatEntries(IEnumerable<ObservationValue> values, string keyPrefix)
        {
            if (values == null) return null;

            var entries = values
                .Where(v => !string.IsNullOrEmpty(v.Key) && v.Key.StartsWith(keyPrefix, StringComparison.Ordinal))
                .OrderBy(v => v.Key, StringComparer.Ordinal)
                .Select(v => $"{v.Key.Substring(keyPrefix.Length)}={v.Value}")
                .ToList();

            return entries.Count > 0 ? string.Join(";", entries) : null;
        }

        /// <summary>Builds the Sparkplug metric name from an observation as <c>Type[/SubType][[DataItemId]]</c>.</summary>
        /// <param name="observation">The observation to name.</param>
        public static string BuildObservationMetricName(IObservation observation) =>
            BuildMetricName(observation.Type, observation.SubType, observation.DataItemId);

        /// <summary>Builds the Sparkplug metric name from a data item as <c>Type[/SubType][[Id]]</c>; used to declare the metric set in DBIRTH.</summary>
        /// <param name="dataItem">The data item to name.</param>
        public static string BuildDataItemMetricName(IDataItem dataItem) =>
            BuildMetricName(dataItem.Type, dataItem.SubType, dataItem.Id);

        // Single source of truth: DDATA metrics correlate with DBIRTH declarations by exact name,
        // so the observation-side and dataitem-side names must be byte-identical. Keep every
        // formatting decision in this one method.
        private static string BuildMetricName(string type, string subType, string id)
        {
            var name = type;
            if (!string.IsNullOrEmpty(subType))
                name += $"/{subType}";
            if (!string.IsNullOrEmpty(id))
                name += $"[{id}]";
            return name;
        }

        /// <summary>
        /// Builds DBIRTH metrics. Sparkplug requires birth values to reflect current device state,
        /// so <paramref name="currentObservations"/> from the agent buffer are used when available.
        /// </summary>
        /// <param name="device">Device whose full metric set is declared.</param>
        /// <param name="currentObservations">Current observations for the device, or null.</param>
        public static List<SparkplugMetric> BirthMetricsForDevice(
            IDevice device,
            IEnumerable<IObservationOutput> currentObservations = null)
        {
            // Index current observations by DataItemId for O(1) lookup
            var byId = currentObservations?
                .Where(o => o.DataItemId != null)
                .GroupBy(o => o.DataItemId)
                .ToDictionary(g => g.Key, g => g.First())
                ?? new Dictionary<string, IObservationOutput>();

            var metrics = new List<SparkplugMetric>();
            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            foreach (var di in device.GetDataItems())
            {
                var metricName = BuildDataItemMetricName(di);
                byId.TryGetValue(di.Id ?? "", out var obs);
                var rawValue = obs != null ? ExtractValue(di.Category, di.Representation, obs.Values) : null;

                // Carry each observation's real timestamp into the DBIRTH metric. Stamping
                // everything 'now' would fabricate a fresh-data event for stale values in
                // downstream historians at every (re)birth/reconnect.
                var ts = obs != null ? ToUnixMilliseconds(obs.Timestamp) : now;
                var hasValue = rawValue != null
                    && !string.Equals(rawValue, Observation.Unavailable, StringComparison.OrdinalIgnoreCase);

                if (IsDoubleMetric(di.Category, di.Representation))
                {
                    // A device that has never reported births every one of its SAMPLEs here.
                    // Declaring them 0.0 would tell a historian that every axis is parked at the
                    // origin and every load is zero; is_null says there is no reading, which is
                    // what is actually true.
                    metrics.Add(hasValue && double.TryParse(
                            rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
                        ? DoubleMetric(metricName, d, ts)
                        : NullMetric(metricName, SparkplugDataType.Double, ts));
                }
                else
                {
                    metrics.Add(StringMetric(metricName, hasValue ? rawValue : Observation.Unavailable, ts));
                }
            }

            return metrics;
        }

        private static ulong ToUnixMilliseconds(DateTime timestamp) =>
            (ulong)new DateTimeOffset(timestamp, TimeSpan.Zero).ToUnixTimeMilliseconds();
    }
}

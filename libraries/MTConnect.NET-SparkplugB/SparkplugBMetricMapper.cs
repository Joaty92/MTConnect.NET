// Copyright (c) 2025 MTConnect.NET fork contributors, All Rights Reserved.
// Licensed to you under the MIT license; see LICENSE.md in the repository root.

using MTConnect.Devices;
using MTConnect.Devices.DataItems;
using MTConnect.Observations;
using MTConnect.Observations.Output;
using SparkplugNet.VersionB.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MTConnect.SparkplugB
{
    /// <summary>Translates MTConnect <see cref="IObservation"/> and <see cref="IDataItem"/> instances into Sparkplug B <see cref="Metric"/> payloads.</summary>
    public static class SparkplugBMetricMapper
    {
        /// <summary>Converts an MTConnect observation into a Sparkplug B <see cref="Metric"/>; scalar SAMPLEs are coerced to <see cref="DataType.Double"/> to match the DBIRTH declaration, while EVENTs, CONDITIONs (emitted as their level) and the set-valued representations (serialised as <c>[key]=value</c> pairs) are emitted as <see cref="DataType.String"/>.</summary>
        public static Metric ToMetric(IObservation observation)
        {
            var name = BuildObservationMetricName(observation);
            // Observation.Timestamp is Kind=Utc (the agent buffer round-trips it
            // through unix ticks), so a zero offset is always correct here.
            var timestamp = new DateTimeOffset(observation.Timestamp, TimeSpan.Zero);

            var isDouble = IsDoubleMetric(observation.Category, observation.Representation);

            if (observation.IsUnavailable) return UnavailableMetric(name, isDouble, timestamp);

            var raw = ExtractValue(observation.Category, observation.Representation, observation.Values);
            if (raw == null) return UnavailableMetric(name, isDouble, timestamp);

            if (isDouble)
            {
                // Always return Double for scalar SAMPLEs to match DBIRTH declaration
                return double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
                    ? new Metric(name, DataType.Double, d, timestamp)
                    : new Metric(name, DataType.Double, 0.0, timestamp);
            }

            return new Metric(name, DataType.String, raw, timestamp);
        }

        // DBIRTH declares a DataType per metric and DDATA must keep matching it, so this
        // single rule decides Double-vs-String for both paths. Only scalar SAMPLEs are
        // numeric: CONDITIONs carry a level string, and the set-valued representations are
        // serialised as text.
        private static bool IsDoubleMetric(DataItemCategory category, DataItemRepresentation representation) =>
            category == DataItemCategory.SAMPLE
            && representation != DataItemRepresentation.DATA_SET
            && representation != DataItemRepresentation.TABLE
            && representation != DataItemRepresentation.TIME_SERIES;

        private static Metric UnavailableMetric(string name, bool isDouble, DateTimeOffset timestamp) =>
            isDouble
                ? new Metric(name, DataType.Double, 0.0, timestamp)
                : new Metric(name, DataType.String, "UNAVAILABLE", timestamp);

        // Each Category/Representation stores its state under a different value key.
        // Reading ValueKeys.Result unconditionally returns null for CONDITIONs — whose
        // level lives in ValueKeys.Level — and for the set-valued representations, which
        // collapsed every alarm and data set to a permanent "UNAVAILABLE" downstream.
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

        // Serialises the keyed entries of a set-valued observation as "[key]=value" pairs.
        // Ordered by key so identical content always produces an identical string —
        // downstream consumers compare metric values to detect change.
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
        public static string BuildObservationMetricName(IObservation observation) =>
            BuildMetricName(observation.Type, observation.SubType, observation.DataItemId);

        /// <summary>Builds the Sparkplug metric name from a data item as <c>Type[/SubType][[Id]]</c>; used to declare the metric set in DBIRTH.</summary>
        public static string BuildDataItemMetricName(IDataItem dataItem) =>
            BuildMetricName(dataItem.Type, dataItem.SubType, dataItem.Id);

        // Single source of truth: DDATA metrics correlate with DBIRTH declarations
        // by exact name, so the observation-side and dataitem-side names must be
        // byte-identical. Keep every formatting decision in this one method.
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
        /// Builds DBIRTH metrics. Per SPB spec, values MUST reflect current device state.
        /// currentObservations (from agent) are used when available; falls back to 0.0/UNAVAILABLE.
        /// </summary>
        public static List<Metric> BirthMetricsForDevice(
            IDevice device,
            IEnumerable<IObservationOutput> currentObservations = null)
        {
            // Index current observations by DataItemId for O(1) lookup
            var byId = currentObservations?
                .Where(o => o.DataItemId != null)
                .GroupBy(o => o.DataItemId)
                .ToDictionary(g => g.Key, g => g.First())
                ?? new Dictionary<string, IObservationOutput>();

            var metrics = new List<Metric>();
            var now = DateTimeOffset.UtcNow;

            foreach (var di in device.GetDataItems())
            {
                var metricName = BuildDataItemMetricName(di);
                byId.TryGetValue(di.Id ?? "", out var obs);
                var rawValue = obs != null ? ExtractValue(di.Category, di.Representation, obs.Values) : null;

                // Carry each observation's real timestamp into the DBIRTH metric.
                // Stamping everything 'now' would fabricate a fresh-data event for
                // stale values in downstream historians at every (re)birth/reconnect.
                var ts = obs != null ? new DateTimeOffset(obs.Timestamp, TimeSpan.Zero) : now;
                var hasValue = rawValue != null
                    && !string.Equals(rawValue, "UNAVAILABLE", StringComparison.OrdinalIgnoreCase);

                Metric metric;
                if (IsDoubleMetric(di.Category, di.Representation))
                {
                    var value = 0.0;
                    if (hasValue && double.TryParse(
                            rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                        value = d;
                    metric = new Metric(metricName, DataType.Double, value, ts);
                }
                else
                {
                    var value = hasValue ? rawValue : "UNAVAILABLE";
                    metric = new Metric(metricName, DataType.String, value, ts);
                }

                metrics.Add(metric);
            }

            return metrics;
        }
    }
}

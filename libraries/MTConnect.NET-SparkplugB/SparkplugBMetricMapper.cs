// Copyright (c) 2025, All Rights Reserved.

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
        /// <summary>Converts an MTConnect observation into a Sparkplug B <see cref="Metric"/>; SAMPLEs are coerced to <see cref="DataType.Double"/> to match the DBIRTH declaration, EVENTs/CONDITIONs are emitted as <see cref="DataType.String"/>.</summary>
        public static Metric ToMetric(IObservation observation)
        {
            var name = BuildObservationMetricName(observation);
            // Observation.Timestamp is Kind=Utc (the agent buffer round-trips it
            // through unix ticks), so a zero offset is always correct here.
            var timestamp = new DateTimeOffset(observation.Timestamp, TimeSpan.Zero);

            var isSample = observation.Category == DataItemCategory.SAMPLE;

            if (observation.IsUnavailable)
            {
                // Must match DataType declared in DBIRTH — SAMPLEs are Double
                return isSample
                    ? new Metric(name, DataType.Double, 0.0, timestamp)
                    : new Metric(name, DataType.String, "UNAVAILABLE", timestamp);
            }

            var raw = observation.GetValue(ValueKeys.Result);
            if (raw == null)
            {
                return isSample
                    ? new Metric(name, DataType.Double, 0.0, timestamp)
                    : new Metric(name, DataType.String, "UNAVAILABLE", timestamp);
            }

            if (isSample)
            {
                // Always return Double for SAMPLEs to match DBIRTH declaration
                return double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
                    ? new Metric(name, DataType.Double, d, timestamp)
                    : new Metric(name, DataType.Double, 0.0, timestamp);
            }

            return new Metric(name, DataType.String, raw, timestamp);
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
                var rawValue = obs?.GetValue(ValueKeys.Result);

                // Carry each observation's real timestamp into the DBIRTH metric.
                // Stamping everything 'now' would fabricate a fresh-data event for
                // stale values in downstream historians at every (re)birth/reconnect.
                var ts = obs != null ? new DateTimeOffset(obs.Timestamp, TimeSpan.Zero) : now;
                var hasValue = rawValue != null
                    && !string.Equals(rawValue, "UNAVAILABLE", StringComparison.OrdinalIgnoreCase);

                Metric metric;
                if (di.Category == DataItemCategory.SAMPLE)
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

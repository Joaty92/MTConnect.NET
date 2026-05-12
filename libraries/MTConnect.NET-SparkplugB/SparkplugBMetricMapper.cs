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
            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(
                new DateTimeOffset(observation.Timestamp, TimeSpan.Zero).ToUnixTimeMilliseconds());

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
        public static string BuildObservationMetricName(IObservation observation)
        {
            var name = observation.Type;
            if (!string.IsNullOrEmpty(observation.SubType))
                name += $"/{observation.SubType}";
            if (!string.IsNullOrEmpty(observation.DataItemId))
                name += $"[{observation.DataItemId}]";
            return name;
        }

        /// <summary>Builds the Sparkplug metric name from a data item as <c>Type[/SubType][[Id]]</c>; used to declare the metric set in DBIRTH.</summary>
        public static string BuildDataItemMetricName(IDataItem dataItem)
        {
            var name = dataItem.Type;
            if (!string.IsNullOrEmpty(dataItem.SubType))
                name += $"/{dataItem.SubType}";
            if (!string.IsNullOrEmpty(dataItem.Id))
                name += $"[{dataItem.Id}]";
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
            var ts = DateTimeOffset.UtcNow;

            foreach (var di in device.GetDataItems())
            {
                var metricName = BuildDataItemMetricName(di);
                byId.TryGetValue(di.Id ?? "", out var obs);
                var rawValue = obs?.GetValue(ValueKeys.Result);
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

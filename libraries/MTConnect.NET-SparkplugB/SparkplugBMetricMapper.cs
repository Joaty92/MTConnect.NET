// Copyright (c) 2025, All Rights Reserved.

using MTConnect.Devices;
using MTConnect.Devices.DataItems;
using MTConnect.Observations;
using SparkplugNet.VersionB.Data;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace MTConnect.SparkplugB
{
    public static class SparkplugBMetricMapper
    {
        public static Metric ToMetric(IObservation observation)
        {
            var name = BuildObservationMetricName(observation);
            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(
                new DateTimeOffset(observation.Timestamp, TimeSpan.Zero).ToUnixTimeMilliseconds());

            if (observation.IsUnavailable)
                return new Metric(name, DataType.String, "UNAVAILABLE", timestamp);

            var raw = observation.GetValue(ValueKeys.Result);
            if (raw == null)
                return new Metric(name, DataType.String, "UNAVAILABLE", timestamp);

            if (observation.Category == DataItemCategory.SAMPLE &&
                double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            {
                return new Metric(name, DataType.Double, d, timestamp);
            }

            return new Metric(name, DataType.String, raw, timestamp);
        }

        public static string BuildObservationMetricName(IObservation observation)
        {
            var name = observation.Type;
            if (!string.IsNullOrEmpty(observation.SubType))
                name += $"/{observation.SubType}";
            if (!string.IsNullOrEmpty(observation.DataItemId))
                name += $"[{observation.DataItemId}]";
            return name;
        }

        public static string BuildDataItemMetricName(IDataItem dataItem)
        {
            var name = dataItem.Type;
            if (!string.IsNullOrEmpty(dataItem.SubType))
                name += $"/{dataItem.SubType}";
            if (!string.IsNullOrEmpty(dataItem.Id))
                name += $"[{dataItem.Id}]";
            return name;
        }

        public static List<Metric> BirthMetricsForDevice(IDevice device)
        {
            var metrics = new List<Metric>();
            var ts = DateTimeOffset.UtcNow;
            foreach (var di in device.GetDataItems())
                metrics.Add(new Metric(BuildDataItemMetricName(di), DataType.String, "UNAVAILABLE", ts));
            return metrics;
        }
    }
}

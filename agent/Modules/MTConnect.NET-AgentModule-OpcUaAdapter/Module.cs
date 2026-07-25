// Copyright (c) 2025, All Rights Reserved.

using MTConnect.Agents;
using MTConnect.Configurations;
using MTConnect.Devices;
using MTConnect.Devices.DataItems;
using MTConnect.Formatters;
using MTConnect.Input;
using MTConnect.Logging;
using MTConnect.Observations;
using MTConnect.OpcUa;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MTConnect
{
    /// <summary>Agent module that hosts an <see cref="OpcUaSubscriptionClient"/>; activated by the <c>opcua-adapter</c> configuration section in <c>agent.config.yaml</c>. One block per device — observations are pushed to the agent via <c>IMTConnectAgentBroker.AddObservation(...)</c>. The device model is resolved in priority order: user-authored <c>devices/&lt;key&gt;.xml</c>, shipped <c>profiles/&lt;name&gt;.device.xml</c> template, or auto-generated flat device built from the profile mappings.</summary>
    public class Module : MTConnectAgentModule
    {
        /// <summary>YAML key under <c>modules:</c> that selects this module's configuration section.</summary>
        public const string ConfigurationTypeId = "opcua-adapter";
        private const string ModuleId = "OpcUa Adapter";

        private readonly OpcUaAdapterModuleConfiguration _configuration;
        private OpcUaSubscriptionClient _client;

        /// <summary>Constructed by the agent's module loader; <paramref name="configuration"/> is the raw YAML section bound to <see cref="OpcUaAdapterModuleConfiguration"/>.</summary>
        public Module(IMTConnectAgentBroker agent, object configuration) : base(agent)
        {
            Id = ModuleId;
            _configuration = AgentApplicationConfiguration.GetConfiguration<OpcUaAdapterModuleConfiguration>(configuration);
        }

        /// <summary>Loads the profile, resolves the device model (user XML → shipped template → auto-generated), opens the OPC UA session, and starts the subscription; values then flow through <see cref="OpcUaSubscriptionClient.ValueReceived"/> into the agent buffer.</summary>
        protected override void OnStartAfterLoad(bool initializeDataItems)
        {
            if (_configuration == null) return;
            if (string.IsNullOrWhiteSpace(_configuration.DeviceKey))
            {
                Log(MTConnectLogLevel.Warning, "OpcUa adapter: deviceKey missing; skipping module start");
                return;
            }
            if (string.IsNullOrWhiteSpace(_configuration.Endpoint))
            {
                Log(MTConnectLogLevel.Warning, $"OpcUa adapter ({_configuration.DeviceKey}): endpoint missing; skipping module start");
                return;
            }

            var profilePath = ResolveProfilePath(_configuration.Profile);
            if (profilePath == null)
            {
                Log(MTConnectLogLevel.Warning, $"OpcUa adapter ({_configuration.DeviceKey}): profile '{_configuration.Profile}' not found under profiles/; skipping module start");
                return;
            }

            OpcUaProfile profile;
            try
            {
                profile = OpcUaProfile.LoadFromFile(profilePath);
            }
            catch (Exception ex)
            {
                Log(MTConnectLogLevel.Error, $"OpcUa adapter ({_configuration.DeviceKey}): failed to parse profile {profilePath}: {ex.Message}");
                return;
            }

            EnsureDevice(profile);

            _client = new OpcUaSubscriptionClient(_configuration, profile, $"MTConnect-OpcUa-{_configuration.DeviceKey}");
            _client.Connected += () => Log(MTConnectLogLevel.Information, $"OpcUa session connected: {_configuration.Endpoint} ({profile.Mappings.Count} mappings)");
            _client.Disconnected += () =>
            {
                Log(MTConnectLogLevel.Information, $"OpcUa session disconnected: {_configuration.Endpoint} — flagging device UNAVAILABLE");
                FlagDeviceUnavailable(profile);
            };
            _client.ConnectionError += ex => Log(MTConnectLogLevel.Warning, $"OpcUa error ({_configuration.DeviceKey}): {ex.Message}");
            _client.Warning += message => Log(MTConnectLogLevel.Warning, $"OpcUa adapter ({_configuration.DeviceKey}): {message}");
            _client.ValueReceived += OnValueReceived;

            _ = _client.Start().ContinueWith(
                t => Log(MTConnectLogLevel.Error, $"OpcUa client failed to start ({_configuration.Endpoint}): {t.Exception?.GetBaseException()?.Message}"),
                TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>Stops the OPC UA subscription and closes the session.</summary>
        protected override void OnStop()
        {
            if (_client != null) _ = _client.Stop();
        }

        private DateTime _lastSkewWarningUtc;

        private void OnValueReceived(string dataItemKey, object value, DateTime timestampUtc, bool isGood)
        {
            WarnOnClockSkew(timestampUtc);
            // CoerceValue returns null for a Good status carrying a null Variant; passing
            // that through published an empty-string observation that looked like real
            // data. Treat "no value" the same as bad quality.
            var payload = isGood && value != null ? value : "UNAVAILABLE";
            PushObservation(dataItemKey, payload, timestampUtc);
        }

        // Forward one observation to the agent, applying this adapter's optional per-adapter
        // IgnoreTimestamps override: null inherits the agent-wide setting; true re-stamps with
        // the agent clock (dropping the controller's source timestamp); false keeps source time.
        private void PushObservation(string dataItemKey, object value, DateTime timestampUtc)
        {
            var input = new ObservationInput
            {
                DeviceKey = _configuration.DeviceKey,
                DataItemKey = dataItemKey,
                Values = new List<ObservationValue> { new ObservationValue(ValueKeys.Result, value) },
                Timestamp = timestampUtc.ToUnixTime()
            };
            Agent.AddObservation(input, ignoreTimestamp: _configuration.IgnoreTimestamps);
        }

        // Controller and agent clocks are independent time bases; large skew silently
        // corrupts cross-machine event ordering in downstream historians. Surface it
        // (rate-limited) so an unsynchronized controller shows up in the module log.
        private void WarnOnClockSkew(DateTime sourceTimestampUtc)
        {
            var nowUtc = DateTime.UtcNow;
            var skewSeconds = (sourceTimestampUtc - nowUtc).TotalSeconds;
            if (Math.Abs(skewSeconds) < 5) return;
            if ((nowUtc - _lastSkewWarningUtc).TotalSeconds < 60) return;
            _lastSkewWarningUtc = nowUtc;
            Log(MTConnectLogLevel.Warning,
                $"OpcUa adapter ({_configuration.DeviceKey}): source timestamps are {skewSeconds:+0.0;-0.0}s from the agent clock — check NTP on the controller and the agent host");
        }

        private void FlagDeviceUnavailable(OpcUaProfile profile)
        {
            // Session-level disconnect: every mapped data item should flip to UNAVAILABLE
            // until the reconnect handler re-establishes the subscription and the server
            // re-publishes current values. Matches the SHDR adapter's source-down behaviour.
            var nowUtc = DateTime.UtcNow;
            foreach (var mapping in profile.Mappings)
            {
                if (string.IsNullOrWhiteSpace(mapping.DataItemKey)) continue;
                PushObservation(mapping.DataItemKey, "UNAVAILABLE", nowUtc);
            }
        }

        private void EnsureDevice(OpcUaProfile profile)
        {
            var existing = Agent.GetDevices()?.FirstOrDefault(d =>
                string.Equals(d.Uuid, _configuration.DeviceKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(d.Name, _configuration.DeviceKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(d.Id, _configuration.DeviceKey, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                Log(MTConnectLogLevel.Information, $"OpcUa adapter ({_configuration.DeviceKey}): using existing device from devices/ folder");
                return;
            }

            var templatePath = ResolveDeviceTemplatePath(_configuration.Profile);
            if (templatePath != null)
            {
                try
                {
                    var configs = DeviceConfiguration.FromFile(templatePath, DocumentFormat.XML);
                    var device = configs?.FirstOrDefault();
                    if (device != null)
                    {
                        device.Id = _configuration.DeviceKey;
                        device.Uuid = _configuration.DeviceKey;
                        device.Name = _configuration.DeviceKey;
                        // Make every DataItem id globally unique per the MTConnect spec by
                        // prefixing with the deviceKey. Names are preserved so the YAML's
                        // dataItemKey continues to route observations correctly (the agent
                        // resolves AddObservation's dataItemKey against Name OR Id OR Source).
                        PrefixDataItemIds(device, _configuration.DeviceKey);
                        Agent.AddDevice(device);
                        Log(MTConnectLogLevel.Information, $"OpcUa adapter ({_configuration.DeviceKey}): created device from template {Path.GetFileName(templatePath)}");
                        return;
                    }
                    Log(MTConnectLogLevel.Warning, $"OpcUa adapter ({_configuration.DeviceKey}): template {Path.GetFileName(templatePath)} parsed but contained no Device; falling back to flat auto-generated device");
                }
                catch (Exception ex)
                {
                    Log(MTConnectLogLevel.Warning, $"OpcUa adapter ({_configuration.DeviceKey}): failed to load template {Path.GetFileName(templatePath)}: {ex.Message}; falling back to flat auto-generated device");
                }
            }

            var generated = BuildFlatDevice(_configuration.DeviceKey, profile);
            Agent.AddDevice(generated);
            Log(MTConnectLogLevel.Information, $"OpcUa adapter ({_configuration.DeviceKey}): created flat device from profile mappings ({generated.DataItems.Count()} data items)");
        }

        private static Device BuildFlatDevice(string deviceKey, OpcUaProfile profile)
        {
            var dataItems = new List<IDataItem>();
            foreach (var mapping in profile.Mappings)
            {
                if (string.IsNullOrWhiteSpace(mapping.DataItemKey)) continue;
                dataItems.Add(new DataItem
                {
                    // Id is globally unique across all devices (deviceKey-prefixed);
                    // Name stays equal to the YAML dataItemKey so AddObservation routes correctly.
                    Id = $"{deviceKey}_{mapping.DataItemKey}",
                    Name = mapping.DataItemKey,
                    // VARIABLE is an EVENT-only type (VariableDataItem.CategoryId), so
                    // declaring Sample mappings as SAMPLE/VARIABLE produced a device model
                    // that fails spec validation. A NodeId carries no MTConnect type or
                    // units to infer a real SAMPLE type from, so this last-resort fallback
                    // declares everything EVENT/VARIABLE; author devices/<key>.xml or a
                    // profile .template.xml (the profile GUI emits one) for proper typing.
                    Category = VariableDataItem.CategoryId,
                    Type = VariableDataItem.TypeId,
                });
            }
            return new Device
            {
                Id = deviceKey,
                Uuid = deviceKey,
                Name = deviceKey,
                DataItems = dataItems,
            };
        }

        private static void PrefixDataItemIds(IDevice device, string prefix)
        {
            // Walk device-level DataItems plus all DataItems in nested components.
            // Preserves Name (used for AddObservation routing) and only rewrites Id.
            // IDataItem exposes read-only properties; the concrete DataItem class is
            // mutable. Skip anything that isn't the concrete type (shouldn't happen
            // with DeviceConfiguration.FromFile output, but cheap to guard).
            foreach (var item in EnumerateAllDataItems(device))
            {
                if (item is not DataItem mutable) continue;
                var originalId = mutable.Id;
                if (string.IsNullOrWhiteSpace(originalId)) continue;
                // If Name is empty, copy the original Id to Name first so observation
                // routing still works after we rewrite Id.
                if (string.IsNullOrWhiteSpace(mutable.Name))
                {
                    mutable.Name = originalId;
                }
                // Don't double-prefix if a previous run already touched this device.
                if (originalId.StartsWith($"{prefix}_", StringComparison.OrdinalIgnoreCase)) continue;
                mutable.Id = $"{prefix}_{originalId}";
            }
        }

        private static IEnumerable<IDataItem> EnumerateAllDataItems(IDevice device)
        {
            if (device.DataItems != null)
            {
                foreach (var di in device.DataItems) yield return di;
            }
            if (device.Components != null)
            {
                foreach (var c in device.Components)
                {
                    foreach (var di in EnumerateComponentDataItems(c)) yield return di;
                }
            }
        }

        private static IEnumerable<IDataItem> EnumerateComponentDataItems(IComponent component)
        {
            if (component.DataItems != null)
            {
                foreach (var di in component.DataItems) yield return di;
            }
            if (component.Components != null)
            {
                foreach (var c in component.Components)
                {
                    foreach (var di in EnumerateComponentDataItems(c)) yield return di;
                }
            }
        }

        private static string ResolveProfilePath(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName)) return null;
            var fileName = profileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
                ? profileName
                : $"{profileName}.yaml";
            var path = Path.Combine(AppContext.BaseDirectory, "profiles", fileName);
            return File.Exists(path) ? path : null;
        }

        private static string ResolveDeviceTemplatePath(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName)) return null;
            var baseName = profileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
                ? profileName.Substring(0, profileName.Length - 5)
                : profileName;
            var profilesDir = Path.Combine(AppContext.BaseDirectory, "profiles");
            // Preferred extension: .template.xml (clearer name, doesn't conflict with the devices/ folder convention).
            // Back-compat: .device.xml is still resolved so previously-generated templates keep working.
            var preferred = Path.Combine(profilesDir, $"{baseName}.template.xml");
            if (File.Exists(preferred)) return preferred;
            var legacy = Path.Combine(profilesDir, $"{baseName}.device.xml");
            if (File.Exists(legacy)) return legacy;
            return null;
        }
    }
}

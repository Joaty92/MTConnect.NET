// Copyright (c) 2025, All Rights Reserved.

using MTConnect.Configurations;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MTConnect.OpcUa
{
    /// <summary>Signature for <see cref="OpcUaSubscriptionClient.ValueReceived"/>: emitted once per MonitoredItem notification carrying the mapped DataItem key, raw value, source timestamp, and StatusCode quality flag.</summary>
    public delegate void OpcUaValueReceivedHandler(string dataItemKey, object value, DateTime timestampUtc, bool isGood);

    /// <summary>Long-running OPC UA client that opens a single <see cref="Session"/>, creates one <see cref="Subscription"/> populated from an <see cref="OpcUaProfile"/>, and surfaces notifications via <see cref="ValueReceived"/>. Reconnects automatically using <see cref="SessionReconnectHandler"/>.</summary>
    public class OpcUaSubscriptionClient
    {
        private readonly OpcUaClientConfiguration _config;
        private readonly OpcUaProfile _profile;
        private readonly string _applicationName;
        private readonly ITelemetryContext _telemetry;

        private ApplicationConfiguration _appConfig;
        private ISession _session;
        private Subscription _subscription;
        private SessionReconnectHandler _reconnectHandler;
        private volatile bool _isRunning;

        /// <summary>Raised on successful session connect (including after reconnect).</summary>
        public event Action Connected;

        /// <summary>Raised when the session is closed cleanly or the keep-alive declares the link dead before reconnect succeeds.</summary>
        public event Action Disconnected;

        /// <summary>Raised when an error occurs during connect, subscribe, or reconnect; the <see cref="Exception"/> carries the underlying SDK error.</summary>
        public event Action<Exception> ConnectionError;

        /// <summary>Raised for every value notification on every MonitoredItem; <c>isGood</c> reflects the OPC UA <see cref="StatusCode"/> quality.</summary>
        public event OpcUaValueReceivedHandler ValueReceived;

        /// <summary>Creates a client bound to <paramref name="config"/> and <paramref name="profile"/>; <paramref name="applicationName"/> is the OPC UA <see cref="ApplicationConfiguration.ApplicationName"/> reported to servers (used for audit/logging on the server side).</summary>
        public OpcUaSubscriptionClient(OpcUaClientConfiguration config, OpcUaProfile profile, string applicationName)
        {
            _config = config;
            _profile = profile;
            _applicationName = applicationName;
            // The SDK forwards this action straight through to Microsoft.Extensions
            // .Logging.LoggerFactory.Create -> AddLogging, which throws NRE on null.
            // Pass a no-op lambda instead.
            _telemetry = DefaultTelemetry.Create(_ => { });
        }

        /// <summary>Opens the session, creates the subscription, and registers every <see cref="OpcUaProfile.Mappings"/> entry as a <see cref="MonitoredItem"/>. Returns once the subscription is live; notifications then flow asynchronously through <see cref="ValueReceived"/>.</summary>
        public async Task Start()
        {
            _isRunning = true;
            try
            {
                _appConfig = await BuildApplicationConfiguration();

                var useSecurity = !string.Equals(_config.SecurityPolicy, "None", StringComparison.OrdinalIgnoreCase);
                var selectedEndpoint = await CoreClientUtils.SelectEndpointAsync(_appConfig, _config.Endpoint, useSecurity, _telemetry, default);
                var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, EndpointConfiguration.Create(_appConfig));

                var sessionFactory = new DefaultSessionFactory(_telemetry);
                _session = await sessionFactory.CreateAsync(
                    _appConfig,
                    endpoint,
                    updateBeforeConnect: false,
                    checkDomain: false,
                    _applicationName,
                    (uint)_config.SessionTimeout,
                    BuildIdentity(),
                    preferredLocales: null,
                    default);

                _session.KeepAlive += OnKeepAlive;
                Connected?.Invoke();

                await CreateSubscriptionAsync();
            }
            catch (Exception ex)
            {
                ConnectionError?.Invoke(ex);
                throw;
            }
        }

        /// <summary>Closes the subscription and the underlying session; safe to call when <see cref="Start"/> has not run.</summary>
        public async Task Stop()
        {
            _isRunning = false;
            try
            {
                if (_reconnectHandler != null)
                {
                    _reconnectHandler.Dispose();
                    _reconnectHandler = null;
                }
                if (_subscription != null)
                {
                    await _subscription.DeleteAsync(true);
                    _subscription.Dispose();
                    _subscription = null;
                }
                if (_session != null)
                {
                    _session.KeepAlive -= OnKeepAlive;
                    await _session.CloseAsync();
                    _session.Dispose();
                    _session = null;
                }
                Disconnected?.Invoke();
            }
            catch (Exception ex)
            {
                ConnectionError?.Invoke(ex);
            }
        }

        private async Task CreateSubscriptionAsync()
        {
            _subscription = new Subscription(_session.DefaultSubscription)
            {
                PublishingInterval = _config.PublishingInterval,
                PublishingEnabled = true,
            };
            _session.AddSubscription(_subscription);
            await _subscription.CreateAsync();

            foreach (var mapping in _profile.Mappings)
            {
                if (string.IsNullOrWhiteSpace(mapping.NodeId)) continue;

                var item = new MonitoredItem(_subscription.DefaultItem)
                {
                    DisplayName = mapping.DataItemKey,
                    StartNodeId = new NodeId(mapping.NodeId),
                    AttributeId = Attributes.Value,
                    MonitoringMode = MonitoringMode.Reporting,
                    SamplingInterval = _config.SamplingInterval,
                    QueueSize = 10,
                    DiscardOldest = true,
                };
                var capturedMapping = mapping;
                item.Notification += (mi, _) => OnNotification(mi, capturedMapping);
                _subscription.AddItem(item);
            }
            await _subscription.ApplyChangesAsync();
        }

        private void OnNotification(MonitoredItem item, OpcUaMapping mapping)
        {
            foreach (var dv in item.DequeueValues())
            {
                var isGood = StatusCode.IsGood(dv.StatusCode);
                var ts = dv.SourceTimestamp == DateTime.MinValue ? DateTime.UtcNow : dv.SourceTimestamp.ToUniversalTime();
                var value = CoerceValue(dv.Value, mapping.Kind, isGood);
                ValueReceived?.Invoke(mapping.DataItemKey, value, ts, isGood);
            }
        }

        private static object CoerceValue(object raw, OpcUaValueKind kind, bool isGood)
        {
            if (!isGood || raw == null) return null;
            if (kind == OpcUaValueKind.Sample)
            {
                return raw switch
                {
                    double d => d,
                    float f => (double)f,
                    int i => (double)i,
                    long l => (double)l,
                    short s => (double)s,
                    byte b => (double)b,
                    bool tf => tf ? 1.0 : 0.0,
                    _ => double.TryParse(raw.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : (object)0.0
                };
            }
            return raw.ToString();
        }

        private void OnKeepAlive(ISession session, KeepAliveEventArgs e)
        {
            if (!_isRunning) return;
            if (!ServiceResult.IsBad(e.Status)) return;
            if (_reconnectHandler != null) return;

            Disconnected?.Invoke();
            _reconnectHandler = new SessionReconnectHandler(_telemetry, true, _config.ReconnectInterval);
            _reconnectHandler.BeginReconnect(_session, _config.ReconnectInterval, OnReconnectComplete);
        }

        private void OnReconnectComplete(object sender, EventArgs e)
        {
            if (!ReferenceEquals(sender, _reconnectHandler)) return;
            try
            {
                if (_reconnectHandler.Session != null && !ReferenceEquals(_reconnectHandler.Session, _session))
                {
                    _session.KeepAlive -= OnKeepAlive;
                    _session = _reconnectHandler.Session;
                    _session.KeepAlive += OnKeepAlive;
                }
                Connected?.Invoke();

                // The disconnect path flagged every mapped item UNAVAILABLE. If the
                // reconnect transferred the existing subscription, MonitoredItems only
                // notify on change — values that didn't change during the outage would
                // otherwise stay UNAVAILABLE until they next move. Re-read everything
                // once so current state is republished immediately.
                _ = RepublishCurrentValuesAsync();
            }
            catch (Exception ex)
            {
                ConnectionError?.Invoke(ex);
            }
            finally
            {
                _reconnectHandler?.Dispose();
                _reconnectHandler = null;
            }
        }

        private async Task RepublishCurrentValuesAsync()
        {
            try
            {
                var session = _session;
                if (session == null || !_isRunning) return;

                var mappings = _profile.Mappings.Where(m => !string.IsNullOrWhiteSpace(m.NodeId)).ToList();
                if (mappings.Count == 0) return;

                var nodesToRead = new ReadValueIdCollection(
                    mappings.Select(m => new ReadValueId { NodeId = new NodeId(m.NodeId), AttributeId = Attributes.Value }));

                var response = await session.ReadAsync(null, 0, TimestampsToReturn.Source, nodesToRead, default);
                var results = response.Results;
                if (results == null || results.Count != mappings.Count) return;

                for (int i = 0; i < mappings.Count; i++)
                {
                    var dv = results[i];
                    var isGood = StatusCode.IsGood(dv.StatusCode);
                    var ts = dv.SourceTimestamp == DateTime.MinValue ? DateTime.UtcNow : dv.SourceTimestamp.ToUniversalTime();
                    ValueReceived?.Invoke(mappings[i].DataItemKey, CoerceValue(dv.Value, mappings[i].Kind, isGood), ts, isGood);
                }
            }
            catch (Exception ex)
            {
                ConnectionError?.Invoke(ex);
            }
        }

        private IUserIdentity BuildIdentity()
        {
            if (string.Equals(_config.Identity?.Mode, "username", StringComparison.OrdinalIgnoreCase))
            {
                var passwordBytes = Encoding.UTF8.GetBytes(_config.Identity.Password ?? string.Empty);
                return new UserIdentity(_config.Identity.Username ?? string.Empty, passwordBytes);
            }
            return new UserIdentity();
        }

        private async Task<ApplicationConfiguration> BuildApplicationConfiguration()
        {
            var pkiRoot = Path.Combine(AppContext.BaseDirectory, "pki");
            Directory.CreateDirectory(pkiRoot);

            var application = new ApplicationInstance(_telemetry)
            {
                ApplicationName = _applicationName,
                ApplicationType = ApplicationType.Client,
                ApplicationConfiguration = new ApplicationConfiguration
                {
                    ApplicationName = _applicationName,
                    ApplicationUri = $"urn:{Dns.GetHostName()}:{_applicationName}",
                    ApplicationType = ApplicationType.Client,
                    SecurityConfiguration = new SecurityConfiguration
                    {
                        ApplicationCertificate = new CertificateIdentifier
                        {
                            StoreType = CertificateStoreType.Directory,
                            StorePath = Path.Combine(pkiRoot, "own"),
                            SubjectName = $"CN={_applicationName}, DC={Dns.GetHostName()}"
                        },
                        TrustedPeerCertificates = new CertificateTrustList
                        {
                            StoreType = CertificateStoreType.Directory,
                            StorePath = Path.Combine(pkiRoot, "trusted")
                        },
                        TrustedIssuerCertificates = new CertificateTrustList
                        {
                            StoreType = CertificateStoreType.Directory,
                            StorePath = Path.Combine(pkiRoot, "issuers")
                        },
                        RejectedCertificateStore = new CertificateTrustList
                        {
                            StoreType = CertificateStoreType.Directory,
                            StorePath = Path.Combine(pkiRoot, "rejected")
                        },
                        AutoAcceptUntrustedCertificates = true,
                        AddAppCertToTrustedStore = true,
                    },
                    TransportConfigurations = new TransportConfigurationCollection(),
                    TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                    ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = _config.SessionTimeout },
                    TraceConfiguration = new TraceConfiguration(),
                    DisableHiResClock = true,
                }
            };

            await application.ApplicationConfiguration.ValidateAsync(ApplicationType.Client);
            await application.CheckApplicationInstanceCertificatesAsync(silent: true);

            application.ApplicationConfiguration.CertificateValidator.CertificateValidation += (s, e) =>
            {
                if (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted)
                    e.Accept = true;
            };

            return application.ApplicationConfiguration;
        }
    }
}

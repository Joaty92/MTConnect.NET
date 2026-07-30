// Copyright (c) 2025 MTConnect.NET fork contributors, All Rights Reserved.
// Licensed to you under the MIT license; see LICENSE.md in the repository root.

using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using MTConnect.Logging;
using MTConnect.SparkplugB.Payload;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MTConnect.SparkplugB.Transport
{
    /// <summary>
    /// A Sparkplug B Edge Node session over MQTT.
    /// <para>
    /// Owns the connection, the Will Message, both sequence counters, and the order in which
    /// lifecycle messages reach the wire. The ordering guarantee that matters most is structural
    /// rather than observed: this class publishes NBIRTH itself and awaits it before raising
    /// <see cref="SessionEstablished"/>, so a DBIRTH cannot precede its NBIRTH
    /// (<c>tck-id-message-flow-device-birth-publish-nbirth-wait</c>) by construction.
    /// </para>
    /// <para>
    /// Per the specification: NBIRTH, DBIRTH, NDATA and DDATA publish at QoS 0 with retain false;
    /// the NDEATH Will Message is registered at QoS 1; NDEATH carries no sequence number; and every
    /// metric in a birth or data message carries a timestamp.
    /// </para>
    /// </summary>
    public sealed class SparkplugEdgeNode : IAsyncDisposable
    {
        /// <summary>Metric name carrying the birth/death sequence in NBIRTH and NDEATH.</summary>
        public const string BirthDeathSequenceMetric = "bdSeq";

        /// <summary>Metric a Host Application writes to request a rebirth.</summary>
        public const string RebirthMetric = "Node Control/Rebirth";

        private readonly SparkplugEdgeNodeOptions _options;
        private readonly SparkplugSequence _sequence = new();
        private readonly SparkplugBirthDeathSequence _birthDeathSequence;
        private readonly SemaphoreSlim _publishLock = new(1, 1);

        private IMqttClient _client;
        private Task _connectionLoop;
        private CancellationTokenSource _cts;

        private long _currentBdSeq;
        private volatile bool _running;
        private volatile bool _sessionUp;
        private volatile bool _primaryHostOnline;

        /// <summary>Raised once NBIRTH is on the wire; publish device births from here.</summary>
        public event Func<Task> SessionEstablished;

        /// <summary>Raised when the session ends, so callers can drop per-session state.</summary>
        public event Func<Task> SessionLost;

        /// <summary>Raised when a Host Application requests a rebirth via NCMD.</summary>
        public event Func<Task> RebirthRequested;

        /// <summary>Raised for diagnostic messages.</summary>
        public event Action<MTConnectLogLevel, string> LogReceived;

        /// <summary>Node-level metrics to include in every NBIRTH, supplied by the caller.</summary>
        public Func<IEnumerable<SparkplugMetric>> NodeMetricsProvider { get; set; }

        /// <summary>Creates an edge node bound to <paramref name="options"/>.</summary>
        /// <param name="options">Connection and identity settings.</param>
        public SparkplugEdgeNode(SparkplugEdgeNodeOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _birthDeathSequence = new SparkplugBirthDeathSequence(options.BirthDeathSequencePath);
            _birthDeathSequence.PersistenceFailed +=
                reason => Log(MTConnectLogLevel.Warning, $"bdSeq persistence: {reason}");
        }

        /// <summary>Whether the MQTT connection is currently up.</summary>
        public bool IsConnected => _client?.IsConnected == true;

        /// <summary>Whether NBIRTH has been published for the current session.</summary>
        public bool IsSessionEstablished => _sessionUp;

        /// <summary>Validates identifiers, then connects and maintains the session until stopped.</summary>
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (!SparkplugTopic.IsValidIdentifier(_options.GroupId))
                throw new ArgumentException($"invalid Sparkplug Group ID '{_options.GroupId}'");
            if (!SparkplugTopic.IsValidIdentifier(_options.EdgeNodeId))
                throw new ArgumentException($"invalid Sparkplug Edge Node ID '{_options.EdgeNodeId}'");

            _running = true;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _client = new MqttFactory().CreateMqttClient();
            _client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
            _client.DisconnectedAsync += OnDisconnectedAsync;

            // MQTTnet's IMqttClient does not reconnect on its own, so the session is maintained by
            // this loop rather than left to the library.
            _connectionLoop = Task.Run(() => MaintainConnectionAsync(_cts.Token));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Ends the session intentionally: publishes NDEATH and then disconnects.
        /// <para>
        /// Both halves matter. On MQTT 3.1.1 a clean DISCONNECT suppresses the Will Message, so a node
        /// that merely disconnects announces nothing at all; and a node that is killed announces its
        /// shutdown as a crash. Publishing NDEATH first and disconnecting cleanly is what lets a Host
        /// Application tell a planned stop from a failure —
        /// <c>tck-id-operational-behavior-edge-node-intentional-disconnect-ndeath</c>.
        /// </para>
        /// </summary>
        public async Task StopAsync()
        {
            _running = false;
            _cts?.Cancel();

            if (_connectionLoop != null)
            {
                try { await _connectionLoop; } catch { }
                _connectionLoop = null;
            }

            if (_client != null && _client.IsConnected)
            {
                try
                {
                    await PublishAsync(
                        SparkplugTopic.Node(_options.GroupId, SparkplugMessageType.NDEATH, _options.EdgeNodeId),
                        BuildDeathPayload(),
                        MqttQualityOfServiceLevel.AtMostOnce);
                    Log(MTConnectLogLevel.Information, "NDEATH published for intentional shutdown");
                }
                catch (Exception ex)
                {
                    Log(MTConnectLogLevel.Warning, $"NDEATH on shutdown failed: {ex.Message}");
                }

                try { await _client.DisconnectAsync(); } catch { }
            }

            _sessionUp = false;
        }

        /// <summary>Publishes a DBIRTH for a device.</summary>
        /// <param name="deviceId">Sparkplug Device ID.</param>
        /// <param name="metrics">Full current-value metric set for the device.</param>
        public Task PublishDeviceBirthAsync(string deviceId, IEnumerable<SparkplugMetric> metrics) =>
            PublishDeviceAsync(SparkplugMessageType.DBIRTH, deviceId, metrics);

        /// <summary>Publishes a DDATA for a device.</summary>
        /// <param name="deviceId">Sparkplug Device ID.</param>
        /// <param name="metrics">Changed metrics.</param>
        public Task PublishDeviceDataAsync(string deviceId, IEnumerable<SparkplugMetric> metrics) =>
            PublishDeviceAsync(SparkplugMessageType.DDATA, deviceId, metrics);

        /// <summary>Publishes a DDEATH for a device.</summary>
        /// <param name="deviceId">Sparkplug Device ID.</param>
        public Task PublishDeviceDeathAsync(string deviceId) =>
            PublishDeviceAsync(SparkplugMessageType.DDEATH, deviceId, Array.Empty<SparkplugMetric>());

        private async Task PublishDeviceAsync(
            SparkplugMessageType messageType, string deviceId, IEnumerable<SparkplugMetric> metrics)
        {
            if (!_sessionUp)
            {
                // No DDATA or DBIRTH may precede the session's NBIRTH, and none may follow its NDEATH.
                Log(MTConnectLogLevel.Debug, $"{messageType} for {deviceId} suppressed — no live session");
                return;
            }

            if (!SparkplugTopic.IsValidIdentifier(deviceId))
            {
                Log(MTConnectLogLevel.Warning, $"{messageType} skipped — invalid Device ID '{deviceId}'");
                return;
            }

            var payload = new SparkplugPayload
            {
                Timestamp = UnixMilliseconds(),
                Seq = _sequence.Next(),
                Metrics = (metrics ?? Array.Empty<SparkplugMetric>()).ToList()
            };

            await PublishAsync(
                SparkplugTopic.Device(_options.GroupId, messageType, _options.EdgeNodeId, deviceId),
                payload,
                MqttQualityOfServiceLevel.AtMostOnce);
        }

        private async Task MaintainConnectionAsync(CancellationToken cancellationToken)
        {
            while (_running && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!_client.IsConnected)
                    {
                        await ConnectAndBirthAsync(cancellationToken);
                    }
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    Log(MTConnectLogLevel.Warning, $"connect failed: {ex.Message}");
                }

                try { await Task.Delay(_options.ReconnectIntervalMs, cancellationToken); }
                catch (OperationCanceledException) { return; }
            }
        }

        private async Task ConnectAndBirthAsync(CancellationToken cancellationToken)
        {
            // A fresh bdSeq per CONNECT, taken before the will is built so that the will and the
            // NBIRTH published on this connection carry the same value — that pairing is what lets a
            // host discard an NDEATH belonging to a session it has already replaced.
            _currentBdSeq = _birthDeathSequence.TakeForConnection();
            _primaryHostOnline = false;

            await _client.ConnectAsync(BuildConnectOptions(), cancellationToken);
            Log(MTConnectLogLevel.Information,
                $"connected to {_options.Server}:{_options.Port} as {_options.GroupId}/{_options.EdgeNodeId} (bdSeq {_currentBdSeq})");

            await _client.SubscribeAsync(
                SparkplugTopic.Node(_options.GroupId, SparkplugMessageType.NCMD, _options.EdgeNodeId),
                MqttQualityOfServiceLevel.AtMostOnce, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.PrimaryHostId))
            {
                // Subscribing to STATE is what the specification actually requires of a node that
                // waits for a primary host; without it the node cannot know the host is there.
                await _client.SubscribeAsync(
                    SparkplugTopic.State(_options.PrimaryHostId),
                    MqttQualityOfServiceLevel.AtLeastOnce, cancellationToken);

                Log(MTConnectLogLevel.Information,
                    $"awaiting primary host '{_options.PrimaryHostId}' before NBIRTH");
                return; // NBIRTH waits for the retained STATE message to arrive.
            }

            await PublishNodeBirthAsync();
        }

        private async Task PublishNodeBirthAsync()
        {
            // Reset first: NBIRTH starts the session's sequence, so it carries seq 0.
            _sequence.Reset();

            var metrics = new List<SparkplugMetric>
            {
                BirthDeathSequenceMetricValue(_currentBdSeq),
                new()
                {
                    Name = RebirthMetric,
                    Timestamp = UnixMilliseconds(),
                    DataType = (uint)SparkplugDataType.Boolean,
                    IsNull = false,
                    BooleanValue = false
                }
            };

            var supplied = NodeMetricsProvider?.Invoke();
            if (supplied != null) metrics.AddRange(supplied);

            var payload = new SparkplugPayload
            {
                Timestamp = UnixMilliseconds(),
                Seq = _sequence.Next(),
                Metrics = metrics
            };

            await PublishAsync(
                SparkplugTopic.Node(_options.GroupId, SparkplugMessageType.NBIRTH, _options.EdgeNodeId),
                payload,
                MqttQualityOfServiceLevel.AtMostOnce);

            _sessionUp = true;
            Log(MTConnectLogLevel.Information, $"NBIRTH published (bdSeq {_currentBdSeq}, seq 0)");

            // Only now may device births follow, and they do so strictly after NBIRTH reached the
            // wire because this is awaited rather than raced against a connection event.
            var handler = SessionEstablished;
            if (handler != null) await handler();
        }

        private SparkplugPayload BuildDeathPayload() => new()
        {
            Timestamp = UnixMilliseconds(),
            // tck-id-payloads-ndeath-seq: NDEATH must carry no sequence number. It is registered
            // before the session exists, so it has no place in that session's ordering.
            Seq = null,
            Metrics = { BirthDeathSequenceMetricValue(_currentBdSeq) }
        };

        private static SparkplugMetric BirthDeathSequenceMetricValue(long value) => new()
        {
            Name = BirthDeathSequenceMetric,
            Timestamp = UnixMilliseconds(),
            DataType = (uint)SparkplugDataType.Int64,
            IsNull = false,
            LongValue = (ulong)value
        };

        private MqttClientOptions BuildConnectOptions()
        {
            var builder = new MqttClientOptionsBuilder()
                .WithTcpServer(_options.Server, _options.Port)
                .WithClientId(_options.ClientId ?? $"MTConnect-{Guid.NewGuid():N}".Substring(0, 23))
                .WithCleanSession()
                .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V311)
                .WithWillTopic(SparkplugTopic.Node(_options.GroupId, SparkplugMessageType.NDEATH, _options.EdgeNodeId))
                .WithWillPayload(Encode(BuildDeathPayload()))
                // The will is the one Sparkplug message registered at QoS 1: it must survive the
                // ungraceful disconnect it exists to report.
                .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithWillRetain(false);

            if (!string.IsNullOrEmpty(_options.Username))
                builder = builder.WithCredentials(_options.Username, _options.Password);

            if (_options.TlsOptions != null)
                builder = builder.WithTlsOptions(_options.TlsOptions);

            return builder.Build();
        }

        private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            try
            {
                var topic = args.ApplicationMessage.Topic;
                var body = args.ApplicationMessage.PayloadSegment.ToArray();

                var stateHost = SparkplugTopic.TryParseStateHostId(topic);
                if (stateHost != null)
                {
                    await HandlePrimaryHostStateAsync(stateHost, body);
                    return;
                }

                if (!SparkplugTopic.TryParse(topic, out _, out var messageType, out _, out _)) return;
                if (messageType != SparkplugMessageType.NCMD) return;

                var payload = Decode(body);
                var rebirth = payload?.Metrics?.FirstOrDefault(m =>
                    string.Equals(m.Name, RebirthMetric, StringComparison.OrdinalIgnoreCase));

                if (rebirth?.BooleanValue == true)
                {
                    Log(MTConnectLogLevel.Information, "NCMD Rebirth received");
                    var handler = RebirthRequested;
                    if (handler != null) await handler();
                    await RebirthAsync();
                }
            }
            catch (Exception ex)
            {
                Log(MTConnectLogLevel.Warning, $"inbound message failed: {ex.Message}");
            }
        }

        private async Task HandlePrimaryHostStateAsync(string hostId, byte[] body)
        {
            if (!string.Equals(hostId, _options.PrimaryHostId, StringComparison.Ordinal)) return;

            bool online;
            try
            {
                using var document = JsonDocument.Parse(body);
                online = document.RootElement.TryGetProperty("online", out var value) &&
                         value.ValueKind == JsonValueKind.True;
            }
            catch (Exception ex)
            {
                Log(MTConnectLogLevel.Warning, $"unreadable STATE from '{hostId}': {ex.Message}");
                return;
            }

            _primaryHostOnline = online;

            if (online && !_sessionUp)
            {
                Log(MTConnectLogLevel.Information, $"primary host '{hostId}' online — publishing NBIRTH");
                await PublishNodeBirthAsync();
            }
            else if (!online && _sessionUp)
            {
                // The host this node exists to feed has gone; the session it established is over.
                Log(MTConnectLogLevel.Information, $"primary host '{hostId}' offline — ending session");
                _sessionUp = false;
                var handler = SessionLost;
                if (handler != null) await handler();
            }
        }

        /// <summary>Republishes NBIRTH, starting a fresh session on the existing connection.</summary>
        public async Task RebirthAsync()
        {
            if (!IsConnected) return;
            _sessionUp = false;
            await PublishNodeBirthAsync();
        }

        private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
        {
            if (!_sessionUp && !_running) return;

            _sessionUp = false;
            Log(MTConnectLogLevel.Information, $"disconnected ({args.Reason}) — session ended");

            var handler = SessionLost;
            if (handler != null)
            {
                try { await handler(); } catch { }
            }
        }

        private async Task PublishAsync(string topic, SparkplugPayload payload, MqttQualityOfServiceLevel qos)
        {
            // Serialised because the sequence number was taken before the await: two publishes
            // interleaving here would put seq on the wire out of order, which a Host Application
            // reads as loss and answers with a fleet-wide rebirth.
            await _publishLock.WaitAsync();
            try
            {
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(Encode(payload))
                    .WithQualityOfServiceLevel(qos)
                    .WithRetainFlag(false)
                    .Build();

                await _client.PublishAsync(message);
            }
            finally
            {
                _publishLock.Release();
            }
        }

        private static byte[] Encode(SparkplugPayload payload)
        {
            using var stream = new MemoryStream();
            ProtoBuf.Serializer.Serialize(stream, payload);
            return stream.ToArray();
        }

        private static SparkplugPayload Decode(byte[] body)
        {
            using var stream = new MemoryStream(body);
            return ProtoBuf.Serializer.Deserialize<SparkplugPayload>(stream);
        }

        private static ulong UnixMilliseconds() => (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        private void Log(MTConnectLogLevel level, string message) => LogReceived?.Invoke(level, message);

        /// <summary>Stops the session and releases the MQTT client.</summary>
        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _client?.Dispose();
            _client = null;
            _cts?.Dispose();
            _publishLock.Dispose();
        }
    }
}

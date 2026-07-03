// Copyright (c) 2020 TrakHound Inc., All Rights Reserved.
// TrakHound Inc. licenses this file to you under the MIT license.
//
// Structural pattern retained from the original TrakHound MTConnect.NET-Haas
// (https://github.com/TrakHound/MTConnect.NET-Haas). Adapted into this fork
// with doc-comment additions to satisfy the WarningsAsErrors;CS1591 gate.
// Fork replaces the original fixed 500ms sleep + single unvalidated read with
// a bounded, terminator-aware read so responses cannot desync across commands,
// a dead endpoint cannot hang the poll thread, and replies return in
// milliseconds instead of half a second.

using MTConnect.Logging;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace MTConnect.Haas
{
    /// <summary>TCP transport for Haas Q-code commands. Typically talks to a Moxa NPort serial-to-Ethernet converter fronting the Haas HMI's RS-232 port. Blocking (synchronous) TcpClient; call from a single polling thread — not thread-safe.</summary>
    public class Ethernet
    {
        private const int ConnectTimeoutMs = 5000;   // bound on TCP connect so a filtered host can't stall a poll cycle for ~21s
        private const int ResponseTimeoutMs = 2000;  // max wait for the first byte of a reply
        private const int QuietPeriodMs = 150;       // silence after the last chunk that marks an unterminated reply complete

        /// <summary>Hostname or IP address of the TCP endpoint (usually the Moxa's IP).</summary>
        public string Server;

        /// <summary>TCP port on the endpoint (Moxa NPort defaults: 4001 for port 1, 4002 for port 2, …).</summary>
        public int Port;

        private TcpClient client = null;
        private NetworkStream stream = null;
        private readonly byte[] _receiveBuffer = new byte[2048];


        /// <summary>Raised when the transport wants to surface a log line to the hosting module.</summary>
        public event MTConnectLogEventHandler LogReceived;


        /// <summary>Creates a client bound to the given endpoint; connection is deferred until <see cref="Connect"/> or the first <see cref="SendCommand"/>.</summary>
        public Ethernet(string server, int port)
        {
            Server = server;
            Port = port;
        }

        /// <summary>Opens the TCP connection, bounded by a 5s connect timeout. Any previous connection is closed first. Errors are logged via <see cref="LogReceived"/> and swallowed; <see cref="SendCommand"/> returns null while disconnected.</summary>
        public void Connect()
        {
            Close();

            TcpClient newClient = null;
            try
            {
                newClient = new TcpClient();
                var ar = newClient.BeginConnect(Server, Port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(ConnectTimeoutMs))
                {
                    throw new SocketException((int)SocketError.TimedOut);
                }
                newClient.EndConnect(ar);

                stream = newClient.GetStream();
                stream.ReadTimeout = ResponseTimeoutMs;
                client = newClient;
            }
            catch (Exception ex)
            {
                try { newClient?.Close(); } catch { }
                client = null;
                stream = null;
                Log(MTConnectLogLevel.Error, $"Connect failed for {Server}:{Port}: {ex.Message}");
            }
        }

        /// <summary>Closes the TCP connection and disposes the underlying <see cref="TcpClient"/> and <see cref="NetworkStream"/>. Safe to call when nothing is open.</summary>
        public void Close()
        {
            try
            {
                stream?.Close();
                client?.Close();
            }
            catch (Exception ex)
            {
                Log(MTConnectLogLevel.Warning, $"Close error: {ex.Message}");
            }
            finally
            {
                stream = null;
                client = null;
            }
        }

        /// <summary>Sends a Q-code command and returns the trimmed ASCII response, or null on transport failure (not connected, no reply within 2s, or connection dropped). Auto-reconnects if the socket has dropped. Reads until an end-of-line / '&gt;' prompt terminator, falling back to a short quiet period for unterminated replies — no fixed sleeps.</summary>
        public string SendCommand(string cmd)
        {
            try
            {
                if (!IsConnected()) Connect();
                if (!IsConnected()) return null;

                // Drain any stale bytes left over from a previous timed-out exchange
                // so a late reply can never be attributed to this (or a later) command.
                DrainPending();

                var data = Encoding.ASCII.GetBytes(cmd + "\r\n");
                stream.Write(data, 0, data.Length);

                var response = new StringBuilder();
                long deadline = Environment.TickCount64 + (ResponseTimeoutMs * 2);
                stream.ReadTimeout = ResponseTimeoutMs;

                while (Environment.TickCount64 < deadline)
                {
                    int bytes;
                    try
                    {
                        bytes = stream.Read(_receiveBuffer, 0, _receiveBuffer.Length);
                    }
                    catch (IOException)
                    {
                        // Read timeout: with no data yet this is a transport failure;
                        // after data, the quiet period elapsing completes the reply.
                        break;
                    }

                    if (bytes <= 0)
                    {
                        // Remote closed the connection — force a clean reconnect next call.
                        Close();
                        break;
                    }

                    response.Append(Encoding.ASCII.GetString(_receiveBuffer, 0, bytes));
                    if (IsCompleteResponse(response)) break;

                    stream.ReadTimeout = QuietPeriodMs;
                }

                if (response.Length == 0)
                {
                    // Timed out without a single byte — treat the link as dead so the
                    // next SendCommand reconnects instead of reading a stale stream.
                    Close();
                    return null;
                }

                var responseData = response.ToString().TrimEnd();
                responseData = responseData.TrimEnd('>').TrimEnd();
                return responseData.Length > 0 ? responseData : null;
            }
            catch (Exception ex)
            {
                Log(MTConnectLogLevel.Error, $"SendCommand failed for {Server}:{Port}: {ex.Message}");
                Close();
                return null;
            }
        }

        private bool IsConnected()
        {
            try { return client != null && stream != null && client.Connected; }
            catch { return false; }
        }

        private void DrainPending()
        {
            try
            {
                while (stream != null && stream.DataAvailable)
                {
                    if (stream.Read(_receiveBuffer, 0, _receiveBuffer.Length) <= 0) break;
                }
            }
            catch { }
        }

        // A reply is complete once it holds real content (not just whitespace or the
        // leading '>' of ">SERIAL NUMBER, ...") and ends at a line break or the '>'
        // prompt the Haas prints for the next command. Unterminated replies fall
        // through to the quiet-period timeout in SendCommand.
        private static bool IsCompleteResponse(StringBuilder response)
        {
            var hasContent = false;
            for (int i = 0; i < response.Length; i++)
            {
                var c = response[i];
                if (!char.IsWhiteSpace(c) && c != '>')
                {
                    hasContent = true;
                    break;
                }
            }
            if (!hasContent) return false;

            var last = response[response.Length - 1];
            return last == '\n' || last == '>';
        }

        private void Log(MTConnectLogLevel logLevel, string message)
        {
            if (LogReceived != null) LogReceived.Invoke(this, logLevel, message);
        }
    }
}

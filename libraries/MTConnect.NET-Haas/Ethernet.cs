// Copyright (c) 2020 TrakHound Inc., All Rights Reserved.
// TrakHound Inc. licenses this file to you under the MIT license.
//
// Structural pattern retained from the original TrakHound MTConnect.NET-Haas
// (https://github.com/TrakHound/MTConnect.NET-Haas). Adapted into this fork
// with doc-comment additions to satisfy the WarningsAsErrors;CS1591 gate.

using MTConnect.Logging;
using System;
using System.Net.Sockets;
using System.Text;

namespace MTConnect.Haas
{
    /// <summary>TCP transport for Haas Q-code commands. Typically talks to a Moxa NPort serial-to-Ethernet converter fronting the Haas HMI's RS-232 port. Blocking (synchronous) TcpClient; call from a polling thread.</summary>
    public class Ethernet
    {
        /// <summary>Hostname or IP address of the TCP endpoint (usually the Moxa's IP).</summary>
        public string Server;

        /// <summary>TCP port on the endpoint (Moxa NPort defaults: 4001 for port 1, 4002 for port 2, …).</summary>
        public int Port;

        private TcpClient client = null;
        private NetworkStream stream = null;


        /// <summary>Raised when the transport wants to surface a log line to the hosting module.</summary>
        public event MTConnectLogEventHandler LogReceived;


        /// <summary>Creates a client bound to the given endpoint; connection is deferred until <see cref="Connect"/> or the first <see cref="SendCommand"/>.</summary>
        public Ethernet(string server, int port)
        {
            Server = server;
            Port = port;
        }

        /// <summary>Opens the TCP connection. Any previous connection is closed first. Errors are logged via <see cref="LogReceived"/> and swallowed; check <see cref="TcpClient.Connected"/> on subsequent operations to detect failure.</summary>
        public void Connect()
        {
            Close();

            try
            {
                client = new TcpClient(Server, Port);
                stream = client.GetStream();
            }
            catch (ArgumentNullException ex)
            {
                Log(MTConnectLogLevel.Error, $"ArgumentNullException: {ex.Message}");
            }
            catch (SocketException ex)
            {
                Log(MTConnectLogLevel.Error, $"SocketException: {ex.Message}");
            }
            catch (Exception ex)
            {
                Log(MTConnectLogLevel.Error, $"Exception: {ex.Message}");
            }
        }

        /// <summary>Closes the TCP connection and disposes the underlying <see cref="TcpClient"/> and <see cref="NetworkStream"/>. Safe to call when nothing is open.</summary>
        public void Close()
        {
            try
            {
                if (client != null) client.Close();
                if (stream != null) stream.Close();
            }
            catch (ArgumentNullException ex)
            {
                Log(MTConnectLogLevel.Error, $"ArgumentNullException: {ex.Message}");
            }
            catch (SocketException ex)
            {
                Log(MTConnectLogLevel.Error, $"SocketException: {ex.Message}");
            }
            catch (Exception ex)
            {
                Log(MTConnectLogLevel.Error, $"Exception: {ex.Message}");
            }
        }

        /// <summary>Sends a Q-code command over the TCP connection and returns the trimmed ASCII response, or null on error. Auto-reconnects if the underlying socket has dropped. Sleeps 500ms between write and read to give the Haas controller time to reply (mirrors the RS-232 timing).</summary>
        public string SendCommand(string cmd)
        {
            string result = null;

            try
            {
                // Connect to Server
                if (client == null || stream == null || (client != null && !client.Connected))
                {
                    Connect();
                }

                if (client != null && stream != null && client.Connected)
                {
                    // Translate the passed message into ASCII and store it as a Byte array.
                    byte[] data = Encoding.ASCII.GetBytes(cmd + "\r\n");

                    // Send the message to the connected TcpServer.
                    stream.Write(data, 0, data.Length);

                    System.Threading.Thread.Sleep(500);

                    // Buffer to store the response bytes.
                    data = new byte[2048];

                    // String to store the response ASCII representation.
                    string responseData = string.Empty;

                    // Read the first batch of the TcpServer response bytes.
                    int bytes = stream.Read(data, 0, data.Length);
                    responseData = Encoding.ASCII.GetString(data, 0, bytes);

                    if (!string.IsNullOrEmpty(responseData))
                    {
                        responseData = responseData.TrimEnd();
                        responseData = responseData.TrimEnd('>');
                        responseData = responseData.TrimEnd();
                    }

                    result = responseData;
                }
            }
            catch (ArgumentNullException ex)
            {
                Log(MTConnectLogLevel.Error, $"ArgumentNullException: {ex.Message}");
            }
            catch (SocketException ex)
            {
                Log(MTConnectLogLevel.Error, $"SocketException: {ex.Message}");
            }
            catch (Exception ex)
            {
                Log(MTConnectLogLevel.Error, $"Exception: {ex.Message}");
            }

            return result;
        }

        private void Log(MTConnectLogLevel logLevel, string message)
        {
            if (LogReceived != null) LogReceived.Invoke(this, logLevel, message);
        }
    }
}

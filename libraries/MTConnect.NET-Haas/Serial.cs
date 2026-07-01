// Copyright (c) 2023 TrakHound Inc., All Rights Reserved.
// TrakHound Inc. licenses this file to you under the MIT license.
//
// Structural pattern retained from the original TrakHound MTConnect.NET-Haas
// (https://github.com/TrakHound/MTConnect.NET-Haas). Adapted into this fork
// with doc-comment additions to satisfy the WarningsAsErrors;CS1591 gate.

using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;

namespace MTConnect.Haas
{
    /// <summary>RS-232 transport for Haas Q-code commands. Opens the serial port per call, sends the command, sleeps 1s, reads what's arrived, closes. Not intended for high-frequency polling.</summary>
    public static class Serial
    {
        /// <summary>Opens <paramref name="port"/> at 19200/N/8/1, writes <paramref name="cmd"/> + CRLF, sleeps 1s, reads the response, and returns the comma/space-separated tokens (with the leading &gt;-marker removed). Returns null on any exception.</summary>
        public static string[] SendCommand(string cmd, string port)
        {
            string[] result = null;

            var serial = new SerialPort(port);

            // Set serial options
            serial.BaudRate = 19200;
            serial.Parity = Parity.None;
            serial.StopBits = StopBits.One;
            serial.DataBits = 7;
            serial.ReadTimeout = 10000;

            try
            {
                // Open serial port connection
                serial.Open();

                // Write the command and newline to serial
                serial.Write(cmd + "\r\n");

                Thread.Sleep(1000);

                // Read response
                string response = serial.ReadExisting();

                // Split string and return array of response values
                var list = new List<string>();
                string[] values = response.Split(new char[] { ',', ' ' });
                for (var x = 1; x <= values.Length - 1; x++)
                {
                    string val = values[x].Trim();
                    if (!string.IsNullOrEmpty(val)) list.Add(val);
                }

                result = list.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine("SendCommand() :: Exception :: " + ex.Message);
            }
            finally
            {
                // Close serial connection
                if (serial.IsOpen) serial.Close();
            }

            return result;
        }
    }
}

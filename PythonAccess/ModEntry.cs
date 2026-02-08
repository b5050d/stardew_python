using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace PythonAccess
{
    internal sealed class ModEntry : Mod
    {
        private const int Port = 7777;
        private const byte Ping = 0xAA;

        private TcpListener? listener;
        private TcpClient? client;
        private NetworkStream? stream;
        private Thread? listenThread;
        private bool connected;

        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            StartServer();
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            StopServer();
        }

        /// <summary>Start the TCP listener on a background thread.</summary>
        private void StartServer()
        {
            if (listener != null)
                return;

            listener = new TcpListener(IPAddress.Loopback, Port);
            listener.Start();
            Monitor.Log($"TCP server listening on 127.0.0.1:{Port}", LogLevel.Info);

            listenThread = new Thread(WaitForClient)
            {
                IsBackground = true,
                Name = "PythonAccess-Listen"
            };
            listenThread.Start();
        }

        /// <summary>Background thread: blocks until a Python client connects.</summary>
        private void WaitForClient()
        {
            try
            {
                Monitor.Log("Waiting for Python client to connect...", LogLevel.Info);
                client = listener!.AcceptTcpClient();
                client.NoDelay = true;
                stream = client.GetStream();
                connected = true;
                Monitor.Log("Python client connected!", LogLevel.Info);

                // Send the first ping immediately
                SendPing();
            }
            catch (SocketException)
            {
                // Listener was stopped (game returned to title / shutdown)
            }
            catch (ObjectDisposedException)
            {
                // Listener disposed during shutdown
            }
        }

        /// <summary>Every game tick, check if Python has responded with 0xAA.</summary>
        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!connected || stream == null)
                return;

            try
            {
                if (stream.DataAvailable)
                {
                    int b = stream.ReadByte();
                    if (b == Ping)
                    {
                        Monitor.Log("Received 0xAA from Python, sending 0xAA back", LogLevel.Trace);
                        SendPing();
                    }
                    else if (b == -1)
                    {
                        Monitor.Log("Python client disconnected.", LogLevel.Warn);
                        Disconnect();
                    }
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Connection error: {ex.Message}", LogLevel.Error);
                Disconnect();
            }
        }

        private void SendPing()
        {
            try
            {
                stream!.WriteByte(Ping);
                stream.Flush();
                Monitor.Log("Sent 0xAA to Python", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to send ping: {ex.Message}", LogLevel.Error);
                Disconnect();
            }
        }

        private void Disconnect()
        {
            connected = false;
            stream?.Close();
            client?.Close();
            stream = null;
            client = null;

            // Re-listen for a new connection
            listenThread = new Thread(WaitForClient)
            {
                IsBackground = true,
                Name = "PythonAccess-Listen"
            };
            listenThread.Start();
            Monitor.Log("Waiting for Python client to reconnect...", LogLevel.Info);
        }

        private void StopServer()
        {
            connected = false;
            stream?.Close();
            client?.Close();
            listener?.Stop();
            stream = null;
            client = null;
            listener = null;
            Monitor.Log("TCP server stopped.", LogLevel.Info);
        }
    }
}

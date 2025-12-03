using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace AssettoCorsaSharedMemory
{
    public class ACCUdpRemoteClient : IDisposable
    {
        private UdpClient _client;
        private volatile bool _registered;
        private Task _registerLoopTask;
        public BroadcastingNetworkProtocol MessageHandler { get; set; }
        public string IpPort { get; }
        public string Ip { get; }
        public int Port { get; }
        public string DisplayName { get; }
        public string ConnectionPassword { get; }
        public string CommandPassword { get; }
        public int MsRealtimeUpdateInterval { get; }
        private CancellationTokenSource _cts;
        private Task _listenerTask;
        /// <summary>
        /// To get the events delivered inside the UI thread, just create this object from the UI thread/synchronization context.
        /// </summary>
        public ACCUdpRemoteClient(string ip, int port, string displayName, string connectionPassword, string commandPassword, int msRealtimeUpdateInterval, Action<Exception, string, bool, bool> bugger, int previousConnectionId)
        {
            Ip = ip;
            Port = port;
            IpPort = $"{ip}:{port}";
            _bugger = bugger;
            Log.ForContext("Context", "Sim").Verbose("ACCUdpRemoteClient: Ip={Ip} Port={IpPort}", Ip, Port);
            MessageHandler = new BroadcastingNetworkProtocol(IpPort, Send, previousConnectionId);
            MessageHandler.OnConnectionStateChanged += (id, success, ro, err) =>
            {
                _registered = success;
                if (success)
                    Log.ForContext("Context", "Sim").Information("ACC UDP registered (id={Id}, readonly={RO})", id, ro);
                else
                    Log.ForContext("Context", "Sim").Warning("ACC UDP registration failed: {Err}", err);
            };
            _client = new UdpClient();

            // Prevent UDP "ConnectionReset" SocketException on Windows when receiving ICMP Port Unreachable
            // Reference: https://stackoverflow.com/a/14388707
            try
            {
                const int SIO_UDP_CONNRESET = -1744830452; // 0x9800000C
                _client.Client.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
            }
            catch (Exception ex)
            {
                // If not supported on platform, log as verbose and continue.
                Log.ForContext("Context", "Sim").Verbose(ex, "Failed to disable UDP connection reset (SIO_UDP_CONNRESET). Continuing without it.");
            }
            
            DisplayName = displayName;
            ConnectionPassword = connectionPassword;
            CommandPassword = commandPassword;
            MsRealtimeUpdateInterval = msRealtimeUpdateInterval;
        }

        public bool Start()
        {
            try
            {
                Log.ForContext("Context", "Sim").Verbose("ACCUdpRemoteClient Start");
                // Validate required passwords early to avoid undefined behavior or server rejects.
                // CommandPassword is allowed to be empty string but not null.
                if (string.IsNullOrWhiteSpace(ConnectionPassword) || CommandPassword == null)
                {
                    var ex = new ArgumentNullException("UDP passwords not configured",
                        new NullReferenceException("ACCAdapter.ConnectUdp: missing UDP passwords"));
                    Log.ForContext("Context", "Sim").Warning("ACC UDP connection requires passwords (connection + command). Configure them before connecting.");
                    Bug(ex, "UDP passwords not configured: ACCAdapter.ConnectUdp: missing UDP passwords");
                    return false;
                }
                _client.Connect(Ip, Port);
                _cts = new CancellationTokenSource();
                _listenerTask = Task.Run(() => ConnectAndRun(_cts.Token), _cts.Token);
                // keep sending REGISTER until acknowledged (or cancelled)
                _registerLoopTask = Task.Run(() => RegisterUntilConnected(_cts.Token), _cts.Token);
                return true;
            }
            catch (Exception e)
            {
                Bug(e, "ACCUdpRemoteClient.Start");
                return false;
            }
        }

        private void Send(byte[] payload)
        {
            try
            {
                if (_client == null) return;
                _client.Send(payload, payload.Length);
            }
            catch (ObjectDisposedException)
            {
                // Ignore during shutdown
            }
            catch (SocketException ex)
            {
                Log.ForContext("Context", "Sim").Verbose(ex, "ACC UDP Send failed");
            }
        }
        
        private async Task ConnectAndRun(CancellationToken token)
        {
            Log.ForContext("Context", "Sim").Verbose("ACCUdpRemoteClient ConnectAndRun");
            //Log.ForContext("Context", "Sim").Information("Waiting for {DisplayName} connection...", DisplayName);
            
            RequestConnection();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Await the task. This will correctly propagate exceptions to the catch block.
                    // Pass the token to ReceiveAsync. This makes it cancellable.
                    if (_client == null)
                        return;
                    
                    var udpPacket = await _client.ReceiveAsync();//.WithCancellation(token);

                    if (udpPacket.Buffer.Length == 0)
                        continue;

                    using var ms = new MemoryStream(udpPacket.Buffer);
                    using var reader = new BinaryReader(ms);
                    // Handler can be nulled during Dispose; guard against races
                    MessageHandler?.ProcessMessage(reader);
                }
                catch (ObjectDisposedException)
                {
                    // This is an expected, clean shutdown signal.
                    // It's thrown when _client.Close() or Dispose() is called on another thread.
                    Log.ForContext("Context", "Sim").Verbose("UdpClient was disposed, listener task is shutting down cleanly");
                    // The token will likely be cancelled, but we break here just in case.
                    break;
                }
                catch (OperationCanceledException)
                {
                    // This is the expected exception when we call Stop(). It's a clean shutdown.
                    Log.ForContext("Context", "Sim").Verbose("ACCUdpRemoteClient listener task cancelled");
                    break; // Exit the loop
                }
                catch (EndOfStreamException ex)
                {
                    // This error indicates an attempt was made to read beyond the end of a data stream, potentially disrupting real-time data broadcasting from the application. Users may lose important telemetry data.
                    // This usually means we received a truncated or malformed UDP packet while
                    // parsing lap data (e.g. protocol mismatch or network issue).
                    // We skip this packet but keep the listener alive so only a single update is lost.

                    Log.ForContext("Context", "Sim").Verbose(ex, "ACC UDP: Truncated/malformed realtime packet while reading lap data. Skipping packet");

                    // Optionally report it through the existing bug callback if you want it in your error telemetry.
                    // Be careful not to spam, but given how rare this is reported, it's probably fine.
                    if (_endOfStreamExceptions >= 0)
                        _endOfStreamExceptions++;
                    
                    if (_endOfStreamExceptions > 20)
                    {
                        _endOfStreamExceptions = -1; //Disables future bug reports
                        Bug(ex, "ACC UdpRemoteClient.ConnectAndRun: EndOfStream while parsing realtime car update (lap data). Packet ignored.");
                    }

                    // Do NOT break; just continue to the next packet.
                }
                catch (SocketException ex)
                {
                    // A SocketException can be a real network error OR part of a clean shutdown.
                    // We can tell the difference by checking the cancellation token.
                    if (token.IsCancellationRequested)
                    {
                        // This is an expected exception during a controlled shutdown.
                        Log.ForContext("Context", "Sim").Verbose(ex, "SocketException received during cancellation, shutting down cleanly");
                        break;
                    }
                    
                    if (ex.SocketErrorCode != SocketError.ConnectionReset) //ConnectionReset: An existing connection was forcibly closed by the remote host
                    {
                        // This is an unexpected network error. Log it and wait before retrying.
                        // This is where "Connessione in corso interrotta..." would be caught if it's not a shutdown.
                        Log.ForContext("Context", "Sim").Verbose(ex, "SocketException in listener loop. Retrying in 1s.");
                    }
                    
                    await Task.Delay(1000, token); // Use the token here so the delay is also cancellable.
                }
                catch (Exception ex)
                {
                    // Catch any other unexpected errors, like _client being null (when shut down)
                    if (ex is NullReferenceException || token.IsCancellationRequested) 
                        break; // If cancellation was requested during an error, just exit.
                        
                    Bug(ex, "ACC UdpRemoteClient ConnectAndRun unhandled exception in loop.");
                    await Task.Delay(1000, token); // Wait a bit before retrying
                }
            }
            Log.ForContext("Context", "Sim").Verbose("ACCUdpRemoteClient listener loop finished.");
        }

        private void Bug(Exception ex, string msg)
        {
            _bugger?.Invoke(ex, msg, false, false);
        }

        private void RequestConnection()
        {
            try
            {
                if (MessageHandler == null)
                {
                    Log.ForContext("Context", "Sim").Verbose("ACC UDP: RequestConnection MessageHandler null");
                    return;
                }
                
                MessageHandler.RequestConnection(DisplayName, ConnectionPassword, MsRealtimeUpdateInterval, CommandPassword);
            }
            catch (Exception e)
            {
                Bug(e, "ACC UdpRemoteClient RequestConnection");
            }
        }

        private async Task RegisterUntilConnected(CancellationToken token)
        {
            var attempt = 0;
            while (!_registered && !token.IsCancellationRequested)
            {
                try
                {
                    RequestConnection();
                }
                catch (Exception ex)
                {
                    Bug(ex, "ACC UdpRemoteClient RegisterUntilConnected RequestConnection");
                }

                attempt++;
                var delayMs = attempt < 5 ? 2000 : 5000;
                try { await Task.Delay(delayMs, token); } catch (OperationCanceledException) { break; }
            }
        }
        
        public async Task Stop()
        {
            // Check if there's anything to stop.
            if (_cts == null || _listenerTask == null || _cts.IsCancellationRequested)
                return;

            try
            {
                // 1. Signal cancellation. This is good practice.
                _cts.Cancel();

                // 2. NEW: Send the graceful disconnection message WHILE the client is still open.
                //    This is the key fix.
                MessageHandler?.Disconnect();

                // Optional: Give the UDP packet a moment to be sent before closing everything.
                await Task.Delay(50); 

                // 3. NOW, forcefully unblock the ReceiveAsync call by closing the client.
                //    This will cause the listener to throw an exception and exit its loop.
                _client?.Close();

                // 4. Await the listener task to ensure it has fully completed.
                //    (Your existing timeout logic here is good).
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(2));
                Task all = _registerLoopTask == null ? _listenerTask : Task.WhenAll(_listenerTask, _registerLoopTask);
                var completedTask = await Task.WhenAny(all, timeoutTask);

                if (completedTask == timeoutTask || (all is Task t && !t.IsCompleted))
                {
                    Log.ForContext("Context", "Sim").Verbose("ACC UDP tasks did not stop in time.");
                }
                else
                {
                    await all;
                }
            }
            catch (Exception ex)
            {
                // This will catch any exception that the _listenerTask might have thrown upon completion.
                // We check for OperationCanceledException because that's an expected, clean shutdown.
                if (ex is not OperationCanceledException)
                {
                    Bug(ex, "Exception while awaiting listener task shutdown.");
                }
            }
            finally
            {
                Dispose();
            }
        }

        private bool disposedValue;
        private readonly Action<Exception,string,bool,bool> _bugger;
        private int _endOfStreamExceptions;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    try
                    {
                        _client?.Dispose();
                        _client = null;
                        
                        _cts?.Dispose();
                        _cts = null;
                        
                        _registerLoopTask = null;
                        _registered = false;
                        
                        MessageHandler = null;
                    }
                    catch (Exception ex)
                    {
                        Bug(ex, "ACCUdpRemoteClient Dispose: " + ex.Message);
                    }
                }

                disposedValue = true;
            }
        }

        // TO DO?: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ACCUdpRemoteClient() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }
        // This code added to correctly implement the disposable pattern.

        public void Dispose()
        {
            Dispose(true);
            // uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
    }
}

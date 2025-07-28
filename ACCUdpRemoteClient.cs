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
            MessageHandler = new BroadcastingNetworkProtocol(IpPort, Send, previousConnectionId);
            _client = new UdpClient();
            
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
                _client.Connect(Ip, Port);
                _cts = new CancellationTokenSource();
                _listenerTask = Task.Run(() => ConnectAndRun(_cts.Token), _cts.Token);
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
            _client.Send(payload, payload.Length);
        }
        
        private async Task ConnectAndRun(CancellationToken token)
        {
            Log.ForContext("Context", "Sim").Verbose("ACCUdpRemoteClient ConnectAndRun");
            RequestConnection();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Await the task. This will correctly propagate exceptions to the catch block.
                    // Pass the token to ReceiveAsync. This makes it cancellable.
                    var udpPacket = await _client.ReceiveAsync();//.WithCancellation(token);

                    if (udpPacket.Buffer.Length == 0)
                        continue;

                    using var ms = new MemoryStream(udpPacket.Buffer);
                    using var reader = new BinaryReader(ms);
                    MessageHandler.ProcessMessage(reader);
                }
                catch (ObjectDisposedException)
                {
                    // This is an expected, clean shutdown signal.
                    // It's thrown when _client.Close() or Dispose() is called on another thread.
                    Log.ForContext("Context", "Sim").Verbose("UdpClient was disposed, listener task is shutting down cleanly.");
                    // The token will likely be cancelled, but we break here just in case.
                    break;
                }
                catch (OperationCanceledException)
                {
                    // This is the expected exception when we call Stop(). It's a clean shutdown.
                    Log.ForContext("Context", "Sim").Verbose("ACCUdpRemoteClient listener task cancelled.");
                    break; // Exit the loop
                }
                catch (SocketException ex)
                {
                    // A SocketException can be a real network error OR part of a clean shutdown.
                    // We can tell the difference by checking the cancellation token.
                    if (token.IsCancellationRequested)
                    {
                        // This is an expected exception during a controlled shutdown.
                        Log.ForContext("Context", "Sim").Verbose(ex, "SocketException received during cancellation, shutting down cleanly.");
                        break;
                    }
                    
                    if (ex.SocketErrorCode != SocketError.ConnectionReset) //ConnectionReset: An existing connection was forcibly closed by the remote host
                    {
                        // This is an unexpected network error. Log it and wait before retrying.
                        // This is where "Connessione in corso interrotta..." would be caught if it's not a shutdown.
                        Log.ForContext("Context", "Sim").Warning(ex, "SocketException in listener loop. Retrying in 1s.");
                    }
                    
                    await Task.Delay(1000, token); // Use the token here so the delay is also cancellable.
                }
                catch (Exception ex)
                {
                    // Catch any other unexpected errors
                    if (token.IsCancellationRequested) 
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
                MessageHandler.RequestConnection(DisplayName, ConnectionPassword, MsRealtimeUpdateInterval, CommandPassword);
            }
            catch (Exception e)
            {
                Bug(e, "ACC UdpRemoteClient RequestConnection");
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
                var completedTask = await Task.WhenAny(_listenerTask, timeoutTask);

                if (completedTask == timeoutTask || !_listenerTask.IsCompleted)
                {
                    Log.ForContext("Context", "Sim").Warning("UDP listener task did not stop in time.");
                }
                else
                {
                    // The listener completed. Await it to observe any final exceptions.
                    // This is critical to prevent a new UnobservedTaskException.
                    await _listenerTask;
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
                        
                        MessageHandler = null;
                    }
                    catch (Exception ex)
                    {
                        Bug(ex, "ACCUdpRemoteClient Dispose: " + ex.Message);
                    }
                }

                // free unmanaged resources (unmanaged objects) and override a finalizer below.
                // set large fields to null.

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

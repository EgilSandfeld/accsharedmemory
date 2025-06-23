using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace AssettoCorsaSharedMemory
{
    public class ACCUdpRemoteClient : IDisposable
    {
        private UdpClient _client;
        private bool _runUdp;
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
                    var udpPacket = await _client.ReceiveAsync().WithCancellation(token);

                    if (udpPacket.Buffer.Length == 0)
                    {
                        continue;
                    }

                    using var ms = new MemoryStream(udpPacket.Buffer);
                    using var reader = new BinaryReader(ms);
                    MessageHandler.ProcessMessage(reader);
                }
                catch (OperationCanceledException)
                {
                    // This is the expected exception when we call Stop(). It's a clean shutdown.
                    Log.ForContext("Context", "Sim").Verbose("ACCUdpRemoteClient listener task cancelled.");
                    break; // Exit the loop
                }
                catch (SocketException ex)
                {
                    // Handle specific socket errors, e.g., if the remote host closes the connection
                    Log.ForContext("Context", "Sim").Warning(ex, "SocketException in listener loop.");
                    await Task.Delay(1000, token); // Wait a bit before retrying
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
        
        // private void ConnectAndRun()
        // {
        //     Log.ForContext("Context", "Sim").Verbose("ACCUdpRemoteClient ConnectAndRun");
        //     _runUdp = true;
        //     try
        //     {
        //         Task.Run(async () =>
        //         {
        //             while (_runUdp)
        //             {
        //                 try
        //                 {
        //                     //await ReceiveLoop();
        //
        //                     if (_client == null || !_client.Client.Connected)
        //                     {
        //                         await Task.Delay(1000);
        //                         continue;
        //                     }
        //
        //                     Task<UdpReceiveResult> udpReceiveTask;
        //                     try
        //                     {
        //                         udpReceiveTask = _client.ReceiveAsync();
        //                     }
        //                     catch (Exception e)
        //                     {
        //                         Bug(e, "_client.ReceiveAsync");
        //                         await Task.Delay(1000);
        //                         continue;
        //                     }
        //
        //                     if (!udpReceiveTask.IsCompleted)
        //                         udpReceiveTask.Wait(20000);
        //
        //                     if (udpReceiveTask.Status == TaskStatus.WaitingForActivation)
        //                     {
        //                         try
        //                         {
        //                             _client.Close();
        //                             _client = null;
        //                             _client = new UdpClient();
        //                             _client.Connect(Ip, Port);
        //                             RequestConnection();
        //                         }
        //                         catch (Exception e)
        //                         {
        //                             Bug(e, "ACC UdpRemoteClient ConnectAndRun Close Exception: " + e.Message);
        //                         }
        //
        //                         await Task.Delay(1000);
        //                         continue;
        //                     }
        //
        //                     var udpPacket = udpReceiveTask.Result;
        //
        //                     if (udpPacket.Buffer.Length == 0)
        //                     {
        //                         await Task.Delay(10);
        //                         continue;
        //                     }
        //
        //                     using var ms = new MemoryStream(udpPacket.Buffer);
        //                     using var reader = new BinaryReader(ms);
        //                     MessageHandler.ProcessMessage(reader);
        //                 }
        //                 catch (Exception ex)
        //                 {
        //                     if (ex is SocketException || ex is AggregateException ae && (ae.InnerException is ObjectDisposedException or TaskCanceledException))
        //                         break;
        //
        //                     Bug(ex, "ACC UdpRemoteClient ConnectAndRun Exception: " + ex.Message);
        //                 }
        //             }
        //         });
        //     }
        //     catch (Exception ex)
        //     {
        //         if (ex is AggregateException ae && ae.InnerException is ObjectDisposedException)
        //             return;
        //
        //         Bug(ex, "ACC UdpRemoteClient ConnectAndRun Outer Exception: " + ex.Message);
        //     }
        //
        //     // Immediately send connection request after starting the listener
        //     RequestConnection();
        // }

        // private async Task ReceiveLoop()
        // {
        //     Log.ForContext("Context", "Sim").Verbose("ACC ReceiveLoop");
        //     var counter = 0;
        //     while (_client != null)
        //     {
        //         try
        //         {
        //             var udpPacket = await _client.ReceiveAsync();
        //             using var ms = new System.IO.MemoryStream(udpPacket.Buffer);
        //             using var reader = new System.IO.BinaryReader(ms);
        //             MessageHandler.ProcessMessage(reader);
        //             counter++;
        //             if (MessageHandler.ConnectionId == -1 && counter % 1000 == 0)
        //                 Log.ForContext("Context", "Sim").Verbose("ACCUdpRemoteClient ReceiveLoop during non-connected, loops completed: {Counter} ", counter);
        //         }
        //         catch (ObjectDisposedException ex)
        //         {
        //             Log.ForContext("Context", "Sim").Verbose("ACC ReceiveLoop ObjectDisposedException: {Message}", ex.Message);
        //     
        //             if (!disposingValue && !disposedValue)
        //                 Bug(ex, "ACC UdpRemoteClient ConnectAndRun ObjectDisposedException: " + ex.Message);
        //             
        //             break;
        //         }
        //         catch (SocketException ex)
        //         {
        //             Log.ForContext("Context", "Sim").Verbose("ACC ReceiveLoop SocketException: {Message}", ex.Message);
        //             if (ex.ErrorCode != 10054)
        //                 Bug(ex, "ACC UdpRemoteClient ConnectAndRun SocketException: " + ex.Message);
        //         }
        //         catch (Exception ex)
        //         {
        //             Log.ForContext("Context", "Sim").Verbose("ACC ReceiveLoop Exception: {Message}", ex.Message);
        //             Bug(ex, "ACC UdpRemoteClient ConnectAndRun Exception: " + ex.Message);
        //         }
        //     }
        // }

        // private async Task ConnectAndRun()
        // {
        //     Log.ForContext("Context", "Sim").Verbose("ACCUdpRemoteClient ConnectAndRun");
        //     RequestConnection();
        //     
        //     while (_client != null)
        //     {
        //         try
        //         {
        //             var udpPacket = await _client.ReceiveAsync();
        //             using var ms = new System.IO.MemoryStream(udpPacket.Buffer);
        //             using var reader = new System.IO.BinaryReader(ms);
        //             MessageHandler.ProcessMessage(reader);
        //         }
        //         catch (ObjectDisposedException ex)
        //         {
        //             // Shutdown happened
        //             if (!disposingValue && !disposedValue)
        //                 Bug(ex, "ACC UdpRemoteClient ConnectAndRun ObjectDisposedException: " + ex.Message);
        //             
        //             break;
        //         }
        //         catch (SocketException ex)
        //         {
        //             //An existing connection was forcibly closed by the remote host
        //             if (ex.ErrorCode != 10054)
        //                 Bug(ex, "ACC UdpRemoteClient ConnectAndRun SocketException: " + ex.Message);
        //         }
        //         catch (Exception ex)
        //         {
        //             // Other exceptions
        //             Bug(ex, "ACC UdpRemoteClient ConnectAndRun Exception: " + ex.Message);
        //         }
        //     }
        // }

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

        // public async Task Stop()
        // {
        //     if (MessageHandler == null || MessageHandler.ConnectionId == -1)
        //         return;
        //
        //     _runUdp = false;
        //     MessageHandler.Disconnect();
        //     await Task.Delay(100);
        //     Dispose();
        //     MessageHandler = null;
        // }
        
        public async Task Stop()
        {
            // Check if there's anything to stop.
            if (_cts == null || _listenerTask == null || _cts.IsCancellationRequested)
                return;

            try
            {
                // 1. Signal the listener task to stop.
                _cts.Cancel();

                // 2. Create a timeout task.
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(2));

                // 3. Wait for EITHER the listener task to complete OR the timeout to expire.
                var completedTask = await Task.WhenAny(_listenerTask, timeoutTask);

                // 4. Check if the timeout task is the one that completed.
                if (completedTask == timeoutTask)
                {
                    // If so, the listener task has timed out.
                    Log.ForContext("Context", "Sim").Warning("UDP listener task did not stop in time.");
                }
                else
                {
                    // The listener task completed successfully. We can now safely check its status.
                    // Awaiting it here will propagate any final exception from the task, preventing UnobservedTaskException.
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
                // 5. Always proceed with disposal.
                MessageHandler?.Disconnect();
                Dispose();
            }
        }

        private bool disposedValue;
        private bool disposingValue;
        private readonly Action<Exception,string,bool,bool> _bugger;

        protected virtual void Dispose(bool disposing)
        {
            _runUdp = false;
            if (!disposedValue)
            {
                disposingValue = true;
                if (disposing)
                {
                    try
                    {
                        if (_client != null)
                        {
                            _client?.Close();
                            _client?.Dispose();
                            _client = null;
                        }
                        
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
                disposingValue = false;
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

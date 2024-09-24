using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using Serilog;

namespace AssettoCorsaSharedMemory
{
    public class ACCUdpRemoteClient : IDisposable
    {
        private UdpClient _client;
        private Task _listenerTask;
        public BroadcastingNetworkProtocol MessageHandler { get; set; }
        public string IpPort { get; }
        public string Ip { get; }
        public int Port { get; }
        public string DisplayName { get; }
        public string ConnectionPassword { get; }
        public string CommandPassword { get; }
        public int MsRealtimeUpdateInterval { get; }

        /// <summary>
        /// To get the events delivered inside the UI thread, just create this object from the UI thread/synchronization context.
        /// </summary>
        public ACCUdpRemoteClient(string ip, int port, string displayName, string connectionPassword, string commandPassword, int msRealtimeUpdateInterval)
        {
            Ip = ip;
            Port = port;
            IpPort = $"{ip}:{port}";
            MessageHandler = new BroadcastingNetworkProtocol(IpPort, Send);
            _client = new UdpClient();
            

            DisplayName = displayName;
            ConnectionPassword = connectionPassword;
            CommandPassword = commandPassword;
            MsRealtimeUpdateInterval = msRealtimeUpdateInterval;
        }

        public void Start()
        {
            _client.Connect(Ip, Port);
            _listenerTask = ConnectAndRun();
        }

        private void Send(byte[] payload)
        {
            /*var sent = */_client.Send(payload, payload.Length);
        }

        private async Task ConnectAndRun()
        {
            MessageHandler.RequestConnection(DisplayName, ConnectionPassword, MsRealtimeUpdateInterval, CommandPassword);
            while (_client != null)
            {
                try
                {
                    var udpPacket = await _client.ReceiveAsync();
                    using var ms = new System.IO.MemoryStream(udpPacket.Buffer);
                    using var reader = new System.IO.BinaryReader(ms);
                    MessageHandler.ProcessMessage(reader);
                }
                catch (ObjectDisposedException ex)
                {
                    // Shutdown happened
                    if (!disposingValue && !disposedValue)
                        Log.Warning("ACC UdpRemoteClient ConnectAndRun ObjectDisposedException: {Message}", ex.Message);
                    
                    break;
                }
                catch (SocketException ex)
                {
                    //An existing connection was forcibly closed by the remote host
                    if (ex.ErrorCode != 10054)
                        Log.Error(ex, "ACC UdpRemoteClient ConnectAndRun SocketException: " + ex.Message);
                }
                catch (Exception ex)
                {
                    // Other exceptions
                    Log.Error(ex, "ACC UdpRemoteClient ConnectAndRun Exception: " + ex.Message);
                }
            }
        }

        public Task Stop()
        {
            if (MessageHandler == null || MessageHandler.ConnectionId == -1)
                return Task.CompletedTask;
            
            MessageHandler.Disconnect();
            Dispose();
            MessageHandler = null;
            return Task.CompletedTask;
        }

        private bool disposedValue;
        private bool disposingValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                disposingValue = true;
                if (disposing)
                {
                    try
                    {
                        if (_client != null)
                        {
                            _client.Close();
                            _client.Dispose();
                            _client = null;
                        }

                    }
                    catch (Exception ex)
                    {
                        Log.Warning("ACCUdpRemoteClient Dispose: {Message}", ex.Message);
                    }
                }

                // free unmanaged resources (unmanaged objects) and override a finalizer below.
                // set large fields to null.

                disposedValue = true;
                disposingValue = false;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
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

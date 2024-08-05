using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AssettoCorsaSharedMemory;

namespace Sim.AssettoCorsaCompetizione;

public class AccClient : IDisposable
{
    public Action<string> Logger { get; }
    //public bool IsAlive => _thread != null && _thread.IsAlive;

    public string CommandPassword { get; set; }
    public int MsRealtimeUpdateInterval { get; set; }
    public string ConnectionPassword { get; set; }
    public string DisplayName { get; set; }
    public string IpAddress { get; set; }
    public int Port { get; set; }

    private static readonly object UdpLock = new ();
    private DateTime _clientStartTime;
    
    //private IPEndPoint _server;
    //private string _displayName;
    //private int _updateIntervalMs;
    private UdpClient _udpClient;
    private UDPState _connectionState = UDPState.Disconnected;
    private const int BroadcastingProtocolVersion = 4;
    private int? _connectionId;
    //private bool _writable;
    private readonly Dictionary<int, int> _entryListCars = new ();
    private readonly Dictionary<InboundMessageTypes, Action<BinaryReader>> _getMethods;
    //private bool _stopSignal;
    //private Thread _thread;
    //private ThreadedSocketReader _reader;
    private bool _udpAlive;
    private bool _requestedEntryList;
    private bool _simPaused;

    public event EventHandler<UDPState> OnConnectionStateChange;
    public event EventHandler<TrackData> OnTrackDataUpdate;
    public event EventHandler<EntryListCar> OnEntryListCarUpdate;
    public event EventHandler<RealtimeUpdate> OnRealtimeUpdate;
    public event EventHandler<RealtimeCarUpdate> OnRealtimeCarUpdate;
    public event EventHandler<BroadcastingEvent> OnBroadcastingEvent;

    public UDPState ConnectionState => _connectionState;

    public AccClient(Action<string> logger)
    {
        Logger = logger;
        _clientStartTime = DateTime.Now;
        _getMethods = new Dictionary<InboundMessageTypes, Action<BinaryReader>>
        {
            { InboundMessageTypes.REGISTRATION_RESULT, GetRegistrationResult },
            { InboundMessageTypes.REALTIME_UPDATE, GetRealtimeUpdate },
            { InboundMessageTypes.REALTIME_CAR_UPDATE, GetRealtimeCarUpdate },
            { InboundMessageTypes.ENTRY_LIST, GetEntryList },
            { InboundMessageTypes.TRACK_DATA, GetTrackData },
            { InboundMessageTypes.ENTRY_LIST_CAR, GetEntryListCar },
            { InboundMessageTypes.BROADCASTING_EVENT, GetBroadcastingEvent }
        };
    }

    public void UpdateSimPaused(bool paused)
    {
        _simPaused = paused;
    }

    private void UpdateConnectionState(UDPState state)
    {
        if (state != _connectionState)
        {
            _connectionState = state;
            _requestedEntryList = false;
            OnConnectionStateChange?.Invoke(this, _connectionState);
        }
    }
    
    private void Send(params object[] args)
    {
        try
        {
            var buffer = new List<byte>();
            foreach (var arg in args)
            {
                if (arg is byte b)
                    buffer.Add(b);
                else if (arg is int i)
                    buffer.AddRange(BitConverter.GetBytes(i));
                else if (arg is float f)
                    buffer.AddRange(BitConverter.GetBytes(f));
                else if (arg is bool bl)
                    buffer.Add(bl ? (byte)1 : (byte)0);
                else if (arg is string s)
                {
                    var encoded = Encoding.UTF8.GetBytes(s);
                    buffer.AddRange(BitConverter.GetBytes((ushort)encoded.Length));
                    buffer.AddRange(encoded);
                }
            }

            _udpClient.Send(buffer.ToArray(), buffer.Count);
        }
        catch (Exception e)
        {
            Logger?.Invoke(e.Message);
        }
    }

    private static string ReadString(BinaryReader br)
    {
        var length = br.ReadUInt16();
        var bytes = br.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }
    
    private void GetRegistrationResult(BinaryReader br)
    {
        try
        {
            _connectionId = br.ReadInt32();
            var connectionSuccess = br.ReadByte() > 0;
            var isReadonly = br.ReadByte() == 0;
            var errMsg = ReadString(br);

            Logger?.Invoke("ACC UDP Client GetRegistrationResult: " + connectionSuccess + " error: " + errMsg);

            if (isReadonly)
            {
                Stop($"ACC UDP Client GetRegistrationResult: Is read only. Rejected: ({errMsg})");
                return;
            }

            UpdateConnectionState(UDPState.Connected);
            RequestEntryList();
            RequestTrackData();
        }
        catch (Exception e)
        {
            Logger?.Invoke(e.Message);
            throw;
        }
    }

    private void GetRealtimeUpdate(BinaryReader br)
    {
        try
        {
            var update = new RealtimeUpdate
            {
                EventIndex = br.ReadUInt16(),
                SessionIndex = br.ReadUInt16(),
                SessionType = (SessionType)br.ReadByte(),
                SessionPhase = (SessionPhase)br.ReadByte(),
                SessionTimeMs = br.ReadSingle(),
                SessionEndTimeMs = br.ReadSingle(),
                FocusedCarIndex = br.ReadInt32(),
                ActiveCameraSet = ReadString(br),
                ActiveCamera = ReadString(br),
                CurrentHudPage = ReadString(br),
                IsReplayPlaying = br.ReadBoolean()
            };

            if (update.IsReplayPlaying)
            {
                update.ReplaySessionTime = br.ReadSingle();
                update.ReplayRemainingTime = br.ReadSingle();
            }

            update.TimeOfDaySeconds = br.ReadSingle();
            update.AmbientTemp = br.ReadByte();
            update.TrackTemp = br.ReadByte();
            update.Clouds = br.ReadByte() / 10f;
            update.RainLevel = br.ReadByte() / 10f;
            update.Wetness = br.ReadByte() / 10f;
            update.BestSessionLap = GetLap(br);

            OnRealtimeUpdate?.Invoke(this, update);
        }
        catch (Exception e)
        {
            Logger?.Invoke(e.Message);
            throw;
        }
    }

    private Lap GetLap(BinaryReader br)
    {
        var lap = new Lap
        {
            LapTimeMs = br.ReadInt32(),
            CarIndex = br.ReadUInt16(),
            DriverIndex = br.ReadUInt16(),
            Splits = new List<int>()
        };

        var splitCount = br.ReadByte();
        for (int i = 0; i < splitCount; i++)
            lap.Splits.Add(br.ReadInt32());

        lap.IsInvalid = br.ReadBoolean();
        lap.IsValidForBest = br.ReadBoolean();
        lap.IsOutlap = br.ReadBoolean();
        lap.IsInlap = br.ReadBoolean();
        lap.Type = lap.IsOutlap ? LapType.Outlap : (lap.IsInlap ? LapType.Inlap : LapType.Regular);

        while (lap.Splits.Count < 3)
            lap.Splits.Add(-1);

        for (int i = 0; i < lap.Splits.Count; i++)
            if (lap.Splits[i] == int.MaxValue)
                lap.Splits[i] = -1;

        if (lap.LapTimeMs == int.MaxValue)
            lap.LapTimeMs = -1;

        return lap;
    }

    private void GetRealtimeCarUpdate(BinaryReader br)
    {
        try
        {
            var update = new RealtimeCarUpdate
            {
                CarIndex = br.ReadUInt16(),
                DriverIndex = br.ReadUInt16(),
                DriverCount = br.ReadByte(),
                Gear = br.ReadByte() - 2,
                WorldPosX = br.ReadSingle(),
                WorldPosY = br.ReadSingle(),
                Yaw = br.ReadSingle(),
                Location = (CarLocation)br.ReadByte(),
                Kmh = br.ReadUInt16(),
                Position = br.ReadUInt16(),
#pragma warning disable CS0618 // Type or member is obsolete
                CupPosition = br.ReadUInt16(),
#pragma warning restore CS0618 // Type or member is obsolete
                TrackPosition = br.ReadUInt16(),
                SplinePosition = br.ReadSingle(),
                Laps = br.ReadUInt16(),
                Delta = br.ReadInt32(),
                BestSessionLap = GetLap(br),
                LastLap = GetLap(br),
                CurrentLap = GetLap(br)
            };

            OnRealtimeCarUpdate?.Invoke(this, update);

            //When the car index exists, and the number of drivers in this car matches the expected number of drivers
            if (_entryListCars.ContainsKey(update.CarIndex) && _entryListCars[update.CarIndex] == update.DriverCount)
            {
                return;
            }

            RequestEntryList();
        }
        catch (Exception e)
        {
            Logger?.Invoke(e.Message);
            throw;
        }
    }

    private void GetEntryList(BinaryReader br)
    {
        try
        {
            var oldCount = _entryListCars.Count;
            var carCount = br.ReadUInt16();
            var changes = false;
            for (int i = 0; i < carCount; i++)
            {
                var carIndex = br.ReadUInt16();
                if (_entryListCars.ContainsKey(carIndex))
                    continue;

                _entryListCars.Add(carIndex, 1);
                changes = true;
            }

            if (changes)
            {
                Logger?.Invoke($"ACC UDP Client GetEntryList from {oldCount} -> {_entryListCars.Count}");
            }
            
            _requestedEntryList = false;
        }
        catch (Exception e)
        {
            Logger?.Invoke(e.Message);
            throw;
        }
    }

    private void GetEntryListCar(BinaryReader br)
    {
        try
        {
            var car = new EntryListCar
            {
                CarIndex = br.ReadUInt16(),
                ModelType = (AssettoCorsa.CarModel)br.ReadByte(),
                TeamName = ReadString(br),
                RaceNumber = br.ReadInt32(),
                CupCategory = br.ReadByte(),
                CurrentDriverIndex = br.ReadByte(),
                Nationality = (Nationality)br.ReadInt16(),
                Drivers = new List<Driver>()
            };

            var driverCount = br.ReadByte();
            for (int i = 0; i < driverCount; i++)
            {
                car.Drivers.Add(new Driver
                {
                    FirstName = ReadString(br),
                    LastName = ReadString(br),
                    ShortName = ReadString(br),
                    Category = (DriverCategory)br.ReadByte(),
                    Nationality = (Nationality)br.ReadInt16()
                });
            }

            if (!_entryListCars.ContainsKey(car.CarIndex))
            {
                if (SecondsSinceStart() > 30)
                    Logger?.Invoke($"New car index entry: {car.CarIndex} with {car.Drivers.Count} drivers");

                _entryListCars[car.CarIndex] = car.Drivers.Count;
                OnEntryListCarUpdate?.Invoke(this, car);
                return;
            }

            if (_entryListCars[car.CarIndex] != car.Drivers.Count)
            {
                if (SecondsSinceStart() > 30)
                    Logger?.Invoke($"{_entryListCars[car.CarIndex]} -> {car.Drivers.Count} drivers in car index: {car.CarIndex}");

                _entryListCars[car.CarIndex] = car.Drivers.Count;
                OnEntryListCarUpdate?.Invoke(this, car);
            }
            else
                OnEntryListCarUpdate?.Invoke(this, car);
        }
        catch (Exception e)
        {
            Logger?.Invoke(e.Message);
            throw;
        }
    }

    private double SecondsSinceStart()
    {
        return DateTime.Now.Subtract(_clientStartTime).TotalSeconds;
    }

    private void GetTrackData(BinaryReader br)
    {
        try
        {
            var trackData = new TrackData
            {
                ConnectionId = br.ReadInt32(),
                TrackName = ReadString(br),
                TrackId = br.ReadInt32(),
                TrackMeters = br.ReadInt32(),
                CameraSets = new Dictionary<string, List<string>>(),
                HudPages = new List<string>()
            };

            trackData.TrackMeters = trackData.TrackMeters > 0 ? trackData.TrackMeters : -1;

            var cameraSetCount = br.ReadByte();
            for (int i = 0; i < cameraSetCount; i++)
            {
                var cameraSetName = ReadString(br);
                trackData.CameraSets[cameraSetName] = new List<string>();
                var cameraCount = br.ReadByte();
                for (int j = 0; j < cameraCount; j++)
                    trackData.CameraSets[cameraSetName].Add(ReadString(br));
            }

            var hudPageCount = br.ReadByte();
            for (int i = 0; i < hudPageCount; i++)
                trackData.HudPages.Add(ReadString(br));

            OnTrackDataUpdate?.Invoke(this, trackData);
        }
        catch (Exception e)
        {
            Logger?.Invoke(e.Message);
            throw;
        }
    }

    private void GetBroadcastingEvent(BinaryReader br)
    {
        try
        {
            var broadcastingEvent = new BroadcastingEvent
            {
                Type = (BroadcastingEventType)br.ReadByte(),
                Message = ReadString(br),
                TimeMs = br.ReadInt32(),
                CarIndex = br.ReadInt32()
            };

            //broadcastingEvent.CarData = _entryListCars.FirstOrDefault(x => x.CarIndex == broadcastingEvent.CarIndex);

            OnBroadcastingEvent?.Invoke(this, broadcastingEvent);
        }
        catch (Exception e)
        {
            Logger?.Invoke(e.Message);
            throw;
        }
    }

    private void RequestConnection()
    {
        Send(
            (byte)OutboundMessageTypes.REGISTER_COMMAND_APPLICATION,
            (byte)BroadcastingProtocolVersion,
            DisplayName,
            ConnectionPassword,
            MsRealtimeUpdateInterval,
            CommandPassword
        );
    }

    private void RequestDisconnection()
    {
        if (_connectionId == null)
            return;
        
        Send(
            (byte)OutboundMessageTypes.UNREGISTER_COMMAND_APPLICATION,
            _connectionId.Value
        );
    }

    private void RequestEntryList()
    {
        if (_requestedEntryList || _connectionId == null)
            return;
        
        _requestedEntryList = true;
        Send(
            (byte)OutboundMessageTypes.REQUEST_ENTRY_LIST,
            _connectionId.Value
        );
    }

    private void RequestTrackData()
    {
        if (_connectionId == null)
            return;
        
        Send(
            (byte)OutboundMessageTypes.REQUEST_TRACK_DATA,
            _connectionId.Value
        );
    }

    public void RequestFocusChange(int carIndex = -1, string cameraSet = null, string camera = null)
    {
        if (_connectionId == null)
            return;
        
        var args = new List<object>
        {
            (byte)OutboundMessageTypes.CHANGE_FOCUS,
            _connectionId.Value
        };

        if (carIndex >= 0)
        {
            args.Add(true);
            args.Add((ushort)carIndex);
        }
        else
        {
            args.Add(false);
        }

        if (!string.IsNullOrEmpty(cameraSet) && !string.IsNullOrEmpty(camera))
        {
            args.Add(true);
            args.Add(cameraSet);
            args.Add(camera);
        }
        else
        {
            args.Add(false);
        }

        Send(args.ToArray());
    }

    public void RequestInstantReplay(float startTime, float durationMs, int carIndex = -1, string cameraSet = "", string camera = "")
    {
        if (_connectionId == null)
            return;
        
        Send(
            (byte)OutboundMessageTypes.INSTANT_REPLAY_REQUEST,
            _connectionId.Value,
            startTime,
            durationMs,
            carIndex,
            cameraSet,
            camera
        );
    }

    public void RequestHudPage(string pageName)
    {
        if (_connectionId == null)
            return;
        
        Send(
            (byte)OutboundMessageTypes.CHANGE_HUD_PAGE,
            _connectionId.Value,
            pageName
        );
    }

    private UdpClient GetUdpClient(bool isSending = false)
    {
        lock(UdpLock)
        { 
            if (_udpClient == null || !_udpClient.Client.Connected)
            {
                if (_udpClient != null)
                {
                    try
                    {
                        _udpClient.Close();
                    }
                    catch (Exception e)
                    {
                        Logger?.Invoke(e.Message);
                    }
                }

                var udpClient = new UdpClient();
                //udpClient.EnableBroadcast = true;
                //udpClient.Client.ReceiveTimeout = 1000;
                //udpClient.Client.SendTimeout = 1000;
                //udpClient.ExclusiveAddressUse = false;
                udpClient.Connect(IpAddress, Port);
                _udpClient = udpClient;

                if(!isSending)
                    RequestConnection();
            }

            return _udpClient;
        }
    }

    public void Start(string url, int port, string password, string commandPassword = "", string displayName = "DRE x ACC", int updateIntervalMs = 16)
    {
        if (!string.IsNullOrEmpty(IpAddress) || _connectionState != UDPState.Disconnected)
            throw new InvalidOperationException($"Must be stopped. IpAddress={IpAddress}, _connectionState={_connectionState}");
    
        IpAddress = url;
        Port = port;
        DisplayName = $"{displayName}-{DateTime.Now:MM-dd-HH-mm-ss}";
        ConnectionPassword = password;
        MsRealtimeUpdateInterval = updateIntervalMs;
        CommandPassword = commandPassword;
        
        UpdateConnectionState(UDPState.Connecting);
        //_server = new IPEndPoint(IPAddress.Parse(url), port);
        //_udpClient = new UdpClient();
        //_reader = new ThreadedSocketReader(_udpClient.Client);
        //_thread = new Thread(Run);
        //_stopSignal = false;
        //_thread.Start();
        _connectionId = null;
        //_displayName = displayName;
        //_updateIntervalMs = updateIntervalMs;
        //RequestConnection(password, commandPassword);
        Task.Run(Connect);
    }

    private async Task Connect()
    {
        _udpAlive = true;

        while (_udpAlive)
        {
            try
            {
                var client = GetUdpClient(_connectionState == UDPState.Connected);

                if (!client.Client.Connected)
                {
                    await Task.Delay(1000);
                    continue;
                }

                var udpReceiveTask = client.ReceiveAsync();
                await Task.Delay(10);

                if (!udpReceiveTask.IsCompleted)
                    udpReceiveTask.Wait(2500);
                
                if (!_udpAlive)
                    break;

                if (udpReceiveTask.Status == TaskStatus.WaitingForActivation)
                {
                    if (_simPaused)
                    {
                        await Task.Delay(1000);
                        continue;
                    }
                    
                    try
                    {
                        RequestDisconnection();
                        
                        await Task.Delay(1000);
                        
                        client.Client.Shutdown(SocketShutdown.Both);
                        //client.Client.Disconnect(true);
                        client.Client.Close();
                        client.Close();
                    }
                    catch (Exception e)
                    {
                        Logger?.Invoke(e.Message);
                    }

                    _udpClient = null;
                }
                else
                {
                    var udpPacket = udpReceiveTask.Result;

                    if (udpPacket.Buffer.Length == 0)
                        await Task.Delay(10);
                    else
                    {
                        using var ms = new MemoryStream(udpPacket.Buffer);
                        using var br = new BinaryReader(ms);
                        
                        var messageType = (InboundMessageTypes)br.ReadByte();
                        _getMethods[messageType](br);
                    }
                }
            }
            catch (SocketException)
            {
                await Task.Delay(1000);
                //Ignored: Not UDP server aka ACC isn't running
                _udpClient?.Close();
                _udpClient = null;

            }
            catch (Exception ex)
            {
                Logger?.Invoke(ex.Message);
            }
        }
    }

    private bool GetUdpClient(out UdpClient udpClient)
    {
        lock(UdpLock)
        { 
            try
            {
                if (_udpClient == null || _udpClient.Client == null || !_udpClient.Client.Connected)
                {
                    if (_udpClient != null)
                    {
                        try
                        {
                            _udpClient.Close();
                            _udpClient = null;
                        }
                        catch (Exception e)
                        {
                            Logger?.Invoke(e.Message);
                        }
                    }

                    udpClient = new UdpClient();
                    udpClient.Connect(IpAddress, Port);
                    _udpClient = udpClient;
                    return true;
                }

                udpClient = _udpClient;
                return false;
            }
            catch (Exception e)
            {
                Logger?.Invoke(e.Message);
                udpClient = null;
                return false;
            }
        }
    }

    public void Stop(string reason = "disconnected")
    {
        // if (!IsAlive)
        //     throw new InvalidOperationException($"Must be started: {reason}");
        //
        // _stopSignal = true;
        // if (_thread != null)
        // {
        //     _thread.Join();
        //     _thread = null;
        // }
        
        _udpAlive = false;
        if (_udpClient != null)
        {
            try
            {
                RequestDisconnection();
                _udpClient.Close();
            }
            catch (Exception e)
            {
                Logger?.Invoke(e.Message);
            }
        }

        Logger?.Invoke($"ACC UDP Client Stopped: {reason}");
        UpdateConnectionState(UDPState.Disconnected);
    }

    public void Dispose()
    {
        _udpClient?.Close();
        _udpClient = null;
    }

    public void RequestDataOnConnected()
    {
        if (_udpClient == null || _udpClient.Client == null || !_udpClient.Client.Connected)
            return;
        
        RequestTrackData();
        RequestEntryList();
    }
}

public enum UDPState
{
    Connected,
    Disconnected,
    Connecting
}
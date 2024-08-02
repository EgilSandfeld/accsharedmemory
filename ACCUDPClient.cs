using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using AssettoCorsaSharedMemory;

namespace Sim.AssettoCorsaCompetizione;

public class AccClient
{
    public Action<string> Logger { get; }
    private IPEndPoint _server;
    private string _displayName;
    private int _updateIntervalMs;
    private UdpClient _udpClient;
    private UDPState _connectionState = UDPState.Disconnected;
    private int _broadcastingProtocolVersion = 4;
    private int? _connectionId;
    private bool _writable;
    private Dictionary<int, int> _cars = new ();
    private Dictionary<byte, Action> _getMethods;
    private bool _stopSignal;
    private Thread _thread;
    private ThreadedSocketReader _reader;

    public event EventHandler<UDPState> OnConnectionStateChange;
    public event EventHandler<TrackData> OnTrackDataUpdate;
    public event EventHandler<EntryListCar> OnEntryListCarUpdate;
    public event EventHandler<RealtimeUpdate> OnRealtimeUpdate;
    public event EventHandler<RealtimeCarUpdate> OnRealtimeCarUpdate;
    public event EventHandler<BroadcastingEvent> OnBroadcastingEvent;

    public UDPState ConnectionState => _connectionState;
    public bool Writable => _writable;

    public AccClient(Action<string> logger)
    {
        Logger = logger;
        _getMethods = new Dictionary<byte, Action>
        {
            { 1, GetRegistrationResult },
            { 2, GetRealtimeUpdate },
            { 3, GetRealtimeCarUpdate },
            { 4, GetEntryList },
            { 5, GetTrackData },
            { 6, GetEntryListCar },
            { 7, GetBroadcastingEvent }
        };
    }

    private void UpdateConnectionState(UDPState state)
    {
        if (state != _connectionState)
        {
            _connectionState = state;
            OnConnectionStateChange?.Invoke(this, _connectionState);
        }
    }

    private void Send(params object[] args)
    {
        if (!IsAlive)
            throw new InvalidOperationException("Must be started");

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

        _udpClient.Send(buffer.ToArray(), buffer.Count, _server);
    }

    // private T Get<T>()
    // {
    //     var data = _reader.Read(sizeof(T));
    //     return BitConverter.ToT(data, 0);
    // }
    private T Get<T>() where T : struct
    {
        var size = Marshal.SizeOf(typeof(T));
        var data = _reader.Read(size);
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            return (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
        }
        finally
        {
            handle.Free();
        }
    }

    private string GetString()
    {
        var length = Get<ushort>();
        if (length > 0)
        {
            var data = _reader.Read(length);
            return Encoding.UTF8.GetString(data);
        }

        return string.Empty;
    }

    private void GetRegistrationResult()
    {
        _connectionId = Get<int>();
        _writable = Get<bool>();
        var errorMessage = GetString();
        if (!_writable)
        {
            Stop($"rejected ({errorMessage})");
        }

        UpdateConnectionState(UDPState.Connected);
        RequestEntryList();
        RequestTrackData();
    }

    private void GetRealtimeUpdate()
    {
        var update = new RealtimeUpdate
        {
            EventIndex = Get<ushort>(),
            SessionIndex = Get<ushort>(),
            SessionType = (SessionType)Get<byte>(),
            SessionPhase = (SessionPhase)Get<byte>(),
            SessionTimeMs = Get<float>(),
            SessionEndTimeMs = Get<float>(),
            FocusedCarIndex = Get<int>(),
            ActiveCameraSet = GetString(),
            ActiveCamera = GetString(),
            CurrentHudPage = GetString(),
            IsReplayPlaying = Get<bool>()
        };

        if (update.IsReplayPlaying)
        {
            update.ReplaySessionTime = Get<float>();
            update.ReplayRemainingTime = Get<float>();
        }

        update.TimeOfDayMs = Get<float>();
        update.AmbientTemp = Get<byte>();
        update.TrackTemp = Get<byte>();
        update.Clouds = Get<byte>() / 10f;
        update.RainLevel = Get<byte>() / 10f;
        update.Wetness = Get<byte>() / 10f;
        update.BestSessionLap = GetLap();

        OnRealtimeUpdate?.Invoke(this, update);
    }

    private Lap GetLap()
    {
        var lap = new Lap
        {
            LapTimeMs = Get<int>(),
            CarIndex = Get<ushort>(),
            DriverIndex = Get<ushort>(),
            Splits = new List<int>()
        };

        var splitCount = Get<byte>();
        for (int i = 0; i < splitCount; i++)
        {
            lap.Splits.Add(Get<int>());
        }

        lap.IsInvalid = Get<bool>();
        lap.IsValidForBest = Get<bool>();
        lap.IsOutlap = Get<bool>();
        lap.IsInlap = Get<bool>();
        lap.Type = lap.IsOutlap ? LapType.Outlap : (lap.IsInlap ? LapType.Inlap : LapType.Regular);

        return lap;
    }

    private void GetRealtimeCarUpdate()
    {
        var update = new RealtimeCarUpdate
        {
            CarIndex = Get<ushort>(),
            DriverIndex = Get<ushort>(),
            DriverCount = Get<byte>(),
            Gear = Get<byte>() - 2, //Offset by -2 to get reverse gear as -1 and neutral as 0
            WorldPosX = Get<float>(),
            WorldPosY = Get<float>(),
            Yaw = Get<float>(),
            Location = (CarLocation)Get<byte>(),
            Kmh = Get<ushort>(),
            Position = Get<ushort>(),
            CupPosition = Get<ushort>(),
            TrackPosition = Get<ushort>(),
            SplinePosition = Get<float>(),
            Laps = Get<ushort>(),
            Delta = Get<int>(),
            BestSessionLap = GetLap(),
            LastLap = GetLap(),
            CurrentLap = GetLap()
        };

        if (_cars.ContainsKey(update.CarIndex) && _cars[update.CarIndex] == update.DriverCount)
        {
            OnRealtimeCarUpdate?.Invoke(this, update);
        }
        else
        {
            RequestEntryList();
        }
    }

    private void GetEntryList()
    {
        var connectionId = Get<int>();
        var carCount = Get<ushort>();
        _cars.Clear();
        for (int i = 0; i < carCount; i++)
        {
            var carIndex = Get<ushort>();
            _cars[carIndex] = _cars.ContainsKey(carIndex) ? _cars[carIndex] : -1;
        }
    }

    private void GetEntryListCar()
    {
        var car = new EntryListCar
        {
            CarIndex = Get<ushort>(),
            ModelType = (AssettoCorsa.CarModel)Get<byte>(),
            TeamName = GetString(),
            RaceNumber = Get<int>(),
            CupCategory = Get<byte>(),
            CurrentDriverIndex = Get<byte>(),
            Nationality = (Nationality)Get<ushort>(),
            Drivers = new List<Driver>()
        };

        var driverCount = Get<byte>();
        for (int i = 0; i < driverCount; i++)
        {
            car.Drivers.Add(new Driver
            {
                FirstName = GetString(),
                LastName = GetString(),
                ShortName = GetString(),
                Category = (DriverCategory)Get<byte>(),
                Nationality = (Nationality)Get<ushort>()
            });
        }

        _cars[car.CarIndex] = car.Drivers.Count;
        OnEntryListCarUpdate?.Invoke(this, car);
    }

    private void GetTrackData()
    {
        var trackData = new TrackData
        {
            ConnectionId = Get<int>(),
            TrackName = GetString(),
            TrackId = Get<int>(),
            TrackMeters = Get<int>(),
            CameraSets = new Dictionary<string, List<string>>(),
            HudPages = new List<string>()
        };

        var cameraSetCount = Get<byte>();
        for (int i = 0; i < cameraSetCount; i++)
        {
            var cameraSetName = GetString();
            trackData.CameraSets[cameraSetName] = new List<string>();
            var cameraCount = Get<byte>();
            for (int j = 0; j < cameraCount; j++)
            {
                trackData.CameraSets[cameraSetName].Add(GetString());
            }
        }

        var hudPageCount = Get<byte>();
        for (int i = 0; i < hudPageCount; i++)
        {
            trackData.HudPages.Add(GetString());
        }

        OnTrackDataUpdate?.Invoke(this, trackData);
    }

    private void GetBroadcastingEvent()
    {
        var broadcastingEvent = new BroadcastingEvent
        {
            Type = (BroadcastingEventType)Get<byte>(),
            Message = GetString(),
            TimeMs = Get<int>(),
            CarIndex = Get<int>()
        };

        OnBroadcastingEvent?.Invoke(this, broadcastingEvent);
    }

    private void RequestConnection(string password, string commandPassword)
    {
        Send(
            (byte)OutboundMessageTypes.REGISTER_COMMAND_APPLICATION,
            (byte)_broadcastingProtocolVersion,
            _displayName,
            password,
            _updateIntervalMs,
            commandPassword
        );
    }

    private void RequestDisconnection()
    {
        Send(
            (byte)OutboundMessageTypes.UNREGISTER_COMMAND_APPLICATION,
            _connectionId.Value
        );
    }

    private void RequestEntryList()
    {
        Send(
            (byte)OutboundMessageTypes.REQUEST_ENTRY_LIST,
            _connectionId.Value
        );
    }

    private void RequestTrackData()
    {
        Send(
            (byte)OutboundMessageTypes.REQUEST_TRACK_DATA,
            _connectionId.Value
        );
    }

    public void RequestFocusChange(int carIndex = -1, string cameraSet = null, string camera = null)
    {
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
        Send(
            (byte)OutboundMessageTypes.CHANGE_HUD_PAGE,
            _connectionId.Value,
            pageName
        );
    }

    private void Run()
    {
        try
        {
            while (!_stopSignal)
            {
                try
                {
                    var messageTypeData = _reader.Read(1, 100);
                    if (messageTypeData == null)
                        continue;

                    var messageType = messageTypeData[0];
                    _getMethods[messageType]();
                }
                catch (Exception)
                {
                    UpdateConnectionState(UDPState.Disconnected);
                    break;
                }
            }
        }
        finally
        {
            try
            {
                RequestDisconnection();
            }
            catch
            {
            }
        }

        _reader.Stop();
        _reader = null;
        _udpClient.Close();
        _udpClient = null;
    }

    public bool IsAlive => _thread != null && _thread.IsAlive;

    public void Start(string url, int port, string password, string commandPassword = "", string displayName = "C# ACCAPI", int updateIntervalMs = 100)
    {
        if (IsAlive)
            throw new InvalidOperationException("Must be stopped");

        UpdateConnectionState(UDPState.Connecting);
        _server = new IPEndPoint(IPAddress.Parse(url), port);
        _udpClient = new UdpClient();
        _reader = new ThreadedSocketReader(_udpClient.Client);
        _thread = new Thread(Run);
        _stopSignal = false;
        _thread.Start();
        _connectionId = null;
        _writable = false;
        _displayName = displayName;
        _updateIntervalMs = updateIntervalMs;
        RequestConnection(password, commandPassword);
    }

    public void Stop(string reason = "disconnected")
    {
        if (!IsAlive)
            throw new InvalidOperationException($"Must be started: {reason}");

        _stopSignal = true;
        if (_thread != null)
        {
            _thread.Join();
            _thread = null;
        }

        Logger($"ACC UDP Client Stopped: {reason}");
        UpdateConnectionState(UDPState.Disconnected);
    }
}

public enum UDPState
{
    Connected,
    Disconnected,
    Connecting
}

public class ThreadedSocketReader
{
    private Socket _source;
    private int _chunkSize;
    private List<byte> _data = new List<byte>();
    private object _dataLock = new object();
    private bool _stopSignal;
    private Thread _thread;
    private Exception _exception;

    public ThreadedSocketReader(Socket source, int chunkSize = 1024)
    {
        _source = source;
        _chunkSize = chunkSize;
        _thread = new Thread(Run);
        _thread.IsBackground = true;
        _thread.Start();
    }

    public bool IsAlive => _thread != null && _thread.IsAlive;

    public int Size
    {
        get
        {
            lock (_dataLock)
            {
                return _data.Count;
            }
        }
    }

    public byte[] Read(int size = -1, int timeout = -1)
    {
        lock (_dataLock)
        {
            if (!IsAlive && (size == -1 || _data.Count < size))
            {
                if (_exception != null)
                    throw _exception;
                throw new EndOfStreamException();
            }

            if (size == -1)
            {
                if (_data.Count > 0)
                {
                    var result = _data.ToArray();
                    _data.Clear();
                    return result;
                }

                return null;
            }

            if (_data.Count < size && !Monitor.Wait(_dataLock, timeout))
                return null;

            var data = _data.GetRange(0, Math.Min(size, _data.Count)).ToArray();
            _data.RemoveRange(0, data.Length);
            return data;
        }
    }

    public void Stop()
    {
        _stopSignal = true;
        _thread = null;
    }

    private void Run()
    {
        var buffer = new byte[_chunkSize];
        while (!_stopSignal)
        {
            try
            {
                var bytesRead = _source.Receive(buffer);
                if (bytesRead > 0)
                {
                    lock (_dataLock)
                    {
                        _data.AddRange(buffer.Take(bytesRead));
                        Monitor.PulseAll(_dataLock);
                    }
                }
            }
            catch (Exception e)
            {
                _exception = e;
                break;
            }
        }
    }
}
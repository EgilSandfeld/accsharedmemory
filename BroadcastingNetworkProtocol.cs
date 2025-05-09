using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AssettoCorsaSharedMemory.Structs;
using Serilog;

namespace AssettoCorsaSharedMemory;

public class BroadcastingNetworkProtocol
{
    public const int BROADCASTING_PROTOCOL_VERSION = 4;
    private string ConnectionIdentifier { get; }
    private SendMessageDelegate Send { get; }
    public int ConnectionId { get; private set; } = -1;
    public float TrackMeters { get; private set; }

    internal delegate void SendMessageDelegate(byte[] payload);

    #region Events

    public delegate void ConnectionStateChangedDelegate(int connectionId, bool connectionSuccess, bool isReadonly, string error);

    public event ConnectionStateChangedDelegate OnConnectionStateChanged;

    public delegate void TrackDataUpdateDelegate(string sender, TrackData trackUpdate);

    public event TrackDataUpdateDelegate OnTrackDataUpdate;

    public delegate void EntryListUpdateDelegate(string sender, CarInfo car);

    public event EntryListUpdateDelegate OnEntrylistUpdate;

    public delegate void RealtimeUpdateDelegate(string sender, RealtimeUpdate update);

    public event RealtimeUpdateDelegate OnRealtimeUpdated;

    public delegate void RealtimeCarUpdateDelegate(string sender, RealtimeCarUpdate carUpdate);

    public event RealtimeCarUpdateDelegate OnRealtimeCarUpdated;

    public delegate void BroadcastingEventDelegate(string sender, BroadcastingEvent evt);

    public event BroadcastingEventDelegate OnBroadcastingEventUpdated;

    #endregion

    // To avoid huge UDP pakets for longer entry lists, we will first receive the indexes of cars and drivers,
    // cache the entries and wait for the detailed updates
    private List<CarInfo> _entryListCars = [];

    private DateTime _lastEntryListRequest = DateTime.Now;
    private DateTime _lastTrackDataUpdateTime;


    internal BroadcastingNetworkProtocol(string connectionIdentifier, SendMessageDelegate sendMessageDelegate, int previousConnectionId)
    {
        if (string.IsNullOrEmpty(connectionIdentifier))
            throw new ArgumentNullException(nameof(connectionIdentifier), "No connection identifier set; we use this to distinguish different connections. Using the remote IP:Port is a good idea");

        ConnectionId = previousConnectionId;
        ConnectionIdentifier = connectionIdentifier;
        Send = sendMessageDelegate ?? throw new ArgumentNullException(nameof(sendMessageDelegate), "The protocol class doesn't know anything about the network layer; please put a callback we can use to send data via UDP");
    }

    internal void ProcessMessage(BinaryReader br)
    {
        // Any message starts with an 1-byte command type
        var messageType = (InboundMessageTypes)br.ReadByte();
        switch (messageType)
        {
            case InboundMessageTypes.REGISTRATION_RESULT:
            {
                OnRegistrationResult(br);
                break;
            }
            case InboundMessageTypes.ENTRY_LIST:
            {
                OnEntryList(br);
                break;
            }
            case InboundMessageTypes.ENTRY_LIST_CAR:
            {
                OnEntryListCar(br);
                break;
            }
            case InboundMessageTypes.REALTIME_UPDATE:
            {
                OnRealtimeUpdate(br);
                break;
            }
            case InboundMessageTypes.REALTIME_CAR_UPDATE:
            {
                OnRealtimeCarUpdate(br);
                break;
            }
            case InboundMessageTypes.TRACK_DATA:
            {
                OnTrackData(br);
                break;
            }
                
            case InboundMessageTypes.BROADCASTING_EVENT:
            {
                OnBroadcastingEvent(br);
                break;
            }
                
            default:
                throw new ArgumentOutOfRangeException(nameof(messageType), messageType, "Handle messageType");
        }
    }

    private void OnRegistrationResult(BinaryReader br)
    {
        if (ConnectionId > -1)
        {
            Log.ForContext("Context", "Sim").Verbose("ACC UDP OnRegistrationResult: ConnectionId already set: {ConnectionId} (maybe a disconnect event)", ConnectionId);
        }
        
        ConnectionId = br.ReadInt32();
        var connectionSuccess = br.ReadByte() > 0;
        var isReadonly = br.ReadByte() == 0;
        var errMsg = ReadString(br);
        OnConnectionStateChanged?.Invoke(ConnectionId, connectionSuccess, isReadonly, errMsg);
    }

    private void OnEntryList(BinaryReader br)
    {
        var entryListCars = new List<CarInfo>();
        var connectionId = br.ReadInt32();
        var carEntryCount = br.ReadUInt16();
        for (int i = 0; i < carEntryCount; i++)
        {
            entryListCars.Add(new CarInfo(br.ReadUInt16()));
        }

        _entryListCars = entryListCars;
    }

    private void OnEntryListCar(BinaryReader br)
    {
        if (OnEntrylistUpdate == null)
            return;
                    
        var carId = br.ReadUInt16();

        var carInfo = _entryListCars.SingleOrDefault(x => x.CarIndex == carId);
        if (carInfo == null)
        {
            Log.ForContext("Context", "Sim").Verbose($"Entry list update for unknown carIndex {carId}");
            return;
        }

        carInfo.CarModelType = br.ReadByte(); // Byte sized car model
        carInfo.ModelType = (ACCSharedMemory.CarModel)carInfo.CarModelType;
        
        carInfo.TeamName = ReadString(br);
        carInfo.RaceNumber = br.ReadInt32();
        carInfo.CupCategory = br.ReadByte(); // Cup: Overall/Pro = 0, ProAm = 1, Am = 2, Silver = 3, National = 4
        carInfo.CurrentDriverIndex = br.ReadByte();
        carInfo.Nationality = (NationalityEnum)br.ReadUInt16();

        // Now the drivers on this car:
        var driversOnCarCount = br.ReadByte();
        for (int di = 0; di < driversOnCarCount; di++)
        {
            var driverInfo = new DriverInfo();

            driverInfo.FirstName = ReadString(br);
            driverInfo.LastName = ReadString(br);
            driverInfo.ShortName = ReadString(br);
            driverInfo.Category = (DriverCategory)br.ReadByte(); // Platinum = 3, Gold = 2, Silver = 1, Bronze = 0

            // new in 1.13.11:
            driverInfo.Nationality = (NationalityEnum)br.ReadUInt16();

            carInfo.AddDriver(driverInfo);
        }

        OnEntrylistUpdate?.Invoke(ConnectionIdentifier, carInfo);
    }

    private void OnRealtimeUpdate(BinaryReader br)
    {
        if (OnRealtimeUpdated == null)
            return;
                    
        RealtimeUpdate update = new RealtimeUpdate();
        update.EventIndex = (int)br.ReadUInt16();
        update.SessionIndex = (int)br.ReadUInt16();
        update.SessionType = (RaceSessionType)br.ReadByte();
        update.Phase = (SessionPhase)br.ReadByte();
        var sessionTime = br.ReadSingle();
        update.SessionTime = TimeSpan.FromMilliseconds(sessionTime);
        //update.SessionTimeMs = sessionTime;
        var sessionEndTime = br.ReadSingle();
        update.SessionEndTime = TimeSpan.FromMilliseconds(sessionEndTime);
        //update.SessionEndTimeMs = sessionEndTime;

        update.FocusedCarIndex = br.ReadInt32();
        update.ActiveCameraSet = ReadString(br);
        update.ActiveCamera = ReadString(br);
        update.CurrentHudPage = ReadString(br);

        update.IsReplayPlaying = br.ReadByte() > 0;
        if (update.IsReplayPlaying)
        {
            update.ReplaySessionTime = br.ReadSingle();
            update.ReplayRemainingTime = br.ReadSingle();
        }

        var rawTimeOfDay = br.ReadSingle();
        update.TimeOfDay = TimeSpan.FromSeconds(rawTimeOfDay);
        update.AmbientTemp = br.ReadByte();
        update.TrackTemp = br.ReadByte();
        /*update.Clouds = */br.ReadByte()/* / 10.0f*/;
        /*update.RainLevel = */br.ReadByte()/* / 10.0f*/;
        /*update.Wetness = */br.ReadByte()/* / 10.0f*/;

        update.BestSessionLap = ReadLap(br);

        OnRealtimeUpdated?.Invoke(ConnectionIdentifier, update);
    }

    private void OnRealtimeCarUpdate(BinaryReader br)
    {
        if (OnRealtimeCarUpdated == null)
            return;

        // ReSharper disable once UseObjectOrCollectionInitializer
        var carUpdate = new RealtimeCarUpdate();

        carUpdate.CarIndex = br.ReadUInt16();
        carUpdate.DriverIndex = br.ReadUInt16(); // Driver swap will make this change
        carUpdate.DriverCount = br.ReadByte();
        carUpdate.Gear = br.ReadByte() - 2; // -2 makes the R -1, N 0 and the rest as-is
        carUpdate.WorldPosX = br.ReadSingle();
        carUpdate.WorldPosY = br.ReadSingle();
        carUpdate.Yaw = br.ReadSingle();
        carUpdate.Location = (CarLocationEnum)br.ReadByte(); // - , Track, Pitlane, PitEntry, PitExit = 4
        carUpdate.Kmh = br.ReadUInt16();
        carUpdate.Position = br.ReadUInt16(); // official P/Q/R position (1 based)
        carUpdate.CupPosition = br.ReadUInt16(); // official P/Q/R position (1 based)
        carUpdate.TrackPosition = br.ReadUInt16(); // position on track (1 based)
        carUpdate.SplinePosition = br.ReadSingle(); // track position between 0.0 and 1.0
        carUpdate.Laps = br.ReadUInt16();

        carUpdate.Delta = br.ReadInt32(); // Realtime delta to best session lap
        carUpdate.BestSessionLap = ReadLap(br);
        carUpdate.LastLap = ReadLap(br);
        carUpdate.CurrentLap = ReadLap(br);
        
        // the concept is: "don't know a car or driver? ask for an entry list update"
        var carEntry = _entryListCars.FirstOrDefault(x => x.CarIndex == carUpdate.CarIndex);
        if (carEntry == null || carEntry.Drivers.Count != carUpdate.DriverCount)
        {
            if ((DateTime.Now - _lastEntryListRequest).TotalSeconds > 10)
            {
                _lastEntryListRequest = DateTime.Now;
                RequestEntryList();
                Log.ForContext("Context", "Sim").Verbose($"CarUpdate {carUpdate.CarIndex}|{carUpdate.DriverIndex} not known, will ask for new EntryList");
            }
        }
        else
        {
            OnRealtimeCarUpdated?.Invoke(ConnectionIdentifier, carUpdate);
        }
    }

    private void OnTrackData(BinaryReader br)
    {
        if (OnTrackDataUpdate == null)
            return;

        _lastTrackDataUpdateTime = DateTime.Now;
        _firstTrackDataCts.Cancel();
        var connectionId = br.ReadInt32();
        var trackData = new TrackData();

        trackData.TrackName = ReadString(br);
        trackData.TrackId = br.ReadInt32();
        trackData.TrackMeters = br.ReadInt32();
        TrackMeters = trackData.TrackMeters > 0 ? trackData.TrackMeters : -1;

        trackData.CameraSets = new Dictionary<string, List<string>>();

        var cameraSetCount = br.ReadByte();
        for (int camSet = 0; camSet < cameraSetCount; camSet++)
        {
            var camSetName = ReadString(br);
            trackData.CameraSets.Add(camSetName, new List<string>());

            var cameraCount = br.ReadByte();
            for (int cam = 0; cam < cameraCount; cam++)
            {
                var cameraName = ReadString(br);
                trackData.CameraSets[camSetName].Add(cameraName);
            }
        }

        var hudPages = new List<string>();
        var hudPagesCount = br.ReadByte();
        for (int i = 0; i < hudPagesCount; i++)
        {
            hudPages.Add(ReadString(br));
        }

        trackData.HUDPages = hudPages;

        OnTrackDataUpdate?.Invoke(ConnectionIdentifier, trackData);

        RequestEntryList();
    }

    private void OnBroadcastingEvent(BinaryReader br)
    {
        if (OnBroadcastingEventUpdated == null)
            return;
                    
        BroadcastingEvent evt = new BroadcastingEvent()
        {
            Type = (BroadcastingCarEventType)br.ReadByte(),
            Message = ReadString(br),
            TimeMs = br.ReadInt32(),
            CarIndex = br.ReadInt32(),
        };

        evt.CarData = _entryListCars.FirstOrDefault(x => x.CarIndex == evt.CarIndex);
        OnBroadcastingEventUpdated?.Invoke(ConnectionIdentifier, evt);
    }

    /// <summary>
    /// Laps are always sent in a common way, it makes sense to have a shared function to parse them
    /// </summary>
    private static LapInfo ReadLap(BinaryReader br)
    {
        var lap = new LapInfo();
        lap.LapTimeMs = br.ReadInt32();

        lap.CarIndex = br.ReadUInt16();
        lap.DriverIndex = br.ReadUInt16();

        var splitCount = br.ReadByte();
        for (int i = 0; i < splitCount; i++)
            lap.Splits.Add(br.ReadInt32());

        lap.IsInvalid = br.ReadByte() > 0;
        lap.IsValidForBest = br.ReadByte() > 0;

        var isOutlap = br.ReadByte() > 0;
        var isInlap = br.ReadByte() > 0;

        if (isOutlap)
            lap.Type = LapType.Outlap;
        else if (isInlap)
            lap.Type = LapType.Inlap;
        else
            lap.Type = LapType.Regular;

        // Now it's possible that this is "no" lap that doesn't even include a 
        // first split, we can detect this by comparing with int32.Max
        while (lap.Splits.Count < 3)
        {
            lap.Splits.Add(null);
        }

        // "null" entries are Int32.Max, in the C# world we can replace this to null
        for (int i = 0; i < lap.Splits.Count; i++)
            if (lap.Splits[i] == int.MaxValue)
                lap.Splits[i] = null;

        if (lap.LapTimeMs == int.MaxValue)
            lap.LapTimeMs = null;

        return lap;
    }

    private static string ReadString(BinaryReader br)
    {
        var length = br.ReadUInt16();
        var bytes = br.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }

    private static void WriteString(BinaryWriter bw, string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        bw.Write(Convert.ToUInt16(bytes.Length));
        bw.Write(bytes);
    }

    /// <summary>
    /// Will try to register this client in the targeted ACC instance.
    /// Needs to be called once, before anything else can happen.
    /// </summary>
    /// <param name="connectionPassword"></param>
    /// <param name="msRealtimeUpdateInterval"></param>
    /// <param name="commandPassword"></param>
    internal void RequestConnection(string displayName, string connectionPassword, int msRealtimeUpdateInterval, string commandPassword)
    {
        using var ms = new MemoryStream();
        using var br = new BinaryWriter(ms);
        
        // if (ConnectionId > -1)
        // {
        //     Log.ForContext("Context", "Sim").Verbose("ACC UdpRemoteClient Previous connection not closed, so closing {ConnectionId} now...", ConnectionId);
        //     Disconnect();
        //     //return;
        // }
        
        br.Write((byte)OutboundMessageTypes.REGISTER_COMMAND_APPLICATION); // First byte is always the command type
        br.Write((byte)BROADCASTING_PROTOCOL_VERSION);

        WriteString(br, displayName);
        WriteString(br, connectionPassword);
        br.Write(msRealtimeUpdateInterval);
        WriteString(br, commandPassword);

        var payload = ms.ToArray();
        Send(payload);
        
        Log.ForContext("Context", "Sim").Verbose("ACC UdpRemoteClient RequestConnection sent with payload length: {PayloadLength}, payload non zeros: {NonZerosPresent}", payload.Length, payload.Any(x => x != 0));
        Log.ForContext("Context", "Sim").Information($"Waiting for {displayName} connection...");
    }

    internal void Disconnect()
    {
        Log.ForContext("Context", "Sim").Verbose("ACC UdpRemoteClient Disconnect");
        _firstTrackDataCts?.Cancel();
        
        using var ms = new MemoryStream();
        using var br = new BinaryWriter(ms);
        br.Write((byte)OutboundMessageTypes.UNREGISTER_COMMAND_APPLICATION);
        if (ConnectionId > -1)
            br.Write((int)ConnectionId); //Keep (int) here just for any reason
        
        Send(ms.ToArray());
    }


    /// <summary>
    /// Will ask the ACC client for an updated entry list, containing all car and driver data.
    /// The client will send this automatically when something changes; however if you detect a carIndex or driverIndex, this may cure the 
    /// problem for future updates
    /// </summary>
    public void RequestEntryList()
    {
        Log.ForContext("Context", "Sim").Verbose("ACC RequestEntryList on ConnectionId {ConnectionId}...", ConnectionId);
        using var ms = new MemoryStream();
        using var br = new BinaryWriter(ms);
        br.Write((byte)OutboundMessageTypes.REQUEST_ENTRY_LIST); // First byte is always the command type
        br.Write(ConnectionId);

        Send(ms.ToArray());
    }

    public void RequestTrackData()
    {
        Log.ForContext("Context", "Sim").Verbose("ACC RequestTrackData on ConnectionId {ConnectionId}...", ConnectionId);
        using var ms = new MemoryStream();
        using var br = new BinaryWriter(ms);
        br.Write((byte)OutboundMessageTypes.REQUEST_TRACK_DATA); // First byte is always the command type
        br.Write(ConnectionId);

        Send(ms.ToArray());

        Task.Run(CheckRequestTrackDataSucceeded);
    }

    private int _checkRequestTrackDataSucceededCount;
    private readonly CancellationTokenSource _firstTrackDataCts = new();

    private async Task CheckRequestTrackDataSucceeded()
    {
        if (_lastTrackDataUpdateTime != DateTime.MinValue)
            return;
        
        _checkRequestTrackDataSucceededCount++;
        
        await Task.Delay(_checkRequestTrackDataSucceededCount < 4 ? _checkRequestTrackDataSucceededCount * 10000 : 30000, _firstTrackDataCts.Token);
        if (_lastTrackDataUpdateTime != DateTime.MinValue || _firstTrackDataCts.IsCancellationRequested)
            return;
            
        //Log.ForContext("Context", "Sim").Verbose("ACC CheckRequestTrackDataSucceeded: Re-requesting track data (attempt #{AttemptCount})...", _checkRequestTrackDataSucceededCount);
        Log.ForContext("Context", "Sim").Information("Waiting for ACC track & entry lists data (attempt #{AttemptCount})...", _checkRequestTrackDataSucceededCount);

        RequestEntryList();
        RequestTrackData();
    }

    public void SetFocus(UInt16 carIndex)
    {
        SetFocusInternal(carIndex, null, null);
    }

    /// <summary>
    /// Always put both cam + cam set; even if it doesn't make sense
    /// </summary>
    public void SetCamera(string cameraSet, string camera)
    {
        SetFocusInternal(null, cameraSet, camera);
    }

    public void SetFocus(UInt16 carIndex, string cameraSet, string camera)
    {
        SetFocusInternal(carIndex, cameraSet, camera);
    }

    public void SetFocus(int carIndex = -1, string cameraSet = null, string camera = null)
    {
        if (ConnectionId == -1)
            return;

        SetFocusInternal(carIndex == -1 ? null : (ushort)carIndex, cameraSet, camera);

        /*Send(bw =>
        {
            bw.Write((byte)OutboundMessageTypes.CHANGE_FOCUS);
            bw.Write(ConnectionId.Value);

            if (carIndex >= 0)
            {
                bw.Write((byte)1);
                bw.Write((ushort)carIndex);
            }
            else
            {
                bw.Write((byte)0);
            }

            if (!string.IsNullOrEmpty(cameraSet) && !string.IsNullOrEmpty(camera))
            {
                bw.Write((byte)1);
                WriteString(bw, cameraSet);
                WriteString(bw, camera);
            }
            else
            {
                bw.Write((byte)0);
            }
        });*/
    }

    /// <summary>
    /// Sends the request to change the focused car and/or the camera used.
    /// The idea is that this often wants to be triggered together, so this is a all-in-one function.
    /// This way we can make sure the switch happens in the same frame, even in more complex scenarios
    /// </summary>
    private void SetFocusInternal(UInt16? carIndex, string cameraSet, string camera)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write((byte)OutboundMessageTypes.CHANGE_FOCUS); // First byte is always the command type
        bw.Write((int)ConnectionId);

        if (!carIndex.HasValue)
        {
            bw.Write((byte)0); // No change of focused car
        }
        else
        {
            bw.Write((byte)1);
            bw.Write((UInt16)(carIndex.Value));
        }

        if (string.IsNullOrEmpty(cameraSet) || string.IsNullOrEmpty(camera))
        {
            bw.Write((byte)0); // No change of camera set or camera
        }
        else
        {
            bw.Write((byte)1);
            WriteString(bw, cameraSet);
            WriteString(bw, camera);
        }

        Send(ms.ToArray());
    }

    public void RequestInstantReplay(float startSessionTime, float durationMS, int initialFocusedCarIndex = -1, string initialCameraSet = "", string initialCamera = "")
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write((byte)OutboundMessageTypes.INSTANT_REPLAY_REQUEST); // First byte is always the command type
        bw.Write((int)ConnectionId);

        bw.Write((float)startSessionTime);
        bw.Write((float)durationMS);
        bw.Write((int)initialFocusedCarIndex);

        WriteString(bw, initialCameraSet);
        WriteString(bw, initialCamera);

        Send(ms.ToArray());
    }

    public void RequestHUDPage(string hudPage)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write((byte)OutboundMessageTypes.CHANGE_HUD_PAGE); // First byte is always the command type
        bw.Write((int)ConnectionId);

        WriteString(bw, hudPage);

        Send(ms.ToArray());
    }
}
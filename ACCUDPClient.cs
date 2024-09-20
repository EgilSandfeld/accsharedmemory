// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Net.Sockets;
// using System.Text;
// using System.Threading;
// using System.Threading.Tasks;
// using ksBroadcastingNetwork;
// using ksBroadcastingNetwork.Structs;
// using Serilog;
// using InboundMessageTypes = Sim.AssettoCorsaCompetizione.InboundMessageTypes;
// using OutboundMessageTypes = Sim.AssettoCorsaCompetizione.OutboundMessageTypes;
//
// namespace AssettoCorsaSharedMemory;
//
// public class AccClient : IDisposable
// {
//     // public Action<string> Logger { get; }
//     //public bool IsAlive => _thread != null && _thread.IsAlive;
//
//     public string CommandPassword { get; set; }
//     public int MsRealtimeUpdateInterval { get; set; }
//     public string ConnectionPassword { get; set; }
//     public string DisplayName { get; set; }
//     public string IpAddress { get; set; }
//     public int Port { get; set; }
//
//     private static readonly object UdpLock = new ();
//     private DateTime _clientStartTime;
//     
//     //private IPEndPoint _server;
//     //private string _displayName;
//     //private int _updateIntervalMs;
//     private UdpClient _udpClient;
//     private UDPState _connectionState = UDPState.Disconnected;
//     private const int BroadcastingProtocolVersion = 4;
//     private int? _connectionId;
//     //private bool _writable;
//     private readonly Dictionary<int, int> _entryListCars = new ();
//     private readonly Dictionary<InboundMessageTypes, Action<BinaryReader>> _getMethods;
//     //private bool _stopSignal;
//     //private Thread _thread;
//     //private ThreadedSocketReader _reader;
//     private bool _udpAlive;
//     private bool _simPaused;
//     
//     private CancellationTokenSource _cts;
//
//     public event EventHandler<UDPState> OnConnectionStateChange;
//     public event EventHandler<ksBroadcastingNetwork.Structs.TrackData> OnTrackDataUpdate;
//     public event EventHandler<ksBroadcastingNetwork.Structs.CarInfo> OnEntryListCarUpdate;
//     public event EventHandler<ksBroadcastingNetwork.Structs.RealtimeUpdate> OnRealtimeUpdate;
//     public event EventHandler<ksBroadcastingNetwork.Structs.RealtimeCarUpdate> OnRealtimeCarUpdate;
//     public event EventHandler<ksBroadcastingNetwork.Structs.BroadcastingEvent> OnBroadcastingEvent;
//
//     public UDPState ConnectionState => _connectionState;
//
//     public AccClient()
//     {
//         _clientStartTime = DateTime.Now;
//         _getMethods = new Dictionary<InboundMessageTypes, Action<BinaryReader>>
//         {
//             { InboundMessageTypes.REGISTRATION_RESULT, GetRegistrationResult },
//             { InboundMessageTypes.REALTIME_UPDATE, GetRealtimeUpdate },
//             { InboundMessageTypes.REALTIME_CAR_UPDATE, GetRealtimeCarUpdate },
//             { InboundMessageTypes.ENTRY_LIST, GetEntryList },
//             { InboundMessageTypes.TRACK_DATA, GetTrackData },
//             { InboundMessageTypes.ENTRY_LIST_CAR, GetEntryListCar },
//             { InboundMessageTypes.BROADCASTING_EVENT, GetBroadcastingEvent }
//         };
//     }
//
//     public void UpdateSimPaused(bool paused)
//     {
//         _simPaused = paused;
//     }
//
//     private void UpdateConnectionState(UDPState state)
//     {
//         if (state != _connectionState)
//         {
//             _connectionState = state;
//             OnConnectionStateChange?.Invoke(this, _connectionState);
//         }
//     }
//     
//     /// <summary>
//     /// Constructs and sends a message using BinaryWriter to ensure correct serialization.
//     /// </summary>
//     /// <param name="messageAction">Action to write the message content.</param>
//     private void Send(Action<BinaryWriter> messageAction)
//     {
//         try
//         {
//             using var ms = new MemoryStream();
//             using var bw = new BinaryWriter(ms);
//
//             // Invoke the action to write specific message content
//             messageAction(bw);
//
//             var messageBytes = ms.ToArray();
//             var hex = BitConverter.ToString(messageBytes).Replace("-", " ");
//             Log.Debug($"Sending bytes: {hex}");
//
//             _udpClient.Send(messageBytes, messageBytes.Length);
//         }
//         catch (Exception e)
//         {
//             Log.Error(e, $"Send Error: {e.Message}");
//         }
//     }
//
//     private static string ReadString(BinaryReader br)
//     {
//         var length = br.ReadUInt16();
//         var bytes = br.ReadBytes(length);
//         return Encoding.UTF8.GetString(bytes);
//     }
//     
//     private void GetRegistrationResult(BinaryReader br)
//     {
//         try
//         {
//             _connectionId = br.ReadInt32();
//             var connectionSuccess = br.ReadByte() > 0;
//             var isReadonly = br.ReadByte() == 0;
//             var errMsg = ReadString(br);
//
//             if (!connectionSuccess)
//             {
//                 Log.Error($"ACC UDP Client GetRegistrationResult: false, error: {errMsg}");
//                 Stop($"Registration failed: {errMsg}");
//                 return;
//             }
//
//             if (isReadonly)
//             {
//                 Log.Error($"ACC UDP Client GetRegistrationResult: Is read only. Rejected: ({errMsg})");
//                 Stop($"Is read only: {errMsg}");
//                 return;
//             }
//
//             UpdateConnectionState(UDPState.Connected);
//             RequestEntryList();
//             RequestTrackData();
//         }
//         catch (Exception e)
//         {
//             Log.Error(e, $"GetRegistrationResult Error: {e.Message}");
//             throw;
//         }
//     }
//
//     private void GetRealtimeUpdate(BinaryReader br)
//     {
//         try
//         {
//             var update = new ksBroadcastingNetwork.Structs.RealtimeUpdate
//             {
//                 EventIndex = br.ReadUInt16(),
//                 SessionIndex = br.ReadUInt16(),
//                 SessionType = (RaceSessionType)br.ReadByte(),
//                 SessionPhase = (ksBroadcastingNetwork.SessionPhase)br.ReadByte(),
//                 SessionTimeMs = br.ReadSingle(),
//                 SessionEndTimeMs = br.ReadSingle(),
//                 FocusedCarIndex = br.ReadInt32(),
//                 ActiveCameraSet = ReadString(br),
//                 ActiveCamera = ReadString(br),
//                 CurrentHudPage = ReadString(br),
//                 IsReplayPlaying = br.ReadBoolean()
//             };
//
//             if (update.IsReplayPlaying)
//             {
//                 update.ReplaySessionTime = br.ReadSingle();
//                 update.ReplayRemainingTime = br.ReadSingle();
//             }
//
//             update.TimeOfDaySeconds = br.ReadSingle();
//             update.AmbientTemp = br.ReadByte();
//             update.TrackTemp = br.ReadByte();
//             update.Clouds = br.ReadByte() / 10f;
//             update.RainLevel = br.ReadByte() / 10f;
//             update.Wetness = br.ReadByte() / 10f;
//             update.BestSessionLap = GetLap(br);
//
//             OnRealtimeUpdate?.Invoke(this, update);
//         }
//         catch (Exception e)
//         {
//             Log.Error(e, "GetRealtimeUpdate");
//             throw;
//         }
//     }
//
//     private LapInfo GetLap(BinaryReader br)
//     {
//         var lap = new LapInfo
//         {
//             LapTimeMs = br.ReadInt32(),
//             CarIndex = br.ReadUInt16(),
//             DriverIndex = br.ReadUInt16()
//         };
//
//         var splitCount = br.ReadByte();
//         for (int i = 0; i < splitCount; i++)
//             lap.Splits.Add(br.ReadInt32());
//
//         lap.IsInvalid = br.ReadBoolean();
//         lap.IsValidForBest = br.ReadBoolean();
//         lap.IsOutlap = br.ReadBoolean();
//         lap.IsInlap = br.ReadBoolean();
//         lap.Type = lap.IsOutlap ? ksBroadcastingNetwork.LapType.Outlap : (lap.IsInlap ? ksBroadcastingNetwork.LapType.Inlap : ksBroadcastingNetwork.LapType.Regular);
//
//         while (lap.Splits.Count < 3)
//             lap.Splits.Add(-1);
//
//         for (int i = 0; i < lap.Splits.Count; i++)
//             if (lap.Splits[i] == int.MaxValue)
//                 lap.Splits[i] = -1;
//
//         if (lap.LapTimeMs == int.MaxValue)
//             lap.LapTimeMs = -1;
//
//         return lap;
//     }
//
//     private void GetRealtimeCarUpdate(BinaryReader br)
//     {
//         try
//         {
//             var update = new ksBroadcastingNetwork.Structs.RealtimeCarUpdate()
//             {
//                 CarIndex = br.ReadUInt16(),
//                 DriverIndex = br.ReadUInt16(),
//                 DriverCount = br.ReadByte(),
//                 Gear = br.ReadByte() - 2,
//                 WorldPosX = br.ReadSingle(),
//                 WorldPosY = br.ReadSingle(),
//                 Yaw = br.ReadSingle(),
//                 Location = (CarLocationEnum)br.ReadByte(),
//                 Kmh = br.ReadUInt16(),
//                 Position = br.ReadUInt16(),
// #pragma warning disable CS0618 // Type or member is obsolete
//                 CupPosition = br.ReadUInt16(),
// #pragma warning restore CS0618 // Type or member is obsolete
//                 TrackPosition = br.ReadUInt16(),
//                 SplinePosition = br.ReadSingle(),
//                 Laps = br.ReadUInt16(),
//                 Delta = br.ReadInt32(),
//                 BestSessionLap = GetLap(br),
//                 LastLap = GetLap(br),
//                 CurrentLap = GetLap(br)
//             };
//
//             OnRealtimeCarUpdate?.Invoke(this, update);
//
//             //When the car index exists, and the number of drivers in this car matches the expected number of drivers
//             if (_entryListCars.ContainsKey(update.CarIndex) && _entryListCars[update.CarIndex] == update.DriverCount)
//             {
//                 return;
//             }
//
//             RequestEntryList();
//         }
//         catch (Exception e)
//         {
//             Log.Error(e, e.Message);
//             throw;
//         }
//     }
//
//     private void GetEntryList(BinaryReader br)
//     {
//         try
//         {
//             var oldCount = _entryListCars.Count;
//             var carCount = br.ReadUInt16();
//             var changes = false;
//             for (int i = 0; i < carCount; i++)
//             {
//                 var carIndex = br.ReadUInt16();
//                 if (_entryListCars.ContainsKey(carIndex))
//                     continue;
//
//                 _entryListCars.Add(carIndex, 1);
//                 changes = true;
//             }
//
//             if (changes)
//             {
//                 Log.Debug($"ACC UDP Client GetEntryList from {oldCount} -> {_entryListCars.Count}");
//             }
//             
//             RequestTrackData();
//         }
//         catch (Exception e)
//         {
//             Log.Error(e, e.Message);
//             throw;
//         }
//     }
//
//     private void GetEntryListCar(BinaryReader br)
//     {
//         try
//         {
//             var car = new CarInfo(br.ReadUInt16());
//             car.ModelType = (AssettoCorsa.CarModel)br.ReadByte();
//             car.TeamName = ReadString(br);
//             car.RaceNumber = br.ReadInt32();
//             car.CupCategory = br.ReadByte();
//             car.CurrentDriverIndex = br.ReadByte();
//             car.Nationality = (NationalityEnum)br.ReadInt16();
//
//             var driverCount = br.ReadByte();
//             for (var i = 0; i < driverCount; i++)
//             {
//                 car.Drivers.Add(new DriverInfo
//                 {
//                     FirstName = ReadString(br),
//                     LastName = ReadString(br),
//                     ShortName = ReadString(br),
//                     Category = (ksBroadcastingNetwork.DriverCategory)br.ReadByte(),
//                     Nationality = (NationalityEnum)br.ReadInt16()
//                 });
//             }
//
//             if (!_entryListCars.ContainsKey(car.CarIndex))
//             {
//                 if (SecondsSinceStart() > 30)
//                     Log.Debug($"New car index entry: {car.CarIndex} with {car.Drivers.Count} drivers");
//
//                 _entryListCars[car.CarIndex] = car.Drivers.Count;
//                 OnEntryListCarUpdate?.Invoke(this, car);
//                 return;
//             }
//
//             if (_entryListCars[car.CarIndex] != car.Drivers.Count)
//             {
//                 if (SecondsSinceStart() > 30)
//                     Log.Debug($"{_entryListCars[car.CarIndex]} -> {car.Drivers.Count} drivers in car index: {car.CarIndex}");
//
//                 _entryListCars[car.CarIndex] = car.Drivers.Count;
//                 OnEntryListCarUpdate?.Invoke(this, car);
//             }
//             else
//                 OnEntryListCarUpdate?.Invoke(this, car);
//         }
//         catch (Exception e)
//         {
//             Log.Error(e, e.Message);
//             throw;
//         }
//     }
//
//     private double SecondsSinceStart()
//     {
//         return DateTime.Now.Subtract(_clientStartTime).TotalSeconds;
//     }
//
//     private void GetTrackData(BinaryReader br)
//     {
//         try
//         {
//             var trackData = new ksBroadcastingNetwork.Structs.TrackData
//             {
//                 ConnectionId = br.ReadInt32(),
//                 TrackName = ReadString(br),
//                 TrackId = br.ReadInt32(),
//                 TrackMeters = br.ReadInt32(),
//                 CameraSets = new Dictionary<string, List<string>>(),
//                 HUDPages = new List<string>()
//             };
//
//             trackData.TrackMeters = trackData.TrackMeters > 0 ? trackData.TrackMeters : -1;
//
//             var cameraSetCount = br.ReadByte();
//             for (int i = 0; i < cameraSetCount; i++)
//             {
//                 var cameraSetName = ReadString(br);
//                 trackData.CameraSets[cameraSetName] = new List<string>();
//                 var cameraCount = br.ReadByte();
//                 for (int j = 0; j < cameraCount; j++)
//                     trackData.CameraSets[cameraSetName].Add(ReadString(br));
//             }
//
//             var hudPageCount = br.ReadByte();
//             var hudPages = new List<string>();
//             for (var i = 0; i < hudPageCount; i++)
//                 hudPages.Add(ReadString(br));
//             
//             trackData.HUDPages = hudPages;
//
//             OnTrackDataUpdate?.Invoke(this, trackData);
//         }
//         catch (Exception e)
//         {
//             Log.Error(e, e.Message);
//             throw;
//         }
//     }
//
//     private void GetBroadcastingEvent(BinaryReader br)
//     {
//         try
//         {
//             var broadcastingEvent = new ksBroadcastingNetwork.Structs.BroadcastingEvent
//             {
//                 Type = (BroadcastingCarEventType)br.ReadByte(),
//                 Message = ReadString(br),
//                 TimeMs = br.ReadInt32(),
//                 CarIndex = br.ReadInt32()
//             };
//
//             //broadcastingEvent.CarData = _entryListCars.FirstOrDefault(x => x.CarIndex == broadcastingEvent.CarIndex);
//
//             OnBroadcastingEvent?.Invoke(this, broadcastingEvent);
//         }
//         catch (Exception e)
//         {
//             Log.Error(e, e.Message);
//             throw;
//         }
//     }
//
//     /// <summary>
//     /// Will try to register this client in the targeted ACC instance.
//     /// Needs to be called once, before anything else can happen.
//     /// </summary>
//     private void RequestConnection()
//     {
//         try
//         {
//             Send(bw =>
//             {
//                 bw.Write((byte)OutboundMessageTypes.REGISTER_COMMAND_APPLICATION);
//                 bw.Write((byte)BroadcastingProtocolVersion);
//                 WriteString(bw, DisplayName);
//                 WriteString(bw, ConnectionPassword);
//                 bw.Write(MsRealtimeUpdateInterval);
//                 WriteString(bw, CommandPassword);
//             });
//         }
//         catch (Exception ex)
//         {
//             Log.Error(ex, "RequestConnection Error: " + ex.Message);
//         }
//     }
//
//     private void RequestDisconnection()
//     {
//         if (_connectionId == null)
//             return;
//
//         Send(bw =>
//         {
//             bw.Write((byte)OutboundMessageTypes.UNREGISTER_COMMAND_APPLICATION);
//             bw.Write(_connectionId.Value);
//         });
//     }
//
//     /// <summary>
//     /// Will ask the ACC client for an updated entry list, containing all car and driver data.
//     /// The client will send this automatically when something changes; however if you detect a carIndex or driverIndex, this may cure the 
//     /// problem for future updates
//     /// </summary>
//     private void RequestEntryList()
//     {
//         /*if (_requestedEntryList || _connectionId == null)
//         {
//             Log.Debug($"RequestEntryList denied: RequestedEntryList={_requestedEntryList} _connectionId={_connectionId}");
//             return;
//         }
//
//         _requestedEntryList = true;*/
//         try
//         {
//             Send(bw =>
//             {
//                 bw.Write((byte)OutboundMessageTypes.REQUEST_ENTRY_LIST);
//                 bw.Write(_connectionId.Value);
//             });
//         }
//         catch (Exception ex)
//         {
//             Log.Debug($"RequestEntryList Error: {ex.Message}");
//         }
//     }
//
//     private void RequestTrackData()
//     {
//         try
//         {
//             Send(bw =>
//             {
//                 bw.Write((byte)OutboundMessageTypes.REQUEST_TRACK_DATA);
//                 bw.Write(_connectionId.Value);
//             });
//         }
//         catch (Exception ex)
//         {
//             Log.Debug($"RequestTrackData Error: {ex.Message}");
//         }
//     }
//
//     public void RequestFocusChange(int carIndex = -1, string cameraSet = null, string camera = null)
//     {
//         if (_connectionId == null)
//             return;
//
//         Send(bw =>
//         {
//             bw.Write((byte)OutboundMessageTypes.CHANGE_FOCUS);
//             bw.Write(_connectionId.Value);
//
//             if (carIndex >= 0)
//             {
//                 bw.Write((byte)1);
//                 bw.Write((ushort)carIndex);
//             }
//             else
//             {
//                 bw.Write((byte)0);
//             }
//
//             if (!string.IsNullOrEmpty(cameraSet) && !string.IsNullOrEmpty(camera))
//             {
//                 bw.Write((byte)1);
//                 WriteString(bw, cameraSet);
//                 WriteString(bw, camera);
//             }
//             else
//             {
//                 bw.Write((byte)0);
//             }
//         });
//     }
//
//     public void RequestInstantReplay(float startTime, float durationMs, int carIndex = -1, string cameraSet = "", string camera = "")
//     {
//         if (_connectionId == null)
//             return;
//
//         Send(bw =>
//         {
//             bw.Write((byte)OutboundMessageTypes.INSTANT_REPLAY_REQUEST);
//             bw.Write(_connectionId.Value);
//             bw.Write(startTime);
//             bw.Write(durationMs);
//             bw.Write(carIndex);
//             WriteString(bw, cameraSet);
//             WriteString(bw, camera);
//         });
//     }
//
//     public void RequestHudPage(string pageName)
//     {
//         if (_connectionId == null)
//             return;
//
//         Send(bw =>
//         {
//             bw.Write((byte)OutboundMessageTypes.CHANGE_HUD_PAGE);
//             bw.Write(_connectionId.Value);
//             WriteString(bw, pageName);
//         });
//     }
//
//     private static void WriteString(BinaryWriter bw, string s)
//     {
//         var bytes = Encoding.UTF8.GetBytes(s);
//         bw.Write((ushort)bytes.Length);
//         bw.Write(bytes);
//     }
//     
//     public void Start(string url, int port, string password, string commandPassword = "", string displayName = "DRE x ACC", int updateIntervalMs = 16)
//         {
//             try
//             {
//                 if (!string.IsNullOrEmpty(IpAddress) || _connectionState != UDPState.Disconnected)
//                     throw new InvalidOperationException($"Must be stopped. IpAddress={IpAddress}, _connectionState={_connectionState}");
//
//                 IpAddress = url;
//                 Port = port;
//                 DisplayName = $"{displayName}-{DateTime.Now:MM-dd-HH-mm-ss}";
//                 ConnectionPassword = password;
//                 MsRealtimeUpdateInterval = updateIntervalMs;
//                 CommandPassword = commandPassword;
//
//                 UpdateConnectionState(UDPState.Connecting);
//                 _connectionId = null;
//
//                 _cts = new CancellationTokenSource();
//                 Task.Run(() => ConnectAsync(_cts.Token), _cts.Token);
//             }
//             catch (Exception ex)
//             {
//                 Log.Error(ex, "Start Error");
//             }
//         }
//
//     private async Task ConnectAsync(CancellationToken token)
//     {
//         _udpAlive = true;
//
//         while (_udpAlive && !token.IsCancellationRequested)
//         {
//             try
//             {
//                 GetUdpClient();
//
//                 if (!_udpClient.Client.Connected)
//                 {
//                     await Task.Delay(1000, token);
//                     continue;
//                 }
//
//                 // Await ReceiveAsync with cancellation support
//                 var receiveTask = _udpClient.ReceiveAsync();
//
//                 using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token);
//                 var completedTask = await Task.WhenAny(receiveTask, Task.Delay(Timeout.Infinite, linkedCts.Token));
//
//                 if (completedTask == receiveTask)
//                 {
//                     // Process the received packet
//                     var udpPacket = receiveTask.Result;
//
//                     if (udpPacket.Buffer.Length == 0)
//                     {
//                         await Task.Delay(10, token);
//                         continue;
//                     }
//
//                     using var ms = new MemoryStream(udpPacket.Buffer);
//                     using var br = new BinaryReader(ms);
//
//                     if (br.BaseStream.Length < 1)
//                     {
//                         Log.Error("ConnectAsync: Received packet too short.");
//                         continue;
//                     }
//
//                     var messageType = (InboundMessageTypes)br.ReadByte();
//                     if (_getMethods.TryGetValue(messageType, out var handler))
//                     {
//                         handler(br);
//                     }
//                     else
//                     {
//                         Log.Error($"ConnectAsync: Unknown message type: {messageType}");
//                     }
//                 }
//                 else
//                 {
//                     // The delay was cancelled, likely due to shutdown
//                     break;
//                 }
//             }
//             catch (OperationCanceledException)
//             {
//                 // Expected during shutdown, no action needed
//             }
//             catch (SocketException se)
//             {
//                 // Socket was closed, likely due to shutdown
//                 if (se.SocketErrorCode is not (SocketError.Interrupted or SocketError.ConnectionReset))
//                     Log.Error(se, $"ConnectAsync SocketException Error: {se.Message} {se.SocketErrorCode} ({se.ErrorCode})");
//             }
//             catch (Exception ex)
//             {
//                 Log.Error(ex, $"ConnectAsync Error: {ex.Message}");
//             }
//
//             await Task.Delay(1000, token);
//         }
//
//         // Cleanup after exiting loop
//         if (_udpClient != null)
//         {
//             _udpClient.Dispose();
//             _udpClient = null;
//         }
//
//         UpdateConnectionState(UDPState.Disconnected);
//         Log.Debug("ACC UDP Client has stopped.");
//     }
//
//     private void GetUdpClient()
//     {
//         lock (UdpLock)
//         {
//             if (_udpClient == null || !_udpClient.Client.Connected)
//             {
//                 _udpClient?.Dispose();
//
//                 _udpClient = new UdpClient();
//                 _udpClient.Connect(IpAddress, Port);
//
//                 RequestConnection();
//             }
//
//             return;
//         }
//     }
//
//     public void Stop(string reason = "disconnected")
//     {
//         try
//         {
//             _udpAlive = false;
//
//             _cts?.Cancel();
//
//             _udpClient?.Close();
//             _udpClient = null;
//
//             Log.Debug($"ACC UDP Client Stopped: {reason}");
//             UpdateConnectionState(UDPState.Disconnected);
//         }
//         catch (Exception ex)
//         {
//             Log.Error(ex, $"Stop Error: {ex.Message}");
//         }
//     }
//
//     public void Dispose()
//     {
//         Stop("disposed");
//         _cts?.Dispose();
//     }
//
//     public void RequestDataOnConnected()
//     {
//         try
//         {
//             if (_udpClient == null || _udpClient.Client == null || !_udpClient.Client.Connected)
//                 return;
//
//             RequestTrackData();
//             RequestEntryList();
//         }
//         catch (Exception ex)
//         {
//             Log.Error(ex, "RequestDataOnConnected");
//         }
//     }
// }
//
// public enum UDPState
// {
//     Connected,
//     Disconnected,
//     Connecting
// }
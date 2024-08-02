using System.Collections.Generic;
using AssettoCorsaSharedMemory;

namespace Sim.AssettoCorsaCompetizione;

public class RealtimeUpdate
{
    public ushort EventIndex { get; set; }
    public ushort SessionIndex { get; set; }
    public SessionType SessionType { get; set; }
    public SessionPhase SessionPhase { get; set; }
    public float SessionTimeMs { get; set; }
    public float SessionEndTimeMs { get; set; }
    public int FocusedCarIndex { get; set; }
    public string ActiveCameraSet { get; set; }
    public string ActiveCamera { get; set; }
    public string CurrentHudPage { get; set; }
    public bool IsReplayPlaying { get; set; }
    public float ReplaySessionTime { get; set; }
    public float ReplayRemainingTime { get; set; }
    public float TimeOfDayMs { get; set; }
    public byte AmbientTemp { get; set; }
    public byte TrackTemp { get; set; }
    public float Clouds { get; set; }
    public float RainLevel { get; set; }
    public float Wetness { get; set; }
    public Lap BestSessionLap { get; set; }
}

public class Lap
{
    public int LapTimeMs { get; set; }
    public ushort CarIndex { get; set; }
    public ushort DriverIndex { get; set; } //Driver swaps update this value
    public List<int> Splits { get; set; }
    public bool IsInvalid { get; set; }
    public bool IsValidForBest { get; set; }
    public bool IsOutlap { get; set; }
    public bool IsInlap { get; set; }
    public LapType Type { get; set; }
}

public class RealtimeCarUpdate
{
    public ushort CarIndex { get; set; }
    public ushort DriverIndex { get; set; } //Driver swaps update this value
    public byte DriverCount { get; set; }
    public int Gear { get; set; }
    public float WorldPosX { get; set; }
    public float WorldPosY { get; set; }
    public float Yaw { get; set; }
    public CarLocation Location { get; set; }
    public ushort Kmh { get; set; }
    public ushort Position { get; set; }
    
    /// <summary>
    /// Official P/Q/R position
    /// </summary>
    public ushort CupPosition { get; set; }
    
    /// <summary>
    /// Allegedly always 0. Use Position instead?
    /// </summary>
    public ushort TrackPosition { get; set; }
    public float SplinePosition { get; set; }
    
    /// <summary>
    /// Is how many laps completed by the car
    /// </summary>
    public ushort Laps { get; set; }
    
    /// <summary>
    /// Expecting this to be delta to the best lap in milliseconds
    /// </summary>
    public int Delta { get; set; }
    public Lap BestSessionLap { get; set; }
    public Lap LastLap { get; set; }
    public Lap CurrentLap { get; set; }
}

public class Driver
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string ShortName { get; set; }
    public DriverCategory Category { get; set; }
    public Nationality Nationality { get; set; }
}

public class EntryListCar
{
    public ushort CarIndex { get; set; }
    public AssettoCorsa.CarModel ModelType { get; set; }
    public string TeamName { get; set; }
    public int RaceNumber { get; set; }
    public byte CupCategory { get; set; }
    public byte CurrentDriverIndex { get; set; } //Driver swaps update this value
    public Nationality Nationality { get; set; }
    public List<Driver> Drivers { get; set; }
}

public class TrackData
{
    public int ConnectionId { get; set; }
    public string TrackName { get; set; }
    public int TrackId { get; set; }
    public int TrackMeters { get; set; }
    public Dictionary<string, List<string>> CameraSets { get; set; }
    public List<string> HudPages { get; set; }
}

public class BroadcastingEvent
{
    public BroadcastingEventType Type { get; set; }
    public string Message { get; set; }
    public int TimeMs { get; set; }
    public int CarIndex { get; set; }
}
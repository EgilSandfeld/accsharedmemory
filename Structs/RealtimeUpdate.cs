using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ksBroadcastingNetwork.Structs
{
    public struct RealtimeUpdate
    {
        public int EventIndex { get; internal set; }
        public int SessionIndex { get; internal set; }
        public SessionPhase Phase { get; internal set; }
        public TimeSpan SessionTime { get; internal set; }
        public TimeSpan RemainingTime { get; internal set; }
        public TimeSpan TimeOfDay { get; internal set; }
        public float TimeOfDaySeconds { get; set; }
        public float RainLevel { get; internal set; }
        public float Clouds { get; internal set; }
        public float Wetness { get; internal set; }
        public LapInfo BestSessionLap { get; internal set; }
        public ushort BestLapCarIndex { get; internal set; }
        public ushort BestLapDriverIndex { get; internal set; }
        public int FocusedCarIndex { get; internal set; }
        public string ActiveCameraSet { get; internal set; }
        public string ActiveCamera { get; internal set; }
        public bool IsReplayPlaying { get; internal set; }
        public float ReplaySessionTime { get; internal set; }
        public float ReplayRemainingTime { get; internal set; }
        public TimeSpan SessionRemainingTime { get; internal set; }
        public TimeSpan SessionEndTime { get; internal set; }
        public RaceSessionType SessionType { get; internal set; }
        public SessionPhase SessionPhase { get; set; }
        public float SessionTimeMs { get; set; }
        public float SessionEndTimeMs { get; set; }
        public byte AmbientTemp { get; internal set; }
        public byte TrackTemp { get; internal set; }
        public string CurrentHudPage { get; internal set; }
        
        public static RealtimeUpdate Null() => new RealtimeUpdate
        {
            EventIndex = 0,
            SessionIndex = 0,
            Phase = default,
            SessionTime = TimeSpan.Zero,
            RemainingTime = TimeSpan.Zero,
            TimeOfDay = TimeSpan.Zero,
            TimeOfDaySeconds = 0f,
            RainLevel = 0f,
            Clouds = 0f,
            Wetness = 0f,
            BestSessionLap = null,
            BestLapCarIndex = 0,
            BestLapDriverIndex = 0,
            FocusedCarIndex = 0,
            ActiveCameraSet = null,
            ActiveCamera = null,
            IsReplayPlaying = false,
            ReplaySessionTime = 0f,
            ReplayRemainingTime = 0f,
            SessionRemainingTime = TimeSpan.Zero,
            SessionEndTime = TimeSpan.Zero,
            SessionType = default,
            SessionPhase = default,
            AmbientTemp = 0,
            TrackTemp = 0,
            CurrentHudPage = null,
            SessionTimeMs = 0f,
            SessionEndTimeMs = -1
        };
    }
}

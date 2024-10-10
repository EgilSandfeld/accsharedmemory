using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Newtonsoft.Json;

namespace AssettoCorsaSharedMemory
{
    /// <summary>
    /// IMPORTANT !!!
    /// DO NOT CHANGE THE ORDER OF THE VARIABLES, OR ADD OR REMOVE VARIABLES
    /// DOING SO WILL BREAK THE SHARED MEMORY INTERFACE WITH ACC
    /// ADDING METHODS IS OK
    /// </summary>
    [StructLayout (LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
    [Serializable]
    public struct ACCSharedMemoryGraphics
    {
        /// <summary>
        /// Current step index
        /// </summary>
        public int PacketId;

        /// <summary>
        /// Off, Replay, Live, Pause
        /// </summary>
        public AC_STATUS Status;

        /// <summary>
        /// Unknown, Practice, qualify, race, etc.
        /// </summary>
        public AC_SESSION_TYPE Session;

        /// <summary>
        /// Current player lap time in text
        /// </summary>
        [MarshalAs (UnmanagedType.ByValTStr, SizeConst = 15)]
        public string CurrentTime;

        /// <summary>
        /// Last player lap time in text
        /// </summary>
        [MarshalAs (UnmanagedType.ByValTStr, SizeConst = 15)]
        public string LastTime;

        /// <summary>
        /// Best player lap time in text
        /// </summary>
        [MarshalAs (UnmanagedType.ByValTStr, SizeConst = 15)]
        public string BestTime;

        /// <summary>
        /// Last split time in text
        /// </summary>
        [MarshalAs (UnmanagedType.ByValTStr, SizeConst = 15)]
        public string Split;

        /// <summary>
        /// No of completed laps
        /// </summary>
        public int CompletedLaps;

        /// <summary>
        /// Current player position
        /// </summary>
        public int Position;

        /// <summary>
        /// Current lap time in milliseconds
        /// </summary>
        public int iCurrentTime;

        /// <summary>
        /// Last lap time in milliseconds
        /// </summary>
        public int iLastTime;

        /// <summary>
        /// Best lap time in milliseconds
        /// </summary>
        public int iBestTime;

        /// <summary>
        /// Session time left
        /// </summary>
        public float SessionTimeLeft;

        /// <summary>
        /// Distance travelled in the current stint
        /// </summary>
        public float DistanceTraveled;

        /// <summary>
        /// Car is pitting
        /// </summary>
        public int IsInPit;

        /// <summary>
        /// Current track sector
        /// </summary>
        public int CurrentSectorIndex;

        /// <summary>
        /// Last sector time in milliseconds
        /// </summary>
        public int LastSectorTime;

        /// <summary>
        /// Number of completed laps
        /// </summary>
        public int NumberOfLaps;

        /// <summary>
        /// Tyre compound used
        /// </summary>
        [MarshalAs (UnmanagedType.ByValTStr, SizeConst = 33)]
        public string TyreCompound;

        /// <summary>
        /// Replay multiplier
        /// <para>
        /// NOT AVAILABLE IN ACC
        /// </para>
        /// </summary>
        public float NA_ReplayTimeMultiplier;

        /// <summary>
        /// Car position on track spline (0.0 start to 1.0 finish)
        /// </summary>
        public float NormalizedCarPosition;

        /// <summary>
        /// Number of cars on track
        /// <para>
        /// NOT AVAILABLE IN AC DOCUMENTATION
        /// ONLY ACC
        /// </para>
        /// </summary>
        public int ActiveCars;

        /// <summary>
        /// Coordinates of cars on track (not world meters!)
        /// Used for rendering the car's position on the track map or minimap, which is a 2D representation. This requires transforming the 3D world positions into 2D coordinates that fit within the map's display area.
        /// <para>
        /// AC DOCUMENTATION ONLY SHOWS ONE CAR VALUE
        /// ACC SHOWS 60 CAR VALUES
        /// </para>
        /// </summary>
        [MarshalAs (UnmanagedType.ByValArray, SizeConst = 60)]
        public Coordinates[] CarCoordinates;

        /// <summary>
        /// Car IDs of cars on track
        /// <para>
        /// NOT AVAILABLE IN AC DOCUMENTATION
        /// ONLY ACC
        /// </para>
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 60)]
        public int[] CarID;

        /// <summary>
        /// Player Car ID
        /// <para>
        /// NOT AVAILABLE IN AC DOCUMENTATION
        /// ONLY ACC
        /// </para>
        /// </summary>
        public int PlayerCarID;

        /// <summary>
        /// Penalty time to wait
        /// </summary>
        public float PenaltyTime;

        /// <summary>
        /// Current flag type
        /// </summary>
        public AC_FLAG_TYPE Flag;

        /// <summary>
        /// Penalty type and reason
        /// <para>
        /// NOT AVAILABLE IN AC DOCUMENTATION
        /// ONLY ACC
        /// </para>
        /// </summary>
        public AC_PENALTY_TYPE Penalty;

        /// <summary>
        /// Ideal line on
        /// </summary>
        public int IdealLineOn;

        // since 1.5

        /// <summary>
        /// Car is in pit lane
        /// </summary>
        public int IsInPitLane;

        /// <summary>
        /// Ideal line friction coefficient
        /// </summary>
        public float SurfaceGrip;

        // since 1.13

        /// <summary>
        /// Mandatory pit is completed
        /// </summary>
        public int MandatoryPitDone;

        // since ???

        /// <summary>
        /// Wind speed in m/s
        /// </summary>
        public float WindSpeed;

        /// <summary>
        /// Wind direction in radians
        /// </summary>
        public float WindDirection;

        /// <summary>
        /// Car is working on setup
        /// </summary>
        public int IsSetupMenuVisible;

        /// <summary>
        /// Current car main display index
        /// see ACCSharedMemoryDocumentation Appendix 1
        /// </summary>
        public int MainDisplayIndex;

        /// <summary>
        /// Current car secondary display index
        /// see ACCSharedMemoryDocumentation Appendix 1
        /// </summary>
        public int SecondaryDisplayIndex;

        /// <summary>
        /// Traction control level
        /// </summary>
        public int TC;

        /// <summary>
        /// Traction control cut level
        /// </summary>
        public int TCCut;

        /// <summary>
        /// Current engine map
        /// </summary>
        public int EngineMap;

        /// <summary>
        /// ABS level
        /// </summary>
        public int ABS;

        /// <summary>
        /// Average fuel consumed per lap in liters
        /// </summary>
        public float FuelXLap;

        /// <summary>
        /// Rain lights on
        /// </summary>
        public int RainLights;

        /// <summary>
        /// Flashing lights on
        /// </summary>
        public int FlashingLights;

        /// <summary>
        /// Current lights stage
        /// </summary>
        public int LightsStage;

        /// <summary>
        /// Exhaust temperature
        /// </summary>
        public float ExhaustTemperature;

        /// <summary>
        /// Current wiper stage
        /// </summary>
        public int WiperLevel;

        /// <summary>
        /// Time the driver is allowed to drive/race (ms)
        /// </summary>
        public int DriverStintTotalTimeLeft;

        /// <summary>
        /// Time the driver is allowed to drive/stint (ms)
        /// </summary>
        public int DriverStintTimeLeft;

        /// <summary>
        /// Are rain tyres equipped
        /// </summary>
        public int RainTyres;

        /// <summary>
        /// No description available
        /// </summary>
        public int SessionIndex;

        /// <summary>
        /// Used fuel since last time refueling
        /// </summary>
        public float UsedFuel;

        /// <summary>
        /// Delta time in text
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
        public string DeltaLapTime;
        
        /// <summary>
        /// Delta time in milliseconds
        /// </summary>
        public int IDeltaLapTime;
        
        /// <summary>
        /// Estimated lap time in text
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
        public string EstimatedLapTime;

        /// <summary>
        /// Estimated lap time in milliseconds
        /// </summary>
        public int iEstimatedLapTime;
        
        /// <summary>
        /// Delta positive (1) or negative (0)
        /// </summary>
        public int IsDeltaPositive;

        /// <summary>
        /// Last split time in milliseconds
        /// </summary>
        public int iSplit;

        /// <summary>
        /// Check if Lap is valid for timing
        /// </summary>
        public int IsValidLap;

        /// <summary>
        /// Laps possible with current fuel level
        /// </summary>
        public float FuelEstimatedLaps;
        
        /// <summary>
        /// Status of track
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
        public string TrackStatus;

        /// <summary>
        /// Mandatory pitstops the player still has to do
        /// </summary>
        public int MissingMandatoryPits;

        /// <summary>
        /// Time of day in seconds
        /// </summary>
        public float Clock;

        /// <summary>
        /// Is Blinker left on
        /// </summary>
        public int DirectionLightsLeft;

        /// <summary>
        /// Is Blinker right on
        /// </summary>
        public int DirectionLightsRight;

        /// <summary>
        /// Yellow Flag is out?
        /// </summary>
        public int GlobalYellow;

        /// <summary>
        /// Yellow Flag in Sector 1 is out?
        /// </summary>
        public int GlobalYellow1;

        /// <summary>
        /// Yellow Flag in Sector 2 is out?
        /// </summary>
        public int GlobalYellow2;

        /// <summary>
        /// Yellow Flag in Sector 3 is out?
        /// </summary>
        public int GlobalYellow3;

        /// <summary>
        /// White Flag is out?
        /// </summary>
        public int GlobalWhite;

        /// <summary>
        /// Green Flag is out?
        /// </summary>
        public int GlobalGreen;

        /// <summary>
        /// Checkered Flag is out?
        /// </summary>
        public int GlobalChequered;

        /// <summary>
        /// Red Flag is out?
        /// </summary>
        public int GlobalRed;

        /// <summary>
        /// # of tyre set on the MFD
        /// 0-indexed
        /// </summary>
        public int MFDTyreSet;

        /// <summary>
        /// How much fuel to add on the MFD
        /// </summary>
        public float MFDFuelToAdd;

        /// <summary>
        /// Tyre pressure left front on the MFD
        /// </summary>
        public float MFDTyrePressureLF;

        /// <summary>
        /// Tyre pressure right front on the MFD
        /// </summary>
        public float MFDTyrePressureRF;

        /// <summary>
        /// Tyre pressure left rear on the MFD
        /// </summary>
        public float MFDTyrePressureLR;

        /// <summary>
        /// Tyre pressure right rear on the MFD
        /// </summary>
        public float MFDTyrePressureRR;

        /// <summary>
        /// Green, Fast, Optimum, Greasy, Damp, Wet, Flooded
        /// </summary>
        public AC_TRACK_GRIP_STATUS TrackGripStatus;

        /// <summary>
        /// No Rain, Drizzle, Light/Med/Heady Rain, Thunderstorm
        /// </summary>
        public AC_RAIN_INTENSITY RainIntensity;

        /// <summary>
        /// No Rain, Drizzle, Light/Med/Heady Rain, Thunderstorm
        /// </summary>
        public AC_RAIN_INTENSITY RainIntensityIn10Min;

        /// <summary>
        /// No Rain, Drizzle, Light/Med/Heady Rain, Thunderstorm
        /// </summary>
        public AC_RAIN_INTENSITY RainIntensityIn30Min;

        /// <summary>
        /// Tyre Set currently in use
        /// 1-indexed
        /// </summary>
        public int CurrentTyreSet;

        /// <summary>
        /// Next Tyre set per strategy
        /// TODO: Check if this is instead 'Pit Strategy' number
        /// </summary>
        public int StrategyTyreSet;

        /// <summary>
        /// Distance in ms to car in front
        /// </summary>
        public int GapAhead;

        /// <summary>
        /// Distance in ms to car behind
        /// </summary>
        public int GapBehind;

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public float GetPrecipitation()
        {
            return ACCSharedMemoryConverters.RainIntensityEnumToFloat(RainIntensity);
        }

        public float Wetness()
        {
            return ACCSharedMemoryConverters.TrackGripStatusEnumToWetnessFloat(TrackGripStatus);
        }
    }
}
using System;
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
    public struct ACCSharedMemoryStatic
    {
        /// <summary>
        /// Version of the Shared Memory structure
        /// </summary>
        [MarshalAs (UnmanagedType.ByValTStr, SizeConst = 15)]
        public string SMVersion;

        /// <summary>
        /// Version of Assetto Corsa
        /// </summary>
        [MarshalAs (UnmanagedType.ByValTStr, SizeConst = 15)]
        public string ACVersion;

        // session static info

        /// <summary>
        /// Number of sessions in this instance
        /// </summary>
        public int NumberOfSessions;

        /// <summary>
        /// Max number of possible cars on track
        /// </summary>
        public int NumCars;

        /// <summary>
        /// Name of the player’s car
        /// see ACCSharedMemoryDocumentation Appendix 2
        /// </summary>
        [MarshalAs (UnmanagedType.ByValTStr, SizeConst = 33)]
        public string CarModel;
        
        /// <summary>
        /// Name of the track
        /// </summary>
        [MarshalAs (UnmanagedType.ByValTStr, SizeConst = 33)]
        public string Track;

        /// <summary>
        /// Name of the player
        /// </summary>
        [MarshalAs (UnmanagedType.ByValTStr, SizeConst = 33)]
        public string PlayerName;

        /// <summary>
        /// Surname of the player
        /// </summary>
        [MarshalAs (UnmanagedType.ByValTStr, SizeConst = 33)]
        public string PlayerSurname;

        /// <summary>
        /// Nickname of the player
        /// </summary>
        [MarshalAs (UnmanagedType.ByValTStr, SizeConst = 33)]
        public string PlayerNick;

        /// <summary>
        /// Number of track sectors
        /// </summary>
        public int SectorCount;

        // car static info

        /// <summary>
        /// Max torque value of the player’s car
        /// <para>
        /// NOT AVAILABLE IN ACC
        /// </para>
        /// </summary>
        public float NA_MaxTorque;

        /// <summary>
        /// Max power value of the player’s car
        /// <para>
        /// NOT AVAILABLE IN ACC
        /// </para>
        /// </summary>
        public float NA_MaxPower;

        /// <summary>
        /// Maximum rpm
        /// </summary>
        public int MaxRpm;

        /// <summary>
        /// Maximum fuel tank capacity
        /// </summary>
        public float MaxFuel;

        /// <summary>
        /// Max travel distance of each tyre [FL, FR, RL, RR]
        /// <para>
        /// NOT AVAILABLE IN ACC
        /// </para>
        /// </summary>
        [MarshalAs (UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] NA_SuspensionMaxTravel;

        /// <summary>
        /// Radius of each tyre [FL, FR, RL, RR]
        /// <para>
        /// NOT AVAILABLE IN ACC
        /// </para>
        /// </summary>
        [MarshalAs (UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] NA_TyreRadius;

        // since 1.5

        /// <summary>
        /// Max turbo boost value of the player’s car
        /// <para>
        /// NOT AVAILABLE IN ACC
        /// </para>
        /// </summary>
        public float NA_MaxTurboBoost;

        //[Obsolete("AirTemp since 1.6 in physic")]
        public float NA_AirTemp;

        //[Obsolete("RoadTemp since 1.6 in physic")]
        public float NA_RoadTemp;

        /// <summary>
        /// Cut penalties enabled: 1 (true) or 0 (false)
        /// </summary>
        public int PenaltiesEnabled;

        /// <summary>
        /// Fuel consumption rate: 0 (no cons), 1 (normal), 2 (double cons)
        /// </summary>
        public float AidFuelRate;

        /// <summary>
        /// Tire wear rate: 0 (no wear), 1 (normal), 2 (double wear) etc.
        /// </summary>
        public float AidTireRate;

        /// <summary>
        /// Damage rate: 0 (no damage) to 1 (normal)
        /// </summary>
        public float AidMechanicalDamage;

        /// <summary>
        /// Player starts with hot (optimal temp) tyres: 1 (true) or 0 (false)
        /// </summary>
        public int AidAllowTyreBlankets;

        /// <summary>
        /// Stability control used
        /// </summary>
        public float AidStability;

        /// <summary>
        /// Auto clutch used
        /// </summary>
        public int AidAutoClutch;

        /// <summary>
        /// If player’s car has the “auto blip” feature enabled : 0 or 1
        /// </summary>
        public int AidAutoBlip;

        // since 1.7.1

        /// <summary>
        /// If player’s car has the “DRS” system: 0 or 1
        /// <para>
        /// NOT AVAILABLE IN ACC
        /// </para>
        /// </summary>
        public int NA_HasDRS;

        /// <summary>
        /// If player’s car has the “ERS” system: 0 or 1
        /// <para>
        /// NOT AVAILABLE IN ACC
        /// </para>
        /// </summary>
        public int NA_HasERS;

        /// <summary>
        /// If player’s car has the “KERS” system: 0 or 1
        /// <para>
        /// NOT AVAILABLE IN ACC
        /// </para>
        /// </summary>
        public int NA_HasKERS;

        /// <summary>
        /// Max KERS Joule value of the player’s car
        /// <para>
        /// NOT AVAILABLE IN ACC
        /// </para>
        /// </summary>
        public float NA_KersMaxJoules;

        /// <summary>
        /// Count of possible engine brake settings of the player’s car
        /// <para>
        /// NOT AVAILABLE IN ACC
        /// </para>
        /// </summary>
        public int NA_EngineBrakeSettingsCount;

        /// <summary>
        /// Count of the possible power controllers of the player’s car
        /// <para>
        /// NOT AVAILABLE IN ACC
        /// </para>
        /// </summary>
        public int NA_ErsPowerControllerCount;

        //since 1.7.2

        /// <summary>
        /// Length of the spline of the selected track
        /// <para>
        /// NOT AVAILABLE IN ACC
        /// </para>
        /// </summary>
        public float NA_TrackSplineLength;

        /// <summary>
        /// Name of the track’s layout (only multi-layout tracks)
        /// <para>
        /// NOT AVAILABLE IN ACC
        /// </para>
        /// </summary>
        [MarshalAs (UnmanagedType.ByValTStr, SizeConst = 15)]
        public string NA_TrackConfiguration;

        //since 1.10.2

        /// <summary>
        /// Max ERS Joule value of the player’s car
        /// <para>
        /// NOT AVAILABLE IN ACC
        /// </para>
        /// </summary>
        public float NA_ErsMaxJ;

        // since 1.13

        /// <summary>
        /// 1 if the race is a timed one
        /// <para>
        /// NOT AVAILABLE IN ACC
        /// </para>
        /// </summary>
        public int NA_IsTimedRace;

        /// <summary>
        /// 1 if the timed race is set with an extra lap
        /// <para>
        /// NOT AVAILABLE IN ACC
        /// </para>
        /// </summary>
        public int NA_HasExtraLap;

        /// <summary>
        /// Name of the used skin
        /// <para>
        /// NOT AVAILABLE IN ACC
        /// </para>
        /// </summary>
        [MarshalAs (UnmanagedType.ByValTStr, SizeConst = 33)]
        public string NA_CarSkin;

        /// <summary>
        /// How many positions are going to be swapped in the second race
        /// <para>
        /// NOT AVAILABLE IN ACC
        /// </para>
        /// </summary>
        public int NA_ReversedGridPositions;

        /// <summary>
        /// Pit window opening time
        /// </summary>
        public int PitWindowStart;

        /// <summary>
        /// Pit windows closing time
        /// </summary>
        public int PitWindowEnd;

        /// <summary>
        /// If is a multiplayer session
        /// </summary>
        public int IsOnline;

        /// <summary>
        /// Name of the dry tyres
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
        public string DryTyresName;

        /// <summary>
        /// Name of the wet tyres
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
        public string WetTyresName;
        
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
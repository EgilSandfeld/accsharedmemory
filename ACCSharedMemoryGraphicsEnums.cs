using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AssettoCorsaSharedMemory
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum AC_PENALTY_TYPE
    {
        ACC_None = 0,
        ACC_DriveThrough_Cutting = 1,
        ACC_StopAndGo_10_Cutting = 2,
        ACC_StopAndGo_20_Cutting = 3,
        ACC_StopAndGo_30_Cutting = 4,
        ACC_Disqualified_Cutting = 5,
        ACC_RemoveBestLaptime_Cutting = 6,
        ACC_DriveThrough_PitSpeeding = 7,
        ACC_StopAndGo_10_PitSpeeding = 8,
        ACC_StopAndGo_20_PitSpeeding = 9,
        ACC_StopAndGo_30_PitSpeeding = 10,
        ACC_Disqualified_PitSpeeding = 11,
        ACC_RemoveBestLaptime_PitSpeeding = 12,
        ACC_Disqualified_IgnoredMandatoryPit = 13,
        ACC_PostRaceTime = 14,
        ACC_Disqualified_Trolling = 15,
        ACC_Disqualified_PitEntry = 16,
        ACC_Disqualified_PitExit = 17,
        ACC_Disqualified_Wrongway = 18,
        ACC_DriveThrough_IgnoredDriverStint = 19,
        ACC_Disqualified_IgnoredDriverStint = 20,
        ACC_Disqualified_ExceededDriverStintLimit = 21
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum AC_FLAG_TYPE
    {
        AC_NO_FLAG = 0,
        AC_BLUE_FLAG = 1,
        AC_YELLOW_FLAG = 2,
        AC_BLACK_FLAG = 3,
        AC_WHITE_FLAG = 4,
        AC_CHECKERED_FLAG = 5,
        AC_PENALTY_FLAG = 6,
        AC_GREEN_FLAG = 7,
        AC_ORANGE_FLAG = 8
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum AC_STATUS
    {
        AC_OFF = 0,
        AC_REPLAY = 1,
        AC_LIVE = 2,
        AC_PAUSE = 3
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum AC_SESSION_TYPE
    {
        AC_UNKNOWN = -1,
        AC_PRACTICE = 0,
        AC_QUALIFY = 1,
        AC_RACE = 2,
        AC_HOTLAP = 3,
        AC_TIME_ATTACK = 4,
        AC_DRIFT = 5,
        AC_DRAG = 6,
        AC_HOTSTINT = 7,
        AC_HOTSTINTSUPERPOLE = 8
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum AC_TRACK_GRIP_STATUS
    {
        AC_GREEN = 0,
        AC_FAST = 1,
        AC_OPTIMUM = 2,
        AC_GREASY = 3,
        AC_DAMP = 4,
        AC_WET = 5,
        AC_FLOODED = 6
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum AC_RAIN_INTENSITY
    {
        AC_NO_RAIN = 0,
        AC_DRIZZLE = 1,
        AC_LIGHT_RAIN = 2,
        AC_MEDIUM_RAIN = 3,
        AC_HEAVY_RAIN = 4,
        AC_THUNDERSTORM = 5
    }
}
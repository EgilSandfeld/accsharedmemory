using System;
using System.Diagnostics;

namespace AssettoCorsaSharedMemory;

public class ACCSharedMemoryConverters
{
    public static float RainIntensityEnumToFloat(AC_RAIN_INTENSITY intensity)
    {
        switch (intensity)
        {
            case AC_RAIN_INTENSITY.AC_NO_RAIN:
                return 0f;
            case AC_RAIN_INTENSITY.AC_DRIZZLE:
                return 0.15f;
            case AC_RAIN_INTENSITY.AC_LIGHT_RAIN:
                return 0.3f;
            case AC_RAIN_INTENSITY.AC_MEDIUM_RAIN:
                return 0.5f;
            case AC_RAIN_INTENSITY.AC_HEAVY_RAIN:
                return 0.75f;
            case AC_RAIN_INTENSITY.AC_THUNDERSTORM:
                return 1f;
            default:
                throw new ArgumentOutOfRangeException(nameof(intensity), intensity, null);
        }
    }

    public static float TrackGripStatusEnumToWetnessFloat(AC_TRACK_GRIP_STATUS trackGripStatus)
    {
        switch (trackGripStatus)
        {
            case AC_TRACK_GRIP_STATUS.AC_GREEN:
            case AC_TRACK_GRIP_STATUS.AC_FAST:
            case AC_TRACK_GRIP_STATUS.AC_OPTIMUM:
                return 0;
            case AC_TRACK_GRIP_STATUS.AC_GREASY:
                return 0.1f;
            case AC_TRACK_GRIP_STATUS.AC_DAMP:
                return 0.2f;
            case AC_TRACK_GRIP_STATUS.AC_WET:
                return 0.4f;
            case AC_TRACK_GRIP_STATUS.AC_FLOODED:
                return 0.75f;
            default:
                throw new ArgumentOutOfRangeException(nameof(trackGripStatus), trackGripStatus, null);
        }
    }
}
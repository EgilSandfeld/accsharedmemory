namespace AssettoCorsaSharedMemory;

public static class Helpers
{
    public static bool _correctSessionType;
    public static AC_SESSION_TYPE _sessionTypeLatestNonUnknown;
    public static float _acceptNewSessionTypeClock;

    public static int GraphicsSessionToSessionNum(ACCSharedMemoryGraphics accSharedMemoryGraphics)
    {
        var sessionType = CorrectSessionType(accSharedMemoryGraphics.Session, accSharedMemoryGraphics.Clock);

        switch (sessionType)
        {
            case AC_SESSION_TYPE.AC_QUALIFY:
                return 1;
            
            case AC_SESSION_TYPE.AC_RACE:
                return 2;
            
            default:
                return 0;
        }
    }

    /// <summary>
    /// Corrects the ACC Session Type for drop-outs going to "Unknown"
    /// </summary>
    /// <param name="graphics"></param>
    /// <returns></returns>
    private static AC_SESSION_TYPE CorrectSessionType(AC_SESSION_TYPE rawSessionType, float clock)
    {
        if (rawSessionType != AC_SESSION_TYPE.AC_UNKNOWN)
        {
            _correctSessionType = false;
            _sessionTypeLatestNonUnknown = rawSessionType;
            return rawSessionType;
        }

        if (!_correctSessionType)
        {
            _correctSessionType = true;
            _acceptNewSessionTypeClock = clock + 1;
            return _sessionTypeLatestNonUnknown;
        }

        if (clock > _acceptNewSessionTypeClock)
        {
            _correctSessionType = false;
            _sessionTypeLatestNonUnknown = rawSessionType;
            return rawSessionType;

        }

        return _sessionTypeLatestNonUnknown;
    }
}
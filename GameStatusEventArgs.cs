using System;

namespace AssettoCorsaSharedMemory
{
    public class MemoryStatusEventArgs : EventArgs
    {
        public AC_MEMORY_STATUS Status {get; private set;}

        public MemoryStatusEventArgs(AC_MEMORY_STATUS status)
        {
            Status = status;
        }
    }
    public class GameStatusEventArgs : EventArgs
    {
        public AC_STATUS GameStatus {get; private set;}

        public GameStatusEventArgs(AC_STATUS status)
        {
            GameStatus = status;
        }
    }
    public class PitStatusEventArgs : EventArgs
    {
        public int PitStatus { get; private set; }

        public PitStatusEventArgs(int status)
        {
            PitStatus = status;
        }
    }
    public class SessionTypeEventArgs : EventArgs
    {
        public AC_SESSION_TYPE SessionType { get; private set; }

        public SessionTypeEventArgs(AC_SESSION_TYPE status)
        {
            SessionType = status;
        }
    }
}

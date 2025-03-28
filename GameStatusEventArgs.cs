﻿using System;

namespace AssettoCorsaSharedMemory
{
    public class PhysicsEventArgs : EventArgs
    {
        public PhysicsEventArgs(ACCSharedMemoryPhysics accSharedMemoryPhysics)
        {
            ACCSharedMemoryPhysics = accSharedMemoryPhysics;
        }

        public ACCSharedMemoryPhysics ACCSharedMemoryPhysics { get; private set; }
    }

    public class StaticInfoEventArgs : EventArgs
    {
        public StaticInfoEventArgs(ACCSharedMemoryStatic accSharedMemoryStatic)
        {
            ACCSharedMemoryStatic = accSharedMemoryStatic;
        }

        public ACCSharedMemoryStatic ACCSharedMemoryStatic { get; private set; }
    }

    public class GraphicsEventArgs : EventArgs
    {
        public GraphicsEventArgs(ACCSharedMemoryGraphics accSharedMemoryGraphics)
        {
            ACCSharedMemoryGraphics = accSharedMemoryGraphics;
        }

        public ACCSharedMemoryGraphics ACCSharedMemoryGraphics { get; private set; }
    }

    public class EvoGraphicsEventArgs : EventArgs
    {
        public EvoGraphicsEventArgs(ACCSharedMemoryGraphics acEvoSharedMemoryGraphics)
        {
            ACEvoSharedMemoryGraphics = acEvoSharedMemoryGraphics;
        }

        public ACCSharedMemoryGraphics ACEvoSharedMemoryGraphics { get; private set; }
    }
    
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
    public class EvoGameStatusEventArgs : EventArgs
    {
        public int GameStatus {get; private set;}

        public EvoGameStatusEventArgs(int status)
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

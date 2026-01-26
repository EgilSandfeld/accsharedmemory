using Newtonsoft.Json;

namespace AssettoCorsaSharedMemory.Structs
{
    public struct RealtimeCarUpdate
    {
        public int CarIndex { get; internal set; }
        public int DriverIndex { get; internal set; }
        public int Gear { get; internal set; }

        /// <summary>
        /// Provides the car's position in the game's global world coordinate system, measured in meters, on the X-axis.
        /// It represents the exact location of the car in the 3D game world, using a coordinate system where positions are typically defined relative to a global origin point (often the center of the track or another fixed point in the game's world).
        /// </summary>
        public float WorldPosX { get; internal set; }

        /// <summary>
        /// Provides the car's position in the game's global world coordinate system, measured in meters, on the Y-axis.
        /// It represents the exact location of the car in the 3D game world, using a coordinate system where positions are typically defined relative to a global origin point (often the center of the track or another fixed point in the game's world).
        /// </summary>
        public float WorldPosY { get; internal set; }
        
        public float Yaw { get; internal set; }
        public CarLocationEnum Location { get; internal set; }
        public int Kmh { get; internal set; }
        public int Position { get; internal set; }
        public int TrackPosition { get; internal set; }
        public float SplinePosition { get; internal set; }
        public int Delta { get; internal set; }
        public LapInfo BestSessionLap { get; internal set; }
        public LapInfo LastLap { get; internal set; }
        public LapInfo CurrentLap { get; internal set; }
        
        /// <summary>
        /// May indicate completed laps?
        /// </summary>
        public int Laps { get; internal set; }
        public ushort CupPosition { get; internal set; }
        public byte DriverCount { get; internal set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
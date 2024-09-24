﻿using System.Collections.Generic;

namespace AssettoCorsaSharedMemory.Structs
{
    public struct TrackData
    {
        public int ConnectionId { get; set; }
        public string TrackName { get; internal set; }
        public int TrackId { get; internal set; }
        public float TrackMeters { get; internal set; }
        public Dictionary<string, List<string>> CameraSets { get; internal set; }
        public IEnumerable<string> HUDPages { get; internal set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ksBroadcastingNetwork.Structs
{
    public struct BroadcastingEvent
    {
        public BroadcastingCarEventType Type { get; internal set; }
        public string Message { get; internal set; }
        public int TimeMs { get; internal set; }
        public int CarIndex { get; internal set; }
        public CarInfo CarData { get; internal set; }
        
        public static BroadcastingEvent Null() => new BroadcastingEvent
        {
            Type = default,
            Message = null,
            TimeMs = 0,
            CarIndex = 0,
            CarData = null
        };
    }
}

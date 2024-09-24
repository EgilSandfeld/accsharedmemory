namespace AssettoCorsaSharedMemory.Structs
{
    public struct BroadcastingEvent
    {
        public BroadcastingCarEventType Type { get; internal set; }
        public string Message { get; internal set; }
        public int TimeMs { get; internal set; }
        public int CarIndex { get; internal set; }
        public CarInfo CarData { get; internal set; }
    }
}

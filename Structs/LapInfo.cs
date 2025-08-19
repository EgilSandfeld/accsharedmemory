using System.Collections.Generic;
using System.Linq;

namespace AssettoCorsaSharedMemory.Structs
{
    public class LapInfo
    {
        public int? LapTimeMs { get; internal set; }
        public List<int?> Splits { get; } = new List<int?>();
        public ushort CarIndex { get; internal set; }
        public ushort DriverIndex { get; internal set; }
        public bool IsInvalid { get; internal set; }
        public bool IsValidForBest { get; internal set; }
        public LapType Type { get; internal set; }

        public override string ToString()
        {
            if (Splits.Any(x => !x.HasValue))
            {
                if (LapTimeMs.HasValue)
                    return $"{LapTimeMs,5}";
                
                return string.Empty;
            }
            
            return $"{LapTimeMs, 5}|{string.Join("|", Splits)}";
        }
    }
}

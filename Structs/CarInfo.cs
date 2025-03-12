using System.Collections.Generic;

namespace AssettoCorsaSharedMemory.Structs
{
    public class CarInfo
    {
        public ushort CarIndex { get; }
        public byte CarModelType { get; internal set; }
        public ACCSharedMemory.CarModel ModelType { get; set; }
        public string TeamName { get; internal set; }
        public int RaceNumber { get; internal set; }
        public byte CupCategory { get; internal set; }
        public int CurrentDriverIndex { get; internal set; }
        public List<DriverInfo> Drivers { get; } = new List<DriverInfo>();
        public NationalityEnum Nationality { get; internal set; }

        public CarInfo(ushort carIndex)
        {
            CarIndex = carIndex;
        }

        internal void AddDriver(DriverInfo driverInfo)
        {
            Drivers.Add(driverInfo);
        }

        public string GetCurrentDriverName()
        {
            if (CurrentDriverIndex < Drivers.Count)
                return Drivers[CurrentDriverIndex].LastName;
            return "nobody(?)";
        }
    }
}

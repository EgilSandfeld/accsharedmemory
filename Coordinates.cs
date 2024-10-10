using System.Runtime.InteropServices;

namespace AssettoCorsaSharedMemory
{
    [StructLayout (LayoutKind.Sequential)]
    public struct Coordinates
    {
        public float X;
        public float Y;
        public float Z;

        public Coordinates(float x, int y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
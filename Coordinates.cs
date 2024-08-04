using System.Runtime.InteropServices;

namespace AssettoCorsaSharedMemory;

[StructLayout (LayoutKind.Sequential)]
public struct Coordinates
{
    public float X;
    public float Y;
    public float Z;
}
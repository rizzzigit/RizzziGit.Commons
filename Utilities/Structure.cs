using System.Runtime.InteropServices;

namespace RizzziGit.Commons.Utilities;

using Memory;

public static class StructureExtensions
{
    public static T ConvertToStructure<T>(this byte[] buffer)
        where T : struct
    {
        int bufferSize = Marshal.SizeOf<T>();
        nint pointer = nint.Zero;
        try
        {
            pointer = Marshal.AllocHGlobal(bufferSize);
            Marshal.Copy(buffer, 0, pointer, bufferSize);

            return Marshal.PtrToStructure<T>(pointer);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    public static byte[] ConvertToByteArray<T>(this T obj)
        where T : struct
    {
        int bufferSize = Marshal.SizeOf<T>();
        byte[] buffer = new byte[bufferSize];
        nint pointer = Marshal.AllocHGlobal(bufferSize);

        try
        {
            Marshal.StructureToPtr(obj, pointer, true);

            Marshal.Copy(pointer, buffer, 0, bufferSize);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }

        return buffer;
    }

    public static CompositeBuffer ConvertToBuffer<T>(this T value)
        where T : struct => value.ConvertToByteArray();

    public static T ConvertToStructure<T>(
        this CompositeBuffer bytes,
        long? start = null,
        long? end = null
    )
        where T : struct =>
        bytes.Slice(start ?? 0, end ?? bytes.Length).ToByteArray().ConvertToStructure<T>();
}

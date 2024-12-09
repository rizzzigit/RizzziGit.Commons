namespace RizzziGit.Commons.Memory;

public sealed partial class CompositeBuffer
{
    public byte[] ToByteArray()
    {
        if (Length > int.MaxValue)
        {
            throw new InvalidOperationException("Buffer size is too long.");
        }

        byte[] output = new byte[Length];
        int written = 0;

        foreach (byte[] block in Blocks)
        {
            int length = int.Min(output.Length - written, block.Length);
            Array.Copy(block, 0, output, written, length);

            written += length;
        }

        return output;
    }

    public override string ToString() => System.Text.Encoding.UTF8.GetString(ToByteArray());

    public string ToHexString() => Convert.ToHexString(ToByteArray());

    public string ToBase64String() => Convert.ToBase64String(ToByteArray());

    public string ToString(StringEncoding encoding)
    {
        return encoding switch
        {
            StringEncoding.UTF8 => ToString(),
            StringEncoding.Hex => ToHexString(),
            StringEncoding.Base64 => ToBase64String(),

            _ => throw new InvalidOperationException($"Unknown encoding: {encoding}")
        };
    }

    public ushort ToUInt16() => BitConverter.ToUInt16(Slice(0, 2).ToByteArray());

    public short ToInt16() => BitConverter.ToInt16(Slice(0, 2).ToByteArray());

    public uint ToUInt32() => BitConverter.ToUInt32(Slice(0, 4).ToByteArray());

    public int ToInt32() => BitConverter.ToInt32(Slice(0, 4).ToByteArray());

    public ulong ToUInt64() => BitConverter.ToUInt64(Slice(0, 8).ToByteArray());

    public long ToInt64() => BitConverter.ToInt64(Slice(0, 8).ToByteArray());

    public Int128 ToInt128() =>
        new(
            BitConverter.ToUInt64(Slice(8, 16).ToByteArray()),
            BitConverter.ToUInt64(Slice(0, 8).ToByteArray())
        );

    public UInt128 ToUInt128() =>
        new(
            BitConverter.ToUInt64(Slice(8, 16).ToByteArray()),
            BitConverter.ToUInt64(Slice(0, 8).ToByteArray())
        );

    public bool Equals(CompositeBuffer? target)
    {
        if (target == null)
        {
            return false;
        }

        return ToByteArray().SequenceEqual(target.ToByteArray());
    }
}

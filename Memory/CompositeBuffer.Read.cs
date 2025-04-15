namespace RizzziGit.Commons.Memory;

public sealed partial class CompositeBuffer
{
    public CompositeBuffer Read(long position, long length)
    {
        if (length > int.MaxValue)
        {
            CompositeBuffer buffer = Empty();

            for (long currentPosition = 0; currentPosition < length; )
            {
                byte[] output = new byte[length];
                int currentLength = (int)long.Min(length - currentPosition, int.MaxValue);

                currentPosition += Read(position + currentPosition, output, 0, currentLength);
                buffer.Append(output, 0, currentLength);
            }

            return buffer;
        }
        else
        {
            byte[] output = new byte[length];
            return new(output, 0, (int)Read(position, output, 0, (int)length));
        }
    }

    public byte Read(long position)
    {
        byte[] output = new byte[1];
        Read(position, output);

        return output[0];
    }

    public long Read(long position, byte[] output) => Read(position, output, 0, output.Length);

    public long Read(long position, byte[] output, int offset, int count)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(position, Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(position + count, Length);

        ResolveIndex(position, out int x, out int y);

        int read = 0;

        for (int index = x; (index < Blocks.Count) && (read < long.Min(count, Length)); index++)
        {
            int length = int.Min(Blocks[index].Length, count - read);

            Array.Copy(Blocks[index], index == x ? y : 0, output, read + offset, length);

            read += length;
        }

        return read;
    }
}

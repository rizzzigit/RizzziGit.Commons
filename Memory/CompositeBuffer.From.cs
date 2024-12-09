namespace RizzziGit.Commons.Memory;

public sealed partial class CompositeBuffer
{
    public static CompositeBuffer Allocate(long length)
    {
        int arraySizeLimit = int.MaxValue / 2;

        if (length > arraySizeLimit)
        {
            CompositeBuffer buffer = Empty();

            while (buffer.Length < length)
            {
                byte[] bytes = new byte[(int)long.Min(length - buffer.Length, arraySizeLimit)];
                buffer.Append(bytes);
            }

            return buffer;
        }
        else
        {
            return new(new byte[length]);
        }
    }

    public static CompositeBuffer Empty() => new();

    public static CompositeBuffer Random(int length) => Random(length, System.Random.Shared);

    public static CompositeBuffer Random(int length, Random random)
    {
        byte[] buffer = new byte[length];
        random.NextBytes(buffer);
        return new(buffer, 0, length);
    }

    public static CompositeBuffer Concat(List<CompositeBuffer> buffers)
    {
        List<byte[]> blocks = [];

        foreach (CompositeBuffer buffer in buffers)
        {
            foreach (byte[] bufferBlock in buffer.Blocks)
            {
                blocks.Add(bufferBlock);
            }
        }

        return new(blocks);
    }

    public static CompositeBuffer Concat(params CompositeBuffer[] buffers) =>
        Concat(buffers.ToList());

    public static CompositeBuffer From(byte[] input) => From(input, 0);

    public static CompositeBuffer From(byte[] input, int start) => From(input, start, input.Length);

    public static CompositeBuffer From(byte[] input, int start, int count) =>
        new(input, start, count);

    public static CompositeBuffer From(string input) => From(input, StringEncoding.UTF8);

    public static CompositeBuffer From(string input, StringEncoding encoding) =>
        encoding switch
        {
            StringEncoding.UTF8 => new(System.Text.Encoding.UTF8.GetBytes(input)),
            StringEncoding.Hex => new(Convert.FromHexString(input)),
            StringEncoding.Base64 => new(Convert.FromBase64String(input)),
            _ => throw new InvalidOperationException($"Unknown encoding: {encoding}"),
        };

    public static CompositeBuffer From(sbyte input) => From((byte)input);

    public static CompositeBuffer From(byte input) => new([input]);

    public static CompositeBuffer From(ushort input) => new(BitConverter.GetBytes(input));

    public static CompositeBuffer From(short input) => new(BitConverter.GetBytes(input));

    public static CompositeBuffer From(uint input) => new(BitConverter.GetBytes(input));

    public static CompositeBuffer From(int input) => new(BitConverter.GetBytes(input));

    public static CompositeBuffer From(ulong input) => new(BitConverter.GetBytes(input));

    public static CompositeBuffer From(long input) => new(BitConverter.GetBytes(input));

    public static CompositeBuffer From(Int128 input) => From((UInt128)input);

    public static CompositeBuffer From(UInt128 input) =>
        Concat((ulong)(input & ulong.MaxValue), (ulong)(input >> 64));
}

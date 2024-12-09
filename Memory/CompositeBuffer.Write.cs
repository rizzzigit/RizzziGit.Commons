namespace RizzziGit.Commons.Memory;

public sealed partial class CompositeBuffer
{
    public void Write(long position, byte input) => Write(position, [input]);

    public long Write(long position, byte[] input) => Write(position, input, 0, input.Length);

    public long Write(long position, byte[] input, int inputOffset, int inputLength)
    {
        if (CopyOnWrite)
        {
            Write(position, From(input, inputOffset, inputLength));
        }
        else
        {
            if (position > Length)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }
            else if ((position + inputLength) > Length)
            {
                throw new ArgumentOutOfRangeException(nameof(inputLength));
            }

            ResolveIndex(position, out int x, out int y);

            int written = 0;
            for (int index = x; (index < Blocks.Count) && (written < inputLength); index++)
            {
                int length = int.Min(Blocks[index].Length, inputLength - written);
                Array.Copy(input, index == x ? y : 0, Blocks[index], written + inputOffset, length);
                written += length;
            }
        }

        return inputLength;
    }

    public void Write(long position, CompositeBuffer input)
    {
        if (!CopyOnWrite)
        {
            Write(position, input.ToByteArray());
        }
        else
        {
            if (position > Length)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }
            else if ((position + input.Length) > Length)
            {
                throw new ArgumentOutOfRangeException(nameof(input));
            }

            Blocks =
            [
                .. SliceBlocks(0, position),
                .. input.Blocks,
                .. SliceBlocks(position + input.Length)
            ];
        }
    }
}

namespace RizzziGit.Commons.Memory;

using Collections;

public sealed partial class CompositeBuffer
{
    public long SpliceStart(byte[] output, int outputOffset, int outputLength) =>
        SpliceStart(outputLength - outputOffset).Read(0, output, outputOffset, outputLength);

    public long SpliceEnd(byte[] output, int outputOffset, int outputLength) =>
        SpliceEnd(outputLength - outputOffset).Read(0, output, outputOffset, outputLength);

    public CompositeBuffer SpliceStart(long length) => Splice(0, length);

    public CompositeBuffer SpliceEnd(long length) => Splice(Length - length, Length);

    public long Splice(long start, byte[] output, int outputOffset, int outputLength) =>
        Splice(start, start + outputLength - outputOffset)
            .Read(0, output, outputOffset, outputLength);

    public CompositeBuffer Splice(long start) => Slice(start, Length);

    public CompositeBuffer Splice(long start, long end)
    {
        if (start > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }
        else if ((end - start) > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(end));
        }

        List<byte[]> splicedBuffers = [];
        long remainingOffset = start;
        long remainingLength = end - start;

        for (int index = 0; (remainingLength > 0) && (index < Blocks.Count); index++)
        {
            byte[] block = Blocks[index];

            if (remainingOffset > 0)
            {
                if (remainingOffset > block.Length)
                {
                    remainingOffset -= block.Length;
                    continue;
                }

                (Blocks[index], byte[] right) = block.Split((int)remainingOffset);
                remainingOffset = 0;

                if (remainingLength > right.Length)
                {
                    splicedBuffers.Add(right);
                    remainingLength -= right.Length;
                }
                else
                {
                    (byte[] left2, byte[] right2) = right.Split((int)remainingLength);
                    splicedBuffers.Add(left2);

                    Blocks.Insert(index + 1, right2);
                    remainingLength = 0;
                }
            }
            else
            {
                if (block.Length < remainingLength)
                {
                    splicedBuffers.Add(block);
                    Blocks.RemoveAt(index);

                    remainingLength -= block.Length;
                    index--;
                }
                else
                {
                    (byte[] left, byte[] right) = block.Split((int)remainingLength);
                    if (right.Length == 0)
                    {
                        Blocks.RemoveAt(index);
                        index--;
                    }
                    else
                    {
                        Blocks[index] = right;
                    }

                    splicedBuffers.Add(left);
                    remainingLength = 0;
                }
            }
        }

        LengthCache = null;
        return new(splicedBuffers);
    }
}

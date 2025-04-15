namespace RizzziGit.Commons.Memory;

public sealed partial class CompositeBuffer
{
    private List<byte[]> SliceBlocks(long start) => SliceBlocks(start, Length);

    private List<byte[]> SliceBlocks(long start, long end)
    {
        if ((start == 0) && (end == Length))
        {
            return Blocks;
        }
        else if ((end - start) == 0)
        {
            return [];
        }

        if (start > Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(start),
                "Start is greater than the length of the buffer."
            );
        }

        if (end > Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(end),
                "End is greater than the length of the buffer."
            );
        }

        List<byte[]> newBlocks = [];
        long remainingOffset = start;
        long remainingLength = end - start;

        for (int index = 0; remainingLength > 0 && (index < Blocks.Count); index++)
        {
            byte[] block = Blocks[index];

            if (remainingOffset >= block.Length)
            {
                remainingOffset -= block.Length;
                continue;
            }

            if (remainingOffset > 0)
            {
                block = block[(int)remainingOffset..];
                remainingOffset = 0;
            }

            block = block[0..(int)long.Min(remainingLength, block.Length)];
            remainingLength -= block.Length;

            newBlocks.Add(block);
        }

        return newBlocks;
    }

    public CompositeBuffer Slice(long start) => Slice(start, Length);

    public CompositeBuffer Slice(long start, long end) => new(SliceBlocks(start, end));
}

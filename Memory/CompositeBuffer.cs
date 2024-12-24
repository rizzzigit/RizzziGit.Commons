using System.Collections;

namespace RizzziGit.Commons.Memory;

public enum StringEncoding
{
    UTF8,
    Hex,
    Base64
}

public enum PaddingType
{
    Left,
    Right
}

public sealed partial class CompositeBuffer : IEnumerable<byte>, IEquatable<CompositeBuffer>
{
    private CompositeBuffer(List<byte[]> blocks)
        : this()
    {
        Blocks.AddRange(blocks);
    }

    public CompositeBuffer(byte[] source, int start, int count)
        : this(source[start..(start + count)]) { }

    public CompositeBuffer(byte[] source)
    {
        Blocks =
        [
            [.. source]
        ];
    }

    public CompositeBuffer()
    {
        Blocks = [];
    }

    private List<byte[]> Blocks;

    public long BlockCount => Blocks.Count;
    public bool CopyOnWrite = true;

    private long? LengthCache;

    public long Length => LengthCache ??= Blocks.Sum((block) => block.LongLength);

    public long RealLength
    {
        get
        {
            long result = 0;

            List<byte[]> knownBlocks = [];
            foreach (byte[] block in Blocks)
            {
                bool isKnown = false;
                bool addToKnown = false;

                foreach (byte[] knownBlock in knownBlocks)
                {
                    if (knownBlock == block)
                    {
                        isKnown = true;
                        break;
                    }

                    if (knownBlock.SequenceEqual(block))
                    {
                        isKnown = true;
                        addToKnown = true;
                        break;
                    }
                }

                if (!isKnown)
                {
                    result += block.Length;

                    addToKnown = true;
                }

                if (addToKnown)
                {
                    knownBlocks.Add(block);
                }
            }

            return result;
        }
    }

    private void ResolveIndex(long inputIndex, out int blockIndex, out int blockOffset)
    {
        if (inputIndex > Length)
        {
            throw new IndexOutOfRangeException(
                $"Requested {nameof(inputIndex)} must be less than the length ({Length})."
            );
        }
        else if (inputIndex < 0)
        {
            ResolveIndex(Length + inputIndex, out int blockIndex2, out int blockOffset2);

            blockIndex = blockIndex2;
            blockOffset = blockOffset2;
            return;
        }

        long tempOffset = inputIndex;
        for (blockIndex = 0; blockIndex < Blocks.Count; blockIndex++)
        {
            byte[] block = Blocks[blockIndex];

            if (tempOffset < block.Length)
            {
                break;
            }

            tempOffset -= block.Length;
        }

        blockOffset = (int)tempOffset;
    }

    public byte this[long index]
    {
        get => Read(index, 1).ToByteArray()[0];
        set => Write(index, value);
    }

    public CompositeBuffer this[Range range]
    {
        get
        {
            int size = (int)long.Min(Length, int.MaxValue);
            int start = range.Start.GetOffset(size);
            int end = range.End.GetOffset(size);


            return Slice(start, end);
        }
        set
        {
            int size = (int)long.Min(Length, int.MaxValue);
            int start = range.Start.GetOffset(size);

            if (value.Length != (range.End.GetOffset(size) - start))
            {
                throw new ArgumentException(
                    "Specified length does not match with the length of the specified value.",
                    nameof(range)
                );
            }

            Write(start, value);
        }
    }

    public CompositeBuffer Repeat(long count)
    {
        List<CompositeBuffer> buffers = [];

        for (long current = 0; current < count; current++)
        {
            buffers.Add(this);
        }

        return Concat(buffers);
    }

    public CompositeBuffer Clone() => new([.. Blocks]);

    public override bool Equals(object? target) =>
        ReferenceEquals(this, target) || Equals(target as CompositeBuffer);

    public override int GetHashCode()
    {
        HashCode hashCodeBuilder = new();
        hashCodeBuilder.AddBytes(ToByteArray());

        int hashCode = hashCodeBuilder.ToHashCode();
        return hashCode;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public IEnumerator<byte> GetEnumerator()
    {
        foreach (byte entry in Blocks.SelectMany((block) => block))
        {
            yield return entry;
        }
    }
}

public static class CompositeBufferExtensions
{
    private static CompositeBuffer AppendInternal(CompositeBuffer seed, CompositeBuffer entry)
    {
        seed.Append(entry);
        return seed;
    }

    public static ValueTask<CompositeBuffer> ConcatAsync(
        this IAsyncEnumerable<CompositeBuffer> bytes,
        CancellationToken cancellationToken = default
    ) => bytes.AggregateAsync(CompositeBuffer.Allocate(0), AppendInternal, cancellationToken);

    public static CompositeBuffer Concat(this IEnumerable<CompositeBuffer> bytes) =>
        bytes.Aggregate(CompositeBuffer.Allocate(0), AppendInternal);
}

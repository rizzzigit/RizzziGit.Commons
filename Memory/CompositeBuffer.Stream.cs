namespace RizzziGit.Commons.Memory;

public sealed partial class CompositeBuffer
{
    private class Stream(CompositeBuffer buffer, bool readable, bool writable) : MemoryStream
    {
        private readonly CompositeBuffer Buffer = buffer;
        private readonly bool Readable = readable;
        private readonly bool Writable = writable;

        private long InternalPosition = 0;

        public override bool CanRead => Readable;
        public override bool CanWrite => Writable;
        public override bool CanSeek => true;
        public override long Length => Buffer.Length;
        public override long Position
        {
            get => InternalPosition;
            set
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(value, Length);

                InternalPosition = value;
            }
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            CompositeBuffer read = Buffer.Slice(
                Position,
                long.Min(Buffer.Length, Position + count)
            );
            int length = (int)read.Length;
            read.Read(0, buffer, offset, length);

            Position += length;
            return length;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Current:
                    return Seek(Position + offset, SeekOrigin.Begin);
                case SeekOrigin.End:
                    return Seek(Length - offset, SeekOrigin.Begin);
                case SeekOrigin.Begin:
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, Length);
                    
                    return Position = offset;

                default:
                    throw new ArgumentNullException(nameof(origin));
            }
        }

        public override void SetLength(long value)
        {
            if (value < Length)
            {
                Buffer.SpliceEnd(Length - value);
            }
            else if (value > Length)
            {
                Buffer.Append(Allocate(value - Length));
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (Position == Buffer.Length)
            {
                Buffer.Append(buffer, offset, count);
            }
            else if ((Position + count) > Buffer.Length)
            {
                int splitIndex = (int)(Buffer.Length - Position);

                Buffer.Write(Position, buffer, offset, splitIndex);
                Buffer.Append(buffer, offset + splitIndex, count - splitIndex);
            }
            else
            {
                Buffer.Write(Position, buffer, offset, count);
            }

            Position += count;
        }
    }

    public MemoryStream CreateStream() => CreateStream(FileAccess.ReadWrite);

    public MemoryStream CreateStream(FileAccess access) =>
        new Stream(this, access.HasFlag(FileAccess.Read), access.HasFlag(FileAccess.Write));
}

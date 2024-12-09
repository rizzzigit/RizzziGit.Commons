namespace RizzziGit.Commons.Memory;

public sealed partial class CompositeBuffer
{
    public CompositeBuffer PadStart(long length) => Pad(length, PaddingType.Left);

    public CompositeBuffer PadEnd(long length) => Pad(length, PaddingType.Right);

    public CompositeBuffer Pad(long length, PaddingType type)
    {
        if (length <= Length)
        {
            return this;
        }

        return type switch
        {
            PaddingType.Left => Concat(Allocate(length - Length), this),
            PaddingType.Right => Concat(this, Allocate(length - Length)),

            _ => throw new ArgumentException($"Invalid padding: {type}")
        };
    }
}

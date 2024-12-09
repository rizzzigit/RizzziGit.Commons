namespace RizzziGit.Commons.Memory;

public sealed partial class CompositeBuffer
{
    public long Prepend(byte[] input) => Prepend(input, 0, input.Length);

    public long Prepend(byte[] input, int inputOffset, int inputLength)
    {
        Blocks.Insert(0, input[inputOffset..(inputOffset + inputLength)]);
        LengthCache = null;

        return inputLength;
    }

    public long Prepend(params CompositeBuffer[] inputs)
    {
        long length = 0;

        foreach (CompositeBuffer input in inputs)
        {
            Blocks.InsertRange(0, input.Blocks);
            length += input.Length;
        }

        LengthCache = null;

        return length;
    }
}

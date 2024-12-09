namespace RizzziGit.Commons.Memory;

public sealed partial class CompositeBuffer
{
    public long Append(byte[] input) => Append(input, 0, input.Length);

    public long Append(byte[] input, int inputOffset, int inputLength)
    {
        Blocks.Add(input[inputOffset..(inputOffset + inputLength)]);
        LengthCache = null;

        return inputLength;
    }

    public long Append(params CompositeBuffer[] inputs)
    {
        long length = 0;

        foreach (CompositeBuffer input in inputs)
        {
            Blocks.AddRange(input.Blocks);
            length += input.Length;
        }

        LengthCache = null;

        return length;
    }
}

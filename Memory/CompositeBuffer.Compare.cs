namespace RizzziGit.Commons.Memory;

public sealed partial class CompositeBuffer
{
    private static bool Compare(CompositeBuffer? left, object? right)
    {
        if (ReferenceEquals(left, right) || ((left is null) && (right == null)))
        {
            return true;
        }
        else if ((left is not null) && (right != null))
        {
            if (right is CompositeBuffer buffer)
            {
                return left.Equals(buffer);
            }

            switch (right)
            {
                case byte @byte:
                    return left[0] == @byte;

                case short @short:
                    return left.ToInt16() == @short;
                case ushort @ushort:
                    return left.ToUInt16() == @ushort;

                case int @int:
                    return left.ToInt32() == @int;
                case uint @uint:
                    return left.ToUInt32() == @uint;

                case long @long:
                    return left.ToInt64() == @long;
                case ulong @ulong:
                    return left.ToUInt64() == @ulong;

                case string @string:
                    return left.ToString() == @string;
            }
        }

        return false;
    }

    public static bool operator ==(CompositeBuffer? left, object? right) => Compare(left, right);

    public static bool operator !=(CompositeBuffer? left, object? right) => !Compare(left, right);
}

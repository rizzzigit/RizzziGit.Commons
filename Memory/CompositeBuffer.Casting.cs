namespace RizzziGit.Commons.Memory;

public sealed partial class CompositeBuffer {

    public static explicit operator byte[](CompositeBuffer input) => input.ToByteArray();

    public static explicit operator sbyte(CompositeBuffer input) => (sbyte)input[0];

    public static explicit operator byte(CompositeBuffer input) => input[0];

    public static explicit operator short(CompositeBuffer input) => input.ToInt16();

    public static explicit operator ushort(CompositeBuffer input) => input.ToUInt16();

    public static explicit operator int(CompositeBuffer input) => input.ToInt32();

    public static explicit operator uint(CompositeBuffer input) => input.ToUInt32();

    public static explicit operator long(CompositeBuffer input) => input.ToInt64();

    public static explicit operator ulong(CompositeBuffer input) => input.ToUInt64();

    public static explicit operator Int128(CompositeBuffer input) => input.ToInt128();

    public static explicit operator UInt128(CompositeBuffer input) => input.ToUInt128();

    public static explicit operator string(CompositeBuffer input) => input.ToString();

    public static implicit operator CompositeBuffer(byte[] input) => From(input);

    public static implicit operator CompositeBuffer(sbyte input) => From(input);

    public static implicit operator CompositeBuffer(byte input) => From(input);

    public static implicit operator CompositeBuffer(short input) => From(input);

    public static implicit operator CompositeBuffer(ushort input) => From(input);

    public static implicit operator CompositeBuffer(int input) => From(input);

    public static implicit operator CompositeBuffer(uint input) => From(input);

    public static implicit operator CompositeBuffer(long input) => From(input);

    public static implicit operator CompositeBuffer(ulong input) => From(input);

    public static implicit operator CompositeBuffer(Int128 input) => From(input);

    public static implicit operator CompositeBuffer(UInt128 input) => From(input);

    public static implicit operator CompositeBuffer(string input) => From(input);
}

using System.Collections;

namespace RizzziGit.Framework.Memory;

using Extensions.Array;

public enum StringEncoding
{
  UTF8, Hex, Base64
}

public enum PaddingType
{
  Left, Right
}

public sealed partial class CompositeBuffer : IEnumerable<byte>, IEquatable<CompositeBuffer>
{
  private static bool Compare(CompositeBuffer? left, object? right)
  {
    if (
      ReferenceEquals(left, right) ||
      ((left is null) && (right == null))
    )
    {
      return true;
    }
    else if ((left is not null) && (right != null))
    {
      if (right is CompositeBuffer buffer) { return left.Equals(buffer); }

      switch (right)
      {
        case byte @byte: return left[0] == @byte;

        case short @short: return left.ToInt16() == @short;
        case ushort @ushort: return left.ToUInt16() == @ushort;

        case int @int: return left.ToInt32() == @int;
        case uint @uint: return left.ToUInt32() == @uint;

        case long @long: return left.ToInt64() == @long;
        case ulong @ulong: return left.ToUInt64() == @ulong;

        case string @string: return left.ToString() == @string;
      }
    }

    return false;
  }

  public static bool operator ==(CompositeBuffer? left, object? right) => Compare(left, right);
  public static bool operator !=(CompositeBuffer? left, object? right) => !Compare(left, right);

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

  public static CompositeBuffer Allocate(long length)
  {
    if (length > (int.MaxValue / 2))
    {
      CompositeBuffer buffer = Empty();

      lock (buffer)
      {
        while (buffer.Length < length)
        {
          byte[] bytes = new byte[(int)long.Min(length - buffer.Length, int.MaxValue / 2)];
          buffer.Append(bytes);
        }

        return buffer;
      }
    }
    else
    {
      return new(new byte[length]);
    }
  }

  public static CompositeBuffer Empty() => new([], 0);
  public static CompositeBuffer Random(int length) => Random(length, System.Random.Shared);
  public static CompositeBuffer Random(int length, Random random)
  {
    byte[] buffer = new byte[length];
    random.NextBytes(buffer);
    return new(buffer, 0, length);
  }

  public static CompositeBuffer Concat(List<CompositeBuffer> buffers)
  {
    List<byte[]> blocks = [];
    long length = 0;

    foreach (CompositeBuffer buffer in buffers)
    {
      lock (buffer.Blocks)
      {
        foreach (byte[] bufferBlock in buffer.Blocks)
        {
          blocks.Add(bufferBlock);
          length += bufferBlock.Length;
        }
      }
    }

    return new(blocks, length);
  }
  public static CompositeBuffer Concat(params CompositeBuffer[] buffers) => Concat(buffers.ToList());

  public static CompositeBuffer From(byte[] input) => From(input, 0);
  public static CompositeBuffer From(byte[] input, int start) => From(input, start, input.Length);
  public static CompositeBuffer From(byte[] input, int start, int end) => new(input, start, end);
  public static CompositeBuffer From(string input) => From(input, StringEncoding.UTF8);
  public static CompositeBuffer From(string input, StringEncoding encoding) => encoding switch
  {
    StringEncoding.UTF8 => new(System.Text.Encoding.UTF8.GetBytes(input)),
    StringEncoding.Hex => new(Convert.FromHexString(input)),
    StringEncoding.Base64 => new(Convert.FromBase64String(input)),
    _ => throw new InvalidOperationException($"Unknown encoding: {encoding}"),
  };

  public static CompositeBuffer From(sbyte input) => From((byte)input);
  public static CompositeBuffer From(byte input) => new([input]);
  public static CompositeBuffer From(ushort input) => new(BitConverter.GetBytes(input));
  public static CompositeBuffer From(short input) => new(BitConverter.GetBytes(input));
  public static CompositeBuffer From(uint input) => new(BitConverter.GetBytes(input));
  public static CompositeBuffer From(int input) => new(BitConverter.GetBytes(input));
  public static CompositeBuffer From(ulong input) => new(BitConverter.GetBytes(input));
  public static CompositeBuffer From(long input) => new(BitConverter.GetBytes(input));
  public static CompositeBuffer From(Int128 input) => From((UInt128)input);
  public static CompositeBuffer From(UInt128 input) => Concat((ulong)(input & ulong.MaxValue), (ulong)(input >> 64));

  public CompositeBuffer() : this([], 0) { }
  public CompositeBuffer(byte[] source) : this(source, 0, source.Length) { }
  public CompositeBuffer(byte[] source, int start, int end) : this(end - start)
  {
    Blocks.Add(source);
  }

  private CompositeBuffer(List<byte[]> blocks, long length) : this(length)
  {
    Blocks.AddRange(blocks);
  }

  private CompositeBuffer(long length)
  {
    Blocks = [];
    Length = length;
  }

  private List<byte[]> Blocks;

  public long BlockCount => Blocks.Count;
  public bool CopyOnWrite = true;

  public long Length { get; private set; }

  public long RealLength
  {
    get
    {
      long result = 0;
      lock (this)
      {
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
      }
      return result;
    }
  }

  private void ResolveIndex(long inputIndex, out int blockIndex, out int blockOffset)
  {
    lock (this)
    {
      if (inputIndex > Length)
      {
        throw new IndexOutOfRangeException($"Requested {nameof(inputIndex)} must be less than the length ({Length}).");
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
  }

  public CompositeBuffer Read(long position, long length)
  {
    lock (this)
    {
      if (length > int.MaxValue)
      {
        CompositeBuffer buffer = Empty();

        for (long currentPosition = 0; currentPosition < length;)
        {
          byte[] output = new byte[length];
          int currentLength = (int)long.Min(length - currentPosition, int.MaxValue);

          currentPosition += Read(position + currentPosition, output, 0, currentLength);
          buffer.Append(output, 0, currentLength);
        }

        return buffer;
      }
      else
      {
        byte[] output = new byte[length];
        return new(output, 0, (int)Read(position, output, 0, (int)length));
      }
    }
  }

  public byte Read(long position)
  {
    lock (this)
    {
      byte[] output = new byte[1];
      Read(position, output);

      return output[0];
    }
  }
  public long Read(long position, byte[] output) => Read(position, output, 0, output.Length);
  public long Read(long position, byte[] output, int outputOffset, int outputLength)
  {
    lock (this)
    {
      if (position > Length)
      {
        throw new ArgumentOutOfRangeException(nameof(position));
      }
      else if ((position + outputLength) > Length)
      {
        throw new ArgumentOutOfRangeException(nameof(outputLength));
      }
      ResolveIndex(position, out int x, out int y);

      int read = 0;
      for (int index = x; (index < Blocks.Count) && (read < long.Min(outputLength, Length)); index++)
      {
        int length = int.Min(Blocks[index].Length, outputLength - read);

        Array.Copy(Blocks[index], index == x ? y : 0, output, read + outputOffset, length);

        read += length;
      }

      return read;
    }
  }

  public void Write(long position, byte input) => Write(position, new byte[] { input });
  public long Write(long position, byte[] input) => Write(position, input, 0, input.Length);
  public long Write(long position, byte[] input, int inputOffset, int inputLength)
  {
    lock (this)
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
    }

    return inputLength;
  }

  public void Write(long position, CompositeBuffer input)
  {
    lock (this)
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

        lock (this)
        {
          Blocks = [.. SliceBlocks(0, position), .. input.Blocks, .. SliceBlocks(position + input.Length)];
        }
      }
    }
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
      int size = (int)long.Max(Length, int.MaxValue);
      int start = range.Start.GetOffset(size);
      int end = range.End.GetOffset(size);

      return Slice(start, end);
    }

    set
    {
      int size = (int)long.Max(Length, int.MaxValue);
      int start = range.Start.GetOffset(size);

      if (value.Length != (range.End.GetOffset(size) - start))
      {
        throw new ArgumentException("Specified length does not match with the length of the specified value.", nameof(range));
      }

      Write(start, value);
    }
  }

  private List<byte[]> SliceBlocks(long start) => SliceBlocks(start, Length);
  private List<byte[]> SliceBlocks(long start, long end)
  {
    lock (this)
    {
      if ((start == 0) && (end == Length))
      {
        return Blocks;
      }
      else if ((end - start) == 0)
      {
        return [];
      }
      else if (start > Length)
      {
        throw new ArgumentOutOfRangeException(nameof(start));
      }
      else if (end < start)
      {
        throw new ArgumentOutOfRangeException(nameof(end));
      }

      List<byte[]> newBlocks = [];
      long remainingOffset = start;
      long remainingLength = end - start;

      for (int index = 0; remainingLength > 0 && (index < Blocks.Count); index++)
      {
        byte[] block = Blocks[index];

        if (remainingOffset > block.Length)
        {
          remainingOffset -= block.Length;
          continue;
        }

        block = (block.Length - remainingOffset) < remainingLength
          ? block[..(int)remainingOffset]
          : block[(int)remainingOffset..(int)(remainingOffset + remainingLength)];
        remainingOffset = 0;

        if (block.Length != 0)
        {
          newBlocks.Add(block);
        }

        remainingLength -= block.Length;
      }

      return newBlocks;
    }
  }

  public CompositeBuffer Slice(long start) => Slice(start, Length);
  public CompositeBuffer Slice(long start, long end) => new(SliceBlocks(start, end), end - start);

  public CompositeBuffer Repeat(long count)
  {
    lock (this)
    {
      List<CompositeBuffer> buffers = [];

      for (long current = 0; current < count; current++)
      {
        buffers.Add(this);
      }

      return Concat(buffers);
    }
  }

  public CompositeBuffer Clone() => new(ToByteArray());

  public byte[] ToByteArray()
  {
    lock (this)
    {
      if (Length > int.MaxValue)
      {
        throw new InvalidOperationException("Buffer size is too long.");
      }

      byte[] output = new byte[Length];
      int written = 0;

      foreach (byte[] block in Blocks)
      {
        int length = int.Min(output.Length - written, block.Length);
        Array.Copy(block, 0, output, written, length);

        written += length;
      }

      return output;
    }
  }

  public override string ToString() => System.Text.Encoding.UTF8.GetString(ToByteArray());
  public string ToHexString() => Convert.ToHexString(ToByteArray());
  public string ToBase64String() => Convert.ToBase64String(ToByteArray());
  public string ToString(StringEncoding encoding)
  {
    return encoding switch
    {
      StringEncoding.UTF8 => ToString(),
      StringEncoding.Hex => ToHexString(),
      StringEncoding.Base64 => ToBase64String(),

      _ => throw new InvalidOperationException($"Unknown encoding: {encoding}")
    };
  }

  public ushort ToUInt16() => BitConverter.ToUInt16(Slice(0, 2).ToByteArray());
  public short ToInt16() => BitConverter.ToInt16(Slice(0, 2).ToByteArray());
  public uint ToUInt32() => BitConverter.ToUInt32(Slice(0, 4).ToByteArray());
  public int ToInt32() => BitConverter.ToInt32(Slice(0, 4).ToByteArray());
  public ulong ToUInt64() => BitConverter.ToUInt64(Slice(0, 8).ToByteArray());
  public long ToInt64() => BitConverter.ToInt64(Slice(0, 8).ToByteArray());
  public Int128 ToInt128() => new(BitConverter.ToUInt64(Slice(8, 16).ToByteArray()), BitConverter.ToUInt64(Slice(0, 8).ToByteArray()));
  public UInt128 ToUInt128() => new(BitConverter.ToUInt64(Slice(8, 16).ToByteArray()), BitConverter.ToUInt64(Slice(0, 8).ToByteArray()));

  public bool Equals(CompositeBuffer? target)
  {
    if (target == null)
    {
      return false;
    }

    return ToByteArray().SequenceEqual(target.ToByteArray());
  }

  public CompositeBuffer PadStart(long length) => Pad(length, PaddingType.Left);
  public CompositeBuffer PadEnd(long length) => Pad(length, PaddingType.Right);
  public CompositeBuffer Pad(long length, PaddingType type)
  {
    lock (this)
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

  public long TruncateStart(byte[] output, int outputOffset, int outputLength) => TruncateStart(outputLength - outputOffset).Read(0, output, outputOffset, outputLength);
  public long Truncate(byte[] output, int outputOffset, int outputLength) => Truncate(outputLength - outputOffset).Read(0, output, outputOffset, outputLength);

  public CompositeBuffer TruncateStart(long length) => Splice(0, length);
  public CompositeBuffer Truncate(long length) => Splice(Length - length, Length);

  public long Splice(long start, byte[] output, int outputOffset, int outputLength) => Splice(start, start + outputLength - outputOffset).Read(0, output, outputOffset, outputLength);

  public CompositeBuffer Splice(long start) => Slice(start, Length);
  public CompositeBuffer Splice(long start, long end)
  {
    lock (this)
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

      Length -= end - start;
      return new(splicedBuffers, end - start);
    }
  }

  public long Append(byte[] input) => Append(input, 0, input.Length);
  public long Append(byte[] input, int inputOffset, int inputLength)
  {
    lock (this)
    {
      Blocks.Add(input[inputOffset..(inputOffset + inputLength)]);
      Length += inputLength;
    }

    return inputLength;
  }

  public long Append(params CompositeBuffer[] inputs)
  {
    lock (this)
    {
      long length = 0;

      foreach (CompositeBuffer input in inputs)
      {
        lock (input)
        {
          Blocks.AddRange(input.Blocks);
          length += input.Length;
        }
      }

      Length += length;
      return length;
    }
  }

  public long Prepend(byte[] input) => Prepend(input, 0, input.Length);
  public long Prepend(byte[] input, int inputOffset, int inputLength)
  {
    lock (this)
    {
      Blocks.Insert(0, input[inputOffset..(inputOffset + inputLength)]);
      Length += inputLength;
    }

    return inputLength;
  }

  public long Prepend(params CompositeBuffer[] inputs)
  {
    lock (this)
    {
      long length = 0;

      foreach (CompositeBuffer input in inputs)
      {
        lock (input)
        {
          Blocks.InsertRange(0, input.Blocks);
          length += input.Length;
        }
      }

      Length += length;
      return length;
    }
  }

  public override bool Equals(object? target) => ReferenceEquals(this, target) || Equals(target as CompositeBuffer);

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
    lock (this)
    {
      foreach (byte[] block in Blocks)
      {
        foreach (byte blockEntry in block)
        {
          yield return blockEntry;
        }
      }
    }
  }
}

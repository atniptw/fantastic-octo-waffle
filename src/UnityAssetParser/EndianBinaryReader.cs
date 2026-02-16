using System;
using System.Buffers.Binary;
using System.Text;

namespace UnityAssetParser;

internal sealed class EndianBinaryReader
{
    private readonly byte[] _buffer;
    private int _position;

    public EndianBinaryReader(byte[] buffer, bool isBigEndian = false)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        IsBigEndian = isBigEndian;
    }

    public int Position
    {
        get => _position;
        set
        {
            if (value < 0 || value > _buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            _position = value;
        }
    }

    public int Length => _buffer.Length;

    public bool IsBigEndian { get; set; }

    public byte ReadByte()
    {
        EnsureAvailable(sizeof(byte));
        return _buffer[_position++];
    }

    public byte[] ReadBytes(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        EnsureAvailable(count);
        var slice = new byte[count];
        Buffer.BlockCopy(_buffer, _position, slice, 0, count);
        _position += count;
        return slice;
    }

    public uint ReadUInt32()
    {
        EnsureAvailable(sizeof(uint));
        var span = _buffer.AsSpan(_position, sizeof(uint));
        _position += sizeof(uint);
        return IsBigEndian
            ? BinaryPrimitives.ReadUInt32BigEndian(span)
            : BinaryPrimitives.ReadUInt32LittleEndian(span);
    }

    public int ReadInt32()
    {
        EnsureAvailable(sizeof(int));
        var span = _buffer.AsSpan(_position, sizeof(int));
        _position += sizeof(int);
        return IsBigEndian
            ? BinaryPrimitives.ReadInt32BigEndian(span)
            : BinaryPrimitives.ReadInt32LittleEndian(span);
    }

    public ushort ReadUInt16()
    {
        EnsureAvailable(sizeof(ushort));
        var span = _buffer.AsSpan(_position, sizeof(ushort));
        _position += sizeof(ushort);
        return IsBigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(span)
            : BinaryPrimitives.ReadUInt16LittleEndian(span);
    }

    public short ReadInt16()
    {
        EnsureAvailable(sizeof(short));
        var span = _buffer.AsSpan(_position, sizeof(short));
        _position += sizeof(short);
        return IsBigEndian
            ? BinaryPrimitives.ReadInt16BigEndian(span)
            : BinaryPrimitives.ReadInt16LittleEndian(span);
    }

    public long ReadInt64()
    {
        EnsureAvailable(sizeof(long));
        var span = _buffer.AsSpan(_position, sizeof(long));
        _position += sizeof(long);
        return IsBigEndian
            ? BinaryPrimitives.ReadInt64BigEndian(span)
            : BinaryPrimitives.ReadInt64LittleEndian(span);
    }

    public string ReadStringToNull(int maxBytes = 0)
    {
        var start = _position;
        while (_position < _buffer.Length)
        {
            if (maxBytes > 0 && _position - start >= maxBytes)
            {
                break;
            }
            if (_buffer[_position] == 0)
            {
                var length = _position - start;
                var text = Encoding.UTF8.GetString(_buffer, start, length);
                _position++;
                return text;
            }
            _position++;
        }

        var fallbackLength = _position - start;
        return fallbackLength > 0 ? Encoding.UTF8.GetString(_buffer, start, fallbackLength) : string.Empty;
    }

    public void Align(int alignment)
    {
        if (alignment <= 1)
        {
            return;
        }
        var offset = _position % alignment;
        if (offset == 0)
        {
            return;
        }
        Position += alignment - offset;
    }

    private void EnsureAvailable(int count)
    {
        if (_position + count > _buffer.Length)
        {
            throw new InvalidOperationException("Attempted to read beyond buffer length.");
        }
    }
}

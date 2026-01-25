using System.Buffers.Binary;
using System.Text;
using UnityAssetParser.Exceptions;

namespace UnityAssetParser.Helpers;

/// <summary>
/// Binary reader that handles endianness conversion.
/// Wraps a BinaryReader and conditionally swaps bytes based on the endianness flag.
/// </summary>
public sealed class EndianBinaryReader : IDisposable
{
    private readonly BinaryReader _reader;
    private readonly bool _isBigEndian;
    private bool _disposed;

    /// <summary>
    /// Gets the underlying base stream.
    /// </summary>
    public Stream BaseStream => _reader.BaseStream;

    /// <summary>
    /// Gets whether this reader uses big-endian byte order.
    /// </summary>
    public bool IsBigEndian => _isBigEndian;

    /// <summary>
    /// Initializes a new instance of the <see cref="EndianBinaryReader"/> class.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="isBigEndian">True if the data is big-endian, false for little-endian.</param>
    public EndianBinaryReader(Stream stream, bool isBigEndian)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        _isBigEndian = isBigEndian;
    }

    /// <summary>
    /// Reads a signed byte from the stream.
    /// </summary>
    public sbyte ReadSByte() => _reader.ReadSByte();

    /// <summary>
    /// Reads an unsigned byte from the stream.
    /// </summary>
    public byte ReadByte() => _reader.ReadByte();

    /// <summary>
    /// Reads a boolean from the stream (1 byte).
    /// </summary>
    public bool ReadBoolean() => _reader.ReadBoolean();

    /// <summary>
    /// Reads a 16-bit signed integer with endianness conversion.
    /// </summary>
    public short ReadInt16()
    {
        var value = _reader.ReadInt16();
        return _isBigEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
    }

    /// <summary>
    /// Reads a 16-bit unsigned integer with endianness conversion.
    /// </summary>
    public ushort ReadUInt16()
    {
        var value = _reader.ReadUInt16();
        return _isBigEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
    }

    /// <summary>
    /// Reads a 32-bit signed integer with endianness conversion.
    /// </summary>
    public int ReadInt32()
    {
        var value = _reader.ReadInt32();
        return _isBigEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
    }

    /// <summary>
    /// Reads a 32-bit unsigned integer with endianness conversion.
    /// </summary>
    public uint ReadUInt32()
    {
        var value = _reader.ReadUInt32();
        return _isBigEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
    }

    /// <summary>
    /// Reads a 64-bit signed integer with endianness conversion.
    /// </summary>
    public long ReadInt64()
    {
        var value = _reader.ReadInt64();
        return _isBigEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
    }

    /// <summary>
    /// Reads a 64-bit unsigned integer with endianness conversion.
    /// </summary>
    public ulong ReadUInt64()
    {
        var value = _reader.ReadUInt64();
        return _isBigEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
    }

    /// <summary>
    /// Reads a single-precision floating point with endianness conversion.
    /// </summary>
    public float ReadSingle()
    {
        if (_isBigEndian)
        {
            var bytes = _reader.ReadBytes(4);
            Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }
        return _reader.ReadSingle();
    }

    /// <summary>
    /// Reads a double-precision floating point with endianness conversion.
    /// </summary>
    public double ReadDouble()
    {
        if (_isBigEndian)
        {
            var bytes = _reader.ReadBytes(8);
            Array.Reverse(bytes);
            return BitConverter.ToDouble(bytes, 0);
        }
        return _reader.ReadDouble();
    }

    /// <summary>
    /// Reads a byte array of specified length.
    /// </summary>
    /// <param name="count">Number of bytes to read.</param>
    public byte[] ReadBytes(int count) => _reader.ReadBytes(count);

    /// <summary>
    /// Reads a null-terminated UTF-8 string from the stream.
    /// </summary>
    /// <param name="maxLength">Maximum allowed string length in bytes.</param>
    /// <returns>The decoded UTF-8 string.</returns>
    public string ReadUtf8NullTerminated(int maxLength = 0x100000)
    {
        return _reader.ReadUtf8NullTerminated(maxLength);
    }

    /// <summary>
    /// Aligns the stream position to the specified boundary.
    /// </summary>
    /// <param name="alignment">Alignment boundary (default: 4 bytes).</param>
    /// <param name="validatePadding">If true, throws if padding bytes are non-zero.</param>
    public void Align(int alignment = 4, bool validatePadding = false)
    {
        _reader.Align(alignment, validatePadding);
    }

    /// <summary>
    /// Gets the current position in the stream.
    /// </summary>
    public long Position
    {
        get => _reader.BaseStream.Position;
        set => _reader.BaseStream.Position = value;
    }

    /// <summary>
    /// Gets the length of the stream.
    /// </summary>
    public long Length => _reader.BaseStream.Length;

    /// <summary>
    /// Disposes the reader.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _reader.Dispose();
            _disposed = true;
        }
    }
}

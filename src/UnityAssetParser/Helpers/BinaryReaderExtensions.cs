using System.Buffers;
using System.Text;
using UnityAssetParser.Exceptions;

namespace UnityAssetParser.Helpers;

/// <summary>
/// Extension methods for BinaryReader to support Unity asset parsing.
/// Provides alignment, bounds checking, and UTF-8 string reading utilities.
/// </summary>
public static class BinaryReaderExtensions
{
    /// <summary>
    /// Maximum allowed string length (1MB) to prevent OOM attacks.
    /// </summary>
    private const int MaxStringLength = 0x100000; // 1MB

    /// <summary>
    /// Threshold for using ArrayPool for string buffers (8KB).
    /// </summary>
    private const int ArrayPoolThreshold = 8192;

    /// <summary>
    /// Strict UTF-8 encoding that throws on invalid byte sequences.
    /// Shared across all string operations for performance.
    /// </summary>
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Calculates padding bytes needed to align to the specified boundary.
    /// </summary>
    /// <param name="offset">Current offset in bytes.</param>
    /// <param name="alignment">Alignment boundary (must be power of 2: 4, 8, 16).</param>
    /// <returns>Number of padding bytes needed (0 if already aligned).</returns>
    /// <exception cref="ArgumentException">Thrown if alignment is not a power of 2.</exception>
    /// <exception cref="OverflowException">Thrown if padding calculation would overflow.</exception>
    public static int CalculatePadding(long offset, int alignment = 4)
    {
        if (alignment <= 0 || (alignment & (alignment - 1)) != 0)
        {
            throw new ArgumentException($"Alignment must be a power of 2, got {alignment}", nameof(alignment));
        }

        var remainder = offset % alignment;
        if (remainder == 0)
        {
            return 0;
        }

        var padding = alignment - remainder;
        
        // Ensure the padding fits in an int32
        if (padding > int.MaxValue)
        {
            throw new OverflowException($"Padding calculation overflow: alignment={alignment}, remainder={remainder}");
        }

        return (int)padding;
    }

    /// <summary>
    /// Aligns the BinaryReader position to the specified boundary by skipping padding bytes.
    /// </summary>
    /// <param name="reader">The BinaryReader to align.</param>
    /// <param name="alignment">Alignment boundary (default: 4 bytes).</param>
    /// <param name="validatePadding">If true, throws if padding bytes are non-zero.</param>
    /// <exception cref="ArgumentNullException">Thrown if reader is null.</exception>
    /// <exception cref="AlignmentValidationException">Thrown if validatePadding is true and padding bytes are non-zero.</exception>
    /// <exception cref="StreamBoundsException">Thrown if alignment would read past end of stream.</exception>
    public static void Align(this BinaryReader reader, int alignment = 4, bool validatePadding = false)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var currentPosition = reader.BaseStream.Position;
        var padding = CalculatePadding(currentPosition, alignment);

        if (padding == 0)
        {
            return; // Already aligned
        }

        // Validate bounds before reading
        ValidateBounds(currentPosition, padding, reader.BaseStream.Length);

        if (validatePadding)
        {
            var paddingBytes = reader.ReadBytes(padding);
            if (!ValidatePadding(paddingBytes, mustBeZero: true))
            {
                throw new AlignmentValidationException(
                    $"Non-zero padding bytes detected at offset {currentPosition}. " +
                    $"Expected {padding} zero bytes for {alignment}-byte alignment.");
            }
        }
        else
        {
            // Skip padding bytes without validation
            reader.BaseStream.Seek(padding, SeekOrigin.Current);
        }
    }

    /// <summary>
    /// Reads a null-terminated UTF-8 string from the stream.
    /// Uses ArrayPool for strings larger than 8KB.
    /// </summary>
    /// <param name="reader">The BinaryReader to read from.</param>
    /// <param name="maxLength">Maximum allowed string length in bytes (default: 1MB).</param>
    /// <returns>The decoded UTF-8 string.</returns>
    /// <exception cref="ArgumentNullException">Thrown if reader is null.</exception>
    /// <exception cref="StringReadException">Thrown if string exceeds maxLength or null terminator not found.</exception>
    /// <exception cref="Utf8DecodingException">Thrown if UTF-8 decoding fails.</exception>
    /// <exception cref="StreamBoundsException">Thrown if reading would exceed stream length.</exception>
    public static string ReadUtf8NullTerminated(this BinaryReader reader, int maxLength = MaxStringLength)
    {
        ArgumentNullException.ThrowIfNull(reader);

        if (maxLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), "maxLength must be positive");
        }

        var startPosition = reader.BaseStream.Position;
        var bytes = new List<byte>();

        try
        {
            while (true)
            {
                // Check bounds before each read
                if (reader.BaseStream.Position >= reader.BaseStream.Length)
                {
                    throw new StreamBoundsException(
                        $"Unexpected end of stream while reading null-terminated string at offset {startPosition}. " +
                        $"Read {bytes.Count} bytes without finding null terminator.");
                }

                var b = reader.ReadByte();

                if (b == 0x00)
                {
                    break; // Found null terminator
                }

                bytes.Add(b);

                if (bytes.Count > maxLength)
                {
                    throw new StringReadException(
                        $"String exceeds maximum length of {maxLength} bytes at offset {startPosition}");
                }
            }

            // Decode UTF-8
            return DecodeUtf8(bytes, startPosition);
        }
        catch (EndOfStreamException ex)
        {
            throw new StreamBoundsException(
                $"Unexpected end of stream while reading null-terminated string at offset {startPosition}", ex);
        }
    }

    /// <summary>
    /// Validates that padding bytes match expected pattern.
    /// </summary>
    /// <param name="paddingBytes">The padding bytes to validate.</param>
    /// <param name="mustBeZero">If true, all bytes must be 0x00.</param>
    /// <returns>True if validation passes, false otherwise.</returns>
    public static bool ValidatePadding(byte[] paddingBytes, bool mustBeZero = true)
    {
        ArgumentNullException.ThrowIfNull(paddingBytes);

        if (!mustBeZero)
        {
            return true; // No validation required
        }

        return paddingBytes.All(b => b == 0x00);
    }

    /// <summary>
    /// Validates that a read operation would not exceed stream bounds.
    /// </summary>
    /// <param name="offset">Current stream offset.</param>
    /// <param name="length">Number of bytes to read.</param>
    /// <param name="streamLength">Total stream length.</param>
    /// <exception cref="StreamBoundsException">Thrown if offset + length > streamLength.</exception>
    public static void ValidateBounds(long offset, long length, long streamLength)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative");
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative");
        }

        // Check for overflow: offset + length > streamLength
        // Rewritten to avoid overflow: offset > streamLength - length
        if (offset > streamLength || length > streamLength || offset > streamLength - length)
        {
            throw new StreamBoundsException(
                $"Read would exceed stream bounds: offset={offset}, length={length}, streamLength={streamLength}");
        }
    }

    /// <summary>
    /// Decodes UTF-8 bytes to string with proper error handling.
    /// Uses ArrayPool for large buffers.
    /// </summary>
    private static string DecodeUtf8(List<byte> bytes, long startPosition)
    {
        if (bytes.Count == 0)
        {
            return string.Empty;
        }

        try
        {
            if (bytes.Count <= ArrayPoolThreshold)
            {
                // Small string: allocate inline
                var byteArray = bytes.ToArray();
                return StrictUtf8.GetString(byteArray);
            }
            else
            {
                // Large string: use ArrayPool
                var buffer = ArrayPool<byte>.Shared.Rent(bytes.Count);
                try
                {
                    bytes.CopyTo(buffer);
                    return StrictUtf8.GetString(buffer, 0, bytes.Count);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
        catch (DecoderFallbackException ex)
        {
            throw new Utf8DecodingException(
                $"UTF-8 decoding failed at offset {startPosition} for {bytes.Count} bytes", ex);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase))
        {
            throw new Utf8DecodingException(
                $"UTF-8 decoding failed at offset {startPosition} for {bytes.Count} bytes", ex);
        }
    }
}

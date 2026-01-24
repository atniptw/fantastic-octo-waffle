using UnityAssetParser.Exceptions;
using UnityAssetParser.Helpers;

namespace UnityAssetParser.Tests.Helpers;

/// <summary>
/// Unit tests for BinaryReaderExtensions alignment calculations and padding validation.
/// </summary>
public class AlignmentTests
{
    [Theory]
    [InlineData(0, 4, 0)]      // Already aligned
    [InlineData(1, 4, 3)]      // Need 3 bytes to reach 4
    [InlineData(2, 4, 2)]      // Need 2 bytes to reach 4
    [InlineData(3, 4, 1)]      // Need 1 byte to reach 4
    [InlineData(4, 4, 0)]      // Already aligned
    [InlineData(5, 4, 3)]      // Need 3 bytes to reach 8
    [InlineData(0, 8, 0)]      // Already aligned to 8
    [InlineData(1, 8, 7)]      // Need 7 bytes to reach 8
    [InlineData(7, 8, 1)]      // Need 1 byte to reach 8
    [InlineData(8, 8, 0)]      // Already aligned to 8
    [InlineData(0, 16, 0)]     // Already aligned to 16
    [InlineData(1, 16, 15)]    // Need 15 bytes to reach 16
    [InlineData(15, 16, 1)]    // Need 1 byte to reach 16
    [InlineData(16, 16, 0)]    // Already aligned to 16
    public void CalculatePadding_ValidAlignment_ReturnsCorrectPadding(long offset, int alignment, int expectedPadding)
    {
        // Act
        var padding = BinaryReaderExtensions.CalculatePadding(offset, alignment);

        // Assert
        Assert.Equal(expectedPadding, padding);
    }

    [Theory]
    [InlineData(0)]   // Zero is not a power of 2
    [InlineData(-1)]  // Negative alignment
    [InlineData(3)]   // Not a power of 2
    [InlineData(5)]   // Not a power of 2
    [InlineData(6)]   // Not a power of 2
    [InlineData(7)]   // Not a power of 2
    [InlineData(9)]   // Not a power of 2
    public void CalculatePadding_InvalidAlignment_ThrowsArgumentException(int invalidAlignment)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            BinaryReaderExtensions.CalculatePadding(0, invalidAlignment));
        Assert.Contains("power of 2", exception.Message);
    }

    [Fact]
    public void Align_AlreadyAligned_DoesNotSeek()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[] { 0x01, 0x02, 0x03, 0x04 });
        using var reader = new BinaryReader(stream);

        // Act
        reader.Align(4); // Position 0 is already aligned to 4

        // Assert
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void Align_UnalignedPosition_SkipsPaddingBytes()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[] { 0x01, 0x00, 0x00, 0x00, 0x02 });
        using var reader = new BinaryReader(stream);
        reader.ReadByte(); // Move to position 1

        // Act
        reader.Align(4); // Skip 3 padding bytes to reach position 4

        // Assert
        Assert.Equal(4, stream.Position);
    }

    [Fact]
    public void Align_WithValidation_AllZeroPadding_Succeeds()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[] { 0x01, 0x00, 0x00, 0x00, 0x02 });
        using var reader = new BinaryReader(stream);
        reader.ReadByte(); // Move to position 1

        // Act
        reader.Align(4, validatePadding: true); // Validate 3 zero bytes

        // Assert
        Assert.Equal(4, stream.Position);
    }

    [Fact]
    public void Align_WithValidation_NonZeroPadding_ThrowsAlignmentValidationException()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[] { 0x01, 0x00, 0xFF, 0x00, 0x02 });
        using var reader = new BinaryReader(stream);
        reader.ReadByte(); // Move to position 1

        // Act & Assert
        var exception = Assert.Throws<AlignmentValidationException>(() =>
            reader.Align(4, validatePadding: true));
        Assert.Contains("Non-zero padding", exception.Message);
    }

    [Fact]
    public void Align_ExceedsStreamBounds_ThrowsStreamBoundsException()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[] { 0x01, 0x02 }); // Only 2 bytes
        using var reader = new BinaryReader(stream);
        reader.ReadByte(); // Move to position 1

        // Act & Assert
        var exception = Assert.Throws<StreamBoundsException>(() =>
            reader.Align(4)); // Would need to read 3 more bytes, but only 1 available
        Assert.Contains("exceed stream bounds", exception.Message);
    }

    [Fact]
    public void Align_NullReader_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            BinaryReaderExtensions.Align(null!, 4));
    }

    [Theory]
    [InlineData(new byte[] { 0x00, 0x00, 0x00 }, true, true)]       // All zeros
    [InlineData(new byte[] { 0x00 }, true, true)]                    // Single zero
    [InlineData(new byte[] { }, true, true)]                         // Empty (valid)
    [InlineData(new byte[] { 0x00, 0xFF, 0x00 }, true, false)]      // Contains non-zero
    [InlineData(new byte[] { 0xFF, 0xFF, 0xFF }, true, false)]      // All non-zero
    [InlineData(new byte[] { 0x00, 0xFF, 0x00 }, false, true)]      // No validation
    public void ValidatePadding_VariousPatterns_ReturnsExpected(byte[] paddingBytes, bool mustBeZero, bool expected)
    {
        // Act
        var result = BinaryReaderExtensions.ValidatePadding(paddingBytes, mustBeZero);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ValidatePadding_NullBytes_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            BinaryReaderExtensions.ValidatePadding(null!));
    }

    [Theory]
    [InlineData(0, 10, 100, true)]     // Valid: within bounds
    [InlineData(90, 10, 100, true)]    // Valid: exact boundary
    [InlineData(0, 100, 100, true)]    // Valid: entire stream
    [InlineData(90, 11, 100, false)]   // Invalid: exceeds by 1
    [InlineData(0, 101, 100, false)]   // Invalid: length too large
    public void ValidateBounds_VariousScenarios_BehavesCorrectly(long offset, long length, long streamLength, bool shouldSucceed)
    {
        // Act & Assert
        if (shouldSucceed)
        {
            // Should not throw
            BinaryReaderExtensions.ValidateBounds(offset, length, streamLength);
        }
        else
        {
            Assert.Throws<StreamBoundsException>(() =>
                BinaryReaderExtensions.ValidateBounds(offset, length, streamLength));
        }
    }

    [Fact]
    public void ValidateBounds_NegativeOffset_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BinaryReaderExtensions.ValidateBounds(-1, 10, 100));
    }

    [Fact]
    public void ValidateBounds_NegativeLength_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BinaryReaderExtensions.ValidateBounds(0, -1, 100));
    }
}

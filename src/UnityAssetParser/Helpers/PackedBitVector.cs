namespace UnityAssetParser.Helpers;

/// <summary>
/// Represents a bit-packed vector for efficient storage of quantized data.
/// This is a verbatim port from UnityPy/helpers/PackedBitVector.py and UnityPy/classes/generated.py.
/// 
/// PackedBitVector stores integers in a bit-packed format where each value uses exactly m_BitSize bits.
/// For floating-point data, values are quantized to integers and can be reconstructed using:
///   value = int * (m_Range / ((1 &lt;&lt; m_BitSize) - 1)) + m_Start
/// 
/// Reference: https://github.com/K0lb3/UnityPy/blob/master/UnityPy/helpers/PackedBitVector.py
/// </summary>
public sealed class PackedBitVector
{
    /// <summary>
    /// Gets the number of items stored in the packed vector.
    /// </summary>
    public uint NumItems { get; private set; }

    /// <summary>
    /// Gets the number of bits used to store each value (optional, can be 0 or null).
    /// </summary>
    public byte? BitSize { get; private set; }

    /// <summary>
    /// Gets the range used for float reconstruction (max - min).
    /// </summary>
    public float? Range { get; private set; }

    /// <summary>
    /// Gets the start value used for float reconstruction (min value).
    /// </summary>
    public float? Start { get; private set; }

    /// <summary>
    /// Gets the raw bit-packed data as a byte array.
    /// </summary>
    public byte[] Data { get; private set; }

    /// <summary>
    /// Initializes an empty PackedBitVector.
    /// </summary>
    public PackedBitVector()
    {
        NumItems = 0;
        BitSize = 0;
        Range = 0f;
        Start = 0f;
        Data = Array.Empty<byte>();
    }

    /// <summary>
    /// Initializes a new instance from TypeTree dictionary data.
    /// Factory method for TypeTree parsing.
    /// </summary>
    private PackedBitVector(uint numItems, byte? bitSize, float? range, float? start, byte[] data)
    {
        NumItems = numItems;
        BitSize = bitSize;
        Range = range;
        Start = start;
        Data = data;
    }

    /// <summary>
    /// Creates a PackedBitVector from TypeTree dictionary data.
    /// </summary>
    public static PackedBitVector FromTypeTreeData(
        Dictionary<string, object?> dict,
        bool hasRangeStart = true)
    {
        uint numItems = 0;
        if (dict.TryGetValue("m_NumItems", out var numObj) && numObj != null)
        {
            numItems = Convert.ToUInt32(numObj);
        }

        byte? bitSize = null;
        if (dict.TryGetValue("m_BitSize", out var bitObj) && bitObj != null)
        {
            bitSize = Convert.ToByte(bitObj);
        }

        float? range = null, start = null;
        if (hasRangeStart)
        {
            if (dict.TryGetValue("m_Range", out var rangeObj) && rangeObj != null)
            {
                range = Convert.ToSingle(rangeObj);
            }
            if (dict.TryGetValue("m_Start", out var startObj) && startObj != null)
            {
                start = Convert.ToSingle(startObj);
            }
        }

        byte[] data = Array.Empty<byte>();
        if (dict.TryGetValue("m_Data", out var dataObj) && dataObj is List<object?> dataList)
        {
            var bytes = new List<byte>();
            foreach (var item in dataList)
            {
                if (item != null)
                {
                    bytes.Add(Convert.ToByte(item));
                }
            }
            data = bytes.ToArray();
        }

        return new PackedBitVector(numItems, bitSize, range, start, data);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PackedBitVector"/> class by reading from a binary reader.
    /// Follows the parsing logic from UnityPy's PackedBitVector class.
    /// </summary>
    /// <param name="reader">The binary reader to read from.</param>
    /// <param name="hasRangeStart">If true, reads Range and Start fields (for float data). If false, skips them (for int data).</param>
    public PackedBitVector(EndianBinaryReader reader, bool hasRangeStart = true)
    {
        ArgumentNullException.ThrowIfNull(reader);

        // Read fields in order as defined in UnityPy TypeTree
        NumItems = reader.ReadUInt32();
        
        // Conditionally read Range and Start based on data type
        if (hasRangeStart)
        {
            Range = reader.ReadSingle();
            Start = reader.ReadSingle();
        }
        else
        {
            Range = null;
            Start = null;
        }

        // Read data array
        var dataLength = reader.ReadInt32();
        Data = dataLength > 0 ? reader.ReadBytes(dataLength) : Array.Empty<byte>();
        reader.Align(4);

        // Read bit size (stored as UInt8 in TypeTree, but we read as uint for the count)
        byte bitSizeValue = reader.ReadByte();
        BitSize = bitSizeValue == 0 ? null : bitSizeValue;
        reader.Align(4);
    }

    /// <summary>
    /// Unpacks the bit-packed integers from the data array.
    /// This is a C# port of UnityPy's unpack_ints function.
    /// </summary>
    /// <param name="start">Starting index in the unpacked array (default: 0).</param>
    /// <param name="count">Number of items to unpack (default: all items).</param>
    /// <returns>Array of unpacked unsigned integers.</returns>
    public uint[] UnpackInts(int start = 0, int? count = null)
    {
        if (BitSize == null || BitSize == 0)
        {
            throw new InvalidOperationException("BitSize must be set and non-zero to unpack integers");
        }

        var actualCount = count ?? (int)NumItems;
        if (actualCount == 0)
        {
            return Array.Empty<uint>();
        }

        var bitSize = BitSize.Value;
        var result = new uint[actualCount];

        // Calculate starting bit position
        var bitPos = bitSize * start;
        var indexPos = bitPos / 8;
        bitPos %= 8;

        // Unpack each value bit by bit
        // Direct port from UnityPy/helpers/PackedBitVector.py unpack_ints function
        for (var i = 0; i < actualCount; i++)
        {
            uint value = 0;
            var bits = 0;

            while (bits < bitSize)
            {
                // Check bounds before accessing Data array
                if (indexPos >= Data.Length)
                {
                    throw new IndexOutOfRangeException(
                        $"PackedBitVector data out of bounds: trying to read byte at index {indexPos}, but Data.Length is {Data.Length}");
                }

                // Extract bits from current byte
                value |= (uint)((Data[indexPos] >> bitPos) << bits);
                
                // Calculate how many bits we can read from current byte
                var num = Math.Min(bitSize - bits, 8 - bitPos);
                bitPos += num;
                bits += num;

                // Move to next byte if we've read all 8 bits
                if (bitPos == 8)
                {
                    indexPos++;
                    bitPos = 0;
                }
            }

            // Mask to extract only the bits we need
            result[i] = value & ((1u << bitSize) - 1);
        }

        return result;
    }

    /// <summary>
    /// Unpacks the bit-packed data and reconstructs floating-point values.
    /// This is a C# port of UnityPy's unpack_floats function.
    /// 
    /// Formula: value = int * (Range / ((1 &lt;&lt; BitSize) - 1)) + Start
    /// </summary>
    /// <param name="start">Starting index in the unpacked array (default: 0).</param>
    /// <param name="count">Number of items to unpack (default: all items).</param>
    /// <returns>Array of reconstructed floating-point values.</returns>
    public float[] UnpackFloats(int start = 0, int? count = null)
    {
        if (Range == null)
        {
            throw new InvalidOperationException("Range must be set to unpack floats");
        }

        if (Start == null)
        {
            throw new InvalidOperationException("Start must be set to unpack floats");
        }

        var actualCount = count ?? (int)NumItems;
        if (actualCount == 0)
        {
            return Array.Empty<float>();
        }

        // Special case: if BitSize is 0 or null, all values are Start
        // This avoids division by zero in the scale calculation
        if (BitSize == null || BitSize == 0)
        {
            var constantResult = new float[actualCount];
            Array.Fill(constantResult, Start.Value);
            return constantResult;
        }

        // Unpack as integers first (cast to double to prevent precision loss)
        var quantizedInts = UnpackInts(start, actualCount);

        // Calculate scale factor for dequantization
        // Using double precision to match Python's behavior
        var scale = (double)Range.Value / ((1u << BitSize.Value) - 1);

        // Reconstruct floats using the formula: value = int * scale + start
        var result = new float[actualCount];
        for (var i = 0; i < actualCount; i++)
        {
            result[i] = (float)(quantizedInts[i] * scale + Start.Value);
        }

        return result;
    }
}

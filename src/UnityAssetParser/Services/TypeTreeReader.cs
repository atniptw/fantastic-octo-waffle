using System.Text;
using UnityAssetParser.Helpers;
using UnityAssetParser.SerializedFile;

namespace UnityAssetParser.Services;

/// <summary>
/// Reads object data dynamically using TypeTree nodes.
/// Port of UnityPy's TypeTreeHelper read_typetree functionality.
/// Uses recursive tree traversal instead of flat list + global index.
/// </summary>
public sealed class TypeTreeReader
{
    private readonly EndianBinaryReader _reader;
    private readonly TypeTreeNode? _root;

    public TypeTreeReader(EndianBinaryReader reader, TypeTreeNode? root)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _root = root;
    }

    /// <summary>
    /// Creates a TypeTreeReader from a flat list (for backwards compatibility).
    /// Converts flat list to tree structure.
    /// </summary>
    public static TypeTreeReader CreateFromFlatList(EndianBinaryReader reader, IReadOnlyList<TypeTreeNode> nodes)
    {
        TypeTreeNode? root = null;

        if (nodes.Count > 0)
        {
            root = nodes[0];

            // Rebuild children for each node (in case it wasn't done during parsing)
            for (int i = 0; i < nodes.Count; i++)
            {
                var parentNode = nodes[i];
                int parentLevel = parentNode.Level;
                int childLevel = parentLevel + 1;

                for (int j = i + 1; j < nodes.Count; j++)
                {
                    var potentialChild = nodes[j];

                    if (potentialChild.Level <= parentLevel)
                        break;

                    if (potentialChild.Level == childLevel)
                    {
                        parentNode.Children.Add(potentialChild);
                    }
                }
            }
        }

        return new TypeTreeReader(reader, root);
    }

    /// <summary>
    /// Reads object data into a dictionary following TypeTree structure.
    /// Uses recursive traversal matching UnityPy architecture.
    /// </summary>
    public Dictionary<string, object?> ReadObject()
    {
        if (_root == null)
            return new Dictionary<string, object?>();

        Console.WriteLine($"[TYPETREE] ReadObject starting - stream position={_reader.BaseStream.Position}, length={_reader.BaseStream.Length}");

        // Read all children of root (skip the root itself)
        var result = new Dictionary<string, object?>();
        foreach (var child in _root.Children)
        {
            Console.WriteLine($"[TYPETREE] Reading field '{child.Name}' (type={child.Type}) at position={_reader.BaseStream.Position}");
            var value = ReadNode(child);
            result[child.Name] = value;
            Console.WriteLine($"[TYPETREE] Field '{child.Name}' done, value={(value != null ? value.GetType().Name : "null")}, position={_reader.BaseStream.Position}");
        }

        Console.WriteLine($"[TYPETREE] ReadObject complete - final position={_reader.BaseStream.Position}");
        return result;
    }

    /// <summary>
    /// Recursively reads a single node and its value from the binary stream.
    /// </summary>
    private object? ReadNode(TypeTreeNode node)
    {
        try
        {
            // Check if this node requires alignment AFTER reading its value
            bool shouldAlign = (node.MetaFlag & 0x4000) != 0;

            object? value;

            // Special case: string types are primitive even if they have children (the "Array" child)
            if (node.Type == "string")
            {
                value = ReadAlignedString();
            }
            // If no children, it's a primitive type
            else if (node.IsPrimitive)
            {
                value = ReadPrimitive(node.Type);
            }
            // If it's an array
            else if (node.IsArray)
            {
                value = ReadArray(node);
            }
            // Otherwise it's a complex object/struct
            else
            {
                value = ReadObject(node);
            }

            // Align stream AFTER reading this node's value if MetaFlag indicates alignment
            if (shouldAlign)
            {
                _reader.Align();
            }

            return value;
        }
        catch (EndOfStreamException)
        {
            // Buffer exhausted - likely reading external data or tree structure mismatch
            // Return null for this node and continue parsing
            Console.WriteLine($"[BUFFER-EXHAUSTED] Cannot read node '{node.Name}' (type={node.Type}) - external data or end of stream");
            return null;
        }
    }

    /// <summary>
    /// Reads a primitive value from the stream.
    /// </summary>
    private object? ReadPrimitive(string typeName)
    {
        return typeName switch
        {
            "int" or "SInt32" => _reader.ReadInt32(),
            "UInt32" or "unsigned int" => _reader.ReadUInt32(),
            "SInt16" or "short" => _reader.ReadInt16(),
            "UInt16" or "unsigned short" => _reader.ReadUInt16(),
            "SInt8" => _reader.ReadSByte(),
            "UInt8" or "char" => _reader.ReadByte(),
            "SInt64" or "long long" => _reader.ReadInt64(),
            "UInt64" or "unsigned long long" => _reader.ReadUInt64(),
            "FileSize" => _reader.ReadUInt64(),
            "float" => _reader.ReadSingle(),
            "double" => _reader.ReadDouble(),
            "bool" => _reader.ReadBoolean(),
            "string" => ReadAlignedString(),
            "TypelessData" => ReadTypelessData(),
            _ => null
        };
    }

    private byte[] ReadTypelessData()
    {
        int length = _reader.ReadInt32();
        if (length <= 0)
        {
            _reader.Align();
            return Array.Empty<byte>();
        }

        byte[] bytes = _reader.ReadBytes(length);
        _reader.Align();
        return bytes;
    }

    /// <summary>
    /// Reads an array type, handling both primitive and complex element types.
    /// Modern Unity array structure (TypeFlags=0x01):
    ///   arrayNode → children[0]: size node (Array wrapper) → children[1]: data template
    /// </summary>
    private List<object?> ReadArray(TypeTreeNode arrayNode)
    {
        var result = new List<object?>();

        // Array structure (TypeFlags bit 0x01):
        // Standard structure (2 children):
        //   children[0]: Size field (reads 4-byte int)
        //   children[1]: Data template (describes one element)
        // Extended structure (3+ children) - newer Unity versions:
        //   children[0]: Size field
        //   children[1]: Data template
        //   children[2+]: Alignment/metadata

        if (arrayNode.Children.Count < 2)
        {
            Console.WriteLine($"[ARRAY-ERROR] Array node '{arrayNode.Name}' has {arrayNode.Children.Count} children, expected at least 2");
            return result;
        }

        TypeTreeNode sizeNode = arrayNode.Children[0];
        TypeTreeNode dataTemplate = arrayNode.Children[1];
        // If there are more than 2 children, they're usually alignment or metadata we can ignore

        // Read array size from binary stream
        int size;
        try
        {
            size = _reader.ReadInt32();
            Console.WriteLine($"[ARRAY-SIZE] Array '{arrayNode.Name}' size={size} (4-byte count), dataTemplate.Type='{dataTemplate.Type}', dataTemplate.ByteSize={dataTemplate.ByteSize}, children={dataTemplate.Children.Count}");
        }
        catch (EndOfStreamException)
        {
            // Can't read even the array size - buffer exhausted, likely external resource
            Console.WriteLine($"[ARRAY-EXTERNAL] Cannot read size for array '{arrayNode.Name}' - buffer exhausted");
            return result;
        }

        // Reject arrays with nonsensical sizes
        if (size < 0 || size > 100_000_000)
        {
            return result;
        }

        if (size == 0)
        {
            return result;
        }

        // Read array elements
        if (dataTemplate.IsPrimitive)
        {
            // Handle empty Type field - check if it's actual data or external resource pointer
            if (string.IsNullOrEmpty(dataTemplate.Type))
            {
                // Arrays with empty Type and large sizes are likely external resource pointers (StreamingInfo)
                // These should NOT be read from the current stream - they point to .resS files
                // Also check if requested bytes exceed available stream data
                long bytesNeeded = (long)size * Math.Max(1, dataTemplate.ByteSize);
                long bytesAvailable = _reader.BaseStream.Length - _reader.BaseStream.Position;

                if (size > 100_000 || bytesNeeded > bytesAvailable)
                {
                    return result; // External resource or stream overflow
                }

                if (dataTemplate.ByteSize > 0)
                {
                    // Small fixed-size elements - read as byte arrays
                    for (int i = 0; i < size; i++)
                    {
                        byte[] bytes = _reader.ReadBytes(dataTemplate.ByteSize);
                        result.Add(bytes);
                    }
                }
                else
                {
                    // Variable size - read length prefix then bytes for each element
                    for (int i = 0; i < size; i++)
                    {
                        int length = _reader.ReadInt32();
                        byte[] bytes = _reader.ReadBytes(length);
                        result.Add(bytes);
                    }
                }
                return result;
            }

            // Primitive array - simple loop
            try
            {
                for (int i = 0; i < size; i++)
                {
                    result.Add(ReadPrimitive(dataTemplate.Type));
                }
            }
            catch (EndOfStreamException)
            {
                // External data (e.g., VertexData in .resS) - return partial or empty
                // This is expected for streaming resources; geometry extraction will use StreamingInfo
                Console.WriteLine($"[ARRAY-EXTERNAL] Array '{arrayNode.Name}' appears to reference external data (size={size}, element type={dataTemplate.Type})");
                return result;
            }
        }
        else
        {
            // Complex array - read each element using template structure
            try
            {
                for (int i = 0; i < size; i++)
                {
                    var element = new Dictionary<string, object?>();
                    foreach (var field in dataTemplate.Children)
                    {
                        element[field.Name] = ReadNode(field);
                    }
                    result.Add(element);
                }
            }
            catch (EndOfStreamException)
            {
                // External data - return what we managed to read
                Console.WriteLine($"[ARRAY-EXTERNAL] Complex array '{arrayNode.Name}' references external data (read {result.Count}/{size} elements)");
                return result;
            }
        }

        return result;
    }

    /// <summary>
    /// Reads a complex object/struct type with multiple fields.
    /// </summary>
    private Dictionary<string, object?> ReadObject(TypeTreeNode structNode)
    {
        var result = new Dictionary<string, object?>();

        foreach (var field in structNode.Children)
        {
            result[field.Name] = ReadNode(field);
        }

        return result;
    }

    /// <summary>
    /// Reads an aligned string (4-byte alignment).
    /// </summary>
    private string ReadAlignedString()
    {
        int length = _reader.ReadInt32();
        if (length == 0)
        {
            _reader.Align();
            return string.Empty;
        }

        byte[] bytes = _reader.ReadBytes(length);
        _reader.Align();

        return Encoding.UTF8.GetString(bytes);
    }
}

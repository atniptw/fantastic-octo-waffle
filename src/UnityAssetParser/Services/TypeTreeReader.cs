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

        // Read all children of root (skip the root itself)
        var result = new Dictionary<string, object?>();
        foreach (var child in _root.Children)
        {
            var value = ReadNode(child);
            result[child.Name] = value;
        }

        return result;
    }

    /// <summary>
    /// Recursively reads a single node and its value from the binary stream.
    /// </summary>
    private object? ReadNode(TypeTreeNode node)
    {
        object? value;
        
        // If no children, it's a primitive type
        if (node.IsPrimitive)
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
        
        // Check if this node requires alignment (UnityPy's kAlignBytes = 0x4000)
        if ((node.MetaFlag & 0x4000) != 0)
        {
            _reader.Align();
        }
        
        return value;
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
            "float" => _reader.ReadSingle(),
            "double" => _reader.ReadDouble(),
            "bool" => _reader.ReadBoolean(),
            "string" => ReadAlignedString(),
            _ => null
        };
    }

    /// <summary>
    /// Reads an array type, handling both primitive and complex element types.
    /// Modern Unity array structure (TypeFlags=0x01):
    ///   arrayNode → children[0]: size node → children[1]: data template
    /// </summary>
    private List<object?> ReadArray(TypeTreeNode arrayNode)
    {
        var result = new List<object?>();

        // Read array size from binary stream
        int size = _reader.ReadInt32();
        
        // DEBUG: Log problematic arrays
        if (size > 10000 || size < 0)
        {
            Console.WriteLine($"[ARRAY-WARN] Suspicious size={size}, arrayNode.Name='{arrayNode.Name}', children={arrayNode.Children.Count}");
            for (int i = 0; i < arrayNode.Children.Count && i < 3; i++)
            {
                var child = arrayNode.Children[i];
                Console.WriteLine($"  child[{i}]: Type='{child.Type}', Name='{child.Name}', ByteSize={child.ByteSize}, TypeFlags=0x{child.TypeFlags:X}, HasChildren={child.Children.Count}");
            }
        }
        
        // CRITICAL: Reject arrays with nonsensical sizes (likely corrupt data or alignment issues)
        if (size < 0 || size > 100_000_000)
        {
            Console.WriteLine($"[ARRAY-ERROR] Rejecting array with invalid size={size}, returning empty");
            return result;
        }
        
        if (size == 0)
        {
            return result;
        }

        // Modern Unity format has 2 children: [size, data]
        // Legacy format may have different structures
        TypeTreeNode? dataTemplate = null;

        if (arrayNode.Children.Count >= 2)
        {
            // Standard case: children[1] is the data template
            dataTemplate = arrayNode.Children[1];
        }
        else if (arrayNode.Children.Count == 1)
        {
            // Fallback: if only one child, use it as template
            dataTemplate = arrayNode.Children[0];
        }
        else
        {
            // No children - cannot deserialize
            Console.WriteLine($"[ARRAY-WARN] Array with no children, name='{arrayNode.Name}'");
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
                    Console.WriteLine($"[ARRAY-DEBUG] Skipping empty-Type array (external/overflow): size={size}, bytesNeeded={bytesNeeded}, bytesAvail={bytesAvailable}");
                    return result; // Return empty - actual data is in .resS file or would overflow
                }
                
                if (dataTemplate.ByteSize > 0)
                {
                    // Small fixed-size elements - read as byte arrays
                    Console.WriteLine($"[ARRAY-DEBUG] Reading byte array (empty Type): {size} elements × {dataTemplate.ByteSize} bytes");
                    for (int i = 0; i < size; i++)
                    {
                        byte[] bytes = _reader.ReadBytes(dataTemplate.ByteSize);
                        result.Add(bytes);
                    }
                }
                else
                {
                    // Variable size - read length prefix then bytes for each element
                    Console.WriteLine($"[ARRAY-DEBUG] Reading variable-size byte array (empty Type): {size} elements with length prefixes");
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
            for (int i = 0; i < size; i++)
            {
                result.Add(ReadPrimitive(dataTemplate.Type));
            }
        }
        else
        {
            // Complex array - read each element using template structure
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

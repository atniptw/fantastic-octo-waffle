using System.Text;
using UnityAssetParser.Helpers;
using UnityAssetParser.SerializedFile;

namespace UnityAssetParser.Services;

/// <summary>
/// Reads object data dynamically using TypeTree nodes.
/// Port of UnityPy's TypeTreeHelper read_typetree functionality.
/// </summary>
public sealed class TypeTreeReader
{
    private readonly EndianBinaryReader _reader;
    private readonly IReadOnlyList<TypeTreeNode> _nodes;
    private int _nodeIndex;

    public TypeTreeReader(EndianBinaryReader reader, IReadOnlyList<TypeTreeNode> nodes)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        _nodeIndex = 0;
    }

    /// <summary>
    /// Reads object data into a dictionary following TypeTree structure.
    /// </summary>
    public Dictionary<string, object?> ReadObject()
    {
        var result = new Dictionary<string, object?>();
        
        if (_nodes.Count == 0)
            return result;

        // Skip root node (the class itself)
        _nodeIndex = 0;
        var rootNode = _nodes[_nodeIndex];
        _nodeIndex++;

        // Read children of root
        int rootLevel = rootNode.Level;
        while (_nodeIndex < _nodes.Count && _nodes[_nodeIndex].Level > rootLevel)
        {
            var fieldNode = _nodes[_nodeIndex];
            var value = ReadValue(fieldNode);
            result[fieldNode.Name] = value;
        }

        return result;
    }

    private object? ReadValue(TypeTreeNode node)
    {
        var currentLevel = node.Level;
        _nodeIndex++;

        // Check if this is a complex type with children
        bool hasChildren = _nodeIndex < _nodes.Count && _nodes[_nodeIndex].Level > currentLevel;

        if (!hasChildren)
        {
            // Primitive type - read directly
            return ReadPrimitive(node.Type);
        }

        // Complex type - handle based on type
        if (node.Type == "vector" || node.Type == "staticvector" || (node.TypeFlags & 0x4000) != 0)
        {
            return ReadArray(currentLevel);
        }
        else
        {
            return ReadComplexObject(currentLevel);
        }
    }

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

    private List<object?> ReadArray(int parentLevel)
    {
        var list = new List<object?>();

        // Array structure: size node, then data node
        if (_nodeIndex >= _nodes.Count)
            return list;

        var sizeNode = _nodes[_nodeIndex];
        if (sizeNode.Name != "size")
            throw new InvalidOperationException($"Expected 'size' node in array, got '{sizeNode.Name}'");

        int size = _reader.ReadInt32();
        _nodeIndex++;

        if (size == 0 || _nodeIndex >= _nodes.Count)
        {
            // Skip to end of array structure
            while (_nodeIndex < _nodes.Count && _nodes[_nodeIndex].Level > parentLevel)
                _nodeIndex++;
            return list;
        }

        var dataNode = _nodes[_nodeIndex];

        // Read array elements
        for (int i = 0; i < size; i++)
        {
            int savedIndex = _nodeIndex;
            var value = ReadValue(dataNode);
            list.Add(value);
            _nodeIndex = savedIndex; // Reset for next element
        }

        // Skip past array structure nodes
        while (_nodeIndex < _nodes.Count && _nodes[_nodeIndex].Level > parentLevel)
            _nodeIndex++;

        return list;
    }

    private Dictionary<string, object?> ReadComplexObject(int parentLevel)
    {
        var obj = new Dictionary<string, object?>();

        // Read all child fields
        while (_nodeIndex < _nodes.Count && _nodes[_nodeIndex].Level > parentLevel)
        {
            var fieldNode = _nodes[_nodeIndex];
            var value = ReadValue(fieldNode);
            obj[fieldNode.Name] = value;
        }

        return obj;
    }

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

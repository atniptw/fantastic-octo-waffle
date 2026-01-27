using System;
using System.IO;
using System.Linq;
using UnityAssetParser.Bundle;
using UnityAssetParser.SerializedFile;
using UnityAssetParser.Services;
using UnityAssetParser.Helpers;

var bundlePath = Path.Combine("Tests/UnityAssetParser.Tests/Fixtures/RealBundles", "Cigar_neck.hhh");
var bundleData = File.ReadAllBytes(bundlePath);

using (var stream = new MemoryStream(bundleData))
{
    var bundleFile = BundleFile.Parse(stream);
    var node0 = bundleFile.Nodes[0];
    var node0Data = bundleFile.ExtractNode(node0);
    var sf = UnityAssetParser.SerializedFile.SerializedFile.Parse(node0Data.Span);
    var meshObject = sf.Objects.FirstOrDefault(o => o.ClassId == 43);
    
    var objectDataSpan = sf.ObjectDataRegion.Span;
    var objectData = objectDataSpan.Slice((int)meshObject.ByteStart, (int)meshObject.ByteSize).ToArray();
    
    Console.WriteLine($"Mesh object: ByteStart={meshObject.ByteStart}, ByteSize={meshObject.ByteSize}");
    Console.WriteLine($"First 80 bytes (hex): {BitConverter.ToString(objectData.Take(80).ToArray()).Replace("-", "")}");
    
    var header = sf.Header;
    var isBigEndian = header.Endianness != 0;
    Console.WriteLine($"Endianness: {header.Endianness} (BigEndian={isBigEndian})");
    
    using (var peekStream = new MemoryStream(objectData, false))
    using (var reader = new EndianBinaryReader(peekStream, isBigEndian))
    {
        // m_Name
        uint nameLength = reader.ReadUInt32();
        Console.WriteLine($"[0] m_Name length: {nameLength}");
        if (nameLength > 0 && nameLength < 256)
        {
            byte[] nameBytes = reader.ReadBytes((int)nameLength);
            string name = System.Text.Encoding.UTF8.GetString(nameBytes);
            Console.WriteLine($"  → Name: '{name}'");
        }
        
        long pos = reader.BaseStream.Position;
        long padding = (4 - (pos % 4)) % 4;
        if (padding > 0) reader.ReadBytes((int)padding);
        Console.WriteLine($"  → After align: position {reader.BaseStream.Position}");
        
        // m_SubMeshes count
        uint submeshCount = reader.ReadUInt32();
        Console.WriteLine($"[{reader.BaseStream.Position - 4}] m_SubMeshes count: {submeshCount}");
        
        // Skip submesh data
        for (int i = 0; i < submeshCount; i++)
        {
            var firstVertex = reader.ReadUInt32();
            var vertexCount = reader.ReadUInt32();
            var firstIndex = reader.ReadUInt32();
            var indexCount = reader.ReadUInt32();
            Console.WriteLine($"  SubMesh {i}: firstVertex={firstVertex}, vertexCount={vertexCount}, firstIndex={firstIndex}, indexCount={indexCount}");
        }
        
        pos = reader.BaseStream.Position;
        padding = (4 - (pos % 4)) % 4;
        if (padding > 0) reader.ReadBytes((int)padding);
        Console.WriteLine($"  → After align: position {reader.BaseStream.Position}");
        
        // m_Shapes / BlendShape count
        uint shapeCount = reader.ReadUInt32();
        Console.WriteLine($"[{reader.BaseStream.Position - 4}] m_Shapes count: {shapeCount}");
    }
}

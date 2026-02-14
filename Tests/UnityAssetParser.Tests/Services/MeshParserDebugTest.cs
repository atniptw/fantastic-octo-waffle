using System;
using System.IO;
using System.Linq;
using Xunit;
using UnityAssetParser.Bundle;
using UnityAssetParser.SerializedFile;
using UnityAssetParser.Services;

namespace UnityAssetParser.Tests.Services;

/// <summary>
/// Isolated test to debug MeshParser.Parse directly on real bundle data.
/// This test surface any exceptions thrown during Mesh parsing.
/// </summary>
public class MeshParserDebugTest
{
    [Fact]
    public void ParseMeshFromCigarNeck_ShouldExtractMeshData()
    {
        // Load bundle
        var bundlePath = Path.Combine("Fixtures", "RealBundles", "Cigar_neck.hhh");
        Assert.True(File.Exists(bundlePath), $"Bundle not found: {bundlePath}");

        var bundleData = File.ReadAllBytes(bundlePath);
        Assert.NotEmpty(bundleData);

        // Parse bundle
        using (var stream = new MemoryStream(bundleData))
        {
            var bundleFile = BundleFile.Parse(stream);
            Assert.NotEmpty(bundleFile.Nodes);

            // Extract SerializedFile from node 0
            var node0 = bundleFile.Nodes[0];
            var node0Data = bundleFile.ExtractNode(node0);
            Assert.False(node0Data.IsEmpty);

            // Parse SerializedFile
            var sf = UnityAssetParser.SerializedFile.SerializedFile.Parse(node0Data.Span);
            Assert.NotEmpty(sf.Objects);

            // Find Mesh object (ClassID 43)
            var meshObject = sf.Objects.FirstOrDefault(o => o.ClassId == 43);
            Assert.NotNull(meshObject);

            // Print all objects for debugging
            Console.WriteLine($"All objects in SerializedFile:");
            foreach (var obj in sf.Objects)
            {
                Console.WriteLine($"  ClassId={obj.ClassId}, PathId={obj.PathId}, ByteStart={obj.ByteStart}, ByteSize={obj.ByteSize}");
            }

            Console.WriteLine($"Found Mesh object, PathId={meshObject.PathId}, ByteStart={meshObject.ByteStart}, ByteSize={meshObject.ByteSize}");

            // Extract object data from the data region
            var objectDataSpan = sf.ObjectDataRegion.Span;
            var objectData = objectDataSpan.Slice((int)meshObject.ByteStart, (int)meshObject.ByteSize).ToArray();
            Console.WriteLine($"Extracted {objectData.Length} bytes of Mesh data");
            Console.WriteLine($"First 64 bytes (hex): {BitConverter.ToString(objectData.Take(64).ToArray()).Replace("-", "")}");

            // Dump bytes around position 148
            if (objectData.Length > 160)
            {
                Console.WriteLine($"Bytes 140-160 (hex): {BitConverter.ToString(objectData.Skip(140).Take(20).ToArray()).Replace("-", "")}");

                // Try to interpret as uint32 little-endian at position 148
                uint val148 = BitConverter.ToUInt32(objectData, 148);
                Console.WriteLine($"As uint32 at position 148: {val148} (0x{val148:08x})");
            }

            // Get header for endianness
            var header = sf.Header;
            var isBigEndian = header.Endianness != 0;  // 0 = little-endian, 1 = big-endian

            // Try to peek at first fields
            using (var peekStream = new System.IO.MemoryStream(objectData, false))
            using (var peekReader = new UnityAssetParser.Helpers.EndianBinaryReader(peekStream, isBigEndian))
            {
                try
                {
                    // Read what should be m_Name (string length + string data)
                    uint nameLength = peekReader.ReadUInt32();
                    Console.WriteLine($"[Offset 0] String length (m_Name): {nameLength}");

                    if (nameLength > 0 && nameLength < 1000)
                    {
                        byte[] nameBytes = peekReader.ReadBytes((int)nameLength);
                        string name = System.Text.Encoding.UTF8.GetString(nameBytes);
                        Console.WriteLine($"  → Name: '{name}'");
                        Console.WriteLine($"  → Position after name: {peekReader.BaseStream.Position}");
                    }

                    // Align to 4-byte boundary
                    long remainder = peekReader.BaseStream.Position % 4;
                    if (remainder != 0)
                    {
                        peekReader.ReadBytes(4 - (int)remainder);
                        Console.WriteLine($"  → Position after align: {peekReader.BaseStream.Position}");
                    }

                    // Peek at next field (should be SubMeshes count)
                    if (peekReader.BaseStream.Position + 4 <= objectData.Length)
                    {
                        uint subMeshCount = peekReader.ReadUInt32();
                        Console.WriteLine($"[Offset {peekReader.BaseStream.Position - 4}] SubMesh count: {subMeshCount}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to peek: {ex.Message}");
                }
            }

            // Parse Mesh - this is where we're failing
            Console.WriteLine("Calling MeshParser.Parse...");
            // Extract version tuple from header
            var versionTuple = ParseVersionTuple(header.UnityVersionString ?? "2022.3.40f1");

            var mesh = MeshParser.Parse(
                objectData,
                versionTuple,
                isBigEndian);

            Assert.NotNull(mesh);
            Assert.NotEmpty(mesh.Name);
            Console.WriteLine($"Mesh Name: {mesh.Name}");
        }
    }

    private static (int, int, int, int) ParseVersionTuple(string versionString)
    {
        // Example: "2022.3.40f1" -> (2022, 3, 40, 0)
        var parts = versionString.Split('.');
        if (parts.Length >= 3)
        {
            return (
                int.Parse(parts[0]),
                int.Parse(parts[1]),
                int.Parse(new string(parts[2].Where(c => char.IsDigit(c)).ToArray())),
                0
            );
        }
        return (2022, 3, 40, 0);
    }
}

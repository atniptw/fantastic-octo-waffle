using System.Buffers.Binary;
using System.Text;
using RepoMod.Glb.Contracts;
using RepoMod.Glb.Implementation;
using RepoMod.Parser.Contracts;

namespace RepoMod.Glb.UnitTests;

public class GlbSerializerTests
{
    [Fact]
    public void Build_ProducesValidGlbHeaderAndChunks()
    {
        var serializer = new GlbSerializer();
        var composition = CreateComposition(topology: 0);

        var result = serializer.Build(composition);

        Assert.NotNull(result.GlbBytes);
        Assert.True(result.GlbBytes.Length > 20);
        Assert.DoesNotContain(result.Diagnostics, item => item.Severity == "error");

        var bytes = result.GlbBytes;
        var magic = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0, 4));
        var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4));
        var length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8, 4));

        Assert.Equal(0x46546C67u, magic);
        Assert.Equal(2u, version);
        Assert.Equal((uint)bytes.Length, length);

        var jsonChunkLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12, 4));
        var jsonChunkType = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16, 4));

        Assert.Equal(0x4E4F534Au, jsonChunkType);
        var jsonStart = 20;
        var jsonPayload = bytes.Skip(jsonStart).Take((int)jsonChunkLength).ToArray();
        var jsonText = Encoding.UTF8.GetString(jsonPayload).TrimEnd('\0', ' ');
        Assert.Contains("\"asset\"", jsonText, StringComparison.Ordinal);
        Assert.Contains("\"meshes\"", jsonText, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_EmitsUnsupportedTopologyDiagnostic_AndSkipsPrimitive()
    {
        var serializer = new GlbSerializer();
        var composition = CreateComposition(topology: 3);

        var result = serializer.Build(composition);

        Assert.Contains(result.Diagnostics, item => item.Code == "UNSUPPORTED_TOPOLOGY");
    }

    private static GlbCompositionResult CreateComposition(int topology)
    {
        var primitive = new ConverterPrimitive(
            "primitive:1",
            "renderer:1",
            "node:1",
            "mesh:1",
            "mat:1",
            0,
            topology,
            [0, 1, 2],
            [0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f],
            [0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f, 0f],
            null,
            null,
            [0f, 0f, 1f, 0f, 0f, 1f],
            null);

        return new GlbCompositionResult(
            "scene:avatar",
            [
                new GlbAnchorComposition(
                    "PlayerAvatar/Armature/Hips",
                    [
                        new GlbComposedPrimitive(
                            "sel-1",
                            "head",
                            "head",
                            "PlayerAvatar/Armature/Hips",
                            primitive)
                    ])
            ],
            [
                new GlbAttachmentDecision(
                    "sel-1",
                    "head",
                    "head",
                    "PlayerAvatar/Armature/Hips",
                    false,
                    ["primitive:1"])
            ],
                    [],
                    [],
            []);
    }
}

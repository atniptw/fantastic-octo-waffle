using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnityAssetParser.Tests;

/// <summary>
/// Helper for snapshot-based testing. Compares C# parser output against UnityPy reference snapshots.
/// </summary>
public static class SnapshotTestHelper
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Load a reference snapshot JSON file.
    /// </summary>
    public static JsonElement LoadSnapshot(string fileName)
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Snapshots",
            fileName
        );

        if (!File.Exists(fixturePath))
            throw new FileNotFoundException($"Snapshot file not found: {fixturePath}");

        var jsonText = File.ReadAllText(fixturePath);
        using var doc = JsonDocument.Parse(jsonText);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Extract summary data from a reference snapshot.
    /// </summary>
    public static (int TotalObjects, int TotalNodes, int TotalMeshes) GetSnaphotSummary(JsonElement snapshot)
    {
        if (!snapshot.TryGetProperty("summary", out var summary))
            throw new InvalidOperationException("Snapshot missing 'summary' property");

        var totalObjects = summary.GetProperty("total_objects").GetInt32();
        var totalNodes = summary.GetProperty("total_nodes").GetInt32();
        var totalMeshes = summary.GetProperty("total_meshes").GetInt32();

        return (totalObjects, totalNodes, totalMeshes);
    }

    /// <summary>
    /// Get object types from reference snapshot.
    /// </summary>
    public static Dictionary<string, int> GetObjectTypes(JsonElement snapshot)
    {
        if (!snapshot.TryGetProperty("object_types", out var objectTypes))
            return new();

        var result = new Dictionary<string, int>();
        foreach (var prop in objectTypes.EnumerateObject())
        {
            result[prop.Name] = prop.Value.GetInt32();
        }
        return result;
    }

    /// <summary>
    /// Get mesh data from reference snapshot.
    /// </summary>
    public static List<MeshSnapshot> GetMeshes(JsonElement snapshot)
    {
        if (!snapshot.TryGetProperty("meshes", out var meshes))
            return new();

        var result = new List<MeshSnapshot>();
        foreach (var mesh in meshes.EnumerateArray())
        {
            result.Add(new MeshSnapshot
            {
                Name = mesh.GetProperty("name").GetString() ?? "",
                VertexCount = mesh.GetProperty("vertex_count").GetInt32(),
                IndexCount = mesh.GetProperty("index_count").GetInt32(),
                HasNormals = mesh.GetProperty("has_normals").GetBoolean(),
                HasUV0 = mesh.GetProperty("has_uv0").GetBoolean(),
                ObjectPath = mesh.GetProperty("object_path").GetString() ?? "",
            });
        }
        return result;
    }
}

/// <summary>
/// Data class for mesh snapshot.
/// </summary>
public record MeshSnapshot
{
    public required string Name { get; init; }
    public required int VertexCount { get; init; }
    public required int IndexCount { get; init; }
    public required bool HasNormals { get; init; }
    public required bool HasUV0 { get; init; }
    public required string ObjectPath { get; init; }
}

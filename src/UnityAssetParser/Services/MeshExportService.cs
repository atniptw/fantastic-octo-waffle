using System;
using System.Collections.Generic;
using UnityAssetParser.Classes;

namespace UnityAssetParser.Services;

/// <summary>
/// Service to export parsed Mesh objects to Three.js-compatible formats.
/// </summary>
public class MeshExportService
{
    /// <summary>
    /// Exports a Mesh to a Three.js BufferGeometry JSON structure.
    /// Returns a dictionary with positions, indices, and other geometry data.
    /// </summary>
    public static Dictionary<string, object> ExportToThreeJS(Mesh mesh)
    {
        var geometry = new Dictionary<string, object>();

        // Metadata
        geometry["name"] = mesh.Name ?? "Mesh";
        geometry["type"] = "BufferGeometry";

        // Vertex count
        geometry["vertexCount"] = mesh.VertexData?.VertexCount ?? 0;

        // Indices from IndexBuffer (stored as byte array)
        if (mesh.IndexBuffer != null && mesh.IndexBuffer.Length > 0)
        {
            geometry["indices"] = Convert.ToBase64String(mesh.IndexBuffer);
            geometry["indexCount"] = mesh.IndexBuffer.Length / 4; // Assuming 4 bytes per index (uint32)
        }

        // Vertex attributes from VertexData channels
        if (mesh.VertexData?.Channels != null)
        {
            var channels = new List<Dictionary<string, object>>();
            foreach (var channel in mesh.VertexData.Channels)
            {
                channels.Add(new Dictionary<string, object>
                {
                    ["stream"] = channel.Stream,
                    ["offset"] = channel.Offset,
                    ["format"] = channel.Format,
                    ["dimension"] = channel.Dimension
                });
            }
            geometry["channels"] = channels;
        }

        // Submeshes
        if (mesh.SubMeshes != null)
        {
            var submeshes = new List<Dictionary<string, object>>();
            foreach (var submesh in mesh.SubMeshes)
            {
                submeshes.Add(new Dictionary<string, object>
                {
                    ["firstByte"] = submesh.FirstByte,
                    ["indexCount"] = submesh.IndexCount,
                    ["topology"] = submesh.Topology,
                    ["firstVertex"] = submesh.FirstVertex,
                    ["vertexCount"] = submesh.VertexCount
                });
            }
            geometry["submeshes"] = submeshes;
        }

        // Bounding box (if we store it in the Mesh class in the future)
        // For now, LocalAABB is read but not stored on the Mesh object
        
        // stream data (for external resource reference)
        if (mesh.StreamData != null)
        {
            geometry["streamData"] = new Dictionary<string, object>
            {
                ["path"] = mesh.StreamData.Path ?? "",
                ["offset"] = mesh.StreamData.Offset,
                ["size"] = mesh.StreamData.Size
            };
        }

        // Note: Actual vertex positions will need to be loaded from the StreamData resource file
        geometry["note"] = "Vertex positions are stored in external resource file (StreamData)";

        return geometry;
    }

    /// <summary>
    /// Exports mesh data to a JSON structure compatible with browser-side Three.js loading.
    /// </summary>
    public static string ExportToJSON(Mesh mesh)
    {
        var geometry = ExportToThreeJS(mesh);
        return System.Text.Json.JsonSerializer.Serialize(geometry, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }
}

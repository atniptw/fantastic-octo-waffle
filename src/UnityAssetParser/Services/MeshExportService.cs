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

    /// <summary>
    /// Exports extracted mesh geometry (MeshGeometryDto) to a Three.js BufferGeometry JSON structure.
    /// This is the primary export function for geometry ready for rendering.
    /// </summary>
    /// <param name="mesh">Extracted mesh geometry with positions, indices, normals, UVs</param>
    /// <returns>Dictionary with Three.js BufferGeometry format</returns>
    public static Dictionary<string, object> ExportToThreeJS(MeshGeometryDto mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var geometry = new Dictionary<string, object>
        {
            // Metadata
            ["name"] = mesh.Name ?? "Mesh",
            ["type"] = "BufferGeometry",

            // Vertex and index counts
            ["vertexCount"] = mesh.VertexCount,
            ["indexCount"] = mesh.Indices?.Length ?? 0,

            // Geometry data (as base64-encoded float/uint arrays)
            ["data"] = new Dictionary<string, object>()
        };

        var data = (Dictionary<string, object>)geometry["data"];

        // Positions (required): flat float array [x0, y0, z0, x1, y1, z1, ...]
        if (mesh.Positions != null && mesh.Positions.Length > 0)
        {
            data["positions"] = Convert.ToBase64String(FloatArrayToBytes(mesh.Positions));
        }

        // Indices (required): flat uint array
        if (mesh.Indices != null && mesh.Indices.Length > 0)
        {
            data["indices"] = mesh.Use16BitIndices
                ? Convert.ToBase64String(UInt16ArrayToBytes(mesh.Indices))
                : Convert.ToBase64String(UIntArrayToBytes(mesh.Indices));
            data["indexFormat"] = mesh.Use16BitIndices ? "uint16" : "uint32";
        }

        // Normals (optional): flat float array [nx0, ny0, nz0, nx1, ny1, nz1, ...]
        if (mesh.Normals != null && mesh.Normals.Length > 0)
        {
            data["normals"] = Convert.ToBase64String(FloatArrayToBytes(mesh.Normals));
        }

        // UVs (optional): flat float array [u0, v0, u1, v1, ...]
        if (mesh.UVs != null && mesh.UVs.Length > 0)
        {
            data["uvs"] = Convert.ToBase64String(FloatArrayToBytes(mesh.UVs));
        }

        // UV2 (optional): second UV set [u0, v0, u1, v1, ...]
        if (mesh.UV2 != null && mesh.UV2.Length > 0)
        {
            data["uv2"] = Convert.ToBase64String(FloatArrayToBytes(mesh.UV2));
        }

        // UV3 (optional): third UV set [u0, v0, u1, v1, ...]
        if (mesh.UV3 != null && mesh.UV3.Length > 0)
        {
            data["uv3"] = Convert.ToBase64String(FloatArrayToBytes(mesh.UV3));
        }

        // Colors (optional): flat float array [r0, g0, b0, a0, r1, g1, b1, a1, ...]
        if (mesh.Colors != null && mesh.Colors.Length > 0)
        {
            data["colors"] = Convert.ToBase64String(FloatArrayToBytes(mesh.Colors));
        }

        // Tangents (optional): flat float array [x0, y0, z0, w0, x1, y1, z1, w1, ...]
        if (mesh.Tangents != null && mesh.Tangents.Length > 0)
        {
            data["tangents"] = Convert.ToBase64String(FloatArrayToBytes(mesh.Tangents));
        }

        // Submesh groups (material slots)
        if (mesh.Groups != null && mesh.Groups.Count > 0)
        {
            var groups = new List<Dictionary<string, object>>();
            foreach (var group in mesh.Groups)
            {
                groups.Add(new Dictionary<string, object>
                {
                    ["start"] = group.Start,
                    ["count"] = group.Count
                });
            }
            geometry["groups"] = groups;
        }

        return geometry;
    }

    /// <summary>
    /// Exports extracted mesh geometry to JSON format.
    /// </summary>
    public static string ExportToJSON(MeshGeometryDto mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var geometry = ExportToThreeJS(mesh);
        return System.Text.Json.JsonSerializer.Serialize(geometry, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }

    /// <summary>
    /// Converts float array to byte array (little-endian).
    /// </summary>
    private static byte[] FloatArrayToBytes(float[] floats)
    {
        var bytes = new byte[floats.Length * sizeof(float)];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>
    /// Converts uint array to byte array (little-endian).
    /// </summary>
    private static byte[] UIntArrayToBytes(uint[] uints)
    {
        var bytes = new byte[uints.Length * sizeof(uint)];
        Buffer.BlockCopy(uints, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>
    /// Converts uint array to uint16 byte array (little-endian).
    /// Used when indices are stored as 16-bit values.
    /// </summary>
    private static byte[] UInt16ArrayToBytes(uint[] uints)
    {
        var bytes = new byte[uints.Length * sizeof(ushort)];
        for (int i = 0; i < uints.Length; i++)
        {
            var ushortVal = (ushort)uints[i];
            Buffer.BlockCopy(new[] { ushortVal }, 0, bytes, i * sizeof(ushort), sizeof(ushort));
        }
        return bytes;
    }
}

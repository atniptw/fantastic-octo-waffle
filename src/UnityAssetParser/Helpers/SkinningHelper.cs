using System.Numerics;
using UnityAssetParser.Classes;

namespace UnityAssetParser.Helpers;

/// <summary>
/// Applies bind-pose skinning to mesh vertices for static preview.
/// </summary>
public static class SkinningHelper
{
    public static bool TryApplyBindPoseSkinning(Mesh mesh)
    {
        if (mesh.Vertices == null || mesh.Vertices.Length == 0)
        {
            return false;
        }

        if (mesh.BindPose == null || mesh.BindPose.Length == 0)
        {
            return false;
        }

        if (mesh.Skin is not BoneWeight4[] weights || weights.Length == 0)
        {
            return false;
        }

        if (weights.Length != mesh.Vertices.Length)
        {
            return false;
        }

        var boneMatrices = BuildBindPoseMatrices(mesh.BindPose);
        if (boneMatrices.Length == 0)
        {
            return false;
        }

        var positions = mesh.Vertices;
        var normals = mesh.Normals;

        for (int i = 0; i < positions.Length; i++)
        {
            var bw = weights[i];
            var pos = new Vector3(positions[i].X, positions[i].Y, positions[i].Z);
            var skinned = Vector3.Zero;

            ApplyWeight(ref skinned, pos, bw.BoneIndex0, bw.Weight0, boneMatrices);
            ApplyWeight(ref skinned, pos, bw.BoneIndex1, bw.Weight1, boneMatrices);
            ApplyWeight(ref skinned, pos, bw.BoneIndex2, bw.Weight2, boneMatrices);
            ApplyWeight(ref skinned, pos, bw.BoneIndex3, bw.Weight3, boneMatrices);

            positions[i] = new Vector3f(skinned.X, skinned.Y, skinned.Z);
        }

        if (normals != null && normals.Length == positions.Length)
        {
            for (int i = 0; i < normals.Length; i++)
            {
                var bw = weights[i];
                var n = new Vector3(normals[i].X, normals[i].Y, normals[i].Z);
                var skinned = Vector3.Zero;

                ApplyNormalWeight(ref skinned, n, bw.BoneIndex0, bw.Weight0, boneMatrices);
                ApplyNormalWeight(ref skinned, n, bw.BoneIndex1, bw.Weight1, boneMatrices);
                ApplyNormalWeight(ref skinned, n, bw.BoneIndex2, bw.Weight2, boneMatrices);
                ApplyNormalWeight(ref skinned, n, bw.BoneIndex3, bw.Weight3, boneMatrices);

                if (skinned.LengthSquared() > 0)
                {
                    skinned = Vector3.Normalize(skinned);
                }

                normals[i] = new Vector3f(skinned.X, skinned.Y, skinned.Z);
            }
        }

        mesh.Vertices = positions;
        mesh.Normals = normals;
        return true;
    }

    public static BoneWeight4[]? BuildWeightsFromVariableBoneCount(uint[] data, int vertexCount)
    {
        if (data.Length == 0 || vertexCount <= 0)
        {
            return null;
        }

        var weights = new BoneWeight4[vertexCount];
        int index = 0;

        for (int v = 0; v < vertexCount; v++)
        {
            if (index >= data.Length)
            {
                return null;
            }

            int count = (int)data[index++];
            if (count < 0)
            {
                return null;
            }

            float w0 = 0, w1 = 0, w2 = 0, w3 = 0;
            int b0 = 0, b1 = 0, b2 = 0, b3 = 0;

            for (int i = 0; i < count; i++)
            {
                if (index + 1 >= data.Length)
                {
                    return null;
                }

                int boneIndex = (int)data[index++];
                float weight = data[index++] / 65535f; // Unity stores weights as UInt16 in most cases

                if (i == 0)
                {
                    b0 = boneIndex; w0 = weight;
                }
                else if (i == 1)
                {
                    b1 = boneIndex; w1 = weight;
                }
                else if (i == 2)
                {
                    b2 = boneIndex; w2 = weight;
                }
                else if (i == 3)
                {
                    b3 = boneIndex; w3 = weight;
                }
            }

            float sum = w0 + w1 + w2 + w3;
            if (sum > 0f)
            {
                w0 /= sum; w1 /= sum; w2 /= sum; w3 /= sum;
            }

            weights[v] = new BoneWeight4
            {
                BoneIndex0 = b0,
                BoneIndex1 = b1,
                BoneIndex2 = b2,
                BoneIndex3 = b3,
                Weight0 = w0,
                Weight1 = w1,
                Weight2 = w2,
                Weight3 = w3
            };
        }

        return weights;
    }

    private static Matrix4x4[] BuildBindPoseMatrices(Matrix4x4f[] bindPoses)
    {
        var matrices = new Matrix4x4[bindPoses.Length];
        for (int i = 0; i < bindPoses.Length; i++)
        {
            if (TryToMatrix(bindPoses[i], out var mat) && Matrix4x4.Invert(mat, out var inv))
            {
                matrices[i] = inv;
            }
            else
            {
                matrices[i] = Matrix4x4.Identity;
            }
        }
        return matrices;
    }

    private static void ApplyWeight(ref Vector3 acc, Vector3 pos, int boneIndex, float weight, Matrix4x4[] bones)
    {
        if (weight <= 0f || boneIndex < 0 || boneIndex >= bones.Length)
        {
            return;
        }

        var transformed = Vector3.Transform(pos, bones[boneIndex]);
        acc += transformed * weight;
    }

    private static void ApplyNormalWeight(ref Vector3 acc, Vector3 normal, int boneIndex, float weight, Matrix4x4[] bones)
    {
        if (weight <= 0f || boneIndex < 0 || boneIndex >= bones.Length)
        {
            return;
        }

        var transformed = Vector3.TransformNormal(normal, bones[boneIndex]);
        acc += transformed * weight;
    }

    private static bool TryToMatrix(Matrix4x4f matrix, out Matrix4x4 result)
    {
        result = Matrix4x4.Identity;
        if (matrix.Values == null || matrix.Values.Length != 16)
        {
            return false;
        }

        var v = matrix.Values;
        result = new Matrix4x4(
            v[0], v[1], v[2], v[3],
            v[4], v[5], v[6], v[7],
            v[8], v[9], v[10], v[11],
            v[12], v[13], v[14], v[15]);
        return true;
    }
}

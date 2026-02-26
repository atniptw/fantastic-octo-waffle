using RepoMod.Parser.Contracts;

namespace RepoMod.Parser.Implementation;

public static class ConverterSceneValidator
{
    public static IReadOnlyList<ConverterDiagnostic> Validate(ConverterScene scene)
    {
        var diagnostics = new List<ConverterDiagnostic>();
        foreach (var primitive in scene.Primitives)
        {
            if (primitive.Topology != 0)
            {
                diagnostics.Add(new ConverterDiagnostic(
                    "warning",
                    "UNSUPPORTED_TOPOLOGY",
                    $"Primitive '{primitive.PrimitiveId}' uses unsupported topology '{primitive.Topology}'.",
                    primitive.PrimitiveId));
            }

            if (primitive.Indices.Count == 0)
            {
                diagnostics.Add(new ConverterDiagnostic(
                    "error",
                    "MISSING_INDICES",
                    $"Primitive '{primitive.PrimitiveId}' is missing index data.",
                    primitive.PrimitiveId));
                continue;
            }

            if (primitive.Positions is not { Count: > 0 })
            {
                diagnostics.Add(new ConverterDiagnostic(
                    "error",
                    "MISSING_POSITIONS",
                    $"Primitive '{primitive.PrimitiveId}' is missing position data.",
                    primitive.PrimitiveId));
                continue;
            }

            if (primitive.Positions.Count % 3 != 0)
            {
                diagnostics.Add(new ConverterDiagnostic(
                    "error",
                    "INVALID_POSITION_COMPONENTS",
                    $"Primitive '{primitive.PrimitiveId}' has invalid position component count '{primitive.Positions.Count}'.",
                    primitive.PrimitiveId));
                continue;
            }

            var vertexCount = primitive.Positions.Count / 3;
            var maxIndex = primitive.Indices.Max();
            if (maxIndex >= vertexCount)
            {
                diagnostics.Add(new ConverterDiagnostic(
                    "error",
                    "INDEX_OUT_OF_RANGE",
                    $"Primitive '{primitive.PrimitiveId}' index '{maxIndex}' exceeds vertex count '{vertexCount}'.",
                    primitive.PrimitiveId));
            }

            ValidateAttributeShape(diagnostics, primitive.PrimitiveId, "NORMAL", primitive.Normals, 3, vertexCount);
            ValidateAttributeShape(diagnostics, primitive.PrimitiveId, "TANGENT", primitive.Tangents, 4, vertexCount);
            ValidateAttributeShape(diagnostics, primitive.PrimitiveId, "COLOR", primitive.Colors, 4, vertexCount);
            ValidateAttributeShape(diagnostics, primitive.PrimitiveId, "UV0", primitive.Uv0, 2, vertexCount);
            ValidateAttributeShape(diagnostics, primitive.PrimitiveId, "UV1", primitive.Uv1, 2, vertexCount);
        }

        return diagnostics;
    }

    private static void ValidateAttributeShape(
        ICollection<ConverterDiagnostic> diagnostics,
        string primitiveId,
        string attribute,
        IReadOnlyList<float>? values,
        int componentsPerVertex,
        int expectedVertexCount)
    {
        if (values is not { Count: > 0 })
        {
            return;
        }

        if (values.Count % componentsPerVertex != 0)
        {
            diagnostics.Add(new ConverterDiagnostic(
                "warning",
                $"INVALID_{attribute}_COMPONENTS",
                $"Primitive '{primitiveId}' attribute '{attribute}' has invalid component count '{values.Count}'.",
                primitiveId));
            return;
        }

        var attributeVertexCount = values.Count / componentsPerVertex;
        if (attributeVertexCount != expectedVertexCount)
        {
            diagnostics.Add(new ConverterDiagnostic(
                "warning",
                $"MISMATCHED_{attribute}_VERTEX_COUNT",
                $"Primitive '{primitiveId}' attribute '{attribute}' vertex count '{attributeVertexCount}' differs from position vertex count '{expectedVertexCount}'.",
                primitiveId));
        }
    }
}

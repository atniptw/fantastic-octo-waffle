using System;
using System.Collections.Generic;
using UnityAssetParser;
using Xunit;

namespace UnityAssetParser.Tests
{
    public class MaterialColorParsingTest
    {
        [Fact]
        public void BlindEye_Material_Should_Parse_With_Diagnostic_Logs()
        {
            // Arrange
            var blindEyePath = "tests/UnityAssetParser.Tests/fixtures/MoreHead-UnityPackage/BlindEye_body.hhh";
            if (!System.IO.File.Exists(blindEyePath))
            {
                throw new InvalidOperationException($"Test fixture not found: {blindEyePath}");
            }

            var blindEyeBytes = System.IO.File.ReadAllBytes(blindEyePath);
            var context = new BaseAssetsContext();

            // Act - Parse the decoration
            SkeletonParser.Parse(blindEyeBytes, "BlindEye_body.hhh", context);

            // Assert
            System.Console.WriteLine($"\n=== BlindEye Material Parsing Results ===");
            System.Console.WriteLine($"Materials found: {context.SemanticMaterials.Count}");
            System.Console.WriteLine($"Textures found: {context.SemanticTextures.Count}");
            
            foreach (var mat in context.SemanticMaterials)
            {
                System.Console.WriteLine($"Material: {mat.Name}");
                System.Console.WriteLine($"  PathId: {mat.PathId}");
                System.Console.WriteLine($"  BaseColor: [{string.Join(",", mat.BaseColorFactor)}]");
                System.Console.WriteLine($"  Metallic: {mat.Metallic}");
                System.Console.WriteLine($"  Roughness: {mat.Roughness}");
            }

            foreach (var tex in context.SemanticTextures)
            {
                System.Console.WriteLine($"Texture: {tex.Name}");
                System.Console.WriteLine($"  PathId: {tex.PathId}");
                System.Console.WriteLine($"  Size: {tex.Width}x{tex.Height}");
                System.Console.WriteLine($"  Format: {tex.Format}");
            }

            Assert.NotEmpty(context.SemanticMaterials);
            Assert.NotEmpty(context.SemanticTextures);
        }
    }
}

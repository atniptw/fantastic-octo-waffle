using System.Text.Json;
using UnityAssetParser.Bundle;
using Xunit.Abstractions;

namespace UnityAssetParser.Tests.Integration;

/// <summary>
/// JSON round-trip validation tests using real fixture files.
/// These tests validate that BundleFile produces JSON matching UnityPy reference outputs.
/// </summary>
public class BundleFileJsonValidationTests
{
    private readonly ITestOutputHelper _output;
    private static readonly string FixturesDir = Path.Combine(
        Path.GetDirectoryName(typeof(BundleFileJsonValidationTests).Assembly.Location)!,
        "..", "..", "..", "Fixtures");

    public BundleFileJsonValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Tests that available fixtures can be parsed and produce valid JSON.
    /// </summary>
    [Fact]
    public void Parse_AvailableFixtures_ProducesValidJson()
    {
        // Arrange: Find all .hhh files in fixtures directory
        if (!Directory.Exists(FixturesDir))
        {
            _output.WriteLine($"Fixtures directory not found: {FixturesDir}");
            _output.WriteLine("Skipping fixture-based validation. To enable:");
            _output.WriteLine("1. Add .hhh fixture files to Tests/UnityAssetParser.Tests/Fixtures/");
            _output.WriteLine("2. Run: python scripts/generate_reference_json.py --all");
            return; // Skip test if no fixtures
        }

        var fixtureFiles = Directory.GetFiles(FixturesDir, "*.hhh");
        
        if (fixtureFiles.Length == 0)
        {
            _output.WriteLine($"No .hhh fixtures found in {FixturesDir}");
            _output.WriteLine("To add fixtures, copy real bundle files with .hhh extension");
            return; // Skip test if no fixtures
        }

        _output.WriteLine($"Found {fixtureFiles.Length} fixture(s)");

        // Act & Assert: Parse each fixture and validate JSON structure
        foreach (var fixturePath in fixtureFiles)
        {
            var fixtureName = Path.GetFileName(fixturePath);
            _output.WriteLine($"\nProcessing: {fixtureName}");

            try
            {
                using var stream = File.OpenRead(fixturePath);
                var bundle = BundleFile.Parse(stream);

                // Generate JSON
                var json = bundle.ToJson();
                Assert.NotNull(json);
                Assert.NotEmpty(json);

                // Validate JSON structure
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Verify required top-level properties
                Assert.True(root.TryGetProperty("header", out _), "Missing 'header'");
                Assert.True(root.TryGetProperty("storage_blocks", out _), "Missing 'storage_blocks'");
                Assert.True(root.TryGetProperty("nodes", out _), "Missing 'nodes'");
                Assert.True(root.TryGetProperty("data_offset", out _), "Missing 'data_offset'");

                // Verify header structure
                var header = root.GetProperty("header");
                Assert.Equal("UnityFS", header.GetProperty("signature").GetString());
                var version = header.GetProperty("version").GetUInt32();
                Assert.True(version is 6 or 7, $"Unexpected version: {version}");

                _output.WriteLine($"  ✓ Signature: {header.GetProperty("signature").GetString()}");
                _output.WriteLine($"  ✓ Version: {version}");
                _output.WriteLine($"  ✓ Storage blocks: {root.GetProperty("storage_blocks").GetArrayLength()}");
                _output.WriteLine($"  ✓ Nodes: {root.GetProperty("nodes").GetArrayLength()}");
                _output.WriteLine($"  ✓ Data offset: {root.GetProperty("data_offset").GetInt64()}");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  ✗ Failed to parse {fixtureName}: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// Tests JSON round-trip validation against UnityPy reference if available.
    /// </summary>
    [Fact]
    public void Parse_WithReferenceJson_MatchesUnityPy()
    {
        // Arrange: Find fixture files with corresponding reference JSON
        if (!Directory.Exists(FixturesDir))
        {
            _output.WriteLine("Fixtures directory not found, skipping validation");
            return;
        }

        var fixtureFiles = Directory.GetFiles(FixturesDir, "*.hhh");
        var validatedCount = 0;

        foreach (var fixturePath in fixtureFiles)
        {
            var fixtureName = Path.GetFileNameWithoutExtension(fixturePath);
            var referenceJsonPath = Path.Combine(FixturesDir, $"{fixtureName}_expected.json");

            if (!File.Exists(referenceJsonPath))
            {
                _output.WriteLine($"No reference JSON for {fixtureName}, skipping");
                continue;
            }

            _output.WriteLine($"\nValidating: {fixtureName}");

            // Parse bundle
            using var stream = File.OpenRead(fixturePath);
            var bundle = BundleFile.Parse(stream);
            var actualJson = bundle.ToJson();

            // Load reference JSON
            var expectedJson = File.ReadAllText(referenceJsonPath);

            // Parse both JSONs for comparison
            var actualDoc = JsonDocument.Parse(actualJson);
            var expectedDoc = JsonDocument.Parse(expectedJson);

            // Compare key fields (allow for float precision differences)
            CompareJsonElements(actualDoc.RootElement, expectedDoc.RootElement, "");

            _output.WriteLine($"  ✓ Matches UnityPy reference");
            validatedCount++;
        }

        if (validatedCount == 0)
        {
            _output.WriteLine("\nNo reference JSONs found for validation.");
            _output.WriteLine("To generate reference JSONs:");
            _output.WriteLine("  pip install UnityPy");
            _output.WriteLine("  python scripts/generate_reference_json.py --all");
        }
        else
        {
            _output.WriteLine($"\n✓ Successfully validated {validatedCount} fixture(s) against UnityPy");
        }
    }

    /// <summary>
    /// Recursively compares two JSON elements for equivalence.
    /// </summary>
    private void CompareJsonElements(JsonElement actual, JsonElement expected, string path)
    {
        if (actual.ValueKind != expected.ValueKind)
        {
            throw new InvalidOperationException(
                $"Type mismatch at {path}: expected {expected.ValueKind}, got {actual.ValueKind}");
        }

        switch (actual.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var expectedProp in expected.EnumerateObject())
                {
                    if (!actual.TryGetProperty(expectedProp.Name, out var actualProp))
                    {
                        throw new InvalidOperationException($"Missing property at {path}.{expectedProp.Name}");
                    }
                    CompareJsonElements(actualProp, expectedProp.Value, $"{path}.{expectedProp.Name}");
                }
                break;

            case JsonValueKind.Array:
                var actualArray = actual.EnumerateArray().ToList();
                var expectedArray = expected.EnumerateArray().ToList();
                
                if (actualArray.Count != expectedArray.Count)
                {
                    throw new InvalidOperationException(
                        $"Array length mismatch at {path}: expected {expectedArray.Count}, got {actualArray.Count}");
                }
                
                for (int i = 0; i < actualArray.Count; i++)
                {
                    CompareJsonElements(actualArray[i], expectedArray[i], $"{path}[{i}]");
                }
                break;

            case JsonValueKind.String:
                Assert.Equal(expected.GetString(), actual.GetString());
                break;

            case JsonValueKind.Number:
                // Allow small differences for floating-point numbers
                var expectedNum = expected.GetDouble();
                var actualNum = actual.GetDouble();
                Assert.True(
                    Math.Abs(expectedNum - actualNum) < 0.0001,
                    $"Number mismatch at {path}: expected {expectedNum}, got {actualNum}");
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                Assert.Equal(expected.GetBoolean(), actual.GetBoolean());
                break;

            case JsonValueKind.Null:
                break;

            default:
                throw new InvalidOperationException($"Unexpected JSON type at {path}: {actual.ValueKind}");
        }
    }
}

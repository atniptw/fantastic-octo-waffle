# Data Models (C# Design)

 

## Thunderstore DTOs
- ThunderstorePackage
- PackageVersion
- PackageCategory
- CommunityInfo

Types: use `Uri`, `Guid`, `List<T>`, `DateTime`. Map v1 JSON fields verbatim for `System.Text.Json`.

## Unity Asset Parsing Models
- BundleNode
- StreamingInfo
- VertexChannel, StreamInfo, VertexData
- PackedBitVector
- MeshData (flat arrays for geometry)

## Three.js Export Models
- ThreeJsGeometry (positions, indices, normals, uv)
- ThreeJsMesh (name, geometry, material)

### ThreeJsGeometry (schema)
- positions: Float32Array (XYZ)
- indices: Uint16Array | Uint32Array (triangle list)
- normals?: Float32Array
- uvs?: Float32Array (UV)
- groups?: List<{ start: int, count: int, materialIndex: int }>
- vertexCount: int
- triangleCount: int

### Material Options
- color: string | int (hex)
- wireframe: bool
- metalness?: float
- roughness?: float

### Conventions
- Winding: counter-clockwise preferred (Three.js defaults)
- Coordinate system: start as-is; if mirrored, flip Z or adjust winding
- Units: keep source units; scale via material or transforms later

## Worker Cache Models
- CachedPackageList
- CachedCategories

## JSON Mapping Notes
- icon/package_url/download_url → `Uri`
- uuid4 → `Guid`
- Arrays → `List<T>`

## Serialization Attributes
Use `System.Text.Json` with `JsonPropertyName` to match v1 fields.

```csharp
using System.Text.Json.Serialization;

public sealed class ThunderstorePackage
{
	[JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
	[JsonPropertyName("full_name")] public string FullName { get; set; } = string.Empty;
	[JsonPropertyName("owner")] public string Owner { get; set; } = string.Empty;
	[JsonPropertyName("package_url")] public Uri PackageUrl { get; set; } = new("https://thunderstore.io/");
	[JsonPropertyName("icon")] public Uri? Icon { get; set; }
	[JsonPropertyName("rating_score")] public double RatingScore { get; set; }
	[JsonPropertyName("is_deprecated")] public bool IsDeprecated { get; set; }
	[JsonPropertyName("has_nsfw_content")] public bool HasNsfwContent { get; set; }
	[JsonPropertyName("date_updated")] public DateTime DateUpdated { get; set; }
	[JsonPropertyName("categories")] public List<string> Categories { get; set; } = new();
	[JsonPropertyName("versions")] public List<PackageVersion> Versions { get; set; } = new();
}

public sealed class PackageVersion
{
	[JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
	[JsonPropertyName("full_name")] public string FullName { get; set; } = string.Empty;
	[JsonPropertyName("version_number")] public string VersionNumber { get; set; } = string.Empty;
	[JsonPropertyName("download_url")] public Uri DownloadUrl { get; set; } = new("https://thunderstore.io/");
	[JsonPropertyName("file_size")] public long? FileSize { get; set; }
	[JsonPropertyName("dependencies")] public List<string> Dependencies { get; set; } = new();
}
```

## File Index Models
- FileIndexItem
	- file_name: string
	- size_bytes: long
	- type: enum { UnityFS, SerializedFile, Resource }
	- renderable: bool (true if shallow scan finds any Mesh objects)
- FileIndex
	- items: List<FileIndexItem>
	- source: full_name + version
	- created_at: DateTime

## Download & Error Models
- DownloadMeta
	- sizeBytes: long
	- filename: string
- ErrorResponse
	- error: string
	- status: int

## Progress Events
- DownloadProgress
	- receivedBytes: long
	- totalBytes: long?
- ParseProgress
	- stage: enum { Downloading, Indexing, Parsing, Rendering }
	- message: string

## Validation
- Save reference JSON
- Diff serialized DTOs vs reference
- Tolerate float precision in geometry
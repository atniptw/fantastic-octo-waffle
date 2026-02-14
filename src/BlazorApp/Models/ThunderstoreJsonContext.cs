using System.Text.Json.Serialization;

namespace BlazorApp.Models;

[JsonSerializable(typeof(List<ThunderstorePackage>))]
[JsonSerializable(typeof(ThunderstorePackage))]
[JsonSerializable(typeof(PackageVersion))]
public sealed partial class ThunderstoreJsonContext : JsonSerializerContext
{
}

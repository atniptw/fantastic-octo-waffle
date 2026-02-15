namespace BlazorApp.Services;

public static class DecorationNameFormatter
{
    private static readonly string[] Tags =
    [
        "head",
        "neck",
        "body",
        "hip",
        "leftarm",
        "rightarm",
        "leftleg",
        "rightleg",
        "world"
    ];

    public static string GetDisplayName(string path)
    {
        var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return path;
        }

        return StripTrailingTag(fileName);
    }

    private static string StripTrailingTag(string fileName)
    {
        foreach (var tag in Tags)
        {
            if (fileName.EndsWith("_" + tag, StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith("-" + tag, StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith("." + tag, StringComparison.OrdinalIgnoreCase))
            {
                return fileName[..^(tag.Length + 1)];
            }
        }

        return fileName;
    }
}

using RepoMod.Parser.Abstractions;
using RepoMod.Parser.Contracts;

namespace RepoMod.Parser.Implementation;

public sealed class ModParser : IModParser
{
    public CosmeticMetadata ExtractMetadata(string bundleFileName)
    {
        if (string.IsNullOrWhiteSpace(bundleFileName))
        {
            return new CosmeticMetadata("Unknown", null, null, "unknown");
        }

        var name = Path.GetFileNameWithoutExtension(bundleFileName);
        var normalized = name.Trim();

        return new CosmeticMetadata(normalized, null, null, InferSlotTag(normalized));
    }

    private static string InferSlotTag(string value)
    {
        var lowered = value.ToLowerInvariant();

        if (lowered.EndsWith("_head", StringComparison.Ordinal)) return "head";
        if (lowered.EndsWith("_neck", StringComparison.Ordinal)) return "neck";
        if (lowered.EndsWith("_body", StringComparison.Ordinal)) return "body";
        if (lowered.EndsWith("_hip", StringComparison.Ordinal)) return "hip";
        if (lowered.EndsWith("_leftarm", StringComparison.Ordinal)) return "leftarm";
        if (lowered.EndsWith("_rightarm", StringComparison.Ordinal)) return "rightarm";
        if (lowered.EndsWith("_leftleg", StringComparison.Ordinal)) return "leftleg";
        if (lowered.EndsWith("_rightleg", StringComparison.Ordinal)) return "rightleg";
        if (lowered.EndsWith("_world", StringComparison.Ordinal)) return "world";

        return "unknown";
    }
}

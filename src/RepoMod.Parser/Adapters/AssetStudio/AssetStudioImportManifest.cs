namespace RepoMod.Parser.Adapters.AssetStudio;

public static class AssetStudioImportManifest
{
    public const string SourceRepository = "https://github.com/aelurum/AssetStudio";

    public const string SourceCommitSha = "TODO_PIN_COMMIT_SHA";

    public static readonly string[] AllowedSourcePaths =
    [
        "TODO/AssetStudio/ParserPath.cs"
    ];

    public static readonly string[] ExcludedSourceRoots =
    [
        "AssetStudio.GUI",
        "AssetStudioCLI",
        "AssetStudioFBXNative",
        "AssetStudio.Utility/FMOD Studio API"
    ];

    public static readonly string[] ForbiddenInteropPatterns =
    [
        "DllImport",
        "LibraryImport",
        "System.Net.Http",
        "System.Net.Sockets"
    ];
}

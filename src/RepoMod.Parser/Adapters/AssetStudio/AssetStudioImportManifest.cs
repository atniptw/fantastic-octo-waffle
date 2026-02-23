namespace RepoMod.Parser.Adapters.AssetStudio;

public static class AssetStudioImportManifest
{
    public const string SourceRepository = "https://github.com/aelurum/AssetStudio";

    public const string SourceCommitSha = "6b66ec74674f61d7b331d0766fc38511e9c885f3";

    public static readonly string[] AllowedSourcePaths =
    [
        "AssetStudio/BuildTarget.cs",
        "AssetStudio/Brotli/*.cs",
        "AssetStudio/BundleCompression/Oodle/Oodle.cs",
        "AssetStudio/BundleCompression/SevenZipLzma/7zip/**/*.cs",
        "AssetStudio/BundleCompression/SevenZipLzma/SevenZipLzma.cs",
        "AssetStudio/BundleDecompressionHelper.cs",
        "AssetStudio/BundleFile.cs",
        "AssetStudio/ClassIDType.cs",
        "AssetStudio/ColorConsole.cs",
        "AssetStudio/ColorConsoleHelper.cs",
        "AssetStudio/CustomOptions/CustomBundleOptions.cs",
        "AssetStudio/CustomOptions/ImportOptions.cs",
        "AssetStudio/EndianBinaryReader.cs",
        "AssetStudio/EndianSpanReader.cs",
        "AssetStudio/EndianType.cs",
        "AssetStudio/FileIdentifier.cs",
        "AssetStudio/FileReader.cs",
        "AssetStudio/FileType.cs",
        "AssetStudio/ILogger.cs",
        "AssetStudio/ImportHelper.cs",
        "AssetStudio/Logger.cs",
        "AssetStudio/ObjectInfo.cs",
        "AssetStudio/Progress.cs",
        "AssetStudio/ResourceReader.cs",
        "AssetStudio/SerializedFileHeader.cs",
        "AssetStudio/StreamFile.cs",
        "AssetStudio/TempFileStream.cs",
        "AssetStudio/UnityVersion.cs",
        "AssetStudio/WebFile.cs"
    ];

    public static readonly string[] CandidateBatch1SourcePaths =
    [
        "AssetStudio/AssetsManager.cs",
        "AssetStudio/BundleFile.cs",
        "AssetStudio/BundleDecompressionHelper.cs",
        "AssetStudio/FileReader.cs",
        "AssetStudio/FileType.cs",
        "AssetStudio/ImportHelper.cs",
        "AssetStudio/ObjectReader.cs",
        "AssetStudio/ObjectInfo.cs",
        "AssetStudio/SerializedFile.cs",
        "AssetStudio/SerializedFileHeader.cs",
        "AssetStudio/FileIdentifier.cs",
        "AssetStudio/EndianBinaryReader.cs",
        "AssetStudio/EndianSpanReader.cs",
        "AssetStudio/ResourceReader.cs",
        "AssetStudio/TypeTree.cs",
        "AssetStudio/TypeTreeNode.cs",
        "AssetStudio/TypeTreeHelper.cs",
        "AssetStudio/ClassIDType.cs",
        "AssetStudio/BuildTarget.cs"
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

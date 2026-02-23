using RepoMod.Parser.Contracts;

namespace RepoMod.Parser.Abstractions;

public interface IModParser
{
    CosmeticMetadata ExtractMetadata(string bundleFileName);
}

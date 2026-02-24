using RepoMod.Parser.Contracts;

namespace RepoMod.Parser.Abstractions;

public interface ISceneExtractor
{
    ParseSceneResult ParseUnityPackage(string unityPackagePath);

    ParseSceneResult ParseCosmeticBundle(string bundlePath);
}

using RepoMod.Parser.Contracts;

namespace RepoMod.Parser.Abstractions;

public interface ISceneExtractor
{
    ParseSceneResult ParseUnityPackage(string unityPackagePath);

    ParseSceneResult ParseUnityPackage(byte[] unityPackageBytes, string sourceName);

    ParseSceneResult ParseCosmeticBundle(string bundlePath);

    ParseSceneResult ParseCosmeticBundle(byte[] bundleBytes, string sourceName);
}

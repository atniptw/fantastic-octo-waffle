namespace UnityAssetParser;

public sealed class UnityPackageParser
{
    public BaseAssetsContext Parse(byte[] packageBytes)
    {
        if (packageBytes is null)
        {
            throw new ArgumentNullException(nameof(packageBytes));
        }

        return new BaseAssetsContext();
    }
}

using System;
using System.IO;
using System.IO.Compression;

namespace UnityAssetParser;

public sealed class UnityPackageParser
{
    public BaseAssetsContext Parse(byte[] packageBytes)
    {
        if (packageBytes is null)
        {
            throw new ArgumentNullException(nameof(packageBytes));
        }

        var context = new BaseAssetsContext();
        var tarBytes = TryDecompressGzip(packageBytes, context);
        var container = new ParsedContainer("unitypackage", ContainerKind.UnityPackageTar, tarBytes.Length);
        foreach (var entry in TarReader.EnumerateEntries(tarBytes))
        {
            container.Entries.Add(new ContainerEntry(entry.Name, entry.Offset, entry.Size, 0));

            if (entry.Name.EndsWith("/asset", StringComparison.OrdinalIgnoreCase))
            {
                var assetBytes = new byte[entry.Size];
                Buffer.BlockCopy(tarBytes, entry.Offset, assetBytes, 0, entry.Size);
                SkeletonParser.Parse(assetBytes, entry.Name, context);
            }
        }
        context.Containers.Add(container);

        return context;
    }

    private static byte[] TryDecompressGzip(byte[] input, BaseAssetsContext context)
    {
        if (input.Length < 2 || input[0] != 0x1F || input[1] != 0x8B)
        {
            return input;
        }

        try
        {
            using var source = new MemoryStream(input, writable: false);
            using var gzip = new GZipStream(source, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }
        catch (InvalidDataException)
        {
            context.Warnings.Add("Failed to decompress unitypackage gzip payload; treating as raw tar.");
            return input;
        }
    }
}

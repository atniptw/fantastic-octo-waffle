using System.Buffers.Binary;
using System.Text;

namespace UnityAssetParser;

public sealed class HhhParser
{
    public byte[] ConvertToGlb(byte[] hhhBytes, BaseAssetsContext? baseAssets = null)
    {
        if (hhhBytes is null)
        {
            throw new ArgumentNullException(nameof(hhhBytes));
        }

        return GlbBuilder.BuildMinimal();
    }

    private static class GlbBuilder
    {
        private static readonly byte[] JsonBytes = Encoding.UTF8.GetBytes(
            "{\"asset\":{\"version\":\"2.0\",\"generator\":\"UnityAssetParser.Stub\"}}"
        );

        public static byte[] BuildMinimal()
        {
            var jsonPadding = (4 - (JsonBytes.Length % 4)) % 4;
            var jsonLength = JsonBytes.Length + jsonPadding;
            var totalLength = 12 + 8 + jsonLength;

            var buffer = new byte[totalLength];
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, 4), 0x46546C67);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4, 4), 2);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(8, 4), (uint)totalLength);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(12, 4), (uint)jsonLength);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(16, 4), 0x4E4F534A);

            JsonBytes.CopyTo(buffer, 20);
            for (var i = 0; i < jsonPadding; i++)
            {
                buffer[20 + JsonBytes.Length + i] = 0x20;
            }

            return buffer;
        }
    }
}

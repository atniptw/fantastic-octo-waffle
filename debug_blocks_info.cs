// Compile and run: csi debug_blocks_info.cs
using System;
using System.IO;
using System.Collections.Generic;

// Minimal bundle reading to see what's happening
var bundlePath = "Tests/UnityAssetParser.Tests/Fixtures/RealBundles/Cigar_neck.hhh";
using var f = File.OpenRead(bundlePath);
using var br = new BinaryReader(f);

// Read header
string sig = new string(br.ReadChars(4)); // "UnityFS"
uint version = ReadBigEndianUInt32(br);  // 0x00000006
string player_version = ReadCString(br);
string engine_version = ReadCString(br);
long bundle_size = ReadBigEndianInt64(br);
uint compressed_size = ReadBigEndianUInt32(br);
uint uncompressed_size = ReadBigEndianUInt32(br);
uint flags = ReadBigEndianUInt32(br);

Console.WriteLine($"Signature: {sig}");
Console.WriteLine($"Version: {version}");
Console.WriteLine($"Bundle size: {bundle_size}");
Console.WriteLine($"Compressed size: {compressed_size}");
Console.WriteLine($"Uncompressed size: {uncompressed_size}");
Console.WriteLine($"Flags: 0x{flags:X8}");
Console.WriteLine($"Stream position after header: {f.Position}");

// The BlocksInfo bytes should start here (or at the end depending on flags)
if ((flags & 0x80) != 0) // BlocksInfoAtTheEnd
{
    Console.WriteLine("BlocksInfo at end of file");
    f.Seek(-compressed_size, SeekOrigin.End);
}

Console.WriteLine($"Reading {compressed_size} bytes of BlocksInfo...");
byte[] blocksInfoCompressed = br.ReadBytes((int)compressed_size);
Console.WriteLine($"Read {blocksInfoCompressed.Length} bytes");

// Try to decompress (skip for now, just see the raw data)
Console.WriteLine($"First 100 bytes of compressed data:");
for (int i = 0; i < Math.Min(100, blocksInfoCompressed.Length); i++)
{
    Console.Write($"{blocksInfoCompressed[i]:X2} ");
    if ((i + 1) % 16 == 0) Console.WriteLine();
}
Console.WriteLine();

static uint ReadBigEndianUInt32(BinaryReader br) => 
    ((uint)br.ReadByte() << 24) | ((uint)br.ReadByte() << 16) | ((uint)br.ReadByte() << 8) | br.ReadByte();

static long ReadBigEndianInt64(BinaryReader br) =>
    ((long)ReadBigEndianUInt32(br) << 32) | ReadBigEndianUInt32(br);

static string ReadCString(BinaryReader br)
{
    var chars = new List<char>();
    while (true)
    {
        char c = br.ReadChar();
        if (c == '\0') break;
        chars.Add(c);
    }
    return new string(chars.ToArray());
}

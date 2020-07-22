using System;
using System.IO;

namespace ZstdSharp.Sandbox
{
    class Program
    {
        static void Main(string[] args)
        {
            byte[] uncompressed = File.ReadAllBytes("mozilla");

            Memory<byte> compressedMemory = Zstd.Compress(uncompressed.AsMemory());
            byte[] compressed = Zstd.Compress(uncompressed);

            byte[] uncompressed2 = Zstd.Decompress(compressed, uncompressed.Length);
            Memory<byte> uncompressedMemory = Zstd.Decompress(compressedMemory, uncompressed.Length);

            ZstdStream stream = new ZstdStream(new MemoryStream(), ZstdStreamMode.Compress);
        }
    }
}

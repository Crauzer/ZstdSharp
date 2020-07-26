using System;
using System.IO;

namespace ZstdSharp.Sandbox
{
    class Program
    {
        static void Main(string[] args)
        {
            byte[] uncompressed = File.ReadAllBytes("mozilla");

            //Memory<byte> compressedMemory = Zstd.Compress(uncompressed.AsMemory());
            byte[] compressed2 = Zstd.Compress(uncompressed);
            //
            //byte[] uncompressed2 = Zstd.Decompress(compressed, uncompressed.Length);
            //Memory<byte> uncompressedMemory = Zstd.Decompress(compressedMemory, uncompressed.Length);
            //
            //ZstdStream stream = new ZstdStream(new MemoryStream(), ZstdStreamMode.Compress);

            Memory<byte> compressed;
            using (ZstdCompressionContext context = new ZstdCompressionContext())
            {
                compressed = context.Compress(uncompressed.AsMemory());
            }

            Memory<byte> uncompressed2;
            using (ZstdDecompressionContext context = new ZstdDecompressionContext())
            {
                uncompressed2 = context.Decompress(compressed, uncompressed.Length);
            }
        }
    }
}

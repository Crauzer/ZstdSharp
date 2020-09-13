using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ZstdSharp.Sandbox
{
    class Program
    {
        static void Main(string[] args)
        {
            using (MemoryStream compressed = new MemoryStream())
            {
                byte[] uncompressedArr = File.ReadAllBytes("mozilla");

                using (ZstdStream zstdStream = new ZstdStream(compressed, ZstdStreamMode.Compress, leaveOpen: true))
                {
                    zstdStream.Write(uncompressedArr, 0, uncompressedArr.Length);
                }

                File.WriteAllBytes("mozilla2_zstd", Zstd.Decompress(compressed.ToArray(), uncompressedArr.Length));

                using (MemoryStream uncompressed = new MemoryStream())
                {
                    compressed.Position = 0;

                    using (ZstdStream zstdStream = new ZstdStream(compressed, ZstdStreamMode.Decompress))
                    {
                        zstdStream.CopyTo(uncompressed);
                    }

                    File.WriteAllBytes("mozilla2", uncompressed.ToArray());
                }
                
            }
        }
    }
}

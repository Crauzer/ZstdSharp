using NUnit.Framework;
using System;
using System.IO;
using System.Linq;

namespace ZstdSharp.Tests
{
    public class Tests
    {
        private byte[] _saoRaw;

        [SetUp]
        public void Setup()
        {
            // "sao" is a Star Catalog used by the Silesia compression corpus to compare compression algorithms
            this._saoRaw = File.ReadAllBytes("sao");
        }

        [Test]
        public void TestImmediateCompression()
        {
            byte[] compressedByteArray = Zstd.Compress(this._saoRaw);
            Memory<byte> compressedMemory = Zstd.Compress(this._saoRaw.AsMemory());

            Assert.AreEqual(compressedByteArray.Length, compressedMemory.Length, "compressed byte[] length does not match compressed Memory<byte> length");

            byte[] compressedMemoryArray = compressedMemory.ToArray();
            Assert.IsTrue(compressedMemoryArray.SequenceEqual(compressedByteArray), "compressed byte[] content does not match compressed Memory<byte> content");

            Assert.Pass();
        }

        [Test]
        public void TestImmediateDecompression()
        {
            byte[] compressedByteArray = Zstd.Compress(this._saoRaw);
            Memory<byte> compressedMemory = Zstd.Compress(this._saoRaw.AsMemory());

            Assert.AreEqual(compressedByteArray.Length, compressedMemory.Length, "compressed byte[] length does not match compressed Memory<byte> length");

            byte[] compressedMemoryArray = compressedMemory.ToArray();
            Assert.IsTrue(compressedMemoryArray.SequenceEqual(compressedByteArray), "compressed byte[] content does not match compressed Memory<byte> content");


            // ------- DECOMPRESSION ------- \\
            byte[] decompressedByteArray = Zstd.Decompress(compressedByteArray, this._saoRaw.Length);
            Memory<byte> decompressedMemory = Zstd.Decompress(compressedMemory, this._saoRaw.Length);

            Assert.AreEqual(decompressedByteArray.Length, decompressedMemory.Length, "decompressed byte[] length does not match decompressed Memory<byte> length");

            byte[] decompressedMemoryArray = decompressedMemory.ToArray();
            Assert.IsTrue(decompressedMemoryArray.SequenceEqual(decompressedByteArray), "decompressed byte[] content does not match decompressed Memory<byte> content");

            Assert.IsTrue(decompressedByteArray.SequenceEqual(this._saoRaw), "decompressed byte[] does not match raw data content");
            Assert.IsTrue(decompressedMemoryArray.SequenceEqual(this._saoRaw), "decompressed Memory<byte> does not match raw data content");

            Assert.Pass();
        }
    }
}
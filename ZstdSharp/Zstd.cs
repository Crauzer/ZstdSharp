using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;

using Size = System.UIntPtr;

namespace ZstdSharp
{
    public static class Zstd
    {
        public static byte[] Compress(byte[] buffer, int compressionLevel = Native.ZSTD_CLEVEL_DEFAULT)
        {
            return Compress(buffer, 0, buffer.Length, compressionLevel);
        }
        public static byte[] Compress(byte[] buffer, int offset, int size, int compressionLevel = Native.ZSTD_CLEVEL_DEFAULT)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer), $"{nameof(buffer)} cannot be null");
            }

            ValidateCompressionArguments(buffer.Length, offset, size, compressionLevel);

            // Allocate compressed buffer
            Size compressionBound = Native.ZSTD_compressBound(new Size((uint)size));
            byte[] compressedBuffer = new byte[compressionBound.ToUInt32()];

            Size compressedBufferSize = Size.Zero;
            unsafe
            {
                fixed (byte* compressedBufferPointer = compressedBuffer)
                {
                    fixed (byte* uncompressedBufferPointer = buffer)
                    {
                        compressedBufferSize = Native.ZSTD_compress(
                            (IntPtr)compressedBufferPointer, compressionBound,
                            (IntPtr)(uncompressedBufferPointer + offset), new Size((uint)size),
                            compressionLevel);
                    }
                }
            }

            // Check for errors
            ThrowOnError(compressedBufferSize);

            // If compressionBound is same as the amount of compressed bytes then we can return the same array
            // otherwise we need to allocate a new one :/
            if (compressionBound == compressedBufferSize)
            {
                return compressedBuffer;
            }
            else
            {
                byte[] actualCompressedBuffer = new byte[compressedBufferSize.ToUInt32()];
                Array.Copy(compressedBuffer, actualCompressedBuffer, (long)compressedBufferSize);
                return actualCompressedBuffer;
            }
        }

        public static Memory<byte> Compress(ReadOnlyMemory<byte> memory, int compressionLevel = Native.ZSTD_CLEVEL_DEFAULT)
        {
            return Compress(memory, 0, memory.Length, compressionLevel);
        }
        public static Memory<byte> Compress(ReadOnlyMemory<byte> memory, int offset, int size, int compressionLevel = Native.ZSTD_CLEVEL_DEFAULT)
        {
            ValidateCompressionArguments(memory.Length, offset, size, compressionLevel);

            // Allocate compressed buffer
            Size compressionBound = Native.ZSTD_compressBound(new Size((uint)size));
            byte[] compressedBuffer = new byte[compressionBound.ToUInt32()];

            // Get handles to buffers
            using MemoryHandle compressedBufferHandle = compressedBuffer.AsMemory().Pin();
            using MemoryHandle uncompressedBufferHandle = memory.Pin();

            unsafe
            {
                // Get raw pointers from handles
                IntPtr compressedBufferPointer = new IntPtr(compressedBufferHandle.Pointer);
                IntPtr uncompressedBufferPointer = new IntPtr(uncompressedBufferHandle.Pointer);

                Size compressedBufferSize = Native.ZSTD_compress(
                    compressedBufferPointer, compressionBound,
                    uncompressedBufferPointer + offset, (Size)size,
                    compressionLevel);

                // Check for errors
                ThrowOnError(compressedBufferSize);

                return compressedBuffer.AsMemory(0, (int)compressedBufferSize.ToUInt32());
            }
        }

        public static byte[] Decompress(byte[] buffer, int uncompressedSize)
        {
            return Decompress(buffer, 0, buffer.Length, uncompressedSize);
        }
        public static byte[] Decompress(byte[] buffer, int offset, int size, int uncompressedSize)
        {
            ValidateDecompressionArguments(buffer.Length, offset, size, uncompressedSize);

            // Allocate uncompressed buffer
            byte[] uncompressedBuffer = new byte[uncompressedSize];

            Size uncompressedBufferSize = Size.Zero;
            unsafe
            {
                fixed (byte* uncompressedBufferPtr = uncompressedBuffer)
                {
                    fixed (byte* compressedBufferPtr = buffer)
                    {
                        uncompressedBufferSize = Native.ZSTD_decompress(
                            (IntPtr)uncompressedBufferPtr, (Size)uncompressedSize,
                            (IntPtr)(compressedBufferPtr + offset), (Size)size);
                    }
                }
            }

            // Check for errors
            ThrowOnError(uncompressedBufferSize);

            // Check for possiblity that user passed in higher than required uncompressed size
            if (uncompressedSize == uncompressedBufferSize.ToUInt32())
            {
                return uncompressedBuffer;
            }
            else
            {
                byte[] actualUncompressedBuffer = new byte[uncompressedBufferSize.ToUInt32()];
                Array.Copy(uncompressedBuffer, actualUncompressedBuffer, (long)uncompressedBufferSize.ToUInt64());
                return actualUncompressedBuffer;
            }
        }

        public static Memory<byte> Decompress(ReadOnlyMemory<byte> memory, int uncompressedSize)
        {
            return Decompress(memory, 0, memory.Length, uncompressedSize);
        }
        public static Memory<byte> Decompress(ReadOnlyMemory<byte> memory, int offset, int size, int uncompressedSize)
        {
            ValidateDecompressionArguments(memory.Length, offset, size, uncompressedSize);

            // Allocate uncompressed buffer
            byte[] uncompressedBuffer = new byte[uncompressedSize];

            // Get handles to buffers
            using MemoryHandle compressedBufferHandle = memory.Pin();
            using MemoryHandle uncompressedBufferHandle = uncompressedBuffer.AsMemory().Pin();

            unsafe
            {
                // Get pointers from handles
                IntPtr compressedBufferPointer = (IntPtr)compressedBufferHandle.Pointer;
                IntPtr uncompressedBufferPointer = (IntPtr)uncompressedBufferHandle.Pointer;

                Size uncompressedBufferSize = Native.ZSTD_decompress(
                    uncompressedBufferPointer, (Size)uncompressedSize,
                    compressedBufferPointer + offset, (Size)size);

                // Check for errors
                ThrowOnError(uncompressedBufferSize);

                return uncompressedBuffer.AsMemory(0, (int)uncompressedBufferSize);
            }
        }

        private static void ValidateCompressionArguments(int bufferLength, int offset, int size, int compressionLevel)
        {
            ValidateBufferParameters(bufferLength, offset, size);
            ThrowOnInvalidCompressionLevel(compressionLevel);
        }
        private static void ValidateDecompressionArguments(int bufferLength, int offset, int size, int uncompressedSize)
        {
            ValidateBufferParameters(bufferLength, offset, size);

            if (uncompressedSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(uncompressedSize), $"{nameof(uncompressedSize)} is <= 0");
            }
        }
        private static void ValidateBufferParameters(int bufferLength, int offset, int size)
        {
            if (offset < 0 || offset > bufferLength)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), $"{nameof(offset)}: {offset} is out of bounds of the buffer (size: {bufferLength})");
            }
            if (size < 0 || size > bufferLength)
            {
                throw new ArgumentOutOfRangeException(nameof(size), $"{nameof(size)}: {size} is out of bounds of the buffer (size: {bufferLength})");
            }
            if (offset + size > bufferLength)
            {
                throw new ArgumentException($"{nameof(offset)} + {nameof(size)} ({offset + size}) is out of bounds of the buffer (size: {bufferLength})");
            }
        }

        /// <summary>
        /// Throws an exception if <paramref name="errorCode"/> is a valid zstd error code
        /// </summary>
        internal static void ThrowOnError(Size errorCode)
        {
            if (Native.ZSTD_isError(errorCode) > 0)
            {
                string error = Marshal.PtrToStringAnsi(Native.ZSTD_getErrorName(errorCode));
                throw new Exception(error);
            }
        }
        internal static void ThrowOnInvalidCompressionLevel(int compressionLevel)
        {
            int minCompressionLevel = Native.ZSTD_minCLevel();
            int maxCompressionLevel = Native.ZSTD_maxCLevel();
            if (compressionLevel < minCompressionLevel || compressionLevel > maxCompressionLevel)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(compressionLevel), $"{nameof(compressionLevel)} must be between {minCompressionLevel} - {maxCompressionLevel}");
            }
        }
    }
}

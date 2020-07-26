using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;

using Size = System.UIntPtr;

namespace ZstdSharp
{
    public static class Zstd
    {
        /// <summary>
        /// The default zstd compression level
        /// </summary>
        public static int DefaultCompressionLevel => Native.ZSTD_CLEVEL_DEFAULT;

        /// <summary>
        /// A <see cref="Range"/> of all valid compression levels
        /// </summary>
        public static Range CompressionLevels => new Range(Native.ZSTD_minCLevel(), Native.ZSTD_maxCLevel());

        // -------------- COMPRESSION ---------------- \\

        /// <summary>
        /// Compresses <paramref name="buffer"/> using the specified <paramref name="compressionLevel"/>
        /// </summary>
        /// <param name="buffer">The buffer to compress</param>
        /// <param name="compressionLevel">The compression level to use</param>
        /// <returns>The compressed buffer</returns>
        /// <remarks>
        /// This method will usually cause a double heap allocation of the compressed buffer,
        /// it is recommended to use <see cref="Compress(ReadOnlyMemory{byte}, int)"/> if you want to avoid this
        /// </remarks>
        public static byte[] Compress(byte[] buffer, int compressionLevel = Native.ZSTD_CLEVEL_DEFAULT)
        {
            return Compress(buffer, 0, buffer.Length, compressionLevel);
        }

        /// <summary>
        /// Compresses data in <paramref name="buffer"/> starting at <paramref name="offset"/> with <paramref name="size"/>
        /// using the specified <paramref name="compressionLevel"/>
        /// </summary>
        /// <param name="buffer">The buffer to compress</param>
        /// <param name="offset">The offset at which the data to compress starts</param>
        /// <param name="size">The size of the data to compress</param>
        /// <param name="compressionLevel">The compression level to use</param>
        /// <returns>The compressed buffer</returns>
        /// <remarks>
        /// This method will usually cause a double heap allocation of the compressed buffer,
        /// it is recommended to use <see cref="Compress(ReadOnlyMemory{byte}, int, int, int)"/> if you want to avoid this.
        /// </remarks>
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
                fixed (byte* uncompressedBufferPointer = buffer)
                {
                    compressedBufferSize = Native.ZSTD_compress(
                        (IntPtr)compressedBufferPointer, compressionBound,
                        (IntPtr)(uncompressedBufferPointer + offset), (Size)size,
                        compressionLevel);
                }
            }

            // Check for errors
            ThrowOnError(compressedBufferSize);

            // If compressionBound is same as the amount of compressed bytes then we can return the same array
            // otherwise we need to allocate a new one :/
            if (compressionBound != compressedBufferSize)
            {
                Array.Resize(ref compressedBuffer, (int)compressedBufferSize);
            }

            return compressedBuffer;
        }

        /// <summary>
        /// Compresses <paramref name="memory"/> using the specified <paramref name="compressionLevel"/>
        /// </summary>
        /// <param name="memory">The buffer to compress</param>
        /// <param name="compressionLevel">The compression level to use</param>
        /// <returns>The compressed buffer wrapped in <see cref="Memory{Byte}"/></returns>
        public static Memory<byte> Compress(ReadOnlyMemory<byte> memory, int compressionLevel = Native.ZSTD_CLEVEL_DEFAULT)
        {
            return Compress(memory, 0, memory.Length, compressionLevel);
        }

        /// <summary>
        /// Compresses data in <paramref name="memory"/> starting at <paramref name="offset"/> with <paramref name="size"/>
        /// using the specified <paramref name="compressionLevel"/>
        /// </summary>
        /// <param name="memory">The buffer to compress</param>
        /// <param name="offset">The offset at which the data to compress starts</param>
        /// <param name="size">The size of the data to compress</param> 
        /// <param name="compressionLevel">The compression level to use</param>
        /// <returns>The compressed buffer wrapped in <see cref="Memory{Byte}"/></returns>
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

        // -------------- DECOMPRESSION ---------------- \\

        /// <summary>
        /// Decompresses <paramref name="buffer"/>
        /// </summary>
        /// <param name="buffer">The compressed data buffer</param>
        /// <param name="uncompressedSize">Uncompressed size of <paramref name="buffer"/></param>
        /// <returns>The uncompressed buffer</returns>
        /// <remarks>If you do not know the uncompressed size then it is recommended to use <see cref="ZstdStream"/></remarks>
        public static byte[] Decompress(byte[] buffer, int uncompressedSize)
        {
            return Decompress(buffer, 0, buffer.Length, uncompressedSize);
        }

        /// <summary>
        /// Decompresses data with <paramref name="size"/> at <paramref name="offset"/> in <paramref name="buffer"/>
        /// </summary>
        /// <param name="buffer">The compressed data buffer</param>
        /// <param name="offset">The offset of the compressed data</param>
        /// <param name="size">The size of the compressed data</param>
        /// <param name="uncompressedSize">Uncompressed size of <paramref name="buffer"/></param>
        /// <returns>The uncompressed buffer</returns>
        /// <remarks>If you do not know the uncompressed size then it is recommended to use <see cref="ZstdStream"/></remarks>
        public static byte[] Decompress(byte[] buffer, int offset, int size, int uncompressedSize)
        {
            ValidateDecompressionArguments(buffer.Length, offset, size, uncompressedSize);

            // Allocate uncompressed buffer
            byte[] uncompressedBuffer = new byte[uncompressedSize];

            Size uncompressedBufferSize = Size.Zero;
            unsafe
            {
                fixed (byte* uncompressedBufferPtr = uncompressedBuffer)
                fixed (byte* compressedBufferPtr = buffer)
                {
                    uncompressedBufferSize = Native.ZSTD_decompress(
                        (IntPtr)uncompressedBufferPtr, (Size)uncompressedSize,
                        (IntPtr)(compressedBufferPtr + offset), (Size)size);
                }
            }

            // Check for errors
            ThrowOnError(uncompressedBufferSize);

            // Check for possiblity that user passed in higher than required uncompressed size
            if (uncompressedSize != uncompressedBufferSize.ToUInt32())
            {
                Array.Resize(ref uncompressedBuffer, (int)uncompressedBufferSize);
            }

            return uncompressedBuffer;
        }

        /// <summary>
        /// Decompresses <paramref name="memory"/>
        /// </summary>
        /// <param name="memory">The compressed data buffer</param>
        /// <param name="uncompressedSize">Uncompressed size of <paramref name="buffer"/></param>
        /// <returns>The uncompressed buffer</returns>
        /// <remarks>If you do not know the uncompressed size then it is recommended to use <see cref="ZstdStream"/></remarks>
        public static Memory<byte> Decompress(ReadOnlyMemory<byte> memory, int uncompressedSize)
        {
            return Decompress(memory, 0, memory.Length, uncompressedSize);
        }

        /// <summary>
        /// Decompresses data with <paramref name="size"/> at <paramref name="offset"/> in <paramref name="memory"/>
        /// </summary>
        /// <param name="memory">The compressed data buffer</param>
        /// <param name="offset">The offset of the compressed data</param>
        /// <param name="size">The size of the compressed data</param>
        /// <param name="uncompressedSize">Uncompressed size of <paramref name="buffer"/></param>
        /// <returns>The uncompressed buffer</returns>
        /// <remarks>If you do not know the uncompressed size then it is recommended to use <see cref="ZstdStream"/></remarks>
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

        // -------------- HELPER FUNCTIONS ---------------- \\

        internal static void ValidateCompressionArguments(int bufferLength, int offset, int size, int compressionLevel)
        {
            ValidateBufferParameters(bufferLength, offset, size);
            ThrowOnInvalidCompressionLevel(compressionLevel);
        }
        internal static void ValidateDecompressionArguments(int bufferLength, int offset, int size, int uncompressedSize)
        {
            ValidateBufferParameters(bufferLength, offset, size);

            if (uncompressedSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(uncompressedSize), $"{nameof(uncompressedSize)} is <= 0");
            }
        }
        internal static void ValidateBufferParameters(int bufferLength, int offset, int size)
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

    public enum ZstdResetDirective
    {
        ResetSessionOnly = 1,
        ResetParameters = 2,
        ResetSessionAndParameters = 3
    }
}

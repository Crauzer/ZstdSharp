using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

using Size = System.UIntPtr;

namespace ZstdSharp
{
    public class ZstdDecompressionContext : IDisposable
    {
        /// <summary>
        /// The dictionary that's used for decompression
        /// </summary>
        public ZstdDictionary Dictionary { get; set; }

        private readonly IntPtr _context;

        private bool _isDisposed;

        public ZstdDecompressionContext() : this(null) { }
        public ZstdDecompressionContext(ZstdDictionary dictionary)
        {
            this.Dictionary = dictionary;
            this._context = Native.ZSTD_createDCtx();
        }

        ~ZstdDecompressionContext()
        {
            Dispose(false);
        }

        // ------------ DECOMPRESSION ------------ \\

        /// <summary>
        /// Decompresses <paramref name="buffer"/> using this <see cref="ZstdDecompressionContext"/>
        /// </summary>
        /// <param name="buffer">The compressed data buffer</param>
        /// <param name="uncompressedSize">Uncompressed size of <paramref name="buffer"/></param>
        /// <returns>The uncompressed buffer</returns>
        /// <remarks>If you do not know the uncompressed size then it is recommended to use <see cref="ZstdStream"/></remarks>
        public byte[] Decompress(byte[] buffer, int uncompressedSize)
        {
            return Decompress(buffer, 0, buffer.Length, uncompressedSize);
        }

        /// <summary>
        /// Decompresses data with <paramref name="size"/> at <paramref name="offset"/> in <paramref name="buffer"/> using this <see cref="ZstdDecompressionContext"/>
        /// </summary>
        /// <param name="buffer">The compressed data buffer</param>
        /// <param name="offset">The offset of the compressed data</param>
        /// <param name="size">The size of the compressed data</param>
        /// <param name="uncompressedSize">Uncompressed size of <paramref name="buffer"/></param>
        /// <returns>The uncompressed buffer</returns>
        /// <remarks>If you do not know the uncompressed size then it is recommended to use <see cref="ZstdStream"/></remarks>
        public byte[] Decompress(byte[] buffer, int offset, int size, int uncompressedSize)
        {
            Zstd.ValidateDecompressionArguments(buffer.Length, offset, size, uncompressedSize);

            // Allocate uncompressed buffer
            byte[] uncompressedBuffer = new byte[uncompressedSize];

            Size uncompressedBufferSize = Size.Zero;
            unsafe
            {
                fixed (byte* uncompressedBufferPtr = uncompressedBuffer)
                fixed (byte* compressedBufferPtr = buffer)
                {
                    if (this.Dictionary == null)
                    {
                        uncompressedBufferSize = Native.ZSTD_decompressDCtx(
                            this._context,
                            (IntPtr)uncompressedBufferPtr, (Size)uncompressedSize,
                            (IntPtr)(compressedBufferPtr + offset), (Size)size);
                    }
                    else
                    {
                        uncompressedBufferSize = Native.ZSTD_decompress_usingDDict(
                            this._context,
                            (IntPtr)uncompressedBufferPtr, (Size)uncompressedSize,
                            (IntPtr)(compressedBufferPtr + offset), (Size)size,
                            this.Dictionary.GetDecompressionDictionary());
                    }

                }
            }

            // Check for errors
            Zstd.ThrowOnError(uncompressedBufferSize);

            // Check for possiblity that user passed in higher than required uncompressed size
            if (uncompressedSize != uncompressedBufferSize.ToUInt32())
            {
                Array.Resize(ref uncompressedBuffer, (int)uncompressedBufferSize);
            }

            return uncompressedBuffer;
        }

        /// <summary>
        /// Decompresses <paramref name="memory"/> using this <see cref="ZstdDecompressionContext"/>
        /// </summary>
        /// <param name="memory">The compressed data buffer</param>
        /// <param name="uncompressedSize">Uncompressed size of <paramref name="buffer"/></param>
        /// <returns>The uncompressed buffer</returns>
        /// <remarks>If you do not know the uncompressed size then it is recommended to use <see cref="ZstdStream"/></remarks>
        public Memory<byte> Decompress(ReadOnlyMemory<byte> memory, int uncompressedSize)
        {
            return Decompress(memory, 0, memory.Length, uncompressedSize);
        }

        /// <summary>
        /// Decompresses data with <paramref name="size"/> at <paramref name="offset"/> in <paramref name="memory"/> using this <see cref="ZstdDecompressionContext"/>
        /// </summary>
        /// <param name="memory">The compressed data buffer</param>
        /// <param name="offset">The offset of the compressed data</param>
        /// <param name="size">The size of the compressed data</param>
        /// <param name="uncompressedSize">Uncompressed size of <paramref name="buffer"/></param>
        /// <returns>The uncompressed buffer</returns>
        /// <remarks>If you do not know the uncompressed size then it is recommended to use <see cref="ZstdStream"/></remarks>
        public Memory<byte> Decompress(ReadOnlyMemory<byte> memory, int offset, int size, int uncompressedSize)
        {
            Zstd.ValidateDecompressionArguments(memory.Length, offset, size, uncompressedSize);

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

                Size uncompressedBufferSize;
                if (this.Dictionary == null)
                {
                    uncompressedBufferSize = Native.ZSTD_decompressDCtx(
                        this._context,
                        uncompressedBufferPointer, (Size)uncompressedSize,
                        compressedBufferPointer + offset, (Size)size);
                }
                else
                {
                    uncompressedBufferSize = Native.ZSTD_decompress_usingDDict(
                        this._context,
                        uncompressedBufferPointer, (Size)uncompressedSize,
                        compressedBufferPointer + offset, (Size)size,
                        this.Dictionary.GetDecompressionDictionary());
                }
                

                // Check for errors
                Zstd.ThrowOnError(uncompressedBufferSize);

                return uncompressedBuffer.AsMemory(0, (int)uncompressedBufferSize);
            }
        }

        // ------------ PARAMETERS ------------ \\

        /// <summary>
        /// Sets a parameter value inside of this <see cref="ZstdDecompressionContext"/>
        /// </summary>
        /// <param name="parameter">The parameter to set</param>
        /// <param name="value">The value of the parameter</param>
        /// <remarks>
        /// <paramref name="value"/> need to be in a valid range or else it will get clamped,
        /// use <see cref="GetParameterBounds(ZstdDecompressionParameter)"/> to get a valid range for a parameter
        /// </remarks>
        public void SetParameter(ZstdDecompressionParameter parameter, int value)
        {
            Zstd.ThrowOnError(Native.ZSTD_DCtx_setParameter(this._context, parameter, value));
        }

        /// <summary>
        /// Gets the value range for <paramref name="parameter"/>
        /// </summary>
        public static Range GetParameterBounds(ZstdDecompressionParameter parameter)
        {
            ZstdBounds bounds = Native.ZSTD_dParam_getBounds(parameter);
            Zstd.ThrowOnError(bounds.ErrorCode);
            return bounds.GetRange();
        }

        /// <summary>
        /// Resets this <see cref="ZstdDecompressionContext"/>
        /// </summary>
        public void Reset(ZstdResetDirective resetDirective)
        {
            Zstd.ThrowOnError(Native.ZSTD_DCtx_reset(this._context, resetDirective));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!this._isDisposed)
            {
                Native.ZSTD_freeDCtx(this._context);

                this._isDisposed = true;
            }
        }
    }

    public enum ZstdDecompressionParameter : int
    {
        WindowLogMax = 100
    }
}

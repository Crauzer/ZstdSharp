using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

using Size = System.UIntPtr;

namespace ZstdSharp
{
    public class ZstdCompressionContext : IDisposable
    {
        /// <summary>
        /// The dictionary that's used for compression
        /// </summary>
        public ZstdDictionary Dictionary { get; set; }

        private readonly IntPtr _context;

        private bool _isDisposed;

        public ZstdCompressionContext() : this(null) { }
        public ZstdCompressionContext(ZstdDictionary dictionary)
        {
            this.Dictionary = dictionary;
            this._context = Native.ZSTD_createCCtx();
        }

        ~ZstdCompressionContext()
        {
            Dispose(false);
        }

        // ------------ COMPRESSION ------------ \\

        /// <summary>
        /// Compresses <paramref name="buffer"/> using the specified <paramref name="compressionLevel"/> 
        /// and parameters set on this <see cref="ZstdCompressionContext"/>
        /// </summary>
        /// <param name="buffer">The buffer to compress</param>
        /// <param name="compressionLevel">The compression level to use</param>
        /// <returns>The compressed buffer</returns>
        /// <remarks>
        /// This method will usually cause a double heap allocation of the compressed buffer,
        /// it is recommended to use <see cref="Compress(ReadOnlyMemory{byte}, int)"/> if you want to avoid this
        /// </remarks>
        public byte[] Compress(byte[] buffer, int compressionLevel = Native.ZSTD_CLEVEL_DEFAULT)
        {
            return Compress(buffer, 0, buffer.Length, compressionLevel);
        }

        /// <summary>
        /// Compresses data in <paramref name="buffer"/> starting at <paramref name="offset"/> with <paramref name="size"/>
        /// using the specified <paramref name="compressionLevel"/> and parameters set on this <see cref="ZstdCompressionContext"/>
        /// </summary>
        /// <param name="buffer">The buffer to compress</param>
        /// <param name="offset">The offset at which the data to compress starts</param>
        /// <param name="size">The size of the data to compress</param>
        /// <param name="compressionLevel">The compression level to use</param>
        /// <returns>The compressed buffer</returns>
        /// <remarks>
        /// This method will usually cause a double heap allocation of the compressed buffer,
        /// it is recommended to use <see cref="Compress(ReadOnlyMemory{byte}, int, int, int)"/> if you want to avoid this
        /// </remarks>
        public byte[] Compress(byte[] buffer, int offset, int size, int compressionLevel = Native.ZSTD_CLEVEL_DEFAULT)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer), $"{nameof(buffer)} cannot be null");
            }

            Zstd.ValidateCompressionArguments(buffer.Length, offset, size, compressionLevel);

            // Allocate compressed buffer
            Size compressionBound = Native.ZSTD_compressBound(new Size((uint)size));
            byte[] compressedBuffer = new byte[compressionBound.ToUInt32()];

            Size compressedBufferSize = Size.Zero;
            unsafe
            {
                fixed (byte* compressedBufferPointer = compressedBuffer)
                fixed (byte* uncompressedBufferPointer = buffer)
                {
                    if (this.Dictionary == null)
                    {
                        compressedBufferSize = Native.ZSTD_compress2(
                            this._context,
                            (IntPtr)compressedBufferPointer, compressionBound,
                            (IntPtr)(uncompressedBufferPointer + offset), (Size)size);
                    }
                    else
                    {
                        compressedBufferSize = Native.ZSTD_compress_usingCDict(
                            this._context,
                            (IntPtr)compressedBufferPointer, compressionBound,
                            (IntPtr)(uncompressedBufferPointer + offset), (Size)size,
                            this.Dictionary.GetCompressionDictionary(compressionLevel));
                    }
                }
            }

            // Check for errors
            Zstd.ThrowOnError(compressedBufferSize);

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
        /// and parameters set on this <see cref="ZstdCompressionContext"/>
        /// </summary>
        /// <param name="memory">The buffer to compress</param>
        /// <param name="compressionLevel">The compression level to use</param>
        /// <returns>The compressed buffer wrapped in <see cref="Memory{Byte}"/></returns>
        public Memory<byte> Compress(ReadOnlyMemory<byte> memory, int compressionLevel = Native.ZSTD_CLEVEL_DEFAULT)
        {
            return Compress(memory, 0, memory.Length, compressionLevel);
        }

        /// <summary>
        /// Compresses data in <paramref name="memory"/> starting at <paramref name="offset"/> with <paramref name="size"/>
        /// using the specified <paramref name="compressionLevel"/> and parameters set on this <see cref="ZstdCompressionContext"/>
        /// </summary>
        /// <param name="memory">The buffer to compress</param>
        /// <param name="offset">The offset at which the data to compress starts</param>
        /// <param name="size">The size of the data to compress</param> 
        /// <param name="compressionLevel">The compression level to use</param>
        /// <returns>The compressed buffer wrapped in <see cref="Memory{Byte}"/></returns>
        public Memory<byte> Compress(ReadOnlyMemory<byte> memory, int offset, int size, int compressionLevel = Native.ZSTD_CLEVEL_DEFAULT)
        {
            Zstd.ValidateCompressionArguments(memory.Length, offset, size, compressionLevel);

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

                Size compressedBufferSize;
                if (this.Dictionary == null)
                {
                    compressedBufferSize = Native.ZSTD_compressCCtx(
                        this._context,
                        compressedBufferPointer, compressionBound,
                        uncompressedBufferPointer + offset, (Size)size,
                        compressionLevel);
                }
                else
                {
                    compressedBufferSize = Native.ZSTD_compress_usingCDict(
                        this._context,
                        compressedBufferPointer, compressionBound,
                        uncompressedBufferPointer + offset, (Size)size,
                        this.Dictionary.GetCompressionDictionary(compressionLevel));
                }


                // Check for errors
                Zstd.ThrowOnError(compressedBufferSize);

                return compressedBuffer.AsMemory(0, (int)compressedBufferSize.ToUInt32());
            }
        }

        // ------------ PARAMETERS ------------ \\

        /// <summary>
        /// Sets a parameter value inside of this <see cref="ZstdCompressionContext"/>
        /// </summary>
        /// <param name="parameter">The parameter to set</param>
        /// <param name="value">The value of the parameter</param>
        /// <remarks>
        /// <paramref name="value"/> need to be in a valid range or else it will get clamped,
        /// use <see cref="GetParameterBounds(ZstdCompressionParameter)"/> to get a valid range for a parameter
        /// </remarks>
        public void SetParameter(ZstdCompressionParameter parameter, int value)
        {
            Zstd.ThrowOnError(Native.ZSTD_CCtx_setParameter(this._context, parameter, value));
        }

        /// <summary>
        /// Gets the value of a parameter
        /// </summary>
        public int GetParameter(ZstdCompressionParameter parameter)
        {
            int value = 0;

            unsafe
            {
                Zstd.ThrowOnError(Native.ZSTD_CCtx_getParameter(this._context, parameter, (IntPtr)(&value)));
            }

            return value;
        }

        /// <summary>
        /// Gets the value range for <paramref name="parameter"/>
        /// </summary>
        public static Range GetParameterBounds(ZstdCompressionParameter parameter)
        {
            ZstdBounds bounds = Native.ZSTD_cParam_getBounds(parameter);
            Zstd.ThrowOnError(bounds.ErrorCode);
            return bounds.GetRange();
        }

        /// <summary>
        /// Resets this <see cref="ZstdCompressionContext"/>
        /// </summary>
        public void Reset(ZstdResetDirective resetDirective)
        {
            Zstd.ThrowOnError(Native.ZSTD_CCtx_reset(this._context, resetDirective));
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
                Native.ZSTD_freeCCtx(this._context);

                this._isDisposed = true;
            }
        }
    }

    public enum ZstdCompressionParameter : int
    {
        CompressionLevel = 100,
        WindowLog = 101,
        HashLog = 102,
        ChainLog = 103,
        SearchLog = 104,
        MinMatch = 105,
        TargetLength = 106,
        Strategy = 107,

        EnableLongDistanceMatching = 160,
        LdmHashLog = 161,
        LdmMinMatch = 162,
        LdmBucketSizeLog = 163,
        LdmHashRateLog = 164,

        ContentSizeFlag = 200,
        ChecksumFlag = 201,
        DictIdFlag = 202,

        NbWorkers = 400,
        JobSize = 401,
        OverlapLog = 402
    }
    public enum ZstdCompressionStrategy : int
    {
        Fast = 1,
        DFast = 2,
        Greedy = 3,
        Lazy = 4,
        Lazy2 = 5,
        BtLazy2 = 6,
        BtOpt = 7,
        BtUltra = 8,
        BtUltra2 = 9
    }
}

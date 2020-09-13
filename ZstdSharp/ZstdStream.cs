using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ZstdSharp
{
    /// <summary>
    /// Zstandard Stream that can be used for compression and decompression
    /// </summary>
    public class ZstdStream : Stream
    {
        /// <summary>
        /// The <see cref="ZstdStreamMode"/> of this stream that indicates whether it's used for compression or decompression
        /// </summary>
        public ZstdStreamMode Mode { get; }

        /// <summary>
        /// The compression level to use for this <see cref="ZstdStream"/>
        /// </summary>
        public int CompressionLevel { get; }

        /// <summary>
        /// The <see cref="ZstdDictionary"/> that's being used by this <see cref="ZstdStream"/>
        /// </summary>
        public ZstdDictionary Dictionary { get; }

        public override bool CanRead => this._stream.CanRead && this.Mode == ZstdStreamMode.Decompress;
        public override bool CanSeek => false;
        public override bool CanWrite => this._stream.CanWrite && this.Mode == ZstdStreamMode.Compress;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        private readonly Stream _stream;
        private readonly bool _leaveOpen;

        private bool _isClosed;
        private bool _isDisposed;

        private byte[] _scratchData;
        private int _scratchDataSize;
        private int _scratchDataPosition;
        private bool _skipDataRead;
        private bool _isDataDepleted;

        private readonly uint _zstdStreamInputSize;
        private readonly uint _zstdStreamOutputSize;
        private readonly IntPtr _zstdStream;
        private ZstdBuffer _zstdInputBuffer = new ZstdBuffer();
        private ZstdBuffer _zstdOutputBuffer = new ZstdBuffer();

        public ZstdStream(Stream stream, int compressionLevel, bool leaveOpen = false) : this(stream, compressionLevel, null, leaveOpen) { }
        public ZstdStream(Stream stream, int compressionLevel, ZstdDictionary dictionary, bool leaveOpen = false)
            : this(stream, ZstdStreamMode.Compress, dictionary, leaveOpen)
        {
            Zstd.ThrowOnInvalidCompressionLevel(compressionLevel);

            this.CompressionLevel = compressionLevel;
        }
        public ZstdStream(Stream stream, ZstdStreamMode mode, bool leaveOpen = false) : this(stream, mode, null, leaveOpen) { }
        public ZstdStream(Stream stream, ZstdStreamMode mode, ZstdDictionary dictionary, bool leaveOpen = false)
        {
            this.Mode = mode;
            this.Dictionary = dictionary;
            this._stream = stream ?? throw new ArgumentNullException(nameof(stream));
            this._leaveOpen = leaveOpen;

            if (mode == ZstdStreamMode.Compress)
            {
                this._zstdStreamInputSize = Native.ZSTD_CStreamInSize().ToUInt32();
                this._zstdStreamOutputSize = Native.ZSTD_CStreamOutSize().ToUInt32();
                this._zstdStream = Native.ZSTD_createCStream();
                this._scratchData = new byte[this._zstdStreamOutputSize];
            }
            else if (mode == ZstdStreamMode.Decompress)
            {
                this._zstdStreamInputSize = Native.ZSTD_DStreamInSize().ToUInt32();
                this._zstdStreamOutputSize = Native.ZSTD_DStreamOutSize().ToUInt32();
                this._zstdStream = Native.ZSTD_createDStream();
                this._scratchData = new byte[this._zstdStreamInputSize];
            }

            InitializeZstdStream();
        }

        private void InitializeZstdStream()
        {
            if (this.Mode == ZstdStreamMode.Compress)
            {
                UIntPtr result;
                if (this.Dictionary == null)
                {
                    result = Native.ZSTD_initCStream(this._zstdStream, this.CompressionLevel);
                }
                else
                {
                    result = Native.ZSTD_initCStream_usingCDict(this._zstdStream, this.Dictionary.GetCompressionDictionary(this.CompressionLevel));
                }

                Zstd.ThrowOnError(result);
            }
            else if (this.Mode == ZstdStreamMode.Decompress)
            {
                UIntPtr result;
                if (this.Dictionary == null)
                {
                    result = Native.ZSTD_initDStream(this._zstdStream);
                }
                else
                {
                    result = Native.ZSTD_initDStream_usingDDict(this._zstdStream, this.Dictionary.GetDecompressionDictionary());
                }

                Zstd.ThrowOnError(result);
            }
        }

        public override void Flush()
        {
            if (this.Mode == ZstdStreamMode.Compress)
            {
                FlushZstdStream();

                this._stream.Flush();
            }
        }
        public override void Close()
        {
            if (!this._isClosed)
            {
                DisposeResources(true);

                this._isClosed = true;
                base.Close();
            }
        }
        private void DisposeResources(bool flush)
        {
            if (this.Mode == ZstdStreamMode.Compress)
            {
                if (flush)
                {
                    FlushZstdStream();
                    EndZstdStream();

                    this._stream.Flush();
                }

                Native.ZSTD_freeCStream(this._zstdStream);

                if (!this._leaveOpen) this._stream.Close();
            }
            else if (this.Mode == ZstdStreamMode.Decompress)
            {
                Native.ZSTD_freeDStream(this._zstdStream);

                if (!this._leaveOpen) this._stream.Close();
            }
        }
        private void FlushZstdStream()
        {
            unsafe
            {
                fixed(byte* scratchDataPr = this._scratchData)
                {
                    this._zstdOutputBuffer.Data = (IntPtr)scratchDataPr;
                    this._zstdOutputBuffer.Size = (UIntPtr)this._zstdStreamOutputSize;
                    this._zstdOutputBuffer.Position = UIntPtr.Zero;

                    Zstd.ThrowOnError(Native.ZSTD_flushStream(this._zstdStream, this._zstdOutputBuffer));

                    this._stream.Write(this._scratchData, 0, (int)this._zstdOutputBuffer.Position);
                }
            }
        }
        private void EndZstdStream()
        {
            unsafe
            {
                fixed (byte* scratchDataPr = this._scratchData)
                {
                    this._zstdOutputBuffer.Data = (IntPtr)scratchDataPr;
                    this._zstdOutputBuffer.Size = (UIntPtr)this._zstdStreamOutputSize;
                    this._zstdOutputBuffer.Position = UIntPtr.Zero;

                    Zstd.ThrowOnError(Native.ZSTD_endStream(this._zstdStream, this._zstdOutputBuffer));

                    this._stream.Write(this._scratchData, 0, (int)this._zstdOutputBuffer.Position);
                }
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if(!this.CanRead)
            {
                throw new NotSupportedException();
            }

            int bytesDecompressed = 0;

            unsafe
            {
                fixed (byte* outputBufferPtr = buffer)
                fixed (byte* scratchDataPtr = this._scratchData)
                {
                    while (count > 0)
                    {
                        int inputSize = this._scratchDataSize - this._scratchDataPosition;

                        if(inputSize <= 0 && !this._isDataDepleted && !this._skipDataRead)
                        {
                            this._scratchDataSize = this._stream.Read(this._scratchData, 0, (int)this._zstdStreamInputSize);
                            this._isDataDepleted = this._scratchDataSize <= 0;
                            this._scratchDataPosition = 0;
                            inputSize = this._scratchDataSize <= 0 ? 0 : this._scratchDataSize;

                            this._skipDataRead = true;
                        }

                        this._zstdInputBuffer.Data = inputSize <= 0 ? IntPtr.Zero : (IntPtr)scratchDataPtr + this._scratchDataPosition;
                        this._zstdInputBuffer.Size = inputSize <= 0 ? UIntPtr.Zero : (UIntPtr)inputSize;
                        this._zstdInputBuffer.Position = UIntPtr.Zero;

                        this._zstdOutputBuffer.Data = (IntPtr)outputBufferPtr + offset;
                        this._zstdOutputBuffer.Size = (UIntPtr)count;
                        this._zstdOutputBuffer.Position = UIntPtr.Zero;

                        UIntPtr result = Native.ZSTD_decompressStream(this._zstdStream, this._zstdOutputBuffer, this._zstdInputBuffer);
                        Zstd.ThrowOnError(result);

                        int outputBufferPosition = (int)this._zstdOutputBuffer.Position;
                        if (outputBufferPosition == 0)
                        {
                            if (this._isDataDepleted) break;

                            this._skipDataRead = false;
                        }
                        
                        bytesDecompressed += outputBufferPosition;
                        offset += outputBufferPosition;
                        count -= outputBufferPosition;

                        this._scratchDataPosition += (int)this._zstdInputBuffer.Position;
                    }
                } 
            }

            return bytesDecompressed;
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            if(!this.CanWrite)
            {
                throw new NotSupportedException();
            }

            unsafe
            {
                fixed(byte* outputBufferPtr = buffer)
                fixed(byte* scratchDataPtr = this._scratchData)
                {
                    while(count > 0)
                    {
                        uint inputSize = Math.Min((uint)count, this._zstdStreamOutputSize);

                        this._zstdOutputBuffer.Data = (IntPtr)scratchDataPtr;
                        this._zstdOutputBuffer.Size = (UIntPtr)this._zstdStreamOutputSize;
                        this._zstdOutputBuffer.Position = UIntPtr.Zero;

                        this._zstdInputBuffer.Data = (IntPtr)outputBufferPtr + offset;
                        this._zstdInputBuffer.Size = (UIntPtr)inputSize;
                        this._zstdInputBuffer.Position = UIntPtr.Zero;

                        Zstd.ThrowOnError(Native.ZSTD_compressStream(this._zstdStream, this._zstdOutputBuffer, this._zstdInputBuffer));

                        this._stream.Write(this._scratchData, 0, (int)this._zstdOutputBuffer.Position);

                        int inputBufferPosition = (int)this._zstdInputBuffer.Position;
                        offset += inputBufferPosition;
                        count -= inputBufferPosition;
                    }
                }
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if(this._isDisposed)
            {
                if (!this._isClosed) 
                {
                    DisposeResources(false);
                }

                ArrayPool<byte>.Shared.Return(this._scratchData, clearArray: false);
                this._scratchData = null;

                this._isDisposed = true;
            }
        }
    }

    public enum ZstdStreamMode
    {
        /// <summary>
        /// Indicates that the <see cref="ZstdStream"/> is in compression mode
        /// </summary>
        Compress,
        /// <summary>
        /// Indicates that the <see cref="ZstdStream"/> is in decompression mode
        /// </summary>
        Decompress
    }
}

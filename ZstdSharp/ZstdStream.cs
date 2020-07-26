using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
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

        private readonly byte[] _data;

        private readonly uint _zstdStreamInputSize;
        private readonly uint _zstdStreamOutputSize;
        private readonly IntPtr _zstdStream;
        private readonly ZstdBuffer _zstdInputBuffer = new ZstdBuffer();
        private readonly ZstdBuffer _zstdOutputBuffer = new ZstdBuffer();

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
                this._data = new byte[this._zstdStreamOutputSize];
            }
            else if (mode == ZstdStreamMode.Decompress)
            {
                this._zstdStreamInputSize = Native.ZSTD_DStreamInSize().ToUInt32();
                this._zstdStreamOutputSize = Native.ZSTD_DStreamOutSize().ToUInt32();
                this._zstdStream = Native.ZSTD_createDStream();
                this._data = new byte[this._zstdStreamInputSize];
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
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
            //unsafe
            //{
            //    fixed (byte* bufferPtr = buffer)
            //    fixed (byte* dataPtr = this._data)
            //    {
            //        while(count > 0)
            //        {
            //            int inputBufferSize = Math.Min((int)this._zstdStreamInputSize, count);
            //
            //
            //        }
            //    }
            //}
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
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

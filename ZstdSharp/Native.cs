using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

using Size = System.UIntPtr;

namespace ZstdSharp
{
    /// <summary>
    /// Bindings to the native zstd dynamic library
    /// </summary>
    internal static class Native
    {
        internal const int ZSTD_CLEVEL_DEFAULT = 3;

        private const string ZSTD_DLL = "libzstd";

        static Native()
        {
            string assemblyLocation = Path.GetDirectoryName(typeof(Native).Assembly.Location);
            string architecture = Environment.Is64BitProcess ? "x64" : "x86";

            SetDllDirectory(Path.Combine(assemblyLocation, architecture));
        }

        #region WinApi
        [Flags]
        private enum LoadLibraryFlags : uint
        {
            DONT_RESOLVE_DLL_REFERENCES = 0x00000001,
            LOAD_IGNORE_CODE_AUTHZ_LEVEL = 0x00000010,
            LOAD_LIBRARY_AS_DATAFILE = 0x00000002,
            LOAD_LIBRARY_AS_DATAFILE_EXCLUSIVE = 0x00000040,
            LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020,
            LOAD_LIBRARY_SEARCH_APPLICATION_DIR = 0x00000200,
            LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000,
            LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x00000100,
            LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800,
            LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400,
            LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008
        }

        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hReservedNull, LoadLibraryFlags dwFlags);

        [DllImport("kernel32", CharSet = CharSet.Auto)]
        private static extern bool SetDllDirectory(string lpPathName);
        #endregion

        #region Simple API
        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_compress(IntPtr destination, Size destinationCapacity, IntPtr source, Size sourceSize, int compressionLevel);

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_decompress(IntPtr destination, Size destinationCapacity, IntPtr source, Size compressedSize);
        #endregion

        #region Context API

        #region Compression

        [DllImport(ZSTD_DLL)]
        internal static extern IntPtr ZSTD_createCCtx();

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_freeCCtx(IntPtr context);

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_compressCCtx(IntPtr context, IntPtr destination, Size destinationCapacity, IntPtr source, Size sourceSize, int compressionLevel);

        #endregion

        #region Decompression

        [DllImport(ZSTD_DLL)]
        internal static extern IntPtr ZSTD_createDCtx();

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_freeDCtx(IntPtr context);

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_compressDCtx(IntPtr context, IntPtr destination, Size destinationCapacity, IntPtr source, IntPtr sourceSize);

        #endregion

        #endregion

        #region Advanced API

        #region Compression

        [DllImport(ZSTD_DLL)]
        internal static extern ZstdBounds ZSTD_cParam_getBounds(ZstdCompressionParameter parameter);

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_CCtx_setParameter(IntPtr context, ZstdCompressionParameter parameter, int value);

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_CCtx_getParameter(IntPtr context, ZstdCompressionParameter parameter, IntPtr value);

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_CCtx_reset(IntPtr context, ZstdResetDirective resetDirective);

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_compress2(IntPtr context, IntPtr destination, Size destinationCapacity, IntPtr source, Size sourceSize);

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_compress_usingCDict(IntPtr context,
            IntPtr destination, Size destionationCapacity,
            IntPtr source, Size sourceSize, IntPtr dictionary);

        #endregion

        #region Decompression

        [DllImport(ZSTD_DLL)]
        internal static extern ZstdBounds ZSTD_dParam_getBounds(ZstdDecompressionParameter parameter);

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_DCtx_setParameter(IntPtr context, ZstdDecompressionParameter parameter, int value);

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_DCtx_reset(IntPtr context, ZstdResetDirective resetDirective);

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_decompress_usingDDict(IntPtr context,
            IntPtr destination, Size destinationCapacity,
            IntPtr source, Size sourceSize, IntPtr dictionary);

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_decompressDCtx(IntPtr context,
            IntPtr destination, Size destinationCapacity,
            IntPtr source, Size sourceSize);

        #endregion

        #endregion

        #region Helper Functions
        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_compressBound(Size sourceSize);

        [DllImport(ZSTD_DLL)]
        internal static extern uint ZSTD_isError(Size errorCode);

        [DllImport(ZSTD_DLL)]
        internal static extern IntPtr ZSTD_getErrorName(Size errorCode);

        [DllImport(ZSTD_DLL)]
        internal static extern int ZSTD_minCLevel();

        [DllImport(ZSTD_DLL)]
        internal static extern int ZSTD_maxCLevel();

        [DllImport(ZSTD_DLL)]
        internal static extern uint ZSTD_versionNumber();

        #endregion

        #region Streaming API

        #region Compression API

        [DllImport(ZSTD_DLL)]
        internal static extern IntPtr ZSTD_createCStream();

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_freeCStream(IntPtr stream);

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_CStreamInSize();

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_CStreamOutSize();

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_initCStream(IntPtr stream, int compressionLevel);

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_compressStream(IntPtr stream,
            [MarshalAs(UnmanagedType.LPStruct)] ZstdBuffer output,
            [MarshalAs(UnmanagedType.LPStruct)] ZstdBuffer input);

        [DllImport(ZSTD_DLL)]
        public static extern IntPtr ZSTD_createCDict(IntPtr dictionaryBuffer, Size dictionarySize, int compressionLevel);

        [DllImport(ZSTD_DLL)]
        public static extern Size ZSTD_freeCDict(IntPtr dictionary);

        [DllImport(ZSTD_DLL)]
        public static extern Size ZSTD_initCStream_usingCDict(IntPtr stream, IntPtr dictionary);

        #endregion

        #region Decompression API

        [DllImport(ZSTD_DLL)]
        internal static extern IntPtr ZSTD_createDStream();

        [DllImport(ZSTD_DLL)]
        public static extern Size ZSTD_initDStream(IntPtr stream);

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_freeDStream(IntPtr stream);

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_DStreamInSize();

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_DStreamOutSize();

        [DllImport(ZSTD_DLL)]
        public static extern Size ZSTD_decompressStream(IntPtr stream,
            [MarshalAs(UnmanagedType.LPStruct)] ZstdBuffer output,
            [MarshalAs(UnmanagedType.LPStruct)] ZstdBuffer input);

        [DllImport(ZSTD_DLL)]
        public static extern IntPtr ZSTD_createDDict(IntPtr dictionaryBuffer, Size dictionarySize);

        [DllImport(ZSTD_DLL)]
        public static extern Size ZSTD_freeDDict(IntPtr dictionary);

        [DllImport(ZSTD_DLL)]
        public static extern Size ZSTD_initDStream_usingDDict(IntPtr stream, IntPtr dictionary);

        #endregion

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_flushStream(IntPtr stream, [MarshalAs(UnmanagedType.LPStruct)] ZstdBuffer output);

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_endStream(IntPtr stream, [MarshalAs(UnmanagedType.LPStruct)] ZstdBuffer output);

        #endregion
    }

    [StructLayout(LayoutKind.Sequential)]
    internal class ZstdBuffer
    {
        internal IntPtr Data;
        internal Size Size;
        internal Size Position;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal class ZstdBounds
    {
        internal Size ErrorCode;
        internal int LowerBound;
        internal int UpperBound;

        internal Range GetRange() => new Range(this.LowerBound, this.UpperBound);
    }
}

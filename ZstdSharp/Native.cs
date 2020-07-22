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

        #if X64
        private const string ZSTD_DLL = "libzstd_x64";
        #else
        private const string ZSTD_DLL = "libzstd_x86";
        #endif

        static Native()
        {
            string assemblyLocation = Path.GetDirectoryName(typeof(Native).Assembly.Location);
            string zstdLibrary = Path.Combine(assemblyLocation, ZSTD_DLL);
            LoadLibraryEx(zstdLibrary, IntPtr.Zero, LoadLibraryFlags.LOAD_LIBRARY_SEARCH_APPLICATION_DIR);
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
        #endregion

        #region Simple API
        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_compress(IntPtr destination, Size destinationCapacity, IntPtr source, Size sourceSize, int compressionLevel);

        [DllImport(ZSTD_DLL)]
        internal static extern Size ZSTD_decompress(IntPtr destination, Size destinationCapacity, IntPtr source, Size compressedSize);
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
    internal struct ZstdBuffer
    {
        internal IntPtr Data;
        internal Size Size;
        internal Size Position;
    }
}

﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ZstdSharp
{
    public class ZstdDictionary: IDisposable
    {
        private readonly byte[] _dictionaryBuffer;

        private IntPtr _decompressionDictionary;
        private Dictionary<int, IntPtr> _compressionDictionaries = new Dictionary<int, IntPtr>();

        public ZstdDictionary(string dictionaryLocation)
        {
            this._dictionaryBuffer = File.ReadAllBytes(dictionaryLocation);
        }
        public ZstdDictionary(Stream dictionaryStream)
        {
            dictionaryStream.Read(this._dictionaryBuffer, 0, (int)dictionaryStream.Length);
        }
        public ZstdDictionary(Stream dictionaryStream, int offset, int size)
        {
            dictionaryStream.Read(this._dictionaryBuffer, offset, size);
        }

        internal IntPtr GetCompressionDictionary(int compressionLevel)
        {
            if(!this._compressionDictionaries.ContainsKey(compressionLevel))
            {
                this._compressionDictionaries.Add(compressionLevel, GetCompressionDictionary(compressionLevel));
            }

            return this._compressionDictionaries[compressionLevel];
        }

        internal IntPtr GetDecompressionDictionary()
        {
            if(this._decompressionDictionary == IntPtr.Zero)
            {
                this._decompressionDictionary = CreateDecompressionDictionary();
            }

            return this._decompressionDictionary;
        }

        private IntPtr CreateCompressionDictionary(int compressionLevel)
        {
            unsafe
            {
                fixed (byte* dictionaryPtr = this._dictionaryBuffer)
                {
                    return Native.ZSTD_createCDict((IntPtr)dictionaryPtr, (UIntPtr)this._dictionaryBuffer.Length, compressionLevel);
                }
            }
        }

        private IntPtr CreateDecompressionDictionary()
        {
            unsafe
            {
                fixed (byte* dictionaryPtr = this._dictionaryBuffer)
                {
                    return Native.ZSTD_createDDict((IntPtr)dictionaryPtr, (UIntPtr)this._dictionaryBuffer.Length);
                }
            }
        }

        public void Dispose()
        {
            if(this._decompressionDictionary != IntPtr.Zero)
            {
                Native.ZSTD_freeDDict(this._decompressionDictionary);
                this._decompressionDictionary = IntPtr.Zero;
            }

            foreach (var compressionDictionary in this._compressionDictionaries.ToList()) 
            {
                Native.ZSTD_freeCDict(compressionDictionary.Value);
                this._compressionDictionaries.Remove(compressionDictionary.Key);
            }
        }
    }
}

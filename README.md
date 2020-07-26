# ZstdSharp [![Nuget](https://img.shields.io/nuget/dt/ZstdSharp?color=blue&logo=nuget&style=flat-square)](https://www.nuget.org/packages/ZstdSharp/) [![GitHub Releases](https://img.shields.io/github/downloads/Crauzer/ZstdSharp/latest/total?logo=github&style=flat-square)](https://github.com/Crauzer/ZstdSharp/releases)

ZstdSharp is a wrapper for the [native Zstandard library](https://github.com/facebook/zstd).

Features:
* Provides functions for immediate compression and decompression of data (Simple API)
* Streaming (soon)
* Possibility to use an explicit context for compression and decompression (Advanced API)
* Dictionaries
* ```Memory<byte>``` functions

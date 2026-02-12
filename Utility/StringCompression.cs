using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Core.Utility
{
    /// <summary>
    /// Provides Brotli compression and decompression for string data.
    /// Intended for JSON storage and data packet handling.
    /// </summary>
    public static class StringCompression
    {
        /// <summary>
        /// Compresses a string using Brotli and returns a Base64-encoded result.
        /// JSON is automatically minified before compression.
        /// </summary>
        /// <param name="input">The string to compress.</param>
        /// <param name="minify">If true, removes whitespace outside string literals. Default: true</param>
        /// <returns>Base64-encoded compressed string, or null if input is null/empty.</returns>
        public static string Compress(string input, bool minify = true)
        {
            if (string.IsNullOrEmpty(input))
                return null;

            if (minify)
                input = MinifyJson(input);

            byte[] inputBytes = Encoding.UTF8.GetBytes(input);

            using var outputStream = new MemoryStream();
            using (var brotliStream = new BrotliStream(outputStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                brotliStream.Write(inputBytes, 0, inputBytes.Length);
            }

            return Convert.ToBase64String(outputStream.ToArray());
        }

        /// <summary>
        /// Decompresses a Base64-encoded Brotli string back to its original form.
        /// </summary>
        /// <param name="compressedBase64">The Base64-encoded compressed string.</param>
        /// <returns>The original decompressed string, or null if input is null/empty.</returns>
        public static string Decompress(string compressedBase64)
        {
            if (string.IsNullOrEmpty(compressedBase64))
                return null;

            byte[] compressedBytes = Convert.FromBase64String(compressedBase64);

            using var inputStream = new MemoryStream(compressedBytes);
            using var brotliStream = new BrotliStream(inputStream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream();

            brotliStream.CopyTo(outputStream);

            return Encoding.UTF8.GetString(outputStream.ToArray());
        }

        /// <summary>
        /// Compresses a string to raw bytes (no Base64 encoding).
        /// JSON is automatically minified before compression.
        /// </summary>
        /// <param name="input">The string to compress.</param>
        /// <param name="minify">If true, removes whitespace outside string literals. Default: true</param>
        public static byte[] CompressToBytes(string input, bool minify = true)
        {
            if (string.IsNullOrEmpty(input))
                return null;

            if (minify)
                input = MinifyJson(input);

            byte[] inputBytes = Encoding.UTF8.GetBytes(input);

            using var outputStream = new MemoryStream();
            using (var brotliStream = new BrotliStream(outputStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                brotliStream.Write(inputBytes, 0, inputBytes.Length);
            }

            return outputStream.ToArray();
        }

        /// <summary>
        /// Decompresses raw Brotli bytes back to a string.
        /// </summary>
        public static string DecompressFromBytes(byte[] compressedBytes)
        {
            if (compressedBytes == null || compressedBytes.Length == 0)
                return null;

            using var inputStream = new MemoryStream(compressedBytes);
            using var brotliStream = new BrotliStream(inputStream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream();

            brotliStream.CopyTo(outputStream);

            return Encoding.UTF8.GetString(outputStream.ToArray());
        }

        /// <summary>
        /// Removes whitespace outside of string literals in JSON.
        /// </summary>
        private static string MinifyJson(string json)
        {
            var sb = new StringBuilder(json.Length);
            bool inString = false;
            bool escape = false;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];

                if (escape)
                {
                    sb.Append(c);
                    escape = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    sb.Append(c);
                    escape = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    sb.Append(c);
                    continue;
                }

                if (!inString && char.IsWhiteSpace(c))
                    continue;

                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}

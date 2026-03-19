using System;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Parser for raw binary format files.
    /// </summary>
    internal class BinaryParser : IFormatParser
    {
        public string FormatName => "Binary";

        public bool CanParse(string data)
        {
            // Binary format can't be reliably detected from string data
            return false;
        }

        public ParseResult Parse(string data)
        {
            throw new NotSupportedException("Binary format requires byte array input, not string");
        }

        /// <summary>
        /// Parses binary data from a byte array at the specified address.
        /// </summary>
        /// <param name="data">The binary data.</param>
        /// <param name="address">The starting address for the data (in WORDS).</param>
        /// <param name="wordSizeBytes">The word size in bytes.</param>
        /// <returns>A ParseResult containing the binary data as a single segment.</returns>
        public ParseResult ParseBinary(byte[] data, ulong address, int wordSizeBytes)
        {
            var result = new ParseResult();
            int wordSizeBits = wordSizeBytes * 8;

            if (data != null && data.Length > 0)
            {
                address *= (ulong)wordSizeBytes;
                ulong maximumAddress = address + (ulong)data.Length;
                var segment = new Segment(address, maximumAddress, data, wordSizeBits);
                result.Segments.Add(segment);
            }

            return result;
        }
    }
}

using System;
using System.Collections.Generic;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Serializer for raw binary format files.
    /// </summary>
    internal class BinarySerializer : IFormatSerializer
    {
        public string FormatName => "Binary";

        public string Serialize(Segments segments, SerializerOptions options)
        {
            throw new NotSupportedException("Binary format produces byte array output, not string. Use SerializeBinary instead.");
        }

        /// <summary>
        /// Serializes segments to a raw binary byte array.
        /// </summary>
        /// <param name="segments">The segments to serialize.</param>
        /// <param name="minimumAddress">The minimum address (inclusive). Null for segment minimum.</param>
        /// <param name="maximumAddress">The maximum address (exclusive). Null for segment maximum.</param>
        /// <param name="padding">The padding byte to use for gaps. Default is 0xFF.</param>
        /// <param name="wordSizeBytes">The word size in bytes.</param>
        /// <returns>A byte array containing the binary data.</returns>
        public byte[] SerializeBinary(Segments segments, ulong? minimumAddress, ulong? maximumAddress, byte padding, int wordSizeBytes)
        {
            // Convert single byte padding to byte array
            byte[] paddingArray = new byte[wordSizeBytes];
            for (int i = 0; i < wordSizeBytes; i++)
            {
                paddingArray[i] = padding;
            }
            return SerializeBinary(segments, minimumAddress, maximumAddress, paddingArray, wordSizeBytes);
        }

        /// <summary>
        /// Serializes segments to a raw binary byte array with custom padding.
        /// </summary>
        /// <param name="segments">The segments to serialize.</param>
        /// <param name="minimumAddress">The minimum address (inclusive). Null for segment minimum.</param>
        /// <param name="maximumAddress">The maximum address (exclusive). Null for segment maximum.</param>
        /// <param name="padding">The padding byte array to use for gaps. Must be a word value.</param>
        /// <param name="wordSizeBytes">The word size in bytes.</param>
        /// <returns>A byte array containing the binary data.</returns>
        public byte[] SerializeBinary(Segments segments, ulong? minimumAddress, ulong? maximumAddress, byte[] padding, int wordSizeBytes)
        {
            if (segments.Count == 0)
            {
                return Array.Empty<byte>();
            }

            ulong currentMaximumAddress;
            if (minimumAddress.HasValue)
            {
                currentMaximumAddress = minimumAddress.Value;
            }
            else
            {
                currentMaximumAddress = segments.MinimumAddress!.Value / (ulong)wordSizeBytes;
            }

            ulong finalMaximumAddress;
            if (maximumAddress.HasValue)
            {
                finalMaximumAddress = maximumAddress.Value;
            }
            else
            {
                finalMaximumAddress = segments.MaximumAddress!.Value / (ulong)wordSizeBytes;
            }

            if (currentMaximumAddress >= finalMaximumAddress)
            {
                return Array.Empty<byte>();
            }

            var binary = new List<byte>();

            foreach (var segment in segments)
            {
                ulong address = segment.MinimumAddress / (ulong)wordSizeBytes;
                ulong length = (ulong)segment.Data.Length / (ulong)wordSizeBytes;
                byte[] data = segment.Data;

                // Discard data below the minimum address
                if (address < currentMaximumAddress)
                {
                    if (address + length <= currentMaximumAddress)
                    {
                        continue;
                    }

                    ulong offset = (currentMaximumAddress - address) * (ulong)wordSizeBytes;
                    var newData = new byte[data.Length - (long)offset];
                    Array.Copy(data, (long)offset, newData, 0, newData.Length);
                    data = newData;
                    length = (ulong)data.Length / (ulong)wordSizeBytes;
                    address = currentMaximumAddress;
                }

                // Discard data above the maximum address
                if (address + length > finalMaximumAddress)
                {
                    if (address < finalMaximumAddress)
                    {
                        ulong size = (finalMaximumAddress - address) * (ulong)wordSizeBytes;
                        var newData = new byte[size];
                        Array.Copy(data, 0, newData, 0, (long)size);
                        data = newData;
                        length = (ulong)data.Length / (ulong)wordSizeBytes;
                    }
                    else if (finalMaximumAddress >= currentMaximumAddress)
                    {
                        // Add padding to reach maximum address
                        ulong wordsToFill = finalMaximumAddress - currentMaximumAddress;
                        for (ulong i = 0; i < wordsToFill; i++)
                        {
                            binary.AddRange(padding);
                        }
                        break;
                    }
                }

                // Add padding between segments
                ulong gapWords = address - currentMaximumAddress;
                for (ulong i = 0; i < gapWords; i++)
                {
                    binary.AddRange(padding);
                }

                // Add segment data
                binary.AddRange(data);
                currentMaximumAddress = address + length;
            }

            return binary.ToArray();
        }
    }
}

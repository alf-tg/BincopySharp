using System;

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
        /// Uses pre-calculated capacity to avoid List resizing.
        /// </summary>
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

            // Pre-calculate total output size in bytes for capacity
            ulong totalCapacityBytes = (finalMaximumAddress - currentMaximumAddress) * (ulong)wordSizeBytes;
            if (totalCapacityBytes > (ulong)int.MaxValue)
            {
                throw new BincopyException($"Requested range is too large: {totalCapacityBytes} bytes exceeds maximum array size of {int.MaxValue} bytes");
            }
            byte[] binary = new byte[totalCapacityBytes];
            long writePos = 0;

            foreach (var segment in segments)
            {
                ulong address = segment.MinimumAddress / (ulong)wordSizeBytes;
                ulong length = segment.Length / (ulong)wordSizeBytes;
                byte[] data = segment.DataSpan.ToArray();
                int dataOffset = 0;
                int dataLength = data.Length;

                // Discard data below the minimum address
                if (address < currentMaximumAddress)
                {
                    if (address + length <= currentMaximumAddress)
                    {
                        continue;
                    }

                    int offset = (int)((currentMaximumAddress - address) * (ulong)wordSizeBytes);
                    dataOffset = offset;
                    dataLength = data.Length - offset;
                    length = (ulong)dataLength / (ulong)wordSizeBytes;
                    address = currentMaximumAddress;
                }

                // Discard data above the maximum address
                if (address + length > finalMaximumAddress)
                {
                    if (address < finalMaximumAddress)
                    {
                        dataLength = (int)((finalMaximumAddress - address) * (ulong)wordSizeBytes);
                        length = (ulong)dataLength / (ulong)wordSizeBytes;
                    }
                    else if (finalMaximumAddress >= currentMaximumAddress)
                    {
                        // Add padding to reach maximum address
                        ulong wordsToFill = finalMaximumAddress - currentMaximumAddress;
                        long fillBytes = (long)(wordsToFill * (ulong)wordSizeBytes);
                        FillWithPadding(binary, writePos, fillBytes, padding);
                        writePos += fillBytes;
                        break;
                    }
                }

                // Add padding between segments
                ulong gapWords = address - currentMaximumAddress;
                if (gapWords > 0)
                {
                    long gapBytes = (long)(gapWords * (ulong)wordSizeBytes);
                    FillWithPadding(binary, writePos, gapBytes, padding);
                    writePos += gapBytes;
                }

                // Add segment data
                Array.Copy(data, dataOffset, binary, writePos, dataLength);
                writePos += dataLength;
                currentMaximumAddress = address + length;
            }

            // Trim to actual written size (original code didn't add trailing padding)
            if (writePos < (long)totalCapacityBytes)
            {
                byte[] result = new byte[writePos];
                Array.Copy(binary, 0, result, 0, writePos);
                return result;
            }

            return binary;
        }

        /// <summary>
        /// Fills a region of a byte array with a repeating padding pattern using Buffer.BlockCopy doubling.
        /// </summary>
        private static void FillWithPadding(byte[] buffer, long offset, long count, byte[] padding)
        {
            if (count <= 0) return;

            int padLen = padding.Length;

            // Check if all padding bytes are the same (common case: 0xFF fill)
            bool allSame = true;
            byte first = padding[0];
            for (int i = 1; i < padLen; i++)
            {
                if (padding[i] != first)
                {
                    allSame = false;
                    break;
                }
            }

            if (allSame)
            {
                // Fast path: single byte fill using doubling pattern
                buffer[offset] = first;
                int filled = 1;
                int total = (int)count;
                while (filled < total)
                {
                    int toCopy = Math.Min(filled, total - filled);
                    Buffer.BlockCopy(buffer, (int)offset, buffer, (int)offset + filled, toCopy);
                    filled += toCopy;
                }
            }
            else
            {
                // Multi-byte padding pattern: seed then double
                int seeded = 0;
                int total = (int)count;
                while (seeded < total && seeded < padLen)
                {
                    buffer[offset + seeded] = padding[seeded % padLen];
                    seeded++;
                }
                // Double using BlockCopy
                int filled = seeded;
                while (filled < total)
                {
                    int toCopy = Math.Min(filled, total - filled);
                    Buffer.BlockCopy(buffer, (int)offset, buffer, (int)offset + filled, toCopy);
                    filled += toCopy;
                }
            }
        }
    }
}

using System;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Serializer for raw binary format files.
    /// </summary>
    internal static class BinarySerializer
    {
        /// <summary>
        /// Serializes segments to a raw binary byte array with custom padding.
        /// </summary>
        /// <param name="segments">The segments to serialize.</param>
        /// <param name="minimumAddress">Start address (inclusive) in bytes. Null uses the first segment's minimum address.</param>
        /// <param name="maximumAddress">End address (exclusive) in bytes. Null uses the last segment's maximum address.</param>
        /// <param name="padding">Byte value used to fill gaps between segments.</param>
        /// <returns>A byte array containing the binary data.</returns>
        public static byte[] SerializeBinary(Segments segments, ulong? minimumAddress, ulong? maximumAddress, byte padding)
        {
            if (segments.Count == 0)
            {
                return Array.Empty<byte>();
            }

            ulong outputCursor;
            if (minimumAddress.HasValue)
            {
                outputCursor = minimumAddress.Value;
            }
            else
            {
                outputCursor = segments[0].MinimumAddress;
            }

            ulong endAddress;
            if (maximumAddress.HasValue)
            {
                endAddress = maximumAddress.Value;
            }
            else
            {
                endAddress = segments[segments.Count - 1].MaximumAddress;
            }

            if (outputCursor >= endAddress)
            {
                return Array.Empty<byte>();
            }

            // Pre-calculate total output size in bytes for capacity
            ulong totalCapacityBytes = endAddress - outputCursor;
            if (totalCapacityBytes > (ulong)int.MaxValue)
            {
                throw new BincopyException($"Requested range is too large: {totalCapacityBytes} bytes exceeds maximum array size of {int.MaxValue} bytes");
            }
            byte[] binary = new byte[(int)totalCapacityBytes];
            int writePos = 0;

            foreach (var segment in segments)
            {
                ulong address = segment.MinimumAddress;
                byte[] data = segment.Data.ToArray();
                int dataOffset = 0;
                int dataLength = data.Length;

                // Discard data below the minimum address
                if (address < outputCursor)
                {
                    if ((address + (ulong)dataLength) <= outputCursor)
                    {
                        continue;
                    }

                    dataOffset = (int)(outputCursor - address);
                    dataLength = data.Length - dataOffset;
                    address = outputCursor;
                }

                // Discard data above the maximum address
                if ((address + (ulong)dataLength) > endAddress)
                {
                    if (address < endAddress)
                    {
                        // Address may have been advanced by the left-trim above, so recompute from address
                        dataLength = (int)(endAddress - address);
                    }
                    else
                    {
                        // Add padding to reach maximum address
                        int bytesToFill = (int)(endAddress - outputCursor);
                        binary.AsSpan(writePos, bytesToFill).Fill(padding);
                        writePos += bytesToFill;
                        break;
                    }
                }

                // Add padding between segments
                int gapBytes = (int)(address - outputCursor);
                if (gapBytes > 0)
                {
                    binary.AsSpan(writePos, gapBytes).Fill(padding);
                    writePos += gapBytes;
                }

                // Add segment data
                Array.Copy(data, dataOffset, binary, writePos, dataLength);
                writePos += dataLength;
                outputCursor = address + (ulong)dataLength;
            }

            // Trim to actual written size
            if (writePos < (int)totalCapacityBytes)
            {
                byte[] trimmed = new byte[writePos];
                Array.Copy(binary, 0, trimmed, 0, writePos);
                return trimmed;
            }

            return binary;
        }
    }
}

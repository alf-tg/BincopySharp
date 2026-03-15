using System;
using System.Collections;
using System.Collections.Generic;

namespace BincopySharp
{
    /// <summary>
    /// Represents a contiguous segment of binary data in memory.
    /// </summary>
    public class Segment : IEnumerable<byte>
    {
        /// <summary>
        /// Gets the minimum address of this segment (inclusive) in BYTES.
        /// </summary>
        public ulong MinimumAddress { get; internal set; }

        /// <summary>
        /// Gets the maximum address of this segment (exclusive) in BYTES.
        /// </summary>
        public ulong MaximumAddress { get; internal set; }

        /// <summary>
        /// Gets the address of this segment in WORDS.
        /// </summary>
        public ulong Address => MinimumAddress / (ulong)WordSizeBytes;

        /// <summary>
        /// Gets the binary data contained in this segment.
        /// </summary>
        public byte[] Data { get; internal set; }

        /// <summary>
        /// Gets or sets the word size in bytes (1, 2, 4, or 8).
        /// </summary>
        public int WordSizeBytes { get; set; }

        /// <summary>
        /// Gets the length of the segment in bytes.
        /// </summary>
        public ulong Length => (ulong)Data.Length;

        /// <summary>
        /// Gets the number of words in the segment.
        /// </summary>
        public ulong WordCount => (ulong)Data.Length / (ulong)WordSizeBytes;

        /// <summary>
        /// Initializes a new instance of the Segment class.
        /// </summary>
        /// <param name="minimumAddress">The minimum address (inclusive) in BYTES.</param>
        /// <param name="maximumAddress">The maximum address (exclusive) in BYTES.</param>
        /// <param name="data">The binary data in bytes.</param>
        /// <param name="wordSizeBytes">The word size in bytes (1, 2, 4, or 8).</param>
        public Segment(ulong minimumAddress, ulong maximumAddress, byte[] data, int wordSizeBytes)
        {
            if (wordSizeBytes != 1 && wordSizeBytes != 2 && wordSizeBytes != 4 && wordSizeBytes != 8)
            {
                throw new ArgumentException(
                    $"Word size must be 1, 2, 4, or 8 bytes, got {wordSizeBytes}",
                    nameof(wordSizeBytes));
            }

            if (maximumAddress <= minimumAddress)
            {
                throw new ArgumentException(
                    $"Maximum address ({maximumAddress}) must be greater than minimum address ({minimumAddress})");
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            // No validation of data length vs address range — maximumAddress can exceed
            // minimumAddress + data.Length (used in Chunks where max_address = address + size)
            MinimumAddress = minimumAddress;
            MaximumAddress = maximumAddress;
            Data = data;
            WordSizeBytes = wordSizeBytes;
        }

        /// <summary>
        /// Splits the segment data into chunks of specified size with optional alignment and padding.
        /// Size and alignment are in WORDS.
        /// </summary>
        /// <param name="size">The size of each chunk in WORDS.</param>
        /// <param name="alignment">The alignment boundary in WORDS.</param>
        /// <param name="padding">Optional padding bytes to use for alignment.</param>
        /// <returns>An enumerable of tuples containing address in WORDS and chunk data.</returns>
        public IEnumerable<(ulong Address, byte[] Data)> Chunks(int size = 32, int alignment = 1, byte[]? padding = null)
        {
            if (size <= 0)
            {
                throw new ArgumentException("Chunk size must be positive", nameof(size));
            }

            if (alignment <= 0)
            {
                throw new ArgumentException("Alignment must be positive", nameof(alignment));
            }

            if ((size % alignment) != 0)
            {
                throw new BincopyException($"size {size} is not a multiple of alignment {alignment}");
            }

            if (padding != null && padding.Length != WordSizeBytes)
            {
                throw new BincopyException($"padding must be a word value (size {WordSizeBytes}), got {padding.Length} bytes");
            }

            // Convert from WORDS to BYTES
            int sizeBytes = size * WordSizeBytes;
            int alignmentBytes = alignment * WordSizeBytes;
            ulong address = MinimumAddress;  // address in BYTES
            byte[] data = (byte[])Data.Clone();

            // Apply padding to first and final chunk if padding is non-empty
            if (padding != null && padding.Length > 0)
            {
                int alignOffset = (int)(address % (ulong)alignmentBytes);
                
                // Adjust address and prepend padding
                address -= (ulong)alignOffset;
                int prependWords = alignOffset / WordSizeBytes;
                byte[] prependPadding = new byte[prependWords * WordSizeBytes];
                for (int i = 0; i < prependWords; i++)
                {
                    Array.Copy(padding, 0, prependPadding, i * WordSizeBytes, WordSizeBytes);
                }
                
                // Append padding to align final chunk
                int totalLength = prependPadding.Length + data.Length;
                int remainder = totalLength % alignmentBytes;
                int appendBytes = (remainder == 0) ? 0 : (alignmentBytes - remainder);
                int appendWords = appendBytes / WordSizeBytes;
                byte[] appendPadding = new byte[appendWords * WordSizeBytes];
                for (int i = 0; i < appendWords; i++)
                {
                    Array.Copy(padding, 0, appendPadding, i * WordSizeBytes, WordSizeBytes);
                }
                
                // Combine: prepend + data + append
                byte[] paddedData = new byte[prependPadding.Length + data.Length + appendPadding.Length];
                Array.Copy(prependPadding, 0, paddedData, 0, prependPadding.Length);
                Array.Copy(data, 0, paddedData, prependPadding.Length, data.Length);
                Array.Copy(appendPadding, 0, paddedData, prependPadding.Length + data.Length, appendPadding.Length);
                data = paddedData;
            }

            int chunkOffset = (int)(address % (ulong)alignmentBytes);

            // First chunk may be non-aligned and shorter than size if padding is empty
            if (chunkOffset != 0)
            {
                int firstChunkSize = alignmentBytes - chunkOffset;
                byte[] firstChunk = new byte[Math.Min(firstChunkSize, data.Length)];
                Array.Copy(data, 0, firstChunk, 0, firstChunk.Length);

                // Return address in WORDS
                yield return (address / (ulong)WordSizeBytes, firstChunk);

                address += (ulong)firstChunk.Length;
                
                // Create new data array without the first chunk
                byte[] remainingData = new byte[data.Length - firstChunk.Length];
                Array.Copy(data, firstChunk.Length, remainingData, 0, remainingData.Length);
                data = remainingData;
            }

            int offset = 0;
            while (offset < data.Length)
            {
                int chunkSize = Math.Min(sizeBytes, data.Length - offset);
                byte[] chunk = new byte[chunkSize];
                Array.Copy(data, offset, chunk, 0, chunkSize);

                // Return address in WORDS
                yield return (address / (ulong)WordSizeBytes, chunk);

                offset += sizeBytes;
                address += (ulong)sizeBytes;
            }
        }

        /// <summary>
        /// Adds data to this segment, optionally overwriting existing data.
        /// </summary>
        /// <param name="minimumAddress">The minimum address of the data to add.</param>
        /// <param name="maximumAddress">The maximum address of the data to add.</param>
        /// <param name="data">The data to add.</param>
        /// <param name="overwrite">Whether to overwrite existing data.</param>
        public void AddData(ulong minimumAddress, ulong maximumAddress, byte[] data, bool overwrite)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            // Check if ranges overlap
            if (maximumAddress <= MinimumAddress || minimumAddress >= MaximumAddress)
            {
                throw new ArgumentException("Address ranges do not overlap");
            }

            // Check for conflicts if not overwriting
            if (!overwrite)
            {
                ulong overlapStart = Math.Max(minimumAddress, MinimumAddress);
                ulong overlapEnd = Math.Min(maximumAddress, MaximumAddress);

                if (overlapEnd > overlapStart)
                {
                    throw new AddDataException(overlapStart);
                }
            }

            // Calculate overlap and copy data using ulong arithmetic (no signed casts)
            int sourceOffset;
            int destOffset;

            if (MinimumAddress > minimumAddress)
            {
                sourceOffset = (int)(MinimumAddress - minimumAddress);
                destOffset = 0;
            }
            else
            {
                sourceOffset = 0;
                destOffset = (int)(minimumAddress - MinimumAddress);
            }

            int copyLength = Math.Min(
                data.Length - sourceOffset,
                Data.Length - destOffset);

            if (copyLength > 0)
            {
                Array.Copy(data, sourceOffset, Data, destOffset, copyLength);
            }
        }

        /// <summary>
        /// Removes data from this segment, potentially splitting it into two segments.
        /// </summary>
        /// <param name="minimumAddress">The minimum address to remove.</param>
        /// <param name="maximumAddress">The maximum address to remove.</param>
        /// <returns>A tuple of (left segment, right segment) or null if segment is completely removed.</returns>
        public (Segment? Left, Segment? Right)? RemoveData(ulong minimumAddress, ulong maximumAddress)
        {
            // No overlap
            if (maximumAddress <= MinimumAddress || minimumAddress >= MaximumAddress)
            {
                return (this, null);
            }

            // Complete removal
            if (minimumAddress <= MinimumAddress && maximumAddress >= MaximumAddress)
            {
                return null;
            }

            // Partial removal - left side remains
            if (minimumAddress > MinimumAddress && maximumAddress >= MaximumAddress)
            {
                ulong newMaxAddress = minimumAddress;
                ulong newLength = newMaxAddress - MinimumAddress;
                byte[] newData = new byte[newLength];
                Array.Copy(Data, 0, newData, 0, (long)newLength);
                return (new Segment(MinimumAddress, newMaxAddress, newData, WordSizeBytes), null);
            }

            // Partial removal - right side remains
            if (minimumAddress <= MinimumAddress && maximumAddress < MaximumAddress)
            {
                ulong newMinAddress = maximumAddress;
                ulong offset = newMinAddress - MinimumAddress;
                ulong newLength = (ulong)Data.Length - offset;
                byte[] newData = new byte[newLength];
                Array.Copy(Data, (long)offset, newData, 0, (long)newLength);
                return (null, new Segment(newMinAddress, MaximumAddress, newData, WordSizeBytes));
            }

            // Middle removal - split into two segments
            ulong leftMaxAddress = minimumAddress;
            ulong leftLength = leftMaxAddress - MinimumAddress;
            byte[] leftData = new byte[leftLength];
            Array.Copy(Data, 0, leftData, 0, (long)leftLength);

            ulong rightMinAddress = maximumAddress;
            ulong rightOffset = rightMinAddress - MinimumAddress;
            ulong rightLength = (ulong)Data.Length - rightOffset;
            byte[] rightData = new byte[rightLength];
            Array.Copy(Data, (long)rightOffset, rightData, 0, (long)rightLength);

            return (
                new Segment(MinimumAddress, leftMaxAddress, leftData, WordSizeBytes),
                new Segment(rightMinAddress, MaximumAddress, rightData, WordSizeBytes)
            );
        }

        /// <summary>
        /// Returns an enumerator that iterates through the segment bytes.
        /// </summary>
        /// <returns>An enumerator for the segment bytes.</returns>
        public IEnumerator<byte> GetEnumerator()
        {
            return ((IEnumerable<byte>)Data).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Returns a string representation of this segment.
        /// </summary>
        /// <returns>A string describing the segment.</returns>
        public override string ToString()
        {
            return $"Segment(address=0x{MinimumAddress:X}, data={Data.Length} bytes)";
        }
    }
}

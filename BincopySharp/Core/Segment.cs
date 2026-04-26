using System;
using System.Collections;
using System.Collections.Generic;
using Collections.Pooled;

namespace BincopySharp
{
    /// <summary>
    /// Represents a contiguous segment of binary data in memory.
    /// </summary>
    public class Segment : IEnumerable<byte>
    {
        // PooledList<byte> gives us geometric growth (identical to List<T>) plus a .Span
        // property that returns a mutable Span<byte> over the populated region without copying,
        // equivalent to CollectionsMarshal.AsSpan(list) which is only available on .NET 5+.
        // On .NET 5+ this dependency could be dropped in favour of List<byte> + CollectionsMarshal.
        private readonly PooledList<byte> _data = new PooledList<byte>();

        /// <summary>
        /// Gets the minimum address of this segment (inclusive) in BYTES.
        /// </summary>
        public ulong MinimumAddress { get; internal set; }

        /// <summary>
        /// Gets the maximum address of this segment (exclusive) in BYTES.
        /// </summary>
        public ulong MaximumAddress { get; internal set; }

        /// <summary>
        /// Zero-copy read-only view of the internal data buffer.
        /// Valid only for the duration of the calling method — do not store the result.
        /// </summary>
        public ReadOnlySpan<byte> DataSpan => _data.Span;

        /// <summary>
        /// Zero-copy mutable view of the internal data buffer. For internal use only.
        /// Valid only for the duration of the calling method — do not store the result.
        /// </summary>
        internal Span<byte> MutableDataSpan => _data.Span;

        /// <summary>
        /// Replaces the internal data buffer with a copy of the given array.
        /// For internal use only.
        /// </summary>
        internal void ReplaceData(byte[] value)
        {
            _data.Clear();
            _data.AddRange(value);
        }

        /// <summary>
        /// Gets the length of the segment in bytes.
        /// </summary>
        public int Length => _data.Count;

        /// <summary>
        /// Appends data to the end of this segment's internal buffer with geometric growth.
        /// Does NOT update MinimumAddress/MaximumAddress — caller must do that.
        /// </summary>
        /// <param name="data">The source array.</param>
        /// <param name="offset">The index within <paramref name="data"/> at which to start reading.</param>
        /// <param name="count">The number of bytes to append.</param>
        internal void AppendToBuffer(byte[] data, int offset, int count)
        {
            _data.AddRange(new ArraySegment<byte>(data, offset, count));
        }

        /// <summary>
        /// Initializes a new instance of the Segment class.
        /// </summary>
        /// <param name="minimumAddress">The minimum address (inclusive) in BYTES.</param>
        /// <param name="maximumAddress">The maximum address (exclusive) in BYTES.</param>
        /// <param name="data">The binary data in bytes.</param>
        public Segment(ulong minimumAddress, ulong maximumAddress, byte[] data)
        {
            if (maximumAddress <= minimumAddress)
            {
                throw new ArgumentException(
                    $"Maximum address ({maximumAddress}) must be greater than minimum address ({minimumAddress})");
            }

            _ = data ?? throw new ArgumentNullException(nameof(data));

            if (maximumAddress - minimumAddress != (ulong)data.Length)
            {
                throw new ArgumentException(
                    $"Address range ({maximumAddress - minimumAddress} bytes) does not match data length ({data.Length} bytes)");
            }

            MinimumAddress = minimumAddress;
            MaximumAddress = maximumAddress;
            _data.AddRange(data);
        }

        /// <summary>
        /// Splits the segment data into chunks of specified size with optional alignment and padding.
        /// Size and alignment are in BYTES.
        /// </summary>
        /// <param name="size">The size of each chunk in BYTES.</param>
        /// <param name="alignment">The alignment boundary in BYTES.</param>
        /// <param name="padding">Optional padding bytes to use for alignment.</param>
        /// <returns>An enumerable of tuples containing address in BYTES and chunk data.</returns>
        public IEnumerable<(ulong Address, byte[] Data)> Chunks(int size = 32, int alignment = 1, byte? padding = null)
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
                throw new BincopyException($"Size {size} is not a multiple of alignment {alignment}");
            }

            ulong address = MinimumAddress;
            byte[] data = _data.ToArray();

            // Apply padding to first and final chunk if padding is non-empty
            if (padding != null)
            {
                int alignOffset = (int)(address % (ulong)alignment);

                // Adjust address and prepend padding
                address -= (ulong)alignOffset;
                byte[] prependPadding = new byte[alignOffset];
                prependPadding.AsSpan().Fill(padding.Value);

                // Append padding to align final chunk
                int totalLength = prependPadding.Length + data.Length;
                int remainder = totalLength % alignment;
                int appendBytes = (remainder == 0) ? 0 : (alignment - remainder);
                byte[] appendPadding = new byte[appendBytes];
                appendPadding.AsSpan().Fill(padding.Value);

                // Combine: prepend + data + append
                byte[] paddedData = new byte[prependPadding.Length + data.Length + appendPadding.Length];
                Array.Copy(prependPadding, 0, paddedData, 0, prependPadding.Length);
                Array.Copy(data, 0, paddedData, prependPadding.Length, data.Length);
                Array.Copy(appendPadding, 0, paddedData, prependPadding.Length + data.Length, appendPadding.Length);
                data = paddedData;
            }

            int chunkOffset = (int)(address % (ulong)alignment);

            // First chunk may be non-aligned and shorter than size if padding is empty
            if (chunkOffset != 0)
            {
                int firstChunkSize = alignment - chunkOffset;
                byte[] firstChunk = new byte[Math.Min(firstChunkSize, data.Length)];
                Array.Copy(data, 0, firstChunk, 0, firstChunk.Length);

                // Return address in BYTES
                yield return (address, firstChunk);

                address += (ulong)firstChunk.Length;

                // Create new data array without the first chunk
                byte[] remainingData = new byte[data.Length - firstChunk.Length];
                Array.Copy(data, firstChunk.Length, remainingData, 0, remainingData.Length);
                data = remainingData;
            }

            int offset = 0;
            while (offset < data.Length)
            {
                int chunkSize = Math.Min(size, data.Length - offset);
                byte[] chunk = new byte[chunkSize];
                Array.Copy(data, offset, chunk, 0, chunkSize);

                // Return address in BYTES
                yield return (address, chunk);

                offset += size;
                address += (ulong)size;
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
            if ((maximumAddress <= MinimumAddress) || (minimumAddress >= MaximumAddress))
            {
                return (this, null);
            }

            // Complete removal
            if ((minimumAddress <= MinimumAddress) && (maximumAddress >= MaximumAddress))
            {
                return null;
            }

            ReadOnlySpan<byte> span = _data.Span;

            // Partial removal - left side remains
            if (minimumAddress > MinimumAddress && maximumAddress >= MaximumAddress)
            {
                ulong newMaxAddress = minimumAddress;
                int newLength = (int)(newMaxAddress - MinimumAddress);
                return (new Segment(MinimumAddress, newMaxAddress, span.Slice(0, newLength).ToArray()), null);
            }

            // Partial removal - right side remains
            if (minimumAddress <= MinimumAddress && maximumAddress < MaximumAddress)
            {
                ulong newMinAddress = maximumAddress;
                int offset = (int)(newMinAddress - MinimumAddress);
                return (null, new Segment(newMinAddress, MaximumAddress, span.Slice(offset).ToArray()));
            }

            // Middle removal - split into two segments
            int leftLength = (int)(minimumAddress - MinimumAddress);
            int rightOffset = (int)(maximumAddress - MinimumAddress);

            return (
                new Segment(MinimumAddress, minimumAddress, span.Slice(0, leftLength).ToArray()),
                new Segment(maximumAddress, MaximumAddress, span.Slice(rightOffset).ToArray())
            );
        }

        /// <summary>
        /// Returns an enumerator that iterates through the segment bytes.
        /// </summary>
        /// <returns>An enumerator for the segment bytes.</returns>
        public IEnumerator<byte> GetEnumerator()
        {
            return ((IEnumerable<byte>)_data).GetEnumerator();
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
            return $"Segment(address=0x{MinimumAddress:X}, data={_data.Count} bytes)";
        }
    }
}

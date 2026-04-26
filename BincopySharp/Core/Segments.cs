using System;
using System.Collections;
using System.Collections.Generic;

namespace BincopySharp
{
    /// <summary>
    /// Represents a collection of non-overlapping memory segments ordered by address.
    /// </summary>
    public class Segments : IReadOnlyList<Segment>
    {
        private readonly List<Segment> _segments;
        private Segment? _currentSegment;
        private int _currentSegmentIndex;

        /// <summary>
        /// Gets the number of segments in this collection.
        /// </summary>
        public int Count => _segments.Count;

        /// <summary>
        /// Gets the minimum address across all segments, or null if no segments exist.
        /// </summary>
        public ulong? MinimumAddress
        {
            get
            {
                if (_segments.Count == 0)
                {
                    return null;
                }
                return _segments[0].MinimumAddress;
            }
        }

        /// <summary>
        /// Gets the maximum address across all segments, or null if no segments exist.
        /// </summary>
        public ulong? MaximumAddress
        {
            get
            {
                if (_segments.Count == 0)
                {
                    return null;
                }
                return _segments[_segments.Count - 1].MaximumAddress;
            }
        }

        /// <summary>
        /// Initializes a new instance of the Segments class.
        /// </summary>
        public Segments()
        {
            _segments = new List<Segment>();
        }

        /// <summary>
        /// Gets the segment at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the segment to get.</param>
        /// <returns>The segment at the specified index.</returns>
        public Segment this[int index]
        {
            get
            {
                if ((index < 0) || (index >= _segments.Count))
                {
                    throw new BincopyException("Segment does not exist");
                }
                return _segments[index];
            }
        }

        /// <summary>
        /// Adds a segment to the collection, merging or splitting as necessary.
        /// </summary>
        /// <param name="segment">The segment to add.</param>
        /// <param name="overwrite">Whether to overwrite existing data.</param>
        internal void Add(Segment segment, bool overwrite = false)
        {
            int insertionIndex;
            Segment existingSegment;

            if (segment == null)
            {
                throw new ArgumentNullException(nameof(segment));
            }

            if (_segments.Count == 0)
            {
                _segments.Add(segment);
                _currentSegment = segment;
                _currentSegmentIndex = 0;
                return;
            }

            // Fast path: adjacent to last added segment (common case when parsing files)
            if ((_currentSegment != null) && (segment.MinimumAddress == _currentSegment.MaximumAddress))
            {
                insertionIndex = _currentSegmentIndex;
                existingSegment = _currentSegment;
            }
            // Slow path: linear search for insertion point
            else
            {
                for (insertionIndex = 0; insertionIndex < _segments.Count; insertionIndex++)
                {
                    if (segment.MinimumAddress <= _segments[insertionIndex].MaximumAddress)
                    {
                        break;
                    }
                }

                if (insertionIndex == _segments.Count)
                {
                    // Non-overlapping, non-adjacent after all existing segments
                    _segments.Add(segment);
                    _currentSegment = segment;
                    _currentSegmentIndex = insertionIndex;
                    return;
                }

                existingSegment = _segments[insertionIndex];

                if (segment.MaximumAddress < existingSegment.MinimumAddress)
                {
                    // Non-overlapping, non-adjacent before
                    _segments.Insert(insertionIndex, segment);
                    _currentSegment = segment;
                    _currentSegmentIndex = insertionIndex;
                    return;
                }
            }

            // Adjacent or overlapping - merge into existing segment
            ThrowIfOverwriteViolation(insertionIndex, segment, overwrite);
            AddDataToSegment(existingSegment, segment.MinimumAddress, segment.MaximumAddress, segment.DataSpan.ToArray());
            _currentSegment = existingSegment;
            _currentSegmentIndex = insertionIndex;

            // Remove overwritten and merge adjacent segments after the current one
            while (insertionIndex < _segments.Count - 1)
            {
                var next = _segments[insertionIndex + 1];

                if (existingSegment.MaximumAddress >= next.MaximumAddress)
                {
                    // The whole next segment is overwritten
                    _segments.RemoveAt(insertionIndex + 1);
                    // As _segments.Count is reduced, the next segment has index insertionIndex.
                }
                else if (existingSegment.MaximumAddress >= next.MinimumAddress)
                {
                    // Adjacent or beginning of the next segment overwritten - merge remaining
                    int offset = (int)(existingSegment.MaximumAddress - next.MinimumAddress);
                    byte[] remainingData = next.DataSpan.Slice(offset).ToArray();
                    AddDataToSegment(existingSegment, existingSegment.MaximumAddress, next.MaximumAddress, remainingData);
                    _segments.RemoveAt(insertionIndex + 1);
                    break;
                }
                else
                {
                    // Segments are not overlapping, nor adjacent
                    break;
                }
            }
        }

        /// <summary>
        /// Throws <see cref="AddDataException"/> if <paramref name="incoming"/> overlaps with
        /// the segment at <paramref name="currentIndex"/> or with the next segment in the list,
        /// and <paramref name="overwrite"/> is false. Must be called before any state mutation.
        /// </summary>
        private void ThrowIfOverwriteViolation(int currentIndex, Segment incoming, bool overwrite)
        {
            if (overwrite)
            {
                return;
            }

            var target = _segments[currentIndex];

            // Incoming segment overlaps current segment
            if ((incoming.MinimumAddress < target.MaximumAddress) && (incoming.MaximumAddress > target.MinimumAddress))
            {
                throw new AddDataException(Math.Max(target.MinimumAddress, incoming.MinimumAddress));
            }

            // Incoming segment overlaps next segment
            if ((currentIndex < _segments.Count - 1) &&
                (incoming.MaximumAddress > _segments[currentIndex + 1].MinimumAddress))
            {
                throw new AddDataException(_segments[currentIndex + 1].MinimumAddress);
            }
        }

        /// <summary>
        /// Adds data to an existing segment, handling adjacent and overlapping cases.
        /// </summary>
        private void AddDataToSegment(Segment target, ulong minAddr, ulong maxAddr, byte[] data)
        {
            int targetWriteOffset;
            int sourceReadOffset = 0;

            // Prepend data if new segment starts before existing
            if (minAddr < target.MinimumAddress)
            {
                int prependSize = (int)(target.MinimumAddress - minAddr);
                ReadOnlySpan<byte> existing = target.DataSpan;
                byte[] newData = new byte[prependSize + existing.Length];
                data.AsSpan(0, prependSize).CopyTo(newData);
                existing.CopyTo(newData.AsSpan(prependSize));
                target.ReplaceData(newData);
                target.MinimumAddress = minAddr;
                sourceReadOffset = prependSize;
                targetWriteOffset = prependSize;
            }
            else
            {
                targetWriteOffset = (int)(minAddr - target.MinimumAddress);
            }

            // Handle overlapping part
            int targetBytesLeft = (int)target.Length - targetWriteOffset;
            int sourceBytesLeft = data.Length - sourceReadOffset;
            int overwriteBytesLeft = Math.Min(targetBytesLeft, sourceBytesLeft);

            // Overwrite what fits
            data.AsSpan(sourceReadOffset, overwriteBytesLeft).CopyTo(target.MutableDataSpan.Slice(targetWriteOffset));
            targetBytesLeft = targetBytesLeft - overwriteBytesLeft;
            sourceBytesLeft = sourceBytesLeft - overwriteBytesLeft;

            // Then append the rest
            sourceReadOffset += overwriteBytesLeft;
            target.AppendToBuffer(data, sourceReadOffset, sourceBytesLeft);
            target.MaximumAddress = Math.Max(target.MaximumAddress, maxAddr);
        }

        /// <summary>
        /// Removes data from the specified address range.
        /// </summary>
        /// <param name="minimumAddress">The minimum address to remove.</param>
        /// <param name="maximumAddress">The maximum address to remove.</param>
        internal void Remove(ulong minimumAddress, ulong maximumAddress)
        {
            if (maximumAddress <= minimumAddress)
            {
                throw new ArgumentException("Maximum address must be greater than minimum address");
            }

            for (int i = 0; i < _segments.Count; i++)
            {
                // Segments are ordered — skip segments entirely before the removal range
                if (_segments[i].MaximumAddress <= minimumAddress)
                {
                    continue;
                }

                // Segments are ordered — once past the removal range, no more segments can be affected
                if (_segments[i].MinimumAddress >= maximumAddress)
                {
                    break;
                }

                var result = _segments[i].RemoveData(minimumAddress, maximumAddress);

                if (result == null)
                {
                    // Segment completely removed
                    _segments.RemoveAt(i--);
                }
                else
                {
                    if (result.Value.Left != null)
                    {
                        _segments[i] = result.Value.Left;
                    }
                    else
                    {
                        _segments.RemoveAt(i--);
                    }

                    if (result.Value.Right != null)
                    {
                        _segments.Insert(++i, result.Value.Right);
                    }
                }
            }
            _currentSegment = null;
            _currentSegmentIndex = 0;
        }

        /// <summary>
        /// Returns chunks of data from all segments.
        /// Size and alignment are in BYTES.
        /// </summary>
        /// <param name="size">The size of each chunk in BYTES.</param>
        /// <param name="alignment">The alignment boundary in BYTES.</param>
        /// <param name="padding">Optional padding bytes to use for alignment.</param>
        /// <returns>An enumerable of tuples containing address in BYTES and chunk data.</returns>
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
                throw new BincopyException($"Size {size} is not a multiple of alignment {alignment}");
            }

            (ulong Address, byte[] Data)? previous = null;

            foreach (var segment in _segments)
            {
                foreach (var chunk in segment.Chunks(size, alignment, padding))
                {
                    var currentChunk = chunk;

                    // When chunks are padded to alignment, the final chunk of the previous
                    // segment and the first chunk of the current segment may overlap by
                    // one alignment block. Merge them to avoid overwriting lower segment data.
                    if (previous.HasValue && currentChunk.Address < previous.Value.Address + (ulong)(previous.Value.Data.Length))
                    {
                        byte[] low = new byte[alignment];
                        byte[] high = new byte[alignment];

                        // Get last alignment block from previous chunk
                        Array.Copy(previous.Value.Data, previous.Value.Data.Length - alignment, low, 0, alignment);

                        // Get first alignment block from current chunk
                        Array.Copy(currentChunk.Data, 0, high, 0, alignment);

                        // Create alignment * padding
                        byte[] alignmentPadding = new byte[alignment];
                        if (padding != null)
                        {
                            for (int i = 0; i < alignment; i++)
                            {
                                Array.Copy(padding, 0, alignmentPadding, i * WordSizeBytes, WordSizeBytes);
                            }
                        }

                        // Direct copy: for each byte position, prefer real data over padding.
                        // If low[i] differs from padding, it has real data — use it.
                        // Otherwise, use high[i] (which may be real data or padding).
                        byte[] merged = new byte[alignment];
                        for (int i = 0; i < alignment; i++)
                        {
                            if (low[i] != alignmentPadding[i])
                            {
                                merged[i] = low[i];
                            }
                            else
                            {
                                merged[i] = high[i];
                            }
                        }

                        // Replace first alignment block of chunk with merged data
                        byte[] newChunkData = new byte[currentChunk.Data.Length];
                        Array.Copy(merged, 0, newChunkData, 0, alignment);
                        Array.Copy(currentChunk.Data, alignment, newChunkData, alignment, currentChunk.Data.Length - alignment);

                        currentChunk = (currentChunk.Address, newChunkData);
                    }

                    yield return currentChunk;
                    previous = currentChunk;
                }
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the segments.
        /// </summary>
        /// <returns>An enumerator for the segments.</returns>
        public IEnumerator<Segment> GetEnumerator()
        {
            return _segments.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Returns a string representation of this segments collection.
        /// </summary>
        /// <returns>A string describing the segments collection.</returns>
        public override string ToString()
        {
            return $"Segments(count={Count}, word_size_bits={WordSizeBits})";
        }
    }
}

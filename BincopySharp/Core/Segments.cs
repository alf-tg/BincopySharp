using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace BincopySharp
{
    /// <summary>
    /// Represents a collection of non-overlapping memory segments ordered by address.
    /// </summary>
    public class Segments : IEnumerable<Segment>
    {
        private readonly List<Segment> _segments;
        private Segment? _currentSegment;
        private int _currentSegmentIndex;

        /// <summary>
        /// Gets or sets the word size in bytes for all segments in this collection.
        /// </summary>
        public int WordSizeBytes { get; set; }

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
        /// <param name="wordSizeBytes">The word size in bytes (1, 2, 4, or 8).</param>
        public Segments(int wordSizeBytes)
        {
            if (wordSizeBytes != 1 && wordSizeBytes != 2 && wordSizeBytes != 4 && wordSizeBytes != 8)
            {
                throw new ArgumentException(
                    $"Word size must be 1, 2, 4, or 8 bytes, got {wordSizeBytes}",
                    nameof(wordSizeBytes));
            }

            WordSizeBytes = wordSizeBytes;
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
                if (index < 0 || index >= _segments.Count)
                {
                    throw new BincopyException("segment does not exist");
                }
                return _segments[index];
            }
        }

        /// <summary>
        /// Adds a segment to the collection, merging or splitting as necessary.
        /// EXACTLY like Python's Segments.add() with fast path optimization.
        /// </summary>
        /// <param name="segment">The segment to add.</param>
        /// <param name="overwrite">Whether to overwrite existing data.</param>
        public void Add(Segment segment, bool overwrite = false)
        {
            if (segment == null)
            {
                throw new ArgumentNullException(nameof(segment));
            }

            if (segment.WordSizeBytes != WordSizeBytes)
            {
                throw new ArgumentException(
                    $"Segment word size ({segment.WordSizeBytes}) does not match collection word size ({WordSizeBytes})");
            }

            if (_segments.Count == 0)
            {
                _segments.Add(segment);
                _currentSegment = segment;
                _currentSegmentIndex = 0;
                return;
            }

            // Fast path: adjacent to last added segment (common case when parsing files)
            if (_currentSegment != null && segment.MinimumAddress == _currentSegment.MaximumAddress)
            {
                AddDataToSegment(_currentSegment, segment.MinimumAddress, segment.MaximumAddress, segment.Data, overwrite);
                
                // Remove overwritten and merge adjacent segments after current
                int currentIndex = _currentSegmentIndex;
                while (currentIndex < _segments.Count - 1)
                {
                    var next = _segments[currentIndex + 1];

                    if (_currentSegment.MaximumAddress >= next.MaximumAddress)
                    {
                        // The whole next segment is overwritten
                        _segments.RemoveAt(currentIndex + 1);
                    }
                    else if (_currentSegment.MaximumAddress >= next.MinimumAddress)
                    {
                        // Adjacent or beginning of the next segment overwritten - merge remaining
                        long offsetLong = (long)(_currentSegment.MaximumAddress - next.MinimumAddress);
                        int offset = (int)offsetLong;
                        byte[] remainingData = new byte[next.Data.Length - offset];
                        Array.Copy(next.Data, offset, remainingData, 0, remainingData.Length);
                        AddDataToSegment(_currentSegment, _currentSegment.MaximumAddress, next.MaximumAddress, remainingData, false);
                        _segments.RemoveAt(currentIndex + 1);
                        break;
                    }
                    else
                    {
                        // Segments are not overlapping, nor adjacent
                        break;
                    }
                }
                return;
            }

            // Slow path: linear search for insertion point
            int i;
            for (i = 0; i < _segments.Count; i++)
            {
                if (segment.MinimumAddress <= _segments[i].MaximumAddress)
                {
                    break;
                }
            }

            if (i == _segments.Count)
            {
                // Non-overlapping, non-adjacent after all existing segments
                _segments.Add(segment);
                _currentSegment = segment;
                _currentSegmentIndex = i;
                return;
            }

            var s = _segments[i];

            if (segment.MaximumAddress < s.MinimumAddress)
            {
                // Non-overlapping, non-adjacent before
                _segments.Insert(i, segment);
                _currentSegment = segment;
                _currentSegmentIndex = i;
                return;
            }

            // Adjacent or overlapping - merge into existing segment
            AddDataToSegment(s, segment.MinimumAddress, segment.MaximumAddress, segment.Data, overwrite);
            _currentSegment = s;
            _currentSegmentIndex = i;

            // Remove overwritten and merge adjacent segments after the current one
            while (i < _segments.Count - 1)
            {
                var next = _segments[i + 1];

                if (s.MaximumAddress >= next.MaximumAddress)
                {
                    // The whole next segment is overwritten
                    _segments.RemoveAt(i + 1);
                }
                else if (s.MaximumAddress >= next.MinimumAddress)
                {
                    // Adjacent or beginning of the next segment overwritten - merge remaining
                    // Use long to handle large address differences safely
                    long offsetLong = (long)(s.MaximumAddress - next.MinimumAddress);
                    int offset = (int)offsetLong;
                    byte[] remainingData = new byte[next.Data.Length - offset];
                    Array.Copy(next.Data, offset, remainingData, 0, remainingData.Length);
                    AddDataToSegment(s, s.MaximumAddress, next.MaximumAddress, remainingData, false);
                    _segments.RemoveAt(i + 1);
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
        /// Adds data to an existing segment, handling adjacent and overlapping cases.
        /// EXACTLY like Python's Segment.add_data().
        /// </summary>
        private void AddDataToSegment(Segment target, ulong minAddr, ulong maxAddr, byte[] data, bool overwrite)
        {
            if (minAddr == target.MaximumAddress)
            {
                // Append: adjacent after
                byte[] newData = new byte[target.Data.Length + data.Length];
                Array.Copy(target.Data, 0, newData, 0, target.Data.Length);
                Array.Copy(data, 0, newData, target.Data.Length, data.Length);
                target.Data = newData;
                target.MaximumAddress = maxAddr;
            }
            else if (maxAddr == target.MinimumAddress)
            {
                // Prepend: adjacent before
                byte[] newData = new byte[data.Length + target.Data.Length];
                Array.Copy(data, 0, newData, 0, data.Length);
                Array.Copy(target.Data, 0, newData, data.Length, target.Data.Length);
                target.Data = newData;
                target.MinimumAddress = minAddr;
            }
            else if (overwrite
                     && minAddr < target.MaximumAddress
                     && maxAddr > target.MinimumAddress)
            {
                // Use long to handle large address differences safely
                long selfDataOffsetLong = (long)minAddr - (long)target.MinimumAddress;
                int selfDataOffset = (int)selfDataOffsetLong;
                int dataOffset = 0;

                // Prepend data if new segment starts before existing
                if (selfDataOffset < 0)
                {
                    int prependSize = -selfDataOffset;
                    byte[] newData = new byte[prependSize + target.Data.Length];
                    Array.Copy(data, 0, newData, 0, prependSize);
                    Array.Copy(target.Data, 0, newData, prependSize, target.Data.Length);
                    target.Data = newData;
                    target.MinimumAddress = minAddr;
                    dataOffset = prependSize;
                    selfDataOffset = prependSize;
                }

                // Overwrite overlapping part
                int selfDataLeft = target.Data.Length - selfDataOffset;
                int remainingData = data.Length - dataOffset;

                if (remainingData <= selfDataLeft)
                {
                    Array.Copy(data, dataOffset, target.Data, selfDataOffset, remainingData);
                }
                else
                {
                    // Overwrite what fits, then append the rest
                    Array.Copy(data, dataOffset, target.Data, selfDataOffset, selfDataLeft);
                    int appendSize = remainingData - selfDataLeft;
                    byte[] newData = new byte[target.Data.Length + appendSize];
                    Array.Copy(target.Data, 0, newData, 0, target.Data.Length);
                    Array.Copy(data, dataOffset + selfDataLeft, newData, target.Data.Length, appendSize);
                    target.Data = newData;
                    target.MaximumAddress = maxAddr;
                }
            }
            else
            {
                throw new AddDataException(
                    (int)Math.Max(target.MinimumAddress, minAddr));
            }
        }


        /// <summary>
        /// Removes data from the specified address range.
        /// </summary>
        /// <param name="minimumAddress">The minimum address to remove.</param>
        /// <param name="maximumAddress">The maximum address to remove.</param>
        public void Remove(ulong minimumAddress, ulong maximumAddress)
        {
            if (maximumAddress <= minimumAddress)
            {
                throw new ArgumentException("Maximum address must be greater than minimum address");
            }

            List<Segment> newSegments = new List<Segment>();

            foreach (var segment in _segments)
            {
                var result = segment.RemoveData(minimumAddress, maximumAddress);

                if (result == null)
                {
                    // Segment completely removed, skip it
                    continue;
                }

                if (result.Value.Left != null)
                {
                    newSegments.Add(result.Value.Left);
                }

                if (result.Value.Right != null)
                {
                    newSegments.Add(result.Value.Right);
                }
            }

            _segments.Clear();
            _segments.AddRange(newSegments);
        }

        /// <summary>
        /// Returns chunks of data from all segments.
        /// EXACTLY like Python: size and alignment are in WORDS.
        /// </summary>
        /// <param name="size">The size of each chunk in WORDS.</param>
        /// <param name="alignment">The alignment boundary in WORDS.</param>
        /// <param name="padding">Optional padding bytes to use for alignment.</param>
        /// <returns>An enumerable of tuples containing address in WORDS and chunk data.</returns>
        public IEnumerable<(ulong Address, byte[] Data)> Chunks(int size = 32, int alignment = 1, byte[]? padding = null)
        {
            // EXACTLY like Python validation
            if ((size % alignment) != 0)
            {
                throw new BincopyException($"size {size} is not a multiple of alignment {alignment}");
            }

            if (padding != null && padding.Length != WordSizeBytes)
            {
                throw new BincopyException($"padding must be a word value (size {WordSizeBytes}), got {padding.Length} bytes");
            }

            // Python: previous = Segment(-1, -1, b'', 1)
            (ulong Address, byte[] Data)? previous = null;

            foreach (var segment in _segments)
            {
                foreach (var chunk in segment.Chunks(size, alignment, padding))
                {
                    var currentChunk = chunk;
                    
                    // Python: When chunks are padded to alignment, the final chunk of the previous
                    // segment and the first chunk of the current segment may overlap by
                    // one alignment block. To avoid overwriting data from the lower
                    // segment, the chunks must be merged.
                    // if chunk.address < previous.address + len(previous):
                    if (previous.HasValue && currentChunk.Address < previous.Value.Address + (ulong)(previous.Value.Data.Length / WordSizeBytes))
                    {
                        // Python:
                        // low = previous.data[-alignment * self.word_size_bytes:]
                        // high = chunk.data[:alignment * self.word_size_bytes]
                        // merged = int.to_bytes(int.from_bytes(low, 'big') ^
                        //                       int.from_bytes(high, 'big') ^
                        //                       int.from_bytes(alignment * padding, 'big'),
                        //                       alignment * self.word_size_bytes, 'big')
                        // chunk.data = merged + chunk.data[alignment * self.word_size_bytes:]
                        
                        int alignmentBytes = alignment * WordSizeBytes;
                        byte[] low = new byte[alignmentBytes];
                        byte[] high = new byte[alignmentBytes];
                        
                        // Get last alignment block from previous chunk
                        Array.Copy(previous.Value.Data, previous.Value.Data.Length - alignmentBytes, low, 0, alignmentBytes);
                        
                        // Get first alignment block from current chunk
                        Array.Copy(currentChunk.Data, 0, high, 0, alignmentBytes);
                        
                        // Create alignment * padding
                        byte[] alignmentPadding = new byte[alignmentBytes];
                        if (padding != null)
                        {
                            for (int i = 0; i < alignment; i++)
                            {
                                Array.Copy(padding, 0, alignmentPadding, i * WordSizeBytes, WordSizeBytes);
                            }
                        }
                        
                        // XOR: low ^ high ^ alignmentPadding
                        byte[] merged = new byte[alignmentBytes];
                        for (int i = 0; i < alignmentBytes; i++)
                        {
                            merged[i] = (byte)(low[i] ^ high[i] ^ alignmentPadding[i]);
                        }
                        
                        // Replace first alignment block of chunk with merged data
                        byte[] newChunkData = new byte[currentChunk.Data.Length];
                        Array.Copy(merged, 0, newChunkData, 0, alignmentBytes);
                        Array.Copy(currentChunk.Data, alignmentBytes, newChunkData, alignmentBytes, currentChunk.Data.Length - alignmentBytes);
                        
                        currentChunk = (currentChunk.Address, newChunkData);
                    }

                    yield return currentChunk;
                    previous = currentChunk;
                }
            }
        }

        private int FindInsertionPoint(ulong address)
        {
            // Binary search for insertion point
            int left = 0;
            int right = _segments.Count;

            while (left < right)
            {
                int mid = (left + right) / 2;
                if (_segments[mid].MinimumAddress < address)
                {
                    left = mid + 1;
                }
                else
                {
                    right = mid;
                }
            }

            return left;
        }

        private void MergeAdjacentSegments()
        {
            if (_segments.Count <= 1)
            {
                return;
            }

            List<Segment> merged = new List<Segment>();
            Segment? current = _segments[0];

            for (int i = 1; i < _segments.Count; i++)
            {
                var next = _segments[i];

                // Check if segments are adjacent
                if (current.MaximumAddress == next.MinimumAddress)
                {
                    // Merge segments
                    byte[] mergedData = new byte[current.Data.Length + next.Data.Length];
                    Array.Copy(current.Data, 0, mergedData, 0, current.Data.Length);
                    Array.Copy(next.Data, 0, mergedData, current.Data.Length, next.Data.Length);

                    current = new Segment(
                        current.MinimumAddress,
                        next.MaximumAddress,
                        mergedData,
                        WordSizeBytes);
                }
                else
                {
                    merged.Add(current);
                    current = next;
                }
            }

            if (current != null)
            {
                merged.Add(current);
            }

            _segments.Clear();
            _segments.AddRange(merged);
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
            return $"Segments(count={Count}, word_size={WordSizeBytes})";
        }
    }
}

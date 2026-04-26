using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BincopySharp
{
    /// <summary>
    /// Main class for reading, writing, and manipulating binary files in various formats.
    /// </summary>
    public class BinFile
    {
        private readonly Segments _segments;
        private readonly string? _headerEncoding;
        private byte[]? _headerBytes;
        private string? _headerTextCache;

        /// <summary>
        /// Gets or sets the execution start address.
        /// </summary>
        public ulong? ExecutionStartAddress { get; set; }

        /// <summary>
        /// Gets or sets the header as raw bytes.
        /// Returns null if no header is set.
        /// </summary>
        public byte[]? HeaderBytes
        {
            get => _headerBytes;
            set
            {
                _headerBytes = value;
                _headerTextCache = null;
            }
        }

        /// <summary>
        /// Gets the header as a decoded string using the configured header encoding.
        /// Returns null if no header is set or no encoding is configured.
        /// The decoded string is cached to avoid repeated Encoding.GetString() calls.
        /// </summary>
        public string? HeaderText
        {
            get
            {
                if ((_headerBytes == null) || (_headerEncoding == null))
                {
                    return null;
                }

                if (_headerTextCache == null)
                {
                    _headerTextCache = Encoding.GetEncoding(_headerEncoding).GetString(_headerBytes);
                }

                return _headerTextCache;
            }
        }

        /// <summary>
        /// Gets the minimum address across all segments in WORDS.
        /// </summary>
        public ulong MinimumAddress
        {
            get
            {
                var min = _segments.MinimumAddress;
                if (min == null)
                {
                    throw new InvalidOperationException("No data available");
                }
                // Convert from byte address to word address
                return min.Value / (ulong)WordSizeBytes;
            }
        }

        /// <summary>
        /// Gets the maximum address across all segments in WORDS.
        /// </summary>
        public ulong MaximumAddress
        {
            get
            {
                var max = _segments.MaximumAddress;
                if (max == null)
                {
                    throw new InvalidOperationException("No data available");
                }
                // Convert from byte address to word address
                return max.Value / (ulong)WordSizeBytes;
            }
        }

        /// <summary>
        /// Gets the segments collection.
        /// </summary>
        public Segments Segments => _segments;

        /// <summary>
        /// Gets the word size in bits. This value is immutable after construction.
        /// </summary>
        public int WordSizeBits => _segments.WordSizeBits;

        /// <summary>
        /// Gets the word size in bytes (derived from WordSizeBits).
        /// </summary>
        private int WordSizeBytes => WordSizeBits / 8;


        /// <summary>
        /// Initializes a new instance of the BinFile class.
        /// </summary>
        /// <param name="wordSizeBits">The word size in bits. Default is 8.</param>
        /// <param name="headerEncoding">The encoding used for the header. Use null for binary headers (byte[]). Default is "utf-8".</param>
        /// <exception cref="ArgumentException">Thrown when wordSizeBits is not a positive multiple of 8.</exception>
        public BinFile(int wordSizeBits = 8, string? headerEncoding = "utf-8")
        {
            if ((wordSizeBits <= 0) ||
                ((wordSizeBits % 8) != 0) ||
                (wordSizeBits > 64))
            {
                throw new ArgumentException($"Word size must be a positive multiple of 8 bits up to 64, got {wordSizeBits}", nameof(wordSizeBits));
            }

            _segments = new Segments(wordSizeBits);
            _headerEncoding = headerEncoding;
        }

        /// <summary>
        /// Gets or sets the word value at the specified address (in words).
        /// Bytes are interpreted in big-endian order.
        /// Addresses that fall in a gap between segments return a word filled with 0xFF bytes.
        /// </summary>
        /// <example>
        /// With wordSizeBits=16, if the bytes at address 0 are [0xAA, 0xBB]:
        /// <code>binFile[0] == 0xAABB</code>
        /// </example>
        /// <param name="address">The address in words.</param>
        /// <returns>The word value as a <see cref="ulong"/> (big-endian).</returns>
        public ulong this[ulong address]
        {
            get
            {
                ulong addrBytes = address * (ulong)WordSizeBytes;
                ulong? minAddr = _segments.MinimumAddress;
                ulong? maxAddr = _segments.MaximumAddress;

                if ((minAddr == null) ||
                    (maxAddr == null) ||
                    (addrBytes < minAddr.Value) ||
                    (addrBytes >= maxAddr.Value))
                {
                    throw new IndexOutOfRangeException($"Binary file index {address} out of range");
                }

                byte[] wordBytes = AsBinary(address, address + 1);
                ulong result = 0;
                for (int i = 0; i < WordSizeBytes; i++)
                {
                    result = (result << 8) | wordBytes[i];
                }
                return result;
            }
            set
            {
                // Validate that value fits in WordSizeBits. For 64-bit words any ulong is valid.
                if (WordSizeBits < 64)
                {
                    ulong maxValue = (1UL << WordSizeBits) - 1UL;
                    if (value > maxValue)
                    {
                        throw new ArgumentOutOfRangeException(nameof(value),
                            $"Value 0x{value:X} does not fit in a {WordSizeBits}-bit word (max 0x{maxValue:X})");
                    }
                }

                ulong addrBytes = address * (ulong)WordSizeBytes;
                byte[] data = new byte[WordSizeBytes];
                ulong v = value;
                for (int i = WordSizeBytes - 1; i >= 0; i--)
                {
                    data[i] = (byte)(v & 0xFF);
                    v >>= 8;
                }

                var newSegment = new Segment(addrBytes, addrBytes + (ulong)WordSizeBytes, data, WordSizeBits);
                _segments.Add(newSegment, overwrite: true);
            }
        }


        /// <summary>
        /// Gets the total length of data across all segments in WORDS.
        /// </summary>
        public ulong Length
        {
            get
            {
                // Sum of all segment data lengths, converted to words
                ulong totalBytes = 0;
                foreach (var segment in _segments)
                {
                    totalBytes += segment.Length;
                }
                return totalBytes / (ulong)WordSizeBytes;
            }
        }

        /// <summary>
        /// Adds binary data at the specified address.
        /// </summary>
        /// <param name="data">The data to add.</param>
        /// <param name="address">The starting address in WORDS. Default is 0.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        public void Add(byte[] data, ulong address = 0, bool overwrite = false)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.Length == 0)
            {
                return;
            }

            if ((data.Length % WordSizeBytes) != 0)
            {
                throw new ArgumentException(
                    $"Data length ({data.Length} bytes) must be a multiple of the word size ({WordSizeBytes} bytes)",
                    nameof(data));
            }

            // Address is in words, convert to bytes
            address *= (ulong)WordSizeBytes;

            // Create segment with addresses in bytes
            ulong maximumAddress = address + (ulong)data.Length;
            var segment = new Segment(address, maximumAddress, data, WordSizeBits);
            _segments.Add(segment, overwrite);
        }

        /// <summary>
        /// Adds data from a string by automatically detecting the format.
        /// Supports SREC, Intel HEX, TI-TXT and VMEM formats.
        /// </summary>
        /// <param name="data">The data string to add.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
        /// <exception cref="UnsupportedFileFormatException">Thrown when the format cannot be detected.</exception>
        public void Add(string data, bool overwrite = false)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var detector = new Formats.FormatDetector();
            var parser = detector.DetectFormat(data);
            ApplyParseResult(parser.Parse(data, WordSizeBytes), overwrite);
        }

        /// <summary>
        /// Validates that the filename is not null or whitespace.
        /// </summary>
        /// <param name="filename">The filename to validate.</param>
        /// <exception cref="ArgumentNullException">Thrown when filename is null.</exception>
        /// <exception cref="ArgumentException">Thrown when filename is empty or whitespace.</exception>
        private void ValidateFilename(string filename)
        {
            if (filename == null)
            {
                throw new ArgumentNullException(nameof(filename));
            }

            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentException("Filename cannot be empty or whitespace", nameof(filename));
            }
        }

/// <summary>
        /// Applies a ParseResult to this BinFile: sets header and execution start address if present,
        /// and adds all segments.
        /// </summary>
        /// <param name="result">The parse result to apply.</param>
        /// <param name="overwrite">Whether to overwrite existing data.</param>
        private void ApplyParseResult(ParseResult result, bool overwrite)
        {
            if ((result.Header != null) && (result.Header.Length > 0))
            {
                _headerBytes = result.Header;
                _headerTextCache = null;
            }

            if (result.ExecutionStartAddress.HasValue)
            {
                ExecutionStartAddress = result.ExecutionStartAddress.Value;
            }

            foreach (var segment in result.Segments)
            {
                _segments.Add(segment, overwrite);
            }
        }

        /// <summary>
        /// Adds data from a file by automatically detecting the format.
        /// Supports SREC, Intel HEX, TI-TXT, VMEM, and ELF formats.
        /// Raw binary files are not supported — use AddBinaryFile() instead.
        /// This is intentional: falling back to binary would silently load unrecognized or corrupt files as valid data.
        /// </summary>
        /// <param name="filename">The path to the file.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        /// <exception cref="ArgumentNullException">Thrown when filename is null.</exception>
        /// <exception cref="ArgumentException">Thrown when filename is empty or whitespace.</exception>
        /// <exception cref="UnsupportedFileFormatException">Thrown when the format cannot be detected.</exception>
        public void AddFile(string filename, bool overwrite = false)
        {
            ValidateFilename(filename);

            try
            {
                Add(File.ReadAllText(filename), overwrite);
                return;
            }
            catch (UnsupportedFileFormatException) { }
            catch (DecoderFallbackException) { }

            try
            {
                AddElf(File.ReadAllBytes(filename), overwrite);
                return;
            }
            catch (UnsupportedFileFormatException) { }

            throw new UnsupportedFileFormatException(filename, "Unable to detect file format");
        }

        /// <summary>
        /// Combines two BinFile instances.
        /// </summary>
        /// <param name="a">The first BinFile.</param>
        /// <param name="b">The second BinFile.</param>
        /// <returns>A new BinFile containing data from both inputs.</returns>
        public static BinFile operator +(BinFile a, BinFile b)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }
            if (b == null)
            {
                throw new ArgumentNullException(nameof(b));
            }

            if (a.WordSizeBits != b.WordSizeBits)
            {
                throw new ArgumentException("Cannot combine BinFiles with different word sizes");
            }

            var result = new BinFile(a.WordSizeBits, a._headerEncoding);

            // Deep copy segments from 'a' to avoid aliasing
            foreach (var segment in a._segments)
            {
                var copy = new Segment(segment.MinimumAddress, segment.MaximumAddress, segment.DataSpan.ToArray(), segment.WordSizeBits);
                result._segments.Add(copy, overwrite: false);
            }

            // Deep copy segments from 'b' to avoid aliasing
            foreach (var segment in b._segments)
            {
                var copy = new Segment(segment.MinimumAddress, segment.MaximumAddress, segment.DataSpan.ToArray(), segment.WordSizeBits);
                result._segments.Add(copy, overwrite: false);
            }

            // Preserve metadata from first file
            result.ExecutionStartAddress = a.ExecutionStartAddress ?? b.ExecutionStartAddress;

            if (a._headerBytes != null)
            {
                result._headerBytes = (byte[])a._headerBytes.Clone();
            }
            else if (b._headerBytes != null)
            {
                result._headerBytes = (byte[])b._headerBytes.Clone();
            }

            result._headerTextCache = null;

            return result;
        }

        /// <summary>
        /// Adds Motorola S-Record (SREC) data from a string.
        /// </summary>
        /// <param name="records">The SREC records as a string.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        public void AddSrec(string records, bool overwrite = false)
        {
            if (records == null)
            {
                throw new ArgumentNullException(nameof(records));
            }

            var parser = new Formats.SrecParser();
            ApplyParseResult(parser.Parse(records, WordSizeBytes), overwrite);
        }

        /// <summary>
        /// Adds Motorola S-Record (SREC) data from a file.
        /// </summary>
        /// <param name="filename">The path to the SREC file.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        public void AddSrecFile(string filename, bool overwrite = false)
        {
            ValidateFilename(filename);
            AddSrec(File.ReadAllText(filename), overwrite);
        }

        /// <summary>
        /// Converts the binary data to Motorola S-Record (SREC) format.
        /// </summary>
        /// <param name="numberOfDataBytes">The number of data bytes per record. Default is 32.</param>
        /// <param name="addressLengthBits">The address length in bits (16, 24, or 32). Default is 32.</param>
        /// <returns>A string containing the SREC records.</returns>
        public string AsSrec(uint numberOfDataBytes = 32, uint addressLengthBits = 32)
        {
            var serializer = new Formats.SrecSerializer();
            var options = new Formats.SerializerOptions
            {
                NumberOfDataBytes = numberOfDataBytes,
                AddressLengthBits = addressLengthBits,
                HeaderBytes = _headerBytes,
                ExecutionStartAddress = ExecutionStartAddress
            };

            return serializer.Serialize(_segments, options);
        }

        /// <summary>
        /// Adds Intel HEX data from a string.
        /// </summary>
        /// <param name="records">The Intel HEX records as a string.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        public void AddIhex(string records, bool overwrite = false)
        {
            if (records == null)
            {
                throw new ArgumentNullException(nameof(records));
            }

            var parser = new Formats.IhexParser();
            ApplyParseResult(parser.Parse(records, WordSizeBytes), overwrite);
        }

        /// <summary>
        /// Adds Intel HEX data from a file.
        /// </summary>
        /// <param name="filename">The path to the Intel HEX file.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        public void AddIhexFile(string filename, bool overwrite = false)
        {
            ValidateFilename(filename);
            AddIhex(File.ReadAllText(filename), overwrite);
        }

        /// <summary>
        /// Converts the binary data to Intel HEX format.
        /// </summary>
        /// <param name="numberOfDataBytes">The number of data bytes per record. Default is 32.</param>
        /// <param name="addressLengthBits">The address length in bits (16, 24, or 32). Default is 32.</param>
        /// <returns>A string containing the Intel HEX records.</returns>
        public string AsIhex(uint numberOfDataBytes = 32, uint addressLengthBits = 32)
        {
            var serializer = new Formats.IhexSerializer();
            var options = new Formats.SerializerOptions
            {
                NumberOfDataBytes = numberOfDataBytes,
                AddressLengthBits = addressLengthBits,
                ExecutionStartAddress = ExecutionStartAddress
            };

            return serializer.Serialize(_segments, options);
        }

        /// <summary>
        /// Adds TI-TXT data from a string.
        /// </summary>
        /// <param name="lines">The TI-TXT lines as a string.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        public void AddTiTxt(string lines, bool overwrite = false)
        {
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            var parser = new Formats.TiTxtParser();
            ApplyParseResult(parser.Parse(lines, WordSizeBytes), overwrite);
        }

        /// <summary>
        /// Adds TI-TXT data from a file.
        /// </summary>
        /// <param name="filename">The path to the TI-TXT file.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        public void AddTiTxtFile(string filename, bool overwrite = false)
        {
            ValidateFilename(filename);
            AddTiTxt(File.ReadAllText(filename), overwrite);
        }

        /// <summary>
        /// Converts the binary data to TI-TXT format.
        /// </summary>
        /// <returns>A string containing the TI-TXT data.</returns>
        public string AsTiTxt()
        {
            var serializer = new Formats.TiTxtSerializer();
            return serializer.Serialize(_segments);
        }

        /// <summary>
        /// Adds Verilog VMEM data from a string.
        /// </summary>
        /// <param name="data">The VMEM data as a string.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        public void AddVerilogVmem(string data, bool overwrite = false)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var parser = new Formats.VmemParser();
            ApplyParseResult(parser.Parse(data, WordSizeBytes), overwrite);
        }

        /// <summary>
        /// Adds Verilog VMEM data from a file.
        /// </summary>
        /// <param name="filename">The path to the VMEM file.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        public void AddVerilogVmemFile(string filename, bool overwrite = false)
        {
            ValidateFilename(filename);
            AddVerilogVmem(File.ReadAllText(filename), overwrite);
        }

        /// <summary>
        /// Converts the binary data to Verilog VMEM format.
        /// </summary>
        /// <returns>A string containing the VMEM data.</returns>
        public string AsVerilogVmem()
        {
            var serializer = new Formats.VmemSerializer();

            var options = new Formats.SerializerOptions
            {
                Header = HeaderText
            };
            return serializer.Serialize(_segments, options);
        }

        /// <summary>
        /// Adds binary data from a file at the specified address.
        /// </summary>
        /// <param name="filename">The path to the binary file.</param>
        /// <param name="address">The starting address in WORDS. Default is 0.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        public void AddBinaryFile(string filename, ulong address = 0, bool overwrite = false)
        {
            ValidateFilename(filename);
            Add(File.ReadAllBytes(filename), address, overwrite);
        }

        /// <summary>
        /// Converts the binary data to a raw binary byte array.
        /// </summary>
        /// <param name="minimumAddress">The minimum address (inclusive). Null for segment minimum.</param>
        /// <param name="maximumAddress">The maximum address (exclusive). Null for segment maximum.</param>
        /// <param name="padding">The padding byte to use for gaps. Default is 0xFF.</param>
        /// <returns>A byte array containing the binary data.</returns>
        public byte[] AsBinary(ulong? minimumAddress = null, ulong? maximumAddress = null, byte padding = 0xFF)
        {
            var serializer = new Formats.BinarySerializer();
            return serializer.SerializeBinary(_segments, minimumAddress, maximumAddress, padding, WordSizeBytes);
        }

        /// <summary>
        /// Adds ELF (Executable and Linkable Format) data from a byte array.
        /// </summary>
        /// <param name="data">The ELF file data.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is true.</param>
        public void AddElf(byte[] data, bool overwrite = true)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var parser = new Formats.ElfParser();
            ApplyParseResult(parser.ParseElf(data, WordSizeBytes), overwrite);
        }

        /// <summary>
        /// Adds ELF (Executable and Linkable Format) data from a file.
        /// </summary>
        /// <param name="filename">The path to the ELF file.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is true.</param>
        public void AddElfFile(string filename, bool overwrite = true)
        {
            ValidateFilename(filename);
            AddElf(File.ReadAllBytes(filename), overwrite);
        }

        /// <summary>
        /// Fills empty space between segments with a specified value.
        /// </summary>
        /// <param name="value">The byte value to fill with. Default is 0xFF.</param>
        /// <param name="maxWords">Maximum number of words to fill between segments. Null fills all gaps.</param>
        public void Fill(byte? value = null, ulong? maxWords = null)
        {
            byte fillValue = value ?? 0xFF;

            // Create padding array for the word size
            byte[] padding = new byte[WordSizeBytes];
            for (int i = 0; i < WordSizeBytes; i++)
            {
                padding[i] = fillValue;
            }

            Fill(padding, maxWords);
        }

        /// <summary>
        /// Fills gaps between segments with custom padding.
        /// </summary>
        /// <param name="padding">The padding byte array to use. Must be a word value (size = WordSizeBytes).</param>
        /// <param name="maxWords">Maximum gap size in words to fill. Gaps larger than this are not filled.</param>
        public void Fill(byte[] padding, ulong? maxWords = null)
        {
            if (padding.Length != WordSizeBytes)
            {
                throw new ArgumentException($"Padding must be a word value (size {WordSizeBytes}), but got {padding.Length} bytes");
            }

            if (_segments.Count == 0)
            {
                return;
            }

            ulong? previousMaxAddress = null;
            var fillSegments = new List<Segment>();

            foreach (var segment in _segments)
            {
                if (previousMaxAddress.HasValue)
                {
                    ulong gapSize = segment.MinimumAddress - previousMaxAddress.Value;
                    ulong gapSizeWords = gapSize / (ulong)WordSizeBytes;

                    if (!maxWords.HasValue || gapSizeWords <= maxWords.Value)
                    {
                        // Create fill data by repeating the padding word using block copy doubling
                        byte[] fillData = new byte[gapSize];
                        // Seed the first word
                        Array.Copy(padding, 0, fillData, 0, WordSizeBytes);
                        // Double using BlockCopy
                        int filled = WordSizeBytes;
                        while (filled < fillData.Length)
                        {
                            int toCopy = Math.Min(filled, fillData.Length - filled);
                            Buffer.BlockCopy(fillData, 0, fillData, filled, toCopy);
                            filled += toCopy;
                        }

                        var fillSegment = new Segment(
                            previousMaxAddress.Value,
                            segment.MinimumAddress,
                            fillData,
                            WordSizeBits);
                        fillSegments.Add(fillSegment);
                    }
                }

                previousMaxAddress = segment.MaximumAddress;
            }

            // Add all fill segments
            foreach (var fillSegment in fillSegments)
            {
                _segments.Add(fillSegment, overwrite: false);
            }
        }


        /// <summary>
        /// Excludes (removes) data in the specified address range.
        /// </summary>
        /// <param name="minimumAddress">The minimum address to exclude (inclusive).</param>
        /// <param name="maximumAddress">The maximum address to exclude (exclusive).</param>
        public void Exclude(ulong minimumAddress, ulong maximumAddress)
        {
            if (maximumAddress < minimumAddress)
            {
                throw new ArgumentException("Maximum address must be greater than or equal to minimum address");
            }

            if (maximumAddress == minimumAddress)
            {
                return; // No-op for empty range
            }

            ulong minAddr = minimumAddress * (ulong)WordSizeBytes;
            ulong maxAddr = maximumAddress * (ulong)WordSizeBytes;
            _segments.Remove(minAddr, maxAddr);
        }

        /// <summary>
        /// Keeps only data in the specified address range and discards the rest.
        /// </summary>
        /// <param name="minimumAddress">The minimum address to keep (inclusive).</param>
        /// <param name="maximumAddress">The maximum address to keep (exclusive).</param>
        public void Crop(ulong minimumAddress, ulong maximumAddress)
        {
            if (maximumAddress < minimumAddress)
            {
                throw new ArgumentException("Maximum address must be greater than or equal to minimum address");
            }

            if (maximumAddress == minimumAddress)
            {
                return;
            }

            ulong? segmentsMaxAddr = _segments.MaximumAddress;
            if (!segmentsMaxAddr.HasValue)
            {
                return;
            }

            ulong minAddr = minimumAddress * (ulong)WordSizeBytes;
            ulong maxAddr = maximumAddress * (ulong)WordSizeBytes;

            if (minAddr > 0)
            {
                _segments.Remove(0, minAddr);
            }

            if (maxAddr < segmentsMaxAddr.Value)
            {
                _segments.Remove(maxAddr, segmentsMaxAddr.Value);
            }
        }

        /// <summary>
        /// Formats the binary data as an array of hex values separated by the specified separator.
        /// This can be used to generate array initialization code for C and other languages.
        /// </summary>
        /// <param name="minimumAddress">The minimum address (inclusive). Null for segment minimum.</param>
        /// <param name="padding">The padding byte to use for gaps. Default is 0xFF.</param>
        /// <param name="separator">The separator between values. Default is ", ".</param>
        /// <returns>A string containing hex values separated by the separator.</returns>
        public string AsArray(ulong? minimumAddress = null, byte padding = 0xFF, string separator = ", ")
        {
            byte[] binaryData = AsBinary(minimumAddress, null, padding);
            var words = new List<string>();
            int hexWidth = WordSizeBytes * 2; // 2 hex chars per byte: 8-bit→x2, 16-bit→x4, 32-bit→x8

            for (int offset = 0; offset < binaryData.Length; offset += WordSizeBytes)
            {
                ulong word = 0;

                for (int i = 0; i < WordSizeBytes && (offset + i) < binaryData.Length; i++)
                {
                    word <<= 8;
                    word += binaryData[offset + i];
                }

                words.Add($"0x{word.ToString("x" + hexWidth)}");
            }

            return string.Join(separator, words);
        }

        /// <summary>
        /// Formats the binary data as a hexdump string similar to the standard hexdump tool.
        /// </summary>
        /// <returns>A string containing the hexdump representation.</returns>
        public string AsHexdump()
        {
            // Empty file?
            if (Length == 0)
            {
                return "\n";
            }

            var lines = new List<string>();
            // Addresses are displayed in words
            ulong lineAddress = AlignToLine(MinimumAddress * (ulong)WordSizeBytes) / (ulong)WordSizeBytes;
            var lineData = new List<byte?>();

            foreach (var chunk in _segments.Chunks(16 / WordSizeBytes, 16 / WordSizeBytes))
            {
                ulong chunkAddress = chunk.Address;  // Already in WORDS
                byte[] chunkData = chunk.Data;
                int chunkOffset = 0;

                while (chunkOffset < chunkData.Length)
                {
                    ulong currentByteAddress = chunkAddress * (ulong)WordSizeBytes + (ulong)chunkOffset;
                    ulong alignedLineAddress = AlignToLine(currentByteAddress) / (ulong)WordSizeBytes;

                    // If we've moved to a new line, output the previous line
                    if (alignedLineAddress > lineAddress)
                    {
                        if (lineData.Count > 0)
                        {
                            lines.Add(FormatHexdumpLine(lineAddress, lineData));
                        }

                        // Check if there's a gap
                        if (alignedLineAddress > lineAddress + (16 / (ulong)WordSizeBytes))
                        {
                            lines.Add("...");
                        }

                        lineAddress = alignedLineAddress;
                        lineData.Clear();
                    }

                    // Add padding before chunk data if needed
                    while (lineData.Count < (int)(currentByteAddress - (lineAddress * (ulong)WordSizeBytes)))
                    {
                        lineData.Add(null);
                    }

                    // Add bytes from chunk until end of line or end of chunk
                    int bytesUntilEndOfLine = 16 - lineData.Count;
                    int bytesToAdd = Math.Min(bytesUntilEndOfLine, chunkData.Length - chunkOffset);

                    for (int i = 0; i < bytesToAdd; i++)
                    {
                        lineData.Add(chunkData[chunkOffset + i]);
                    }

                    chunkOffset += bytesToAdd;
                }
            }

            // Output the last line
            if (lineData.Count > 0)
            {
                lines.Add(FormatHexdumpLine(lineAddress, lineData));
            }

            return string.Join("\n", lines) + "\n";
        }

        private ulong AlignToLine(ulong addressInBytes)
        {
            // Align to 16 bytes
            return addressInBytes - (addressInBytes % 16);
        }

        private string FormatHexdumpLine(ulong address, IReadOnlyList<byte?> data)
        {
            var hexdata = new List<string>();
            for (int i = 0; i < 16; i++)
            {
                byte? b = (i < data.Count) ? data[i] : null;
                hexdata.Add(b.HasValue ? $"{b.Value:x2}" : "  ");
            }

            string firstHalf = string.Join(" ", hexdata.GetRange(0, 8));
            string secondHalf = string.Join(" ", hexdata.GetRange(8, 8));

            var text = new StringBuilder();
            for (int i = 0; i < 16; i++)
            {
                byte? b = (i < data.Count) ? data[i] : null;
                if (!b.HasValue)
                {
                    text.Append(' ');
                }
                else if (IsPrintable((char)b.Value))
                {
                    text.Append((char)b.Value);
                }
                else
                {
                    text.Append('.');
                }
            }

            return $"{address:x8}  {firstHalf}  {secondHalf}  |{text}|";
        }

        private bool IsPrintable(char c) => (c >= 32) && (c <= 126);



        /// <summary>
        /// Returns human-readable information about the binary file including header,
        /// execution start address, and data ranges.
        /// </summary>
        /// <returns>A string containing information about the binary file.</returns>
        public string Info()
        {
            var info = new StringBuilder();

            // Add header if present
            if (_headerBytes != null)
            {
                string headerDisplay;
                if (HeaderText != null)
                {
                    headerDisplay = $"\"{HeaderText}\"";
                }
                else
                {
                    // No encoding — display binary header with escape sequences
                    var sb = new StringBuilder();
                    foreach (byte b in _headerBytes)
                    {
                        if ((b >= 32) && (b <= 126) && (b != '\\'))  // Printable ASCII
                        {
                            sb.Append((char)b);
                        }
                        else
                        {
                            sb.Append($"\\x{b:x2}");
                        }
                    }
                    headerDisplay = sb.ToString();
                }

                info.Append($"Header:                  {headerDisplay}\n");
            }

            // Add execution start address if present
            if (ExecutionStartAddress.HasValue)
            {
                info.Append($"Execution start address: 0x{ExecutionStartAddress.Value:x8}\n");
            }

            info.Append("Data ranges:\n");
            info.Append('\n');

            // Add segment information
            foreach (var segment in _segments)
            {
                ulong minimumAddress = segment.MinimumAddress / (ulong)WordSizeBytes;
                int size = segment.Length;
                ulong maximumAddress = minimumAddress + ((ulong)size / (ulong)WordSizeBytes);

                string sizeStr = FormatSize(size);
                info.Append($"    0x{minimumAddress:x8} - 0x{maximumAddress:x8} ({sizeStr})\n");
            }

            return info.ToString();
        }

        /// <summary>
        /// Returns the memory layout as a visual ASCII representation.
        /// Shows data segments as '=' and gaps as ' ' or '-'.
        /// </summary>
        /// <returns>A string containing the memory layout visualization.</returns>
        public string Layout()
        {
            if (_segments.Count == 0)
            {
                return "\n";
            }

            ulong size = MaximumAddress - MinimumAddress;
            int width = (int)Math.Min(80UL, size);
            ulong chunkAddress = MinimumAddress;
            ulong chunkSize = size / (ulong)width;

            if (chunkSize == 0)
            {
                chunkSize = 1;
            }

            string minAddrStr = $"0x{MinimumAddress:x}";
            string maxAddrStr = $"0x{MaximumAddress:x}";
            int padding = Math.Max(width - minAddrStr.Length - maxAddrStr.Length, 0);

            var output = new StringBuilder();
            output.Append($"{minAddrStr}{new string(' ', padding)}{maxAddrStr}\n");

            for (int i = 0; i < width; i++)
            {
                ulong columnMax;
                if (i < (width - 1))
                {
                    columnMax = chunkAddress + chunkSize;
                }
                else
                {
                    columnMax = MaximumAddress;
                }

                ulong coveredWords = 0;
                ulong columnSpanWords = columnMax - chunkAddress;
                ulong wordSizeBytes = (ulong)WordSizeBytes;

                foreach (var segment in _segments)
                {
                    ulong segmentAddress = segment.MinimumAddress / wordSizeBytes;
                    ulong segmentLength = (ulong)segment.Length / wordSizeBytes;
                    ulong segmentEnd = segmentAddress + segmentLength;

                    if (segmentAddress >= columnMax || segmentEnd <= chunkAddress)
                    {
                        continue; // No overlap
                    }

                    ulong overlapStart = Math.Max(segmentAddress, chunkAddress);
                    ulong overlapEnd = Math.Min(segmentEnd, columnMax);
                    if (overlapEnd > overlapStart)
                    {
                        coveredWords += overlapEnd - overlapStart;
                    }
                }

                if (coveredWords == 0)
                {
                    output.Append(' ');
                }
                else if (coveredWords < columnSpanWords)
                {
                    output.Append('-');
                }
                else
                {
                    output.Append('=');
                }

                chunkAddress += chunkSize;
            }

            output.Append('\n');
            return output.ToString();
        }

        private string FormatSize(ulong bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} bytes";
            }
            else if (bytes < (1024 * 1024))
            {
                double kb = bytes / 1024.0;
                return $"{kb:F2} KiB";
            }
            else
            {
                double mb = bytes / (1024.0 * 1024.0);
                return $"{mb:F2} MiB";
            }
        }

        /// <summary>
        /// Returns a string representation of this BinFile.
        /// </summary>
        /// <returns>A string describing the BinFile.</returns>
        public override string ToString()
        {
            if (_segments.Count == 0)
            {
                return "BinFile(empty)";
            }
            return $"BinFile(segments={_segments.Count}, " +
                   $"address_range=0x{MinimumAddress:X}-0x{MaximumAddress:X}, " +
                   $"length={Length} bytes)";
        }
    }
}

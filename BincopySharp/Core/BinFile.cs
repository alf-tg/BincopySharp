using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BincopySharp.Formats;

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
            set => _headerBytes = value;
        }

        /// <summary>
        /// Gets or sets the header as a decoded string using the configured header encoding.
        /// Returns null if no header is set or no encoding is configured.
        /// Setting this property encodes the string and stores it in HeaderBytes.
        /// Throws InvalidOperationException if no encoding is configured.
        /// </summary>
        public string? HeaderText
        {
            get
            {
                if ((_headerBytes == null) || (_headerEncoding == null))
                {
                    return null;
                }

                return Encoding.GetEncoding(_headerEncoding).GetString(_headerBytes);
            }
            set
            {
                if (_headerEncoding == null)
                {
                    throw new InvalidOperationException("Cannot set HeaderText: no header encoding is configured. Use HeaderBytes instead.");
                }

                _headerBytes = (value == null) ? null : Encoding.GetEncoding(_headerEncoding).GetBytes(value);
            }
        }

        /// <summary>
        /// Gets the minimum address across all segments in BYTES.
        /// </summary>
        public ulong MinimumAddress
        {
            get
            {
                if (_segments.Count == 0)
                {
                    throw new InvalidOperationException("No data available");
                }

                return _segments[0].MinimumAddress;
            }
        }

        /// <summary>
        /// Gets the maximum address across all segments in BYTES.
        /// </summary>
        public ulong MaximumAddress
        {
            get
            {
                if (_segments.Count == 0)
                {
                    throw new InvalidOperationException("No data available");
                }

                return _segments[_segments.Count - 1].MaximumAddress;
            }
        }

        /// <summary>
        /// Gets the segments collection.
        /// </summary>
        public Segments Segments => _segments;

        /// <summary>
        /// Initializes a new instance of the BinFile class.
        /// </summary>
        /// <param name="headerEncoding">The encoding used for the header. Use null for binary headers (byte[]). Default is "utf-8".</param>
        public BinFile(string? headerEncoding = "utf-8")
        {
            _segments = new Segments();
            _headerEncoding = headerEncoding;
        }

        /// <summary>
        /// Gets or sets the value at the specified address (in BYTES).
        /// Addresses that fall in a gap between segments return an 0xFF byte.
        /// </summary>
        /// <param name="address">The address in BYTES.</param>
        /// <returns>Value at given address.</returns>
        public byte this[ulong address]
        {
            get
            {
                if ((_segments.Count == 0) ||
                    (address < _segments[0].MinimumAddress) ||
                    (address >= _segments[_segments.Count - 1].MaximumAddress))
                {
                    throw new IndexOutOfRangeException($"Binary file index {address} out of range");
                }

                byte[] result = AsBinary(address, address + 1);
                return result[0];
            }
            set
            {
                byte[] data = new byte[] { value };
                var newSegment = new Segment(address, address + 1, data);
                _segments.Add(newSegment, overwrite: true);
            }
        }


        /// <summary>
        /// Gets the total length of data across all segments in BYTES.
        /// </summary>
        public int Length
        {
            get
            {
                int totalBytes = 0;
                foreach (var segment in _segments)
                {
                    totalBytes += segment.Length;
                }
                return totalBytes;
            }
        }

        /// <summary>
        /// Adds binary data at the specified address.
        /// </summary>
        /// <param name="data">The data to add.</param>
        /// <param name="address">The starting address in BYTES. Default is 0.</param>
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

            // Create segment with addresses in bytes
            ulong maximumAddress = address + (ulong)data.Length;
            var segment = new Segment(address, maximumAddress, data);
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

            if (string.IsNullOrWhiteSpace(data))
            {
                throw new UnsupportedFileFormatException("Data is empty or whitespace");
            }

            if (Formats.SrecParser.CanParse(data))
            {
                ApplyParseResult(Formats.SrecParser.Parse(data), overwrite);
                return;
            }

            if (Formats.IhexParser.CanParse(data))
            {
                ApplyParseResult(Formats.IhexParser.Parse(data), overwrite);
                return;
            }

            if (Formats.TiTxtParser.CanParse(data))
            {
                ApplyParseResult(Formats.TiTxtParser.Parse(data), overwrite);
                return;
            }

            throw new UnsupportedFileFormatException("Unable to detect file format");
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
        /// Raw binary files are not supported - use AddBinaryFile() instead.
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

            var result = new BinFile(a._headerEncoding);

            // Deep copy segments from 'a' to avoid aliasing
            foreach (var segment in a._segments)
            {
                var copy = new Segment(segment.MinimumAddress, segment.MaximumAddress, segment.Data.ToArray());
                result._segments.Add(copy, overwrite: false);
            }

            // Deep copy segments from 'b' to avoid aliasing
            foreach (var segment in b._segments)
            {
                var copy = new Segment(segment.MinimumAddress, segment.MaximumAddress, segment.Data.ToArray());
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

            ApplyParseResult(Formats.SrecParser.Parse(records), overwrite);
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
        /// <param name="variant">The SREC variant that determines the address width. Default is S37 (32-bit).</param>
        /// <returns>A string containing the SREC records.</returns>
        public string AsSrec(int numberOfDataBytes = 32, Formats.SrecVariant variant = Formats.SrecVariant.S37)
        {
            if (numberOfDataBytes <= 0)
            {
                throw new ArgumentException(
                    $"numberOfDataBytes must be positive, got {numberOfDataBytes}",
                    nameof(numberOfDataBytes));
            }
            return Formats.SrecSerializer.Serialize(_segments, numberOfDataBytes, variant, _headerBytes, ExecutionStartAddress);
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

            ApplyParseResult(Formats.IhexParser.Parse(records), overwrite);
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
        /// <param name="variant">The Intel HEX variant that determines the addressing scheme. Default is I32Hex.</param>
        /// <returns>A string containing the Intel HEX records.</returns>
        public string AsIhex(int numberOfDataBytes = 32, Formats.IhexVariant variant = Formats.IhexVariant.I32Hex)
        {
            if (numberOfDataBytes <= 0)
            {
                throw new ArgumentException(
                    $"numberOfDataBytes must be positive, got {numberOfDataBytes}",
                    nameof(numberOfDataBytes));
            }
            return Formats.IhexSerializer.Serialize(_segments, numberOfDataBytes, variant, ExecutionStartAddress);
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

            ApplyParseResult(Formats.TiTxtParser.Parse(lines), overwrite);
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
            return Formats.TiTxtSerializer.Serialize(_segments);
        }

        /// <summary>
        /// Adds binary data from a file at the specified address.
        /// </summary>
        /// <param name="filename">The path to the binary file.</param>
        /// <param name="address">The starting address in BYTES. Default is 0.</param>
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
            return Formats.BinarySerializer.SerializeBinary(_segments, minimumAddress, maximumAddress, padding);
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

            ApplyParseResult(Formats.ElfParser.ParseElf(data), overwrite);
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
        /// <param name="maxBytes">Maximum gap size in bytes to fill. Gaps larger than this are not filled.</param>
        public void Fill(byte? value = null, int? maxBytes = null)
        {
            if (maxBytes.HasValue && (maxBytes.Value < 0))
            {
                throw new ArgumentException($"maxBytes must be positive, got {maxBytes}", nameof(maxBytes));
            }

            if (_segments.Count == 0)
            {
                return;
            }

            byte fillValue = value ?? 0xFF;
            ulong? previousMaxAddress = null;
            var fillSegments = new List<Segment>();

            foreach (var segment in _segments)
            {
                if (previousMaxAddress.HasValue)
                {
                    int gapSize = (int)(segment.MinimumAddress - previousMaxAddress.Value);

                    if ((!maxBytes.HasValue) || (gapSize <= maxBytes.Value))
                    {
                        byte[] fillData = new byte[gapSize];
                        fillData.AsSpan().Fill(fillValue);

                        var fillSegment = new Segment(
                            previousMaxAddress.Value,
                            segment.MinimumAddress,
                            fillData);
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

            _segments.Remove(minimumAddress, maximumAddress);
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

            if (_segments.Count == 0)
            {
                return;
            }

            ulong segmentsMaxAddr = _segments[_segments.Count - 1].MaximumAddress;

            if (minimumAddress > 0)
            {
                _segments.Remove(0, minimumAddress);
            }

            if (maximumAddress < segmentsMaxAddr)
            {
                _segments.Remove(maximumAddress, segmentsMaxAddr);
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
            var sb = new StringBuilder(binaryData.Length * (4 + separator.Length));

            for (int i = 0; i < binaryData.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(separator);
                }
                sb.Append($"0x{binaryData[i]:x2}");
            }

            return sb.ToString();
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

            var output = new StringBuilder();
            ulong lineAddress = AlignToLine(MinimumAddress);
            var lineData = new List<byte?>();

            foreach (var chunk in _segments.Chunks(16, 16))
            {
                byte[] chunkData = chunk.Data;
                int chunkOffset = 0;

                while (chunkOffset < chunkData.Length)
                {
                    ulong currentByteAddress = chunk.Address + (ulong)chunkOffset;
                    ulong alignedLineAddress = AlignToLine(currentByteAddress);

                    // If we've moved to a new line, output the previous line
                    if (alignedLineAddress > lineAddress)
                    {
                        if (lineData.Count > 0)
                        {
                            output.Append(FormatHexdumpLine(lineAddress, lineData));
                            output.Append('\n');
                        }

                        // Check if there's a gap
                        if (alignedLineAddress > (lineAddress + 16))
                        {
                            output.Append("...\n");
                        }

                        lineAddress = alignedLineAddress;
                        lineData.Clear();
                    }

                    // Add padding before chunk data if needed
                    while (lineData.Count < (int)(currentByteAddress - lineAddress))
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
                output.Append(FormatHexdumpLine(lineAddress, lineData));
                output.Append('\n');
            }

            return output.ToString();
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

        private bool IsPrintable(char c) => (c >= ' ') && (c <= '~');

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
                string headerContent;
                if (HeaderText != null)
                {
                    headerContent = HeaderText;
                }
                else
                {
                    // No encoding - display binary header with escape sequences
                    var sb = new StringBuilder();
                    foreach (byte b in _headerBytes)
                    {
                        if (IsPrintable((char)b) && (b != '\\'))
                        {
                            sb.Append((char)b);
                        }
                        else
                        {
                            sb.Append($"\\x{b:x2}");
                        }
                    }
                    headerContent = sb.ToString();
                }

                info.Append($"Header:                  \"{headerContent}\"\n");
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
                string sizeStr = FormatSize(segment.Length);
                info.Append($"    0x{segment.MinimumAddress:x8} - 0x{segment.MaximumAddress:x8} ({sizeStr})\n");
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

            ulong rawSize = MaximumAddress - MinimumAddress;
            int size = rawSize > (ulong)int.MaxValue ? int.MaxValue : (int)rawSize;
            int numColumns = Math.Min(80, size);
            ulong columnStartAddress = MinimumAddress;
            int bytesPerColumn = size / numColumns;

            if (bytesPerColumn == 0)
            {
                bytesPerColumn = 1;
            }

            string minAddrStr = $"0x{MinimumAddress:x}";
            string maxAddrStr = $"0x{MaximumAddress:x}";
            int headerSpacing = Math.Max(numColumns - minAddrStr.Length - maxAddrStr.Length, 0);

            var output = new StringBuilder();
            output.Append($"{minAddrStr}{new string(' ', headerSpacing)}{maxAddrStr}\n");

            for (int i = 0; i < numColumns; i++)
            {
                ulong columnEndAddress;
                if (i < (numColumns - 1))
                {
                    columnEndAddress = columnStartAddress + (ulong)bytesPerColumn;
                }
                else
                {
                    columnEndAddress = MaximumAddress;
                }

                ulong coveredBytes = 0;
                ulong columnLength = columnEndAddress - columnStartAddress;

                foreach (var segment in _segments)
                {
                    if (segment.MinimumAddress >= columnEndAddress)
                    {
                        break;
                    }

                    if (segment.MaximumAddress <= columnStartAddress)
                    {
                        continue;
                    }

                    ulong overlapStart = Math.Max(segment.MinimumAddress, columnStartAddress);
                    ulong overlapEnd = Math.Min(segment.MaximumAddress, columnEndAddress);
                    if (overlapEnd > overlapStart)
                    {
                        coveredBytes += overlapEnd - overlapStart;
                    }
                }

                if (coveredBytes == 0)
                {
                    output.Append(' ');
                }
                else if (coveredBytes < columnLength)
                {
                    output.Append('-');
                }
                else
                {
                    output.Append('=');
                }

                columnStartAddress += (ulong)bytesPerColumn;
            }

            output.Append('\n');
            return output.ToString();
        }

        private string FormatSize(int bytes)
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
                   $"addressRange=0x{MinimumAddress:X}-0x{MaximumAddress:X}, " +
                   $"length={Length} bytes)";
        }
    }
}

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

        /// <summary>
        /// Gets or sets the execution start address.
        /// </summary>
        public ulong? ExecutionStartAddress { get; set; }

        /// <summary>
        /// Gets or sets the header.
        /// When headerEncoding is null: returns/accepts byte[] only.
        /// When headerEncoding is set: returns/accepts string only (encodes/decodes using headerEncoding).
        /// Internally always stored as bytes (_headerBytes).
        /// </summary>
        public object? Header
        {
            get
            {
                if (_headerBytes == null)
                {
                    return null;
                }

                // No encoding: return raw bytes
                if (_headerEncoding == null)
                {
                    return _headerBytes;
                }
                else
                {
                    return Encoding.GetEncoding(_headerEncoding).GetString(_headerBytes);
                }
            }
            set
            {
                if (value == null)
                {
                    _headerBytes = null;
                    return;
                }

                // No encoding: expect byte[]
                if (_headerEncoding == null)
                {
                    if (!(value is byte[] bytes))
                    {
                        throw new ArgumentException($"Expected a byte array, but got {value.GetType()}");
                    }
                    _headerBytes = bytes;
                }
                else
                {
                    // Has encoding: expect string, encode to bytes
                    if (!(value is string str))
                    {
                        throw new ArgumentException($"Expected a string, but got {value.GetType()}");
                    }
                    _headerBytes = Encoding.GetEncoding(_headerEncoding).GetBytes(str);
                }
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
                // Convert from word address to byte address
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
                // Convert from word address to byte address
                return max.Value / (ulong)WordSizeBytes;
            }
        }

        /// <summary>
        /// Gets the segments collection.
        /// </summary>
        public Segments Segments => _segments;

        /// <summary>
        /// Gets or sets the word size in bits.
        /// </summary>
        public int WordSizeBits
        {
            get => _segments.WordSizeBits;
            set => _segments.WordSizeBits = value;
        }

        /// <summary>
        /// Gets the word size in bytes (derived from WordSizeBits).
        /// </summary>
        internal int WordSizeBytes => WordSizeBits / 8;


        /// <summary>
        /// Initializes a new instance of the BinFile class.
        /// </summary>
        /// <param name="wordSizeBits">The word size in bits (8, 16, 32, or 64). Default is 8.</param>
        /// <param name="headerEncoding">The encoding used for the header. Use null for binary headers (byte[]). Default is "utf-8".</param>
        /// <exception cref="ArgumentException">Thrown when wordSizeBits is not 8, 16, 32, or 64.</exception>
        public BinFile(int wordSizeBits = 8, string? headerEncoding = "utf-8")
        {
            if (wordSizeBits != 8 && wordSizeBits != 16 && wordSizeBits != 32 && wordSizeBits != 64)
            {
                throw new ArgumentException($"Word size must be 8, 16, 32, or 64 bits, but got {wordSizeBits}", nameof(wordSizeBits));
            }
            
            _segments = new Segments(wordSizeBits);
            _headerEncoding = headerEncoding;
        }

        /// <summary>
        /// Gets or sets a byte at the specified address.
        /// </summary>
        /// <param name="address">The address to access.</param>
        /// <returns>The byte at the specified address.</returns>
        public byte this[ulong address]
        {
            get
            {
                // Throws if address is outside [min, max)
                ulong addrBytes = address * (ulong)WordSizeBytes;
                ulong? minAddr = _segments.MinimumAddress;
                ulong? maxAddr = _segments.MaximumAddress;
                if (minAddr == null || maxAddr == null || addrBytes < minAddr.Value || addrBytes >= maxAddr.Value)
                {
                    throw new IndexOutOfRangeException($"Binary file index {address} out of range");
                }

                // Check each segment for the data
                foreach (var segment in _segments)
                {
                    if (addrBytes >= segment.MinimumAddress && addrBytes < segment.MaximumAddress)
                    {
                        ulong offset = (addrBytes - segment.MinimumAddress);
                        return segment.Data[offset];
                    }
                }

                // Address is in a gap between segments - return padding value (0xFF)
                return 0xFF;
            }
            set
            {
                ulong addrBytes = address * (ulong)WordSizeBytes;

                // Try to find existing segment
                foreach (var segment in _segments)
                {
                    if (addrBytes >= segment.MinimumAddress && addrBytes < segment.MaximumAddress)
                    {
                        ulong offset = (addrBytes - segment.MinimumAddress);
                        segment.Data[offset] = value;
                        return;
                    }
                }

                // Create new segment with single byte
                byte[] data = new byte[WordSizeBytes];
                data[0] = value;
                var newSegment = new Segment(addrBytes, addrBytes + (ulong)WordSizeBytes, data, WordSizeBits);
                _segments.Add(newSegment, overwrite: false);
            }
        }


        /// <summary>
        /// Gets bytes in the specified address range.
        /// </summary>
        /// <param name="startAddress">The starting address (inclusive).</param>
        /// <param name="endAddress">The ending address (exclusive).</param>
        /// <returns>The bytes in the specified range.</returns>
        public byte[] GetRange(ulong startAddress, ulong endAddress)
        {
            if (endAddress <= startAddress)
            {
                return Array.Empty<byte>();
            }

            // Convert word addresses to byte addresses
            ulong startBytes = startAddress * (ulong)WordSizeBytes;
            ulong endBytes = endAddress * (ulong)WordSizeBytes;
            ulong length = endBytes - startBytes;
            byte[] result = new byte[length];
            
            // Initialize with 0xFF (default padding)
            for (ulong i = 0; i < length; i++)
            {
                result[i] = 0xFF;
            }

            foreach (var segment in _segments)
            {
                if (segment.MaximumAddress <= startBytes)
                {
                    continue;
                }
                if (segment.MinimumAddress >= endBytes)
                {
                    break;
                }

                ulong segmentStart = Math.Max(startBytes, segment.MinimumAddress);
                ulong segmentEnd = Math.Min(endBytes, segment.MaximumAddress);
                ulong segmentOffset = segmentStart - segment.MinimumAddress;
                ulong copyLength = segmentEnd - segmentStart;
                
                // Calculate where in the result array this segment data should go
                ulong resultPosition = segmentStart - startBytes;

                Array.Copy(segment.Data, (long)segmentOffset, result, (long)resultPosition, (long)copyLength);
            }

            return result;
        }

        /// <summary>
        /// Sets bytes in the specified address range.
        /// </summary>
        /// <param name="startAddress">The starting address.</param>
        /// <param name="data">The data to set.</param>
        /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
        public void SetRange(ulong startAddress, byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            
            Add(data, startAddress, overwrite: true);
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
            if (data == null || data.Length == 0)
            {
                return;
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
        /// Supports SREC, Intel HEX, TI-TXT, VMEM, and Microchip HEX formats.
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
            var result = detector.DetectAndParse(data);

            // Add header if present
            if (result.Header != null && result.Header.Length > 0)
            {
                if (_headerEncoding == null)
                {
                    Header = result.Header;
                }
                else
                {
                    Header = Encoding.GetEncoding(_headerEncoding).GetString(result.Header);
                }
            }

            // Add execution start address if present
            if (result.ExecutionStartAddress.HasValue)
            {
                ExecutionStartAddress = result.ExecutionStartAddress.Value;
            }

            // Add all segments
            foreach (var segment in result.Segments)
            {
                _segments.Add(segment, overwrite);
            }
        }

        /// <summary>
        /// Adds data from a file by automatically detecting the format.
        /// Supports SREC, Intel HEX, TI-TXT, VMEM, ELF, Binary, and Microchip HEX formats.
        /// </summary>
        /// <param name="filename">The path to the file.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        /// <exception cref="ArgumentNullException">Thrown when filename is null.</exception>
        /// <exception cref="ArgumentException">Thrown when filename is empty or whitespace.</exception>
        /// <exception cref="UnsupportedFileFormatException">Thrown when the format cannot be detected.</exception>
        public void AddFile(string filename, bool overwrite = false)
        {
            if (filename == null)
            {
                throw new ArgumentNullException(nameof(filename));
            }
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentException("Filename cannot be empty or whitespace", nameof(filename));
            }
            
            // Try to read as text first (for SREC, IHEX, TI-TXT, VMEM, Microchip HEX)
            try
            {
                string content = File.ReadAllText(filename);
                Add(content, overwrite);
                return;
            }
            catch (UnsupportedFileFormatException)
            {
                // Not a text format, try binary formats
            }
            catch (DecoderFallbackException)
            {
                // Not a text file, try binary formats
            }

            // Try ELF format
            try
            {
                byte[] data = File.ReadAllBytes(filename);
                AddElf(data, overwrite);
                return;
            }
            catch (BincopyException)
            {
                // Not ELF format
            }

            // If we get here, no format was recognized
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

            var result = new BinFile(a.WordSizeBits);

            // Copy segments from a
            foreach (var segment in a._segments)
            {
                result._segments.Add(segment, overwrite: false);
            }

            // Copy segments from b
            foreach (var segment in b._segments)
            {
                result._segments.Add(segment, overwrite: false);
            }

            // Preserve metadata from first file
            result.ExecutionStartAddress = a.ExecutionStartAddress ?? b.ExecutionStartAddress;
            result.Header = a.Header ?? b.Header;

            return result;
        }

        /// <summary>
        /// Adds Motorola S-Record (SREC) data from a string.
        /// </summary>
        /// <param name="records">The SREC records as a string.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        public void AddSrec(string records, bool overwrite = false)
        {
            var parser = new Formats.SrecParser();
            var result = parser.Parse(records, WordSizeBytes);

            // Add header if present
            if (result.Header != null && result.Header.Length > 0)
            {
                if (_headerEncoding == null)
                {
                    Header = result.Header;
                }
                else
                {
                    Header = Encoding.GetEncoding(_headerEncoding).GetString(result.Header);
                }
            }

            // Add execution start address if present
            if (result.ExecutionStartAddress.HasValue)
            {
                ExecutionStartAddress = result.ExecutionStartAddress.Value;
            }

            // Add all segments
            foreach (var segment in result.Segments)
            {
                _segments.Add(segment, overwrite);
            }
        }

        /// <summary>
        /// Adds Motorola S-Record (SREC) data from a file.
        /// </summary>
        /// <param name="filename">The path to the SREC file.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        public void AddSrecFile(string filename, bool overwrite = false)
        {
            string content = File.ReadAllText(filename);
            AddSrec(content, overwrite);
        }

        /// <summary>
        /// Converts the binary data to Motorola S-Record (SREC) format.
        /// </summary>
        /// <param name="numberOfDataBytes">The number of data bytes per record. Default is 32.</param>
        /// <param name="addressLengthBits">The address length in bits (16, 24, or 32). Default is 32.</param>
        /// <returns>A string containing the SREC records.</returns>
        public string AsSrec(int numberOfDataBytes = 32, int addressLengthBits = 32)
        {
            var serializer = new Formats.SrecSerializer();
            
            byte[]? headerBytes = _headerBytes;
            
            var options = new Formats.SerializerOptions
            {
                NumberOfDataBytes = numberOfDataBytes,
                AddressLengthBits = addressLengthBits,
                HeaderBytes = headerBytes,
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
            var parser = new Formats.IhexParser();
            var result = parser.Parse(records, WordSizeBytes);

            // Add execution start address if present
            if (result.ExecutionStartAddress.HasValue)
            {
                ExecutionStartAddress = result.ExecutionStartAddress.Value;
            }

            // Add all segments
            foreach (var segment in result.Segments)
            {
                _segments.Add(segment, overwrite);
            }
        }

        /// <summary>
        /// Adds Intel HEX data from a file.
        /// </summary>
        /// <param name="filename">The path to the Intel HEX file.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        public void AddIhexFile(string filename, bool overwrite = false)
        {
            string content = File.ReadAllText(filename);
            AddIhex(content, overwrite);
        }

        /// <summary>
        /// Converts the binary data to Intel HEX format.
        /// </summary>
        /// <param name="numberOfDataBytes">The number of data bytes per record. Default is 32.</param>
        /// <param name="addressLengthBits">The address length in bits (16, 24, or 32). Default is 32.</param>
        /// <returns>A string containing the Intel HEX records.</returns>
        public string AsIhex(int numberOfDataBytes = 32, int addressLengthBits = 32)
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
        /// Adds Microchip HEX data from a string.
        /// Microchip's HEX format is identical to Intel's except an address in
        /// the HEX file is twice the actual machine address.
        /// </summary>
        /// <param name="records">The Microchip HEX records as a string.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        public void AddMicrochipHex(string records, bool overwrite = false)
        {
            var parser = new Formats.MicrochipHexParser();
            var result = parser.Parse(records);

            // Set word size to 16 bits (Microchip HEX uses 2-byte words)
            WordSizeBits = 16;

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
        /// Adds Microchip HEX data from a file.
        /// </summary>
        /// <param name="filename">The path to the Microchip HEX file.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        public void AddMicrochipHexFile(string filename, bool overwrite = false)
        {
            string content = File.ReadAllText(filename);
            AddMicrochipHex(content, overwrite);
        }

        /// <summary>
        /// Converts the binary data to Microchip HEX format.
        /// </summary>
        /// <param name="numberOfDataBytes">The number of data bytes per record. Default is 32.</param>
        /// <param name="addressLengthBits">The address length in bits (16, 24, or 32). Default is 32.</param>
        /// <returns>A string containing the Microchip HEX records.</returns>
        public string AsMicrochipHex(int numberOfDataBytes = 32, int addressLengthBits = 32)
        {
            var serializer = new Formats.MicrochipHexSerializer();
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
            var parser = new Formats.TiTxtParser();
            var result = parser.Parse(lines, WordSizeBytes);

            // Add all segments
            foreach (var segment in result.Segments)
            {
                _segments.Add(segment, overwrite);
            }
        }

        /// <summary>
        /// Adds TI-TXT data from a file.
        /// </summary>
        /// <param name="filename">The path to the TI-TXT file.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        public void AddTiTxtFile(string filename, bool overwrite = false)
        {
            string content = File.ReadAllText(filename);
            AddTiTxt(content, overwrite);
        }

        /// <summary>
        /// Converts the binary data to TI-TXT format.
        /// </summary>
        /// <returns>A string containing the TI-TXT data.</returns>
        public string AsTiTxt()
        {
            var serializer = new Formats.TiTxtSerializer();
            var options = new Formats.SerializerOptions();
            return serializer.Serialize(_segments, options);
        }

        /// <summary>
        /// Adds Verilog VMEM data from a string.
        /// </summary>
        /// <param name="data">The VMEM data as a string.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        public void AddVerilogVmem(string data, bool overwrite = false)
        {
            var parser = new Formats.VmemParser();
            var result = parser.Parse(data, WordSizeBytes);

            // Add all segments
            foreach (var segment in result.Segments)
            {
                _segments.Add(segment, overwrite);
            }
        }

        /// <summary>
        /// Adds Verilog VMEM data from a file.
        /// </summary>
        /// <param name="filename">The path to the VMEM file.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        public void AddVerilogVmemFile(string filename, bool overwrite = false)
        {
            string content = File.ReadAllText(filename);
            AddVerilogVmem(content, overwrite);
        }

        /// <summary>
        /// Converts the binary data to Verilog VMEM format.
        /// </summary>
        /// <returns>A string containing the VMEM data.</returns>
        public string AsVerilogVmem()
        {
            var serializer = new Formats.VmemSerializer();
            
            // VMEM only supports string headers
            string? headerStr = null;
            if (_headerEncoding != null && _headerBytes != null)
            {
                headerStr = Encoding.GetEncoding(_headerEncoding).GetString(_headerBytes);
            }
            
            var options = new Formats.SerializerOptions
            {
                Header = headerStr
            };
            return serializer.Serialize(_segments, options);
        }

        /// <summary>
        /// Adds binary data at the specified address.
        /// </summary>
        /// <param name="data">The binary data to add.</param>
        /// <param name="address">The starting address in WORDS. Default is 0.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        public void AddBinary(byte[] data, ulong address = 0, bool overwrite = false)
        {
            var parser = new Formats.BinaryParser();
            var result = parser.ParseBinary(data, address, WordSizeBytes);

            // Add all segments
            foreach (var segment in result.Segments)
            {
                _segments.Add(segment, overwrite);
            }
        }

        /// <summary>
        /// Adds binary data from a file at the specified address.
        /// </summary>
        /// <param name="filename">The path to the binary file.</param>
        /// <param name="address">The starting address. Default is 0.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is false.</param>
        public void AddBinaryFile(string filename, ulong address = 0, bool overwrite = false)
        {
            byte[] data = File.ReadAllBytes(filename);
            AddBinary(data, address, overwrite);
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
        /// Converts the binary file to a raw binary byte array with custom padding.
        /// </summary>
        /// <param name="minimumAddress">The minimum address (inclusive) in words. Null for segment minimum.</param>
        /// <param name="maximumAddress">The maximum address (exclusive) in words. Null for segment maximum.</param>
        /// <param name="padding">The padding byte array to use for gaps. Must be a word value (size = WordSizeBytes).</param>
        /// <returns>A byte array containing the binary data.</returns>
        public byte[] AsBinary(ulong? minimumAddress, ulong? maximumAddress, byte[] padding)
        {
            if (padding.Length != WordSizeBytes)
            {
                throw new ArgumentException($"Padding must be a word value (size {WordSizeBytes}), but got {padding.Length} bytes");
            }

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
            var parser = new Formats.ElfParser();
            var result = parser.ParseElf(data, WordSizeBytes);

            // Set execution start address if present
            if (result.ExecutionStartAddress.HasValue)
            {
                ExecutionStartAddress = result.ExecutionStartAddress.Value;
            }

            // Add all segments
            foreach (var segment in result.Segments)
            {
                _segments.Add(segment, overwrite);
            }
        }

        /// <summary>
        /// Adds ELF (Executable and Linkable Format) data from a file.
        /// </summary>
        /// <param name="filename">The path to the ELF file.</param>
        /// <param name="overwrite">Whether to overwrite existing data. Default is true.</param>
        public void AddElfFile(string filename, bool overwrite = true)
        {
            byte[] data = File.ReadAllBytes(filename);
            AddElf(data, overwrite);
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
                        // Create fill data by repeating the padding word
                        byte[] fillData = new byte[gapSize];
                        for (ulong i = 0; i < gapSizeWords; i++)
                        {
                            Array.Copy(padding, 0, fillData, (long)(i * (ulong)WordSizeBytes), WordSizeBytes);
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
                throw new BincopyException("Bad address range");
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

            ulong minAddr = minimumAddress * (ulong)WordSizeBytes;
            ulong maxAddr = maximumAddress * (ulong)WordSizeBytes;
            
            // Get the maximum address of all segments
            ulong? segmentsMaxAddr = _segments.MaximumAddress;
            
            // Remove everything before minimum address
            if (minAddr > 0)
            {
                _segments.Remove(0, minAddr);
            }
            
            // Remove everything after maximum address
            if (segmentsMaxAddr.HasValue && maxAddr < segmentsMaxAddr.Value)
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

            for (ulong offset = 0; offset < (ulong)binaryData.Length; offset += (ulong)WordSizeBytes)
            {
                ulong word = 0;

                for (ulong i = 0; i < (ulong)WordSizeBytes && (offset + i) < (ulong)binaryData.Length; i++)
                {
                    word <<= 8;
                    word += binaryData[offset + i];
                }

                words.Add($"0x{word:x2}");
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

        private string FormatHexdumpLine(ulong address, List<byte?> data)
        {
            // Pad to 16 bytes
            while (data.Count < 16)
            {
                data.Add(null);
            }

            var hexdata = new List<string>();
            foreach (var b in data)
            {
                if (b.HasValue)
                {
                    hexdata.Add($"{b.Value:x2}");
                }
                else
                {
                    hexdata.Add("  ");
                }
            }

            string firstHalf = string.Join(" ", hexdata.GetRange(0, 8));
            string secondHalf = string.Join(" ", hexdata.GetRange(8, 8));

            var text = new StringBuilder();
            foreach (var b in data)
            {
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

            return $"{address:x8}  {firstHalf,-23}  {secondHalf,-23}  |{text,-16}|";
        }

        private bool IsPrintable(char c)
        {
            // Printable characters excluding whitespace (except space)
            if (c == ' ')
            {
                return true;
            }
            if (char.IsWhiteSpace(c))
            {
                return false;
            }
            return !char.IsControl(c) && c >= 32 && c <= 126;
        }



        /// <summary>
        /// Returns human-readable information about the binary file including header,
        /// execution start address, and data ranges.
        /// </summary>
        /// <returns>A string containing information about the binary file.</returns>
        public string Info()
        {
            var info = new StringBuilder();

            // Add header if present
            if (Header != null)
            {
                string headerDisplay;
                if (Header is string str)
                {
                    headerDisplay = $"\"{str}\"";
                }
                else if (Header is byte[] bytes)
                {
                    // Display binary header with escape sequences
                    var sb = new StringBuilder();
                    foreach (byte b in bytes)
                    {
                        if (b >= 32 && b <= 126 && b != '\\')  // Printable ASCII
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
                else
                {
                    headerDisplay = Header.ToString() ?? "";
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
                ulong size = (ulong)segment.Data.Length;
                ulong maximumAddress = minimumAddress + (size / (ulong)WordSizeBytes);
                
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
            int width = Math.Min(80, (int)size);
            ulong chunkAddress = MinimumAddress;
            ulong chunkSize = size / (ulong)width;
            
            string minAddrStr = $"0x{MinimumAddress:x}";
            string maxAddrStr = $"0x{MaximumAddress:x}";
            int padding = Math.Max(width - minAddrStr.Length - maxAddrStr.Length, 0);
            
            var output = new StringBuilder();
            output.Append($"{minAddrStr}{new string(' ', padding)}{maxAddrStr}\n");

            for (int i = 0; i < width; i++)
            {
                ulong maximumAddress;
                if (i < (width - 1))
                {
                    maximumAddress = chunkAddress + chunkSize;
                }
                else
                {
                    maximumAddress = MaximumAddress;
                }

                // Create a temporary BinFile to check this chunk
                var chunk = new BinFile(WordSizeBits);
                foreach (var segment in _segments)
                {
                    ulong segmentAddress = segment.MinimumAddress / (ulong)WordSizeBytes;
                    ulong segmentLength = (ulong)segment.Data.Length / (ulong)WordSizeBytes;
                    
                    if (segmentAddress < maximumAddress && 
                        (segmentAddress + segmentLength) > chunkAddress)
                    {
                        // This segment overlaps with the chunk
                        ulong segStart = Math.Max(segmentAddress, chunkAddress);
                        ulong segEnd = Math.Min(segmentAddress + segmentLength, maximumAddress);
                        
                        if (segEnd > segStart)
                        {
                            ulong offset = (segStart - segmentAddress) * (ulong)WordSizeBytes;
                            ulong length = (segEnd - segStart) * (ulong)WordSizeBytes;
                            byte[] data = new byte[length];
                            Array.Copy(segment.Data, (long)offset, data, 0, (long)length);
                            chunk.Add(data, segStart, overwrite: true);
                        }
                    }
                }

                if (chunk.Length == 0)
                {
                    output.Append(' ');
                }
                else if (chunk.Length != ((maximumAddress - chunkAddress) * (ulong)WordSizeBytes))
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
            else if (bytes < 1024 * 1024)
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

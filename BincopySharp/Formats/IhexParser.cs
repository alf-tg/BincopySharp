using System;
using System.IO;
using BincopySharp.Utilities;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Parser for Intel HEX format files.
    /// </summary>
    internal class IhexParser : IFormatParser
    {
        // Intel HEX record types
        private const byte IHEX_DATA = 0;
        private const byte IHEX_END_OF_FILE = 1;
        private const byte IHEX_EXTENDED_SEGMENT_ADDRESS = 2;
        private const byte IHEX_START_SEGMENT_ADDRESS = 3;
        private const byte IHEX_EXTENDED_LINEAR_ADDRESS = 4;
        private const byte IHEX_START_LINEAR_ADDRESS = 5;

        public string FormatName => "Intel HEX";

        public bool CanParse(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return false;
            }

            // Get first non-empty line
            using (var reader = new StringReader(data))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (!string.IsNullOrEmpty(line))
                    {
                        // Intel HEX records start with ':'
                        if (line.Length >= 11 && line[0] == ':')
                        {
                            try
                            {
                                UnpackIhex(line);
                                return true;
                            }
                            catch
                            {
                                return false;
                            }
                        }
                        return false;
                    }
                }
            }

            return false;
        }

        public ParseResult Parse(string data)
        {
            return Parse(data, 1); // Default word size
        }

        public ParseResult Parse(string data, int wordSizeBytes)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                throw new BincopyException("Cannot parse empty Intel HEX data");
            }

            var result = new ParseResult();
            ulong extendedSegmentAddress = 0;
            ulong extendedLinearAddress = 0;

            using (var reader = new StringReader(data))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();

                    // Ignore blank lines
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    var (type, address, size, recordData) = UnpackIhex(line);

                    if (type == IHEX_DATA)
                    {
                        // Data record
                        ulong fullAddress = address + extendedSegmentAddress + extendedLinearAddress;
                        ulong segmentAddress = fullAddress * (ulong)wordSizeBytes;
                        ulong segmentMaxAddress = segmentAddress + (ulong)size;
                        var segment = new Segment(segmentAddress, segmentMaxAddress, recordData, wordSizeBytes);
                        result.Segments.Add(segment);
                    }
                    else if (type == IHEX_END_OF_FILE)
                    {
                        // End of file record - ignore and continue
                    }
                    else if (type == IHEX_EXTENDED_SEGMENT_ADDRESS)
                    {
                        // Extended segment address record
                        extendedSegmentAddress = (ulong)((recordData[0] << 8) | recordData[1]);
                        extendedSegmentAddress *= 16;
                    }
                    else if (type == IHEX_EXTENDED_LINEAR_ADDRESS)
                    {
                        // Extended linear address record
                        extendedLinearAddress = (ulong)((recordData[0] << 8) | recordData[1]);
                        extendedLinearAddress <<= 16;
                    }
                    else if (type == IHEX_START_SEGMENT_ADDRESS || type == IHEX_START_LINEAR_ADDRESS)
                    {
                        // Execution start address record
                        ulong startAddress = 0;
                        for (int i = 0; i < recordData.Length; i++)
                        {
                            startAddress = (startAddress << 8) | recordData[i];
                        }
                        result.ExecutionStartAddress = startAddress;
                    }
                    else
                    {
                        throw new InvalidRecordException(line, $"Expected type 0..5 in record {line}, but got {type}");
                    }
                }
            }

            return result;
        }

        private (byte Type, ulong Address, int Size, byte[] Data) UnpackIhex(string record)
        {
            // Minimum :SSAAAATTCC, where SS is size, AAAA is address, TT is type and CC is checksum
            if (record.Length < 11)
            {
                throw new InvalidRecordException(record, $"Record '{record}' too short");
            }

            if (record[0] != ':')
            {
                throw new InvalidRecordException(record, $"Record '{record}' not starting with a ':'");
            }

            // Parse hex data starting from position 1
            byte[] value;
            try
            {
                value = HexConverter.FromHexString(record.Substring(1));
            }
            catch
            {
                throw new InvalidRecordException(record, $"Invalid hex data in record '{record}'", null, null);
            }

            if (value.Length < 5)
            {
                throw new InvalidRecordException(record, $"Record '{record}' too short");
            }

            int size = value[0];

            if (size != value.Length - 5)
            {
                throw new InvalidRecordException(record, $"Record '{record}' has wrong size");
            }

            // Extract address (2 bytes, big-endian)
            ulong address = (ulong)((value[1] << 8) | value[2]);

            // Extract type
            byte type = value[3];

            // Extract data
            byte[] data = new byte[size];
            Array.Copy(value, 4, data, 0, size);

            // Validate checksum
            byte actualChecksum = value[value.Length - 1];
            byte expectedChecksum = ChecksumCalculator.CalculateIhexChecksum(record.Substring(1, record.Length - 3));

            if (actualChecksum != expectedChecksum)
            {
                throw new InvalidRecordException(
                    record,
                    $"Expected checksum '{expectedChecksum:X2}' in record {record}, but got '{actualChecksum:X2}'",
                    expectedChecksum,
                    actualChecksum);
            }

            return (type, address, size, data);
        }
    }
}

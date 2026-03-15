using System;
using System.IO;
using BincopySharp.Utilities;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Parser for Motorola S-Record (SREC) format files.
    /// </summary>
    internal class SrecParser : IFormatParser
    {
        public string FormatName => "SREC";

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
                        // SREC records start with 'S' followed by a digit
                        if (line.Length >= 2 && line[0] == 'S' && char.IsDigit(line[1]))
                        {
                            try
                            {
                                UnpackSrec(line);
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
                throw new BincopyException("Cannot parse empty SREC data");
            }

            var result = new ParseResult();

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

                    var (type, address, size, recordData) = UnpackSrec(line);

                    if (type == '0')
                    {
                        // S0 record contains header
                        result.Header = recordData;
                    }
                    else if (type == '1' || type == '2' || type == '3')
                    {
                        // S1, S2, S3 records contain data
                        ulong segmentAddress = address * (ulong)wordSizeBytes;
                        ulong segmentMaxAddress = segmentAddress + (ulong)size;
                        var segment = new Segment(segmentAddress, segmentMaxAddress, recordData, wordSizeBytes);
                        result.Segments.Add(segment);
                    }
                    else if (type == '7' || type == '8' || type == '9')
                    {
                        // S7, S8, S9 records contain execution start address
                        result.ExecutionStartAddress = address;
                    }
                    // S5 and S6 are record count records, we ignore them
                }
            }

            return result;
        }

        private (char Type, ulong Address, int Size, byte[] Data) UnpackSrec(string record)
        {
            // Minimum STSSCC, where T is type, SS is size and CC is crc
            if (record.Length < 6)
            {
                throw new InvalidRecordException(record, $"Record '{record}' too short");
            }

            if (record[0] != 'S')
            {
                throw new InvalidRecordException(record, $"Record '{record}' not starting with an 'S'");
            }

            char type = record[1];

            // Parse hex data starting from position 2
            byte[] value;
            try
            {
                value = HexConverter.FromHexString(record.Substring(2));
            }
            catch
            {
                throw new InvalidRecordException(record, $"Invalid hex data in record '{record}'", null, null);
            }

            if (value.Length < 2)
            {
                throw new InvalidRecordException(record, $"Record '{record}' too short");
            }

            int size = value[0];

            if (size != value.Length - 1)
            {
                throw new InvalidRecordException(record, $"Record '{record}' has wrong size");
            }

            // Determine address width based on record type (validate type before CRC)
            int width;
            if ("0159".IndexOf(type) >= 0)
            {
                width = 2;
            }
            else if ("268".IndexOf(type) >= 0)
            {
                width = 3;
            }
            else if ("37".IndexOf(type) >= 0)
            {
                width = 4;
            }
            else
            {
                throw new InvalidRecordException(record, $"expected record type 0..3 or 5..9, but got '{type}'");
            }

            int dataOffset = 1 + width;
            
            // Extract address bytes and convert to ulong
            byte[] addressBytes = new byte[width];
            Array.Copy(value, 1, addressBytes, 0, width);
            ulong address = HexConverter.UInt64FromBigEndian(addressBytes);

            // Extract data (everything except size, address, and CRC)
            int dataLength = value.Length - dataOffset - 1;
            if (dataLength < 0) 
            {
                dataLength = 0;
            }
            byte[] data = new byte[dataLength];
            if (dataLength > 0)
            {
                Array.Copy(value, dataOffset, data, 0, dataLength);
            }

            // Validate CRC
            byte actualCrc = value[value.Length - 1];
            byte expectedCrc = CrcCalculator.CalculateSrecCrc(record.Substring(2, record.Length - 4));

            if (actualCrc != expectedCrc)
            {
                throw new InvalidRecordException(
                    record,
                    $"expected crc '{expectedCrc:X2}' in record {record}, but got '{actualCrc:X2}'",
                    expectedCrc,
                    actualCrc);
            }

            return (type, address, dataLength, data);
        }
    }
}

using System;
using System.Collections.Generic;
using BincopySharp.Utilities;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Serializer for Motorola S-Record (SREC) format files.
    /// </summary>
    internal class SrecSerializer : IFormatSerializer
    {
        public string FormatName => "SREC";

        public string Serialize(Segments segments, SerializerOptions options)
        {
            var lines = new List<string>();

            // Add header record (S0) if present
            if (options.HeaderBytes != null)
            {
                string headerRecord = PackSrec('0', 0, options.HeaderBytes.Length, options.HeaderBytes);
                lines.Add(headerRecord);
            }

            // Determine data record type based on address length
            char dataType;
            if (options.AddressLengthBits == 16)
            {
                dataType = '1';
            }
            else if (options.AddressLengthBits == 24)
            {
                dataType = '2';
            }
            else if (options.AddressLengthBits == 32)
            {
                dataType = '3';
            }
            else
            {
                throw new ArgumentException(
                    $"Expected address length 16, 24 or 32, but got {options.AddressLengthBits}",
                    nameof(options.AddressLengthBits));
            }

            // Add data records
            ulong numberOfRecords = 0;
            foreach (var (address, data) in segments.Chunks(options.NumberOfDataBytes / segments.WordSizeBytes))
            {
                string dataRecord = PackSrec(dataType, address, data.Length, data);
                lines.Add(dataRecord);
                numberOfRecords++;
            }

            // Add record count record (S5 or S6)
            if (numberOfRecords <= 0xFFFF)
            {
                lines.Add(PackSrec('5', numberOfRecords, 0, null));
            }
            else if (numberOfRecords <= 0xFFFFFF)
            {
                lines.Add(PackSrec('6', numberOfRecords, 0, null));
            }
            else
            {
                throw new InvalidOperationException($"Too many records: {numberOfRecords}");
            }

            // Add execution start address record (S7, S8, or S9) if present
            if (options.ExecutionStartAddress.HasValue)
            {
                char startType;
                if (dataType == '1')
                {
                    startType = '9';
                }
                else if (dataType == '2')
                {
                    startType = '8';
                }
                else
                {
                    startType = '7';
                }

                lines.Add(PackSrec(startType, options.ExecutionStartAddress.Value, 0, null));
            }

            return string.Join("\n", lines) + "\n";
        }

        private string PackSrec(char type, ulong address, int size, byte[]? data)
        {
            string line;

            // Build the line based on record type
            if (type == '0' || type == '1' || type == '5' || type == '9')
            {
                // 2-byte address (16-bit)
                line = $"{size + 2 + 1:X2}{address:X4}";
            }
            else if (type == '2' || type == '6' || type == '8')
            {
                // 3-byte address (24-bit)
                line = $"{size + 3 + 1:X2}{address:X6}";
            }
            else if (type == '3' || type == '7')
            {
                // 4-byte address (32-bit)
                line = $"{size + 4 + 1:X2}{address:X8}";
            }
            else
            {
                throw new ArgumentException(
                    $"Expected record type 0..3 or 5..9, but got '{type}'",
                    nameof(type));
            }

            // Add data if present
            if (data != null && data.Length > 0)
            {
                line += HexConverter.ToHexString(data);
            }

            // Calculate and append CRC
            byte crc = CrcCalculator.CalculateSrecCrc(line);
            line = $"S{type}{line}{crc:X2}";

            return line;
        }
    }
}

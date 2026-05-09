using System;
using System.Text;
using BincopySharp.Utilities;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Serializer for Motorola S-Record (SREC) format files.
    /// </summary>
    internal static class SrecSerializer
    {
        /// <summary>Serializes segments to Motorola S-Record (SREC) format.</summary>
        /// <param name="segments">The segments to serialize.</param>
        /// <param name="numberOfDataBytes">Number of data bytes per record.</param>
        /// <param name="variant">The SREC variant that determines the address width.</param>
        /// <param name="headerBytes">Optional header bytes written as an S0 record.</param>
        /// <param name="executionStartAddress">Optional execution start address.</param>
        /// <returns>A string containing the SREC records.</returns>
        public static string Serialize(Segments segments, int numberOfDataBytes, SrecVariant variant, byte[]? headerBytes, ulong? executionStartAddress)
        {
            // Pre-estimate capacity: ~80 chars per record (1 'S' + 1 type + 2 size + 8 address + 64 data + 2 crc + 1 newline)
            int estimatedLines = 0;
            foreach (var segment in segments)
            {
                estimatedLines += (segment.Length + numberOfDataBytes - 1) / numberOfDataBytes;
            }
            estimatedLines += 10; // overhead for header, count, start address records
            var sb = new StringBuilder(estimatedLines * 80);

            // Add header record (S0) if present
            if (headerBytes != null)
            {
                AppendSrecRecord(sb, '0', 0, headerBytes);
            }

            // Determine data record type and max address based on variant
            char dataType;
            ulong maxAddress;
            if (variant == SrecVariant.S19)
            {
                dataType = '1';
                maxAddress = 0xFFFF;
            }
            else if (variant == SrecVariant.S28)
            {
                dataType = '2';
                maxAddress = 0xFFFFFF;
            }
            else
            {
                dataType = '3';
                maxAddress = 0xFFFFFFFF;
            }

            // Validate that all segment addresses fit in the address range
            foreach (var segment in segments)
            {
                ulong address = segment.MaximumAddress - 1;
                if (address > maxAddress)
                {
                    throw new BincopyException(
                        $"Cannot address more than 0x{maxAddress:X} in SREC {variant} records");
                }
            }

            // Add data records
            int numberOfRecords = 0;
            foreach (var (address, data) in segments.Chunks(numberOfDataBytes))
            {
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }
                AppendSrecRecord(sb, dataType, address, data);
                numberOfRecords++;
            }

            // Add record count record (S5 or S6)
            if (numberOfRecords <= 0xFFFF)
            {
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }
                AppendSrecRecord(sb, '5', (ulong)numberOfRecords, null);
            }
            else if (numberOfRecords <= 0xFFFFFF)
            {
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }
                AppendSrecRecord(sb, '6', (ulong)numberOfRecords, null);
            }
            else
            {
                throw new InvalidOperationException($"Too many records: {numberOfRecords}");
            }

            // Add execution start address record (S7, S8, or S9) if present
            if (executionStartAddress.HasValue)
            {
                if (executionStartAddress.Value > maxAddress)
                {
                    throw new BincopyException(
                        $"Cannot address more than 0x{maxAddress:X} in SREC {variant} records");
                }

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

                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }
                AppendSrecRecord(sb, startType, executionStartAddress.Value, null);
            }

            sb.Append('\n');
            return sb.ToString();
        }

        private static void AppendSrecRecord(StringBuilder sb, char type, ulong address, byte[]? data)
        {
            int addressBytes;
            if ((type == '0') || (type == '1') || (type == '5') || (type == '9'))
            {
                addressBytes = 2;
            }
            else if ((type == '2') || (type == '6') || (type == '8'))
            {
                addressBytes = 3;
            }
            else if ((type == '3') || (type == '7'))
            {
                addressBytes = 4;
            }
            else
            {
                throw new ArgumentException(
                    $"Expected record type 0..3 or 5..9, but got '{type}'",
                    nameof(type));
            }

            byte byteCount = (byte)((data?.Length ?? 0) + addressBytes + 1);
            byte crc = SrecCrcCalculator.CalculateFromBytes(byteCount, address, addressBytes, data);

            sb.Append('S');
            sb.Append(type);
            HexConverter.AppendHexByte(sb, byteCount);

            // Address bytes, big-endian, exact number of bytes
            for (int i = addressBytes - 1; i >= 0; i--)
            {
                byte byteToAppend = (byte)((address >> (i * 8)) & 0xFF);
                HexConverter.AppendHexByte(sb, byteToAppend);
            }

            // Data bytes
            if (data != null)
            {
                foreach (byte b in data)
                {
                    HexConverter.AppendHexByte(sb, b);
                }
            }

            // CRC
            HexConverter.AppendHexByte(sb, crc);
        }
    }
}

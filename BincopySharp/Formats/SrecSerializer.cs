using System;
using System.Text;
using BincopySharp.Utilities;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Serializer for Motorola S-Record (SREC) format files.
    /// </summary>
    internal class SrecSerializer : IFormatSerializer
    {
        private static readonly char[] _hexUpper = "0123456789ABCDEF".ToCharArray();

        public string FormatName => "SREC";

        public string Serialize(Segments segments, SerializerOptions options)
        {
            // Pre-estimate capacity: ~45 chars per record
            int estimatedLines = 0;
            foreach (var segment in segments)
            {
                int segmentBytes = (int)segment.Length;
                int dataBytes = options.NumberOfDataBytes > 0 ? options.NumberOfDataBytes : 32;
                estimatedLines += (segmentBytes + dataBytes - 1) / dataBytes;
            }
            estimatedLines += 10; // overhead for header, count, start address records
            var sb = new StringBuilder(estimatedLines * 50);

            // Add header record (S0) if present
            if (options.HeaderBytes != null)
            {
                AppendSrecRecord(sb, '0', 0, options.HeaderBytes.Length, options.HeaderBytes);
            }

            // Determine data record type and max address based on address length
            char dataType;
            ulong maxAddress;
            if (options.AddressLengthBits == 16)
            {
                dataType = '1';
                maxAddress = 0xFFFF;
            }
            else if (options.AddressLengthBits == 24)
            {
                dataType = '2';
                maxAddress = 0xFFFFFF;
            }
            else if (options.AddressLengthBits == 32)
            {
                dataType = '3';
                maxAddress = 0xFFFFFFFF;
            }
            else
            {
                throw new ArgumentException(
                    $"Expected address length 16, 24 or 32, but got {options.AddressLengthBits}",
                    nameof(options.AddressLengthBits));
            }

            // Validate that all segment addresses fit in the address range
            // Segment addresses are stored in bytes internally; divide by WordSizeBytes
            // to get word addresses for comparison against the SREC address limit
            int wordSizeBytes = segments.WordSizeBytes;
            foreach (var segment in segments)
            {
                ulong wordAddress = (segment.MaximumAddress - 1) / (ulong)wordSizeBytes;
                if (wordAddress > maxAddress)
                {
                    throw new BincopyException(
                        $"Cannot address more than 0x{maxAddress:X} in SREC S{dataType} records ({options.AddressLengthBits} bits addresses)");
                }
            }

            // Add data records
            ulong numberOfRecords = 0;
            foreach (var (address, data) in segments.Chunks(options.NumberOfDataBytes / segments.WordSizeBytes))
            {
                if (sb.Length > 0) sb.Append('\n');
                AppendSrecRecord(sb, dataType, address, data.Length, data);
                numberOfRecords++;
            }

            // Add record count record (S5 or S6)
            if (numberOfRecords <= 0xFFFF)
            {
                if (sb.Length > 0) sb.Append('\n');
                AppendSrecRecord(sb, '5', numberOfRecords, 0, null);
            }
            else if (numberOfRecords <= 0xFFFFFF)
            {
                if (sb.Length > 0) sb.Append('\n');
                AppendSrecRecord(sb, '6', numberOfRecords, 0, null);
            }
            else
            {
                throw new InvalidOperationException($"Too many records: {numberOfRecords}");
            }

            // Add execution start address record (S7, S8, or S9) if present
            if (options.ExecutionStartAddress.HasValue)
            {
                if (options.ExecutionStartAddress.Value > maxAddress)
                {
                    throw new BincopyException(
                        $"Cannot address more than 0x{maxAddress:X} in SREC S{dataType} records ({options.AddressLengthBits} bits addresses)");
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

                if (sb.Length > 0) sb.Append('\n');
                AppendSrecRecord(sb, startType, options.ExecutionStartAddress.Value, 0, null);
            }

            sb.Append('\n');
            return sb.ToString();
        }

        private void AppendSrecRecord(StringBuilder sb, char type, ulong address, int size, byte[]? data)
        {
            int addressBytes;
            if (type == '0' || type == '1' || type == '5' || type == '9')
                addressBytes = 2;
            else if (type == '2' || type == '6' || type == '8')
                addressBytes = 3;
            else if (type == '3' || type == '7')
                addressBytes = 4;
            else
                throw new ArgumentException(
                    $"Expected record type 0..3 or 5..9, but got '{type}'",
                    nameof(type));

            byte byteCount = (byte)(size + addressBytes + 1);
            byte crc = CrcCalculator.CalculateSrecCrcRaw(byteCount, address, addressBytes, data);

            sb.Append('S');
            sb.Append(type);
            AppendByte(sb, byteCount);

            // Address bytes, big-endian, exact number of bytes
            for (int i = addressBytes - 1; i >= 0; i--)
            {
                AppendByte(sb, (int)((address >> (i * 8)) & 0xFF));
            }

            // Data bytes
            if (data != null)
            {
                foreach (byte b in data)
                {
                    AppendByte(sb, b);
                }
            }

            // CRC
            AppendByte(sb, crc);
        }

        private void AppendByte(StringBuilder sb, int b)
        {
            sb.Append(_hexUpper[b >> 4]);
            sb.Append(_hexUpper[b & 0x0F]);
        }
    }
}

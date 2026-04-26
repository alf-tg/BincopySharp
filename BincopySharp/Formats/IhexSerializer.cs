using System;
using System.Text;
using BincopySharp.Utilities;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Serializer for Intel HEX format files.
    /// </summary>
    internal class IhexSerializer : IFormatSerializer
    {
        // Intel HEX record types
        private const byte IHEX_DATA = 0;
        private const byte IHEX_END_OF_FILE = 1;
        private const byte IHEX_EXTENDED_SEGMENT_ADDRESS = 2;
        private const byte IHEX_START_SEGMENT_ADDRESS = 3;
        private const byte IHEX_EXTENDED_LINEAR_ADDRESS = 4;
        private const byte IHEX_START_LINEAR_ADDRESS = 5;

        public string FormatName => "Intel HEX";

        public string Serialize(Segments segments, SerializerOptions options)
        {
            // Pre-estimate capacity: ~45 chars per record (header + data + checksum + newline)
            // A rough estimate based on typical IHEX line length
            int estimatedLines = 0;
            foreach (var segment in segments)
            {
                int segmentBytes = (int)segment.Length;
                int dataBytes = options.NumberOfDataBytes > 0 ? options.NumberOfDataBytes : 32;
                estimatedLines += (segmentBytes + dataBytes - 1) / dataBytes;
            }
            estimatedLines += 10; // overhead for address records, EOF, etc.
            var sb = new StringBuilder(estimatedLines * 50);

            ulong extendedSegmentAddress = 0;
            ulong extendedLinearAddress = 0;
            int numberOfDataWords = options.NumberOfDataBytes / segments.WordSizeBytes;

            // Generate data records
            foreach (var (chunkAddress, data) in segments.Chunks(numberOfDataWords))
            {
                ulong address = chunkAddress;

                if (options.AddressLengthBits == 32)
                {
                    // 32-bit addressing (I32HEX)
                    address = HandleI32Hex(address, ref extendedLinearAddress, sb);
                }
                else if (options.AddressLengthBits == 24)
                {
                    // 24-bit addressing (I16HEX)
                    address = HandleI16Hex(address, ref extendedSegmentAddress, sb);
                }
                else if (options.AddressLengthBits == 16)
                {
                    // 16-bit addressing (I8HEX)
                    HandleI8Hex(address);
                }
                else
                {
                    throw new ArgumentException(
                        $"Expected address length 16, 24 or 32, but got {options.AddressLengthBits}",
                        nameof(options.AddressLengthBits));
                }

                // Add data record
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(PackIhex(IHEX_DATA, address, data.Length, data));
            }

            // Add execution start address if present
            if (options.ExecutionStartAddress.HasValue)
            {
                if (options.AddressLengthBits == 24)
                {
                    // Start segment address (CS:IP format)
                    byte[] addressBytes = new byte[4];
                    addressBytes[0] = (byte)((options.ExecutionStartAddress.Value >> 24) & 0xFF);
                    addressBytes[1] = (byte)((options.ExecutionStartAddress.Value >> 16) & 0xFF);
                    addressBytes[2] = (byte)((options.ExecutionStartAddress.Value >> 8) & 0xFF);
                    addressBytes[3] = (byte)(options.ExecutionStartAddress.Value & 0xFF);
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(PackIhex(IHEX_START_SEGMENT_ADDRESS, 0, 4, addressBytes));
                }
                else if (options.AddressLengthBits == 32)
                {
                    // Start linear address (EIP format)
                    byte[] addressBytes = new byte[4];
                    addressBytes[0] = (byte)((options.ExecutionStartAddress.Value >> 24) & 0xFF);
                    addressBytes[1] = (byte)((options.ExecutionStartAddress.Value >> 16) & 0xFF);
                    addressBytes[2] = (byte)((options.ExecutionStartAddress.Value >> 8) & 0xFF);
                    addressBytes[3] = (byte)(options.ExecutionStartAddress.Value & 0xFF);
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(PackIhex(IHEX_START_LINEAR_ADDRESS, 0, 4, addressBytes));
                }
            }

            // Add end of file record
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(PackIhex(IHEX_END_OF_FILE, 0, 0, null));
            sb.Append('\n');

            return sb.ToString();
        }

        private ulong HandleI32Hex(ulong address, ref ulong extendedLinearAddress, StringBuilder sb)
        {
            if (address > 0xFFFFFFFFUL)
            {
                throw new BincopyException(
                    "Cannot address more than 4 GB in I32HEX files (32 bits addresses)");
            }

            ulong addressUpper16Bits = (address >> 16);
            ulong addressLower16Bits = address & 0xFFFF;

            // Update extended linear address when required
            if (addressUpper16Bits > extendedLinearAddress)
            {
                extendedLinearAddress = addressUpper16Bits;
                byte[] extAddressBytes = new byte[2];
                extAddressBytes[0] = (byte)((extendedLinearAddress >> 8) & 0xFF);
                extAddressBytes[1] = (byte)(extendedLinearAddress & 0xFF);
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(PackIhex(IHEX_EXTENDED_LINEAR_ADDRESS, 0, 2, extAddressBytes));
            }

            return addressLower16Bits;
        }

        private ulong HandleI16Hex(ulong address, ref ulong extendedSegmentAddress, StringBuilder sb)
        {
            if (address > 16 * 0xFFFF + 0xFFFF)
            {
                throw new BincopyException(
                    "Cannot address more than 1 MB in I16HEX files (20 bits addresses)");
            }

            ulong addressLower = address - 16 * extendedSegmentAddress;

            // Update extended segment address when required
            if (addressLower > 0xFFFF)
            {
                extendedSegmentAddress = 4096 * (address >> 16);

                if (extendedSegmentAddress > 0xFFFF)
                {
                    extendedSegmentAddress = 0xFFFF;
                }

                addressLower = address - 16 * extendedSegmentAddress;
                byte[] extAddressBytes = new byte[2];
                extAddressBytes[0] = (byte)((extendedSegmentAddress >> 8) & 0xFF);
                extAddressBytes[1] = (byte)(extendedSegmentAddress & 0xFF);
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(PackIhex(IHEX_EXTENDED_SEGMENT_ADDRESS, 0, 2, extAddressBytes));
            }

            return addressLower;
        }

        private void HandleI8Hex(ulong address)
        {
            if (address > 0xFFFF)
            {
                throw new BincopyException(
                    "Cannot address more than 64 kB in I8HEX files (16 bits addresses)");
            }
        }

        private string PackIhex(byte type, ulong address, int size, byte[]? data)
        {
            // Build the line: size (1 byte) + address (2 bytes) + type (1 byte) + data
            // Note: address is truncated to 16 bits for Intel HEX format
            string line = $"{size:X2}{(address & 0xFFFF):X4}{type:X2}";

            // Add data if present
            if (data != null && data.Length > 0)
            {
                line += HexConverter.ToHexString(data);
            }

            // Calculate and append checksum
            byte checksum = ChecksumCalculator.CalculateIhexChecksum(line);
            line = $":{line}{checksum:X2}";

            return line;
        }
    }
}

using System.Text;
using BincopySharp.Utilities;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Serializer for Intel HEX format files.
    /// </summary>
    internal static class IhexSerializer
    {
        // Intel HEX record types
        private const byte IHEX_DATA = 0;
        private const byte IHEX_END_OF_FILE = 1;
        private const byte IHEX_EXTENDED_SEGMENT_ADDRESS = 2;
        private const byte IHEX_START_SEGMENT_ADDRESS = 3;
        private const byte IHEX_EXTENDED_LINEAR_ADDRESS = 4;
        private const byte IHEX_START_LINEAR_ADDRESS = 5;

        /// <summary>Serializes segments to Intel HEX format.</summary>
        /// <param name="segments">The segments to serialize.</param>
        /// <param name="numberOfDataBytes">Number of data bytes per record.</param>
        /// <param name="variant">The Intel HEX variant that determines the addressing scheme.</param>
        /// <param name="executionStartAddress">Optional execution start address.</param>
        /// <returns>A string containing the Intel HEX records.</returns>
        public static string Serialize(Segments segments, int numberOfDataBytes, IhexVariant variant, ulong? executionStartAddress)
        {
            // Pre-estimate capacity: ~76 chars per record (1 ':' + 2 size + 4 address + 2 type + 64 data + 2 checksum + 1 newline)
            int estimatedLines = 0;
            foreach (var segment in segments)
            {
                // Round up to ensure we count partial records at the end of segments
                estimatedLines += (segment.Length + numberOfDataBytes - 1) / numberOfDataBytes;
            }
            estimatedLines += 10; // overhead for address records, EOF, etc.
            var sb = new StringBuilder(estimatedLines * 76);

            ulong currentExtendedAddress = 0;

            // Generate data records
            foreach (var (chunkAddress, data) in segments.Chunks(numberOfDataBytes))
            {
                ulong address = chunkAddress;

                if (variant == IhexVariant.I8Hex)
                {
                    ValidateI8Address(address);
                }
                else if (variant == IhexVariant.I16Hex)
                {
                    (address, currentExtendedAddress) = ResolveI16Address(address, currentExtendedAddress, sb);
                }
                else if (variant == IhexVariant.I32Hex)
                {
                    (address, currentExtendedAddress) = ResolveI32Address(address, currentExtendedAddress, sb);
                }

                // Add data record
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }
                sb.Append(PackIhex(IHEX_DATA, address, data.Length, data));
            }

            // Add execution start address if present
            if ((executionStartAddress.HasValue) && (variant != IhexVariant.I8Hex))
            {
                byte[] addressBytes = new byte[4];
                addressBytes[0] = (byte)((executionStartAddress.Value >> 24) & 0xFF);
                addressBytes[1] = (byte)((executionStartAddress.Value >> 16) & 0xFF);
                addressBytes[2] = (byte)((executionStartAddress.Value >> 8) & 0xFF);
                addressBytes[3] = (byte)(executionStartAddress.Value & 0xFF);

                if (variant == IhexVariant.I16Hex)
                {
                    if (executionStartAddress.Value > 0xFFFFF)
                    {
                        throw new BincopyException(
                            "Cannot set execution start address above 1 MB in I16HEX files (20 bits addresses)");
                    }
                    // Start segment address (CS:IP format)
                    if (sb.Length > 0)
                    {
                        sb.Append('\n');
                    }
                    sb.Append(PackIhex(IHEX_START_SEGMENT_ADDRESS, 0, 4, addressBytes));
                }
                else if (variant == IhexVariant.I32Hex)
                {
                    if (executionStartAddress.Value > 0xFFFFFFFF)
                    {
                        throw new BincopyException(
                            "Cannot set execution start address above 4 GB in I32HEX files (32 bits addresses)");
                    }
                    // Start linear address (EIP format)
                    if (sb.Length > 0)
                    {
                        sb.Append('\n');
                    }
                    sb.Append(PackIhex(IHEX_START_LINEAR_ADDRESS, 0, 4, addressBytes));
                }
            }

            // Add end of file record
            if (sb.Length > 0)
            {
                sb.Append('\n');
            }
            sb.Append(PackIhex(IHEX_END_OF_FILE, 0, 0, null));
            sb.Append('\n');

            return sb.ToString();
        }

        private static (ulong recordAddress, ulong updatedExtendedLinearAddress) ResolveI32Address(ulong address, ulong extendedLinearAddress, StringBuilder sb)
        {
            if (address > 0xFFFFFFFFUL)
            {
                throw new BincopyException(
                    "Cannot address more than 4 GB in I32HEX files (32 bits addresses)");
            }

            ulong upper16 = (address >> 16) & 0xFFFF;
            ulong recordAddress = address & 0xFFFF;

            // Update extended linear address when required
            if (upper16 > extendedLinearAddress)
            {
                extendedLinearAddress = upper16;
                byte[] extAddressBytes = new byte[2];
                extAddressBytes[0] = (byte)((extendedLinearAddress >> 8) & 0xFF);
                extAddressBytes[1] = (byte)(extendedLinearAddress & 0xFF);
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }
                sb.Append(PackIhex(IHEX_EXTENDED_LINEAR_ADDRESS, 0, 2, extAddressBytes));
            }

            return (recordAddress, extendedLinearAddress);
        }

        private static (ulong recordAddress, ulong updatedExtendedSegmentAddress) ResolveI16Address(ulong address, ulong extendedSegmentAddress, StringBuilder sb)
        {
            if (address > (16 * 0xFFFF + 0xFFFF))
            {
                throw new BincopyException(
                    "Cannot address more than 1 MB in I16HEX files (20 bits addresses)");
            }

            ulong recordAddress = address - 16 * extendedSegmentAddress;

            // Update extended segment address when required
            if (recordAddress > 0xFFFF)
            {
                extendedSegmentAddress = 4096 * (address >> 16);

                if (extendedSegmentAddress > 0xFFFF)
                {
                    extendedSegmentAddress = 0xFFFF;
                }

                recordAddress = address - 16 * extendedSegmentAddress;
                byte[] extAddressBytes = new byte[2];
                extAddressBytes[0] = (byte)((extendedSegmentAddress >> 8) & 0xFF);
                extAddressBytes[1] = (byte)(extendedSegmentAddress & 0xFF);
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }
                sb.Append(PackIhex(IHEX_EXTENDED_SEGMENT_ADDRESS, 0, 2, extAddressBytes));
            }

            return (recordAddress, extendedSegmentAddress);
        }

        private static void ValidateI8Address(ulong address)
        {
            if (address > 0xFFFF)
            {
                throw new BincopyException(
                    "Cannot address more than 64 kB in I8HEX files (16 bits addresses)");
            }
        }

        private static string PackIhex(byte type, ulong address, int size, byte[]? data)
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
            byte checksum = IhexChecksumCalculator.Calculate(line);
            line = $":{line}{checksum:X2}";

            return line;
        }
    }
}

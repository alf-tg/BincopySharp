using System;
using System.Text;

namespace BincopySharp.Utilities
{
    /// <summary>
    /// Provides utility methods for hexadecimal conversions.
    /// </summary>
    internal static class HexConverter
    {
        /// <summary>
        /// Converts a byte array to a hexadecimal string.
        /// </summary>
        /// <param name="bytes">The byte array to convert.</param>
        /// <param name="uppercase">Whether to use uppercase letters. Default is true.</param>
        /// <returns>The hexadecimal string representation.</returns>
        public static string ToHexString(byte[] bytes, bool uppercase = true)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder(bytes.Length * 2);
            string format = uppercase ? "X2" : "x2";

            foreach (byte b in bytes)
            {
                sb.Append(b.ToString(format));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Converts a hexadecimal string to a byte array.
        /// </summary>
        /// <param name="hexString">The hexadecimal string to convert.</param>
        /// <returns>The byte array.</returns>
        public static byte[] FromHexString(string hexString)
        {
            if (string.IsNullOrEmpty(hexString))
            {
                return Array.Empty<byte>();
            }

            if (hexString.Length % 2 != 0)
            {
                throw new ArgumentException("Hex string must have even length", nameof(hexString));
            }

            byte[] bytes = new byte[hexString.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                string byteStr = hexString.Substring(i * 2, 2);
                bytes[i] = Convert.ToByte(byteStr, 16);
            }

            return bytes;
        }

        /// <summary>
        /// Parses a hexadecimal address string to a ulong.
        /// </summary>
        /// <param name="hexAddress">The hexadecimal address string (without 0x prefix).</param>
        /// <returns>The parsed address as a ulong.</returns>
        public static ulong ParseHexAddress(string hexAddress)
        {
            if (string.IsNullOrEmpty(hexAddress))
            {
                throw new ArgumentException("Hex address cannot be null or empty", nameof(hexAddress));
            }

            // Remove 0x prefix if present
            if (hexAddress.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                hexAddress.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
            {
                hexAddress = hexAddress.Substring(2);
            }

            // Remove @ prefix if present (TI-TXT format)
            if (hexAddress.StartsWith("@"))
            {
                hexAddress = hexAddress.Substring(1);
            }

            return Convert.ToUInt64(hexAddress, 16);
        }

        /// <summary>
        /// Formats an address as a hexadecimal string with specified width.
        /// </summary>
        /// <param name="address">The address to format.</param>
        /// <param name="width">The width in characters (e.g., 4 for 16-bit, 8 for 32-bit).</param>
        /// <param name="uppercase">Whether to use uppercase letters. Default is true.</param>
        /// <returns>The formatted hexadecimal address string.</returns>
        public static string FormatAddress(ulong address, int width, bool uppercase = true)
        {
            string format = uppercase ? "X" : "x";
            return address.ToString(format + width);
        }

        /// <summary>
        /// Converts a byte array in big-endian format to a 64-bit unsigned integer.
        /// Equivalent to Python's int.from_bytes(bytes, 'big').
        /// </summary>
        /// <param name="bytes">The byte array in big-endian format.</param>
        /// <returns>The 64-bit unsigned integer value in native format.</returns>
        public static ulong UInt64FromBigEndian(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return 0;
            }

            ulong result = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                result = (result << 8) | bytes[i];
            }

            return result;
        }
    }
}

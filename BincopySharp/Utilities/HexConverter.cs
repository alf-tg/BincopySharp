using System;
using System.Text;

namespace BincopySharp.Utilities
{
    /// <summary>
    /// Provides utility methods for hexadecimal conversions.
    /// </summary>
    internal static class HexConverter
    {
        private static readonly char[] _hexUpper = "0123456789ABCDEF".ToCharArray();
        private static readonly char[] _hexLower = "0123456789abcdef".ToCharArray();

        /// <summary>
        /// Converts a byte array to a hexadecimal string.
        /// Uses a nibble lookup table to avoid per-byte string allocations.
        /// </summary>
        /// <param name="bytes">The byte array to convert.</param>
        /// <param name="uppercase">Whether to use uppercase letters. Default is true.</param>
        /// <returns>The hexadecimal string representation.</returns>
        public static string ToHexString(byte[] bytes, bool uppercase = true)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length == 0)
            {
                return string.Empty;
            }

            var hex = uppercase ? _hexUpper : _hexLower;
            var sb = new StringBuilder(bytes.Length * 2);

            foreach (byte b in bytes)
            {
                sb.Append(hex[b >> 4]);
                sb.Append(hex[b & 0x0F]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Converts a hexadecimal string to a byte array.
        /// Uses direct nibble arithmetic to avoid per-byte string allocations.
        /// </summary>
        /// <param name="hexString">The hexadecimal string to convert.</param>
        /// <returns>The byte array.</returns>
        public static byte[] FromHexString(string hexString)
        {
            if (string.IsNullOrEmpty(hexString))
            {
                return Array.Empty<byte>();
            }

            if ((hexString.Length % 2) != 0)
            {
                throw new ArgumentException("Hex string must have even length", nameof(hexString));
            }

            byte[] bytes = new byte[hexString.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                int hi = HexCharToNibble(hexString[i * 2]);
                int lo = HexCharToNibble(hexString[i * 2 + 1]);
                bytes[i] = (byte)((hi << 4) | lo);
            }

            return bytes;
        }

        /// <summary>
        /// Converts a single hex character to its 4-bit nibble value.
        /// </summary>
        private static int HexCharToNibble(char c)
        {
            if ((c >= '0') && (c <= '9'))
            {
                return c - '0';
            }
            if ((c >= 'A') && (c <= 'F'))
            {
                return c - 'A' + 10;
            }
            if ((c >= 'a') && (c <= 'f'))
            {
                return c - 'a' + 10;
            }
            throw new ArgumentException($"Invalid hex character: '{c}'");
        }

        /// <summary>
        /// Converts a byte array in big-endian format to a 64-bit unsigned integer.
        /// </summary>
        /// <param name="bytes">The byte array in big-endian format.</param>
        /// <returns>The 64-bit unsigned integer value in native format.</returns>
        public static ulong UInt64FromBigEndian(byte[] bytes)
        {
            if ((bytes == null) || (bytes.Length == 0))
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

        /// <summary>
        /// Appends a single byte as two uppercase hex characters to the StringBuilder.
        /// Avoids per-byte string allocations.
        /// </summary>
        public static void AppendHexByte(StringBuilder sb, byte b)
        {
            sb.Append(_hexUpper[b >> 4]);
            sb.Append(_hexUpper[b & 0x0F]);
        }

        /// <summary>
        /// Sums the byte values of a hex string (without allocating an intermediate byte array).
        /// </summary>
        /// <param name="hexString">The hex string with even length.</param>
        /// <returns>The sum of all byte values.</returns>
        public static int SumHexBytes(string hexString)
        {
            if (string.IsNullOrEmpty(hexString))
            {
                throw new ArgumentException("Hex string cannot be null or empty", nameof(hexString));
            }

            if ((hexString.Length % 2) != 0)
            {
                throw new ArgumentException("Hex string must have even length", nameof(hexString));
            }

            int sum = 0;
            for (int i = 0; i < hexString.Length; i += 2)
            {
                int hi = HexCharToNibble(hexString[i]);
                int lo = HexCharToNibble(hexString[i + 1]);
                sum += (hi << 4) | lo;
            }

            return sum;
        }
    }
}

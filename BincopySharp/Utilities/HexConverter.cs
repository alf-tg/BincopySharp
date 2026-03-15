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
        /// Converts a byte array in big-endian format to a 64-bit unsigned integer.
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

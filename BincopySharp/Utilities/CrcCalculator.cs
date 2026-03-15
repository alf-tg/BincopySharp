using System;

namespace BincopySharp.Utilities
{
    /// <summary>
    /// Provides CRC calculation methods for Motorola S-Record format.
    /// </summary>
    internal static class CrcCalculator
    {
        /// <summary>
        /// Calculates the CRC for a Motorola S-Record hex string.
        /// </summary>
        /// <param name="hexString">The hex string (without S prefix and CRC).</param>
        /// <returns>The calculated CRC byte.</returns>
        public static byte CalculateSrecCrc(string hexString)
        {
            if (string.IsNullOrEmpty(hexString))
            {
                throw new ArgumentException("Hex string cannot be null or empty", nameof(hexString));
            }

            if (hexString.Length % 2 != 0)
            {
                throw new ArgumentException("Hex string must have even length", nameof(hexString));
            }

            // Convert hex string to bytes and sum them
            int sum = 0;
            for (int i = 0; i < hexString.Length; i += 2)
            {
                string byteStr = hexString.Substring(i, 2);
                byte b = Convert.ToByte(byteStr, 16);
                sum += b;
            }

            // Apply CRC formula: (sum & 0xFF) ^ 0xFF
            int crc = (sum & 0xFF) ^ 0xFF;
            return (byte)crc;
        }

    }
}

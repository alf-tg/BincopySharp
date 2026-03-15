using System;

namespace BincopySharp.Utilities
{
    /// <summary>
    /// Provides checksum calculation methods for Intel HEX format.
    /// </summary>
    public static class ChecksumCalculator
    {
        /// <summary>
        /// Calculates the checksum for an Intel HEX record.
        /// </summary>
        /// <param name="hexString">The hex string (without : prefix and checksum).</param>
        /// <returns>The calculated checksum byte.</returns>
        public static byte CalculateIhexChecksum(string hexString)
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

            // Apply checksum formula: ((~sum + 1) & 0xFF)
            int checksum = ((~sum + 1) & 0xFF);
            return (byte)checksum;
        }

    }
}

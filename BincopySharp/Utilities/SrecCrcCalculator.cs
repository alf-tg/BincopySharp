namespace BincopySharp.Utilities
{
    /// <summary>
    /// Provides CRC calculation methods for Motorola S-Record format.
    /// </summary>
    internal static class SrecCrcCalculator
    {
        /// <summary>
        /// Calculates the CRC from raw values, avoiding hex string parsing.
        /// </summary>
        /// <param name="byteCount">The byte count field (addressBytes + dataLength + 1).</param>
        /// <param name="address">The record address.</param>
        /// <param name="addressBytes">Number of address bytes (2, 3, or 4).</param>
        /// <param name="data">The data bytes (may be null).</param>
        /// <returns>The calculated CRC byte.</returns>
        public static byte CalculateFromBytes(byte byteCount, ulong address, int addressBytes, byte[]? data)
        {
            int sum = byteCount;
            for (int i = addressBytes - 1; i >= 0; i--)
            {
                sum += (int)((address >> (i * 8)) & 0xFF);
            }
            if (data != null)
            {
                foreach (byte b in data)
                {
                    sum += b;
                }
            }
            return (byte)((sum & 0xFF) ^ 0xFF);
        }

        /// <summary>
        /// Calculates the CRC from a hex string.
        /// </summary>
        /// <param name="hexString">The hex string (without S prefix and CRC).</param>
        /// <returns>The calculated CRC byte.</returns>
        public static byte CalculateFromHexString(string hexString)
        {
            int sum = HexConverter.SumHexBytes(hexString);
            return (byte)((sum & 0xFF) ^ 0xFF);
        }
    }
}

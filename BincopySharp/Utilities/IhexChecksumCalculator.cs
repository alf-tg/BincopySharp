namespace BincopySharp.Utilities
{
    /// <summary>
    /// Provides checksum calculation methods for Intel HEX format.
    /// </summary>
    internal static class IhexChecksumCalculator
    {
        /// <summary>
        /// Calculates the checksum for an Intel HEX record.
        /// </summary>
        /// <param name="hexString">The hex string (without : prefix and checksum).</param>
        /// <returns>The calculated checksum byte.</returns>
        public static byte Calculate(string hexString)
        {
            int sum = HexConverter.SumHexBytes(hexString);
            return (byte)((~sum + 1) & 0xFF);
        }

    }
}
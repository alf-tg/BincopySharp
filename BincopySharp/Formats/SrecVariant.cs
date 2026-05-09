namespace BincopySharp.Formats
{
    /// <summary>
    /// Motorola S-Record variant that determines the address width used in data records.
    /// </summary>
    public enum SrecVariant
    {
        /// <summary>S1/S9 records — 16-bit addresses (up to 64 KB).</summary>
        S19,
        /// <summary>S2/S8 records — 24-bit addresses (up to 16 MB).</summary>
        S28,
        /// <summary>S3/S7 records — 32-bit addresses (up to 4 GB).</summary>
        S37
    }
}
namespace BincopySharp.Formats
{
    /// <summary>
    /// Intel HEX format variant, which determines the addressing scheme used when serializing.
    /// </summary>
    public enum IhexVariant
    {
        /// <summary>I8HEX — 16-bit addresses, up to 64 KB. No extended address records.</summary>
        I8Hex,

        /// <summary>I16HEX — 20-bit addresses via extended segment address records, up to 1 MB.</summary>
        I16Hex,

        /// <summary>I32HEX — 32-bit addresses via extended linear address records, up to 4 GB.</summary>
        I32Hex
    }
}
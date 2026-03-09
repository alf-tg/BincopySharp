namespace BincopySharp.Formats
{
    /// <summary>
    /// Options for serializing binary data to various formats.
    /// </summary>
    internal class SerializerOptions
    {
        /// <summary>
        /// Gets or sets the number of data bytes per line/record.
        /// </summary>
        public int NumberOfDataBytes { get; set; } = 32;

        /// <summary>
        /// Gets or sets the address length in bits (16 or 32).
        /// </summary>
        public int AddressLengthBits { get; set; } = 32;

        /// <summary>
        /// Gets or sets the minimum address to export (null for all data).
        /// </summary>
        public ulong? MinimumAddress { get; set; }

        /// <summary>
        /// Gets or sets the maximum address to export (null for all data).
        /// </summary>
        public ulong? MaximumAddress { get; set; }

        /// <summary>
        /// Gets or sets the padding byte to use for gaps (default 0xFF).
        /// </summary>
        public byte Padding { get; set; } = 0xFF;

        /// <summary>
        /// Gets or sets the header string (used in SREC S0 records).
        /// DEPRECATED: Use HeaderBytes instead.
        /// </summary>
        public string? Header { get; set; }

        /// <summary>
        /// Gets or sets the header bytes (used in SREC S0 records).
        /// EXACTLY like Python: headers are always bytes internally.
        /// </summary>
        public byte[]? HeaderBytes { get; set; }

        /// <summary>
        /// Gets or sets the execution start address.
        /// </summary>
        public ulong? ExecutionStartAddress { get; set; }
    }
}
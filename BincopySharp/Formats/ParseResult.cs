using System.Collections.Generic;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Represents the result of parsing a binary file format.
    /// </summary>
    internal class ParseResult
    {
        /// <summary>
        /// Gets or sets the list of segments parsed from the file.
        /// </summary>
        public List<Segment> Segments { get; set; }

        /// <summary>
        /// Gets or sets the execution start address, if present in the file.
        /// </summary>
        public ulong? ExecutionStartAddress { get; set; }

        /// <summary>
        /// Gets or sets the header bytes, if present in the file.
        /// </summary>
        public byte[]? Header { get; set; }

        /// <summary>
        /// Initializes a new instance of the ParseResult class.
        /// </summary>
        public ParseResult()
        {
            Segments = new List<Segment>();
        }
    }
}
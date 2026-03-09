using System;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Interface for format parsers that can detect and parse binary file formats.
    /// </summary>
    internal interface IFormatParser
    {
        /// <summary>
        /// Gets the name of the format this parser handles.
        /// </summary>
        string FormatName { get; }

        /// <summary>
        /// Determines whether this parser can parse the given data.
        /// </summary>
        /// <param name="data">The data to check.</param>
        /// <returns>True if this parser can handle the data, false otherwise.</returns>
        bool CanParse(string data);

        /// <summary>
        /// Parses the given data into a ParseResult.
        /// </summary>
        /// <param name="data">The data to parse.</param>
        /// <returns>A ParseResult containing the parsed segments and metadata.</returns>
        ParseResult Parse(string data);
    }
}

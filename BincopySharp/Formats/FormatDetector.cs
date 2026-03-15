using System.Collections.Generic;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Detects the format of binary file data by trying different parsers.
    /// </summary>
    internal class FormatDetector
    {
        private readonly List<IFormatParser> _parsers;

        /// <summary>
        /// Initializes a new instance of the FormatDetector class.
        /// </summary>
        public FormatDetector()
        {
            // Order matters: try parsers in order of likelihood/specificity
            _parsers = new List<IFormatParser>
            {
                new SrecParser(),
                new IhexParser(),
                new TiTxtParser(),
                new VmemParser(),
                new MicrochipHexParser()
                // Note: ELF and Binary are not included as they require byte[] input
            };
        }

        /// <summary>
        /// Detects the format of the given data and returns the appropriate parser.
        /// </summary>
        /// <param name="data">The data to analyze.</param>
        /// <returns>The parser that can handle the data.</returns>
        /// <exception cref="UnsupportedFileFormatException">Thrown when no parser can handle the data.</exception>
        public IFormatParser DetectFormat(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                throw new UnsupportedFileFormatException("", "Data is empty or whitespace");
            }

            foreach (var parser in _parsers)
            {
                if (parser.CanParse(data))
                {
                    return parser;
                }
            }

            throw new UnsupportedFileFormatException("", "Unable to detect file format");
        }

        /// <summary>
        /// Tries to detect the format and parse the data.
        /// </summary>
        /// <param name="data">The data to parse.</param>
        /// <returns>The parse result.</returns>
        /// <exception cref="UnsupportedFileFormatException">Thrown when no parser can handle the data.</exception>
        public ParseResult DetectAndParse(string data)
        {
            var parser = DetectFormat(data);
            return parser.Parse(data);
        }
    }
}

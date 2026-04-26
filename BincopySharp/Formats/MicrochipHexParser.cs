using System.Collections.Generic;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Parser for Microchip HEX format.
    /// Microchip HEX is identical to Intel HEX except addresses are doubled.
    /// This format cannot be reliably auto-detected because it is syntactically
    /// identical to standard Intel HEX. Use AddMicrochipHex() explicitly.
    /// </summary>
    internal class MicrochipHexParser : IFormatParser
    {
        private readonly IhexParser _ihexParser;

        /// <summary>
        /// Gets the name of the format this parser handles.
        /// </summary>
        public string FormatName => "Microchip HEX";

        public MicrochipHexParser()
        {
            _ihexParser = new IhexParser();
        }

        /// <summary>
        /// Always returns false for auto-detection purposes.
        /// Microchip HEX is syntactically identical to Intel HEX and cannot be
        /// reliably distinguished by content alone. Users must explicitly call
        /// AddMicrochipHex() to parse Microchip HEX data.
        /// </summary>
        public bool CanParse(string data)
        {
            // Microchip HEX cannot be distinguished from standard Intel HEX
            // by content alone — both use identical record formats.
            // Return false so FormatDetector never picks this parser.
            return false;
        }

        /// <summary>
        /// Parses Microchip HEX data.
        /// Addresses in the file are twice the actual machine address.
        /// </summary>
        public ParseResult Parse(string data)
        {
            // Parse as Intel HEX with word size 1
            var result = _ihexParser.Parse(data);

            // Convert all segments to word size 16 bits (2 bytes)
            var convertedSegments = new List<Segment>();
            foreach (var segment in result.Segments)
            {
                // Create new segment with word size 16 bits
                var newSegment = new Segment(
                    segment.MinimumAddress,
                    segment.MaximumAddress,
                    segment.DataSpan.ToArray(),
                    16
                );
                convertedSegments.Add(newSegment);
            }

            return new ParseResult
            {
                Segments = convertedSegments,
                ExecutionStartAddress = result.ExecutionStartAddress,
                Header = result.Header
            };
        }

        public ParseResult Parse(string data, int wordSizeBytes)
        {
            return Parse(data);
        }
    }
}

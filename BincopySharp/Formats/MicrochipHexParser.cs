using System.Collections.Generic;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Parser for Microchip HEX format.
    /// Microchip HEX is identical to Intel HEX except addresses are doubled.
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
        /// Checks if the data can be parsed as Microchip HEX format.
        /// </summary>
        public bool CanParse(string data)
        {
            // Microchip HEX uses the same format as Intel HEX
            return _ihexParser.CanParse(data);
        }

        /// <summary>
        /// Parses Microchip HEX data.
        /// Addresses in the file are twice the actual machine address.
        /// </summary>
        public ParseResult Parse(string data)
        {
            // Parse as Intel HEX with word size 1
            var result = _ihexParser.Parse(data);

            // Convert all segments to word size 2
            var convertedSegments = new List<Segment>();
            foreach (var segment in result.Segments)
            {
                // Create new segment with word size 2
                var newSegment = new Segment(
                    segment.MinimumAddress,
                    segment.MaximumAddress,
                    segment.Data,
                    2
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
    }
}

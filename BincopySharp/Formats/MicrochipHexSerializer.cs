namespace BincopySharp.Formats
{
    /// <summary>
    /// Serializer for Microchip HEX format.
    /// Microchip HEX is identical to Intel HEX except addresses are doubled.
    /// </summary>
    internal class MicrochipHexSerializer : IFormatSerializer
    {
        private readonly IhexSerializer _ihexSerializer;

        /// <summary>
        /// Gets the name of the format this serializer handles.
        /// </summary>
        public string FormatName => "Microchip HEX";

        public MicrochipHexSerializer()
        {
            _ihexSerializer = new IhexSerializer();
        }

        /// <summary>
        /// Serializes segments to Microchip HEX format.
        /// Addresses in the output are twice the actual machine address.
        /// </summary>
        public string Serialize(Segments segments, SerializerOptions options)
        {
            // Convert all segments to word size 8 bits (1 byte)
            var convertedSegments = new Segments(8);
            foreach (var segment in segments)
            {
                // Create new segment with word size 8 bits
                var newSegment = new Segment(
                    segment.MinimumAddress,
                    segment.MaximumAddress,
                    segment.Data,
                    8
                );
                convertedSegments.Add(newSegment, overwrite: false);
            }

            // Serialize as Intel HEX
            return _ihexSerializer.Serialize(convertedSegments, options);
        }
    }
}

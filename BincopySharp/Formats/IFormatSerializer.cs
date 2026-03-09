using System;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Interface for format serializers that can convert segments to specific binary file formats.
    /// </summary>
    internal interface IFormatSerializer
    {
        /// <summary>
        /// Gets the name of the format this serializer handles.
        /// </summary>
        string FormatName { get; }

        /// <summary>
        /// Serializes the given segments to the format's string representation.
        /// </summary>
        /// <param name="segments">The segments to serialize.</param>
        /// <param name="options">Serialization options.</param>
        /// <returns>A string representation of the data in the format.</returns>
        string Serialize(Segments segments, SerializerOptions options);
    }
}

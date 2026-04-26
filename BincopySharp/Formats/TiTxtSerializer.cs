using System.Collections.Generic;
using System.Text;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Serializer for TI-TXT format files.
    /// </summary>
    internal class TiTxtSerializer : IFormatSerializer
    {
        private const int TI_TXT_BYTES_PER_LINE = 16;

        public string FormatName => "TI-TXT";

        public string Serialize(Segments segments, SerializerOptions options)
        {
            var lines = new List<string>();
            int numberOfDataWords = TI_TXT_BYTES_PER_LINE / segments.WordSizeBytes;

            // Validate address range (TI-TXT uses variable-width hex addresses, but 32-bit max is practical)
            foreach (var segment in segments)
            {
                if (segment.MaximumAddress - 1 > 0xFFFFFFFF)
                {
                    throw new BincopyException(
                        "Cannot address more than 0xFFFFFFFF in TI-TXT files (32 bits addresses)");
                }
            }

            foreach (var segment in segments)
            {
                // Add address directive
                lines.Add($"@{segment.MinimumAddress / (ulong)segments.WordSizeBytes:X4}");

                // Add data lines
                foreach (var (_, data) in segment.Chunks(numberOfDataWords))
                {
                    var sb = new StringBuilder();
                    for (int i = 0; i < data.Length; i++)
                    {
                        if (i > 0)
                        {
                            sb.Append(' ');
                        }
                        sb.Append($"{data[i]:X2}");
                    }
                    lines.Add(sb.ToString());
                }
            }

            // Add end of file marker
            lines.Add("q");

            return string.Join("\n", lines) + "\n";
        }
    }
}

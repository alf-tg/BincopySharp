using System;
using System.Collections.Generic;
using System.Text;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Serializer for Verilog VMEM format files.
    /// </summary>
    internal class VmemSerializer : IFormatSerializer
    {
        public string FormatName => "Verilog VMEM";

        public string Serialize(Segments segments, SerializerOptions options)
        {
            var lines = new List<string>();

            // Validate address range (VMEM uses @XXXXXXXX format, 32-bit max)
            foreach (var segment in segments)
            {
                if (segment.MaximumAddress - 1 > 0xFFFFFFFF)
                {
                    throw new BincopyException(
                        "Cannot address more than 0xFFFFFFFF in Verilog VMEM files (32 bits addresses)");
                }
            }

            // Add header comment if present
            if (options.Header != null)
            {
                lines.Add($"/* {options.Header} */");
            }

            // Generate data lines
            int numberOfDataWords = 32 / segments.WordSizeBytes;

            foreach (var segment in segments)
            {
                foreach (var (address, data) in segment.Chunks(numberOfDataWords))
                {
                    var words = new List<string>();

                    // Convert data to words
                    for (int i = 0; i < data.Length; i += segments.WordSizeBytes)
                    {
                        var sb = new StringBuilder();
                        for (int j = 0; j < segments.WordSizeBytes && (i + j) < data.Length; j++)
                        {
                            sb.Append($"{data[i + j]:X2}");
                        }
                        words.Add(sb.ToString());
                    }

                    string dataHex = string.Join(" ", words);
                    lines.Add($"@{address:X8} {dataHex}");
                }
            }

            return string.Join("\n", lines) + "\n";
        }
    }
}

using System.Text;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Serializer for TI-TXT format files.
    /// </summary>
    internal static class TiTxtSerializer
    {
        private const int TI_TXT_BYTES_PER_LINE = 16;

        public static string Serialize(Segments segments)
        {
            // Pre-estimate capacity: ~50 chars per line (16 bytes * 3 chars + newline) + address directives
            int estimatedLines = 0;
            foreach (var segment in segments)
            {
                estimatedLines += 1 + (segment.Length + TI_TXT_BYTES_PER_LINE - 1) / TI_TXT_BYTES_PER_LINE;
            }
            estimatedLines += 1; // 'q' terminator
            var sb = new StringBuilder(estimatedLines * 50);

            foreach (var segment in segments)
            {
                if ((segment.MaximumAddress - 1) > 0xFFFFFFFF)
                {
                    throw new BincopyException(
                        "Cannot address more than 0xFFFFFFFF in TI-TXT files (32 bits addresses)");
                }

                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }
                sb.Append($"@{segment.MinimumAddress:X4}");

                foreach (var (_, data) in segment.Chunks(TI_TXT_BYTES_PER_LINE))
                {
                    sb.Append('\n');
                    for (int i = 0; i < data.Length; i++)
                    {
                        if (i > 0)
                        {
                            sb.Append(' ');
                        }
                        sb.Append($"{data[i]:X2}");
                    }
                }
            }

            sb.Append('\n');
            sb.Append('q');
            sb.Append('\n');

            return sb.ToString();
        }
    }
}

using System;
using System.IO;
using BincopySharp.Utilities;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Parser for TI-TXT format files.
    /// </summary>
    internal class TiTxtParser : IFormatParser
    {
        private const int TI_TXT_BYTES_PER_LINE = 16;

        public string FormatName => "TI-TXT";

        public bool CanParse(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return false;
            }

            try
            {
                Parse(data);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public ParseResult Parse(string data)
        {
            var result = new ParseResult();
            int wordSizeBytes = 1; // Default word size
            ulong? address = null;
            bool eofFound = false;

            using (var reader = new StringReader(data))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    // Abort if data is found after end of file
                    if (eofFound)
                    {
                        throw new BincopyException("bad file terminator");
                    }

                    line = line.Trim();

                    if (line.Length < 1)
                    {
                        throw new BincopyException("bad line length");
                    }

                    if (line[0] == 'q' || line[0] == 'Q')
                    {
                        eofFound = true;
                    }
                    else if (line[0] == '@')
                    {
                        // Address directive
                        try
                        {
                            address = Convert.ToUInt64(line.Substring(1), 16);
                        }
                        catch
                        {
                            throw new BincopyException("bad section address");
                        }
                    }
                    else
                    {
                        // Data line
                        byte[] lineData;
                        try
                        {
                            lineData = HexConverter.FromHexString(line.Replace(" ", ""));
                        }
                        catch
                        {
                            throw new BincopyException("bad data");
                        }

                        int size = lineData.Length;

                        // Check that there are correct number of bytes per line
                        // There should be TI_TXT_BYTES_PER_LINE. Only exception is
                        // last line of section which may be shorter.
                        if (size > TI_TXT_BYTES_PER_LINE)
                        {
                            throw new BincopyException("bad line length");
                        }

                        if (!address.HasValue)
                        {
                            throw new BincopyException("missing section address");
                        }

                        ulong segmentAddress = address.Value;
                        ulong segmentMaxAddress = segmentAddress + (ulong)size;
                        var segment = new Segment(segmentAddress, segmentMaxAddress, lineData, wordSizeBytes);
                        result.Segments.Add(segment);

                        if (size == TI_TXT_BYTES_PER_LINE)
                        {
                            address = address.Value + (ulong)size;
                        }
                        else
                        {
                            address = null;
                        }
                    }
                }
            }

            if (!eofFound)
            {
                throw new BincopyException("missing file terminator");
            }

            return result;
        }
    }
}

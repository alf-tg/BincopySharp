using System;
using System.Collections.Generic;
using System.IO;
using BincopySharp.Utilities;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Parser for TI-TXT format files.
    /// </summary>
    internal static class TiTxtParser
    {
        private const int TI_TXT_BYTES_PER_LINE = 16;

        internal static bool CanParse(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return false;
            }

            // Check first non-empty line only - O(1) like SrecParser and IhexParser.
            // A valid TI-TXT file starts with an address directive: '@' followed by hex digits.
            using var reader = new StringReader(data);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    if ((line[0] != '@') || (line.Length < 2))
                    {
                        return false;
                    }

                    try
                    {
                        Convert.ToUInt64(line.Substring(1), 16);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }

            return false;
        }

        public static ParseResult Parse(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                throw new BincopyException("Cannot parse empty TI-TXT data");
            }

            var result = new ParseResult();
            ulong? address = null;
            bool eofFound = false;

            // Accumulator for merging consecutive data lines into a single segment
            ulong accumMinAddr = 0;
            ulong accumMaxAddr = 0;
            List<byte>? accumData = null;

            using (var reader = new StringReader(data))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    // Abort if data is found after end of file
                    if (eofFound)
                    {
                        throw new BincopyException("Bad file terminator");
                    }

                    line = line.Trim();

                    if (line.Length < 1)
                    {
                        throw new BincopyException("Bad line length");
                    }

                    if (line[0] == 'q')
                    {
                        eofFound = true;
                    }
                    else if (line[0] == '@')
                    {
                        // Flush accumulator before new address directive
                        if (accumData != null)
                        {
                            result.Segments.Add(new Segment(accumMinAddr, accumMaxAddr, accumData.ToArray()));
                            accumData = null;
                        }

                        // Address directive
                        try
                        {
                            address = Convert.ToUInt64(line.Substring(1), 16);
                        }
                        catch
                        {
                            throw new BincopyException("Bad section address");
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
                            throw new BincopyException("Bad data");
                        }

                        if (lineData.Length > TI_TXT_BYTES_PER_LINE)
                        {
                            throw new BincopyException("Bad line length");
                        }

                        if (!address.HasValue)
                        {
                            throw new BincopyException("Missing section address");
                        }

                        ulong segmentAddress = address.Value;
                        ulong segmentMaxAddress = segmentAddress + (ulong)lineData.Length;

                        // Accumulate consecutive lines into one segment
                        if ((accumData != null) && (segmentAddress == accumMaxAddr))
                        {
                            accumData.AddRange(lineData);
                            accumMaxAddr = segmentMaxAddress;
                        }
                        else
                        {
                            if (accumData != null)
                            {
                                result.Segments.Add(new Segment(accumMinAddr, accumMaxAddr, accumData.ToArray()));
                            }
                            accumData = new List<byte>(lineData);
                            accumMinAddr = segmentAddress;
                            accumMaxAddr = segmentMaxAddress;
                        }

                        if (lineData.Length == TI_TXT_BYTES_PER_LINE)
                        {
                            address = address.Value + (ulong)lineData.Length;
                        }
                        else
                        {
                            address = null;
                        }
                    }
                }
            }

            // Flush remaining accumulator
            if (accumData != null)
            {
                result.Segments.Add(new Segment(accumMinAddr, accumMaxAddr, accumData.ToArray()));
            }

            if (!eofFound)
            {
                throw new BincopyException("Missing file terminator");
            }

            return result;
        }
    }
}

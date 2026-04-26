using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BincopySharp.Utilities;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Parser for Verilog VMEM format files.
    /// </summary>
    internal class VmemParser : IFormatParser
    {
        public string FormatName => "Verilog VMEM";

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
            return Parse(data, 1);
        }

        /// <summary>
        /// Parses VMEM data. The word size for address calculations is deduced from the
        /// file content (length of hex words). The wordSizeBytes parameter is used only
        /// to set the WordSizeBits on the resulting segments, matching the Python bincopy
        /// behavior where VMEM word size is always inferred from the data.
        /// </summary>
        public ParseResult Parse(string data, int wordSizeBytes)
        {
            var result = new ParseResult();
            int wordSizeBits = wordSizeBytes * 8;

            // Remove comments (// style)
            data = RemoveComments(data);

            // Split by whitespace and filter empty strings
            string[] words = Regex.Split(data.Trim(), @"\s+")
                .Where(w => !string.IsNullOrEmpty(w))
                .ToArray();

            // First pass: determine word size from file
            int? determinedWordSize = null;
            foreach (var word in words)
            {
                if (!word.StartsWith("@"))
                {
                    int length = word.Length;

                    if (length % 2 != 0)
                    {
                        throw new BincopyException("Invalid word length.");
                    }

                    length /= 2;

                    if (!determinedWordSize.HasValue)
                    {
                        determinedWordSize = length;
                    }
                    else if (length != determinedWordSize.Value)
                    {
                        throw new BincopyException(
                            $"Mixed word lengths {length} and {determinedWordSize.Value}.");
                    }
                }
            }

            if (!determinedWordSize.HasValue)
            {
                determinedWordSize = 1;
            }

            // Use the word size from file for address calculation
            int fileWordSize = determinedWordSize.Value;

            // Second pass: parse data
            ulong? address = null;
            var chunk = new List<byte>();

            foreach (var word in words)
            {
                if (word.StartsWith("@"))
                {
                    // Save previous chunk if any
                    if (address.HasValue && chunk.Count > 0)
                    {
                        var segment = new Segment(
                            address.Value,
                            address.Value + (ulong)chunk.Count,
                            chunk.ToArray(),
                            wordSizeBits);
                        result.Segments.Add(segment);
                    }

                    // Parse new address (address in file is in words of fileWordSize)
                    try
                    {
                        address = Convert.ToUInt64(word.Substring(1), 16) * (ulong)fileWordSize;
                    }
                    catch
                    {
                        throw new BincopyException($"Invalid address: {word}");
                    }

                    chunk.Clear();
                }
                else
                {
                    // Parse data word
                    try
                    {
                        byte[] wordBytes = HexConverter.FromHexString(word);
                        chunk.AddRange(wordBytes);
                    }
                    catch
                    {
                        throw new BincopyException($"Invalid data word: {word}");
                    }
                }
            }

            // Save final chunk if any
            if (address.HasValue && chunk.Count > 0)
            {
                var segment = new Segment(
                    address.Value,
                    address.Value + (ulong)chunk.Count,
                    chunk.ToArray(),
                    wordSizeBits);
                result.Segments.Add(segment);
            }

            return result;
        }

        private string RemoveComments(string text)
        {
            // Remove /* */ style comments. Replace with space
            text = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            
            // Remove // style comments
            var lines = text.Split('\n');
            var result = new List<string>();

            foreach (var line in lines)
            {
                int commentIndex = line.IndexOf("//");
                if (commentIndex >= 0)
                {
                    result.Add(line.Substring(0, commentIndex));
                }
                else
                {
                    result.Add(line);
                }
            }

            return string.Join("\n", result);
        }
    }
}

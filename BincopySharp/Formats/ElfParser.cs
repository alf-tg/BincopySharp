using System;
using System.IO;
using ELFSharp.ELF;
using ELFSharp.ELF.Segments;
using ELFSharp.ELF.Sections;

namespace BincopySharp.Formats
{
    /// <summary>
    /// Parser for ELF (Executable and Linkable Format) files.
    /// </summary>
    internal class ElfParser : IFormatParser
    {
        public string FormatName => "ELF";

        public bool CanParse(string data)
        {
            // ELF format can't be reliably detected from string data
            return false;
        }

        public ParseResult Parse(string data)
        {
            throw new NotSupportedException("ELF format requires byte array input, not string");
        }

        public ParseResult Parse(string data, int wordSizeBytes)
        {
            throw new NotSupportedException("ELF format requires byte array input, not string");
        }

        /// <summary>
        /// Parses ELF data from a byte array.
        /// </summary>
        /// <param name="data">The ELF file data.</param>
        /// <param name="wordSizeBytes">The word size in bytes.</param>
        /// <returns>A ParseResult containing the ELF segments and execution start address.</returns>
        public ParseResult ParseElf(byte[] data, int wordSizeBytes)
        {
            var result = new ParseResult();
            int wordSizeBits = wordSizeBytes * 8;

            using (var stream = new MemoryStream(data))
            {
                IELF elfFile;
                try
                {
                    elfFile = ELFReader.Load(stream, true);
                }
                catch (ArgumentException ex) when (ex.Message.Contains("not a proper ELF"))
                {
                    // File is not ELF format at all — signal format detection to try next format
                    throw new UnsupportedFileFormatException("ELF", $"Not a valid ELF file: {ex.Message}");
                }
                catch (Exception ex)
                {
                    throw new BincopyException($"Failed to parse ELF file: {ex.Message}", ex);
                }

                // Get execution start address - handle both 32-bit and 64-bit ELF
                if (elfFile is ELF<uint> elf32)
                {
                    result.ExecutionStartAddress = (ulong)elf32.EntryPoint;
                }
                else if (elfFile is ELF<ulong> elf64)
                {
                    result.ExecutionStartAddress = elf64.EntryPoint;
                }

                // Iterate through PT_LOAD segments and extract ALLOC sections within each.
                // For each PT_LOAD segment, read section data using the raw byte array
                // at the section's file offset, which preserves the exact file content.
                foreach (var segment in elfFile.Segments)
                {
                    // Only process PT_LOAD segments
                    if (segment.Type != SegmentType.Load)
                    {
                        continue;
                    }

                    // Get segment properties - handle both 32-bit and 64-bit
                    ulong segmentAddress = 0;
                    ulong segmentOffset = 0;
                    ulong segmentSize = 0;

                    if (segment is Segment<uint> seg32)
                    {
                        segmentAddress = seg32.PhysicalAddress;
                        segmentOffset = (ulong)seg32.Offset;
                        segmentSize = (ulong)seg32.FileSize;
                    }
                    else if (segment is Segment<ulong> seg64)
                    {
                        segmentAddress = seg64.PhysicalAddress;
                        segmentOffset = (ulong)seg64.Offset;
                        segmentSize = (ulong)seg64.FileSize;
                    }

                    // Skip BSS segments (FileSize == 0)
                    if (segmentSize == 0)
                    {
                        continue;
                    }

                    // Iterate through sections within this segment
                    foreach (var section in elfFile.Sections)
                    {
                        ulong sectionOffset = 0;
                        ulong sectionSize = 0;

                        if (section is Section<uint> sec32)
                        {
                            sectionOffset = (ulong)sec32.Offset;
                            sectionSize = (ulong)sec32.Size;
                        }
                        else if (section is Section<ulong> sec64)
                        {
                            sectionOffset = (ulong)sec64.Offset;
                            sectionSize = (ulong)sec64.Size;
                        }

                        if (sectionSize == 0)
                        {
                            continue;
                        }

                        // Check if section is within this segment
                        if (sectionOffset >= segmentOffset && sectionOffset < segmentOffset + segmentSize)
                        {
                            // Skip SHT_NOBITS sections (BSS - uninitialized data)
                            if (section.Type == SectionType.NoBits)
                            {
                                continue;
                            }

                            // Skip sections without SHF_ALLOC flag
                            if ((section.Flags & SectionFlags.Allocatable) == 0)
                            {
                                continue;
                            }

                            // Calculate section address
                            ulong sectionAddress = segmentAddress + sectionOffset - segmentOffset;

                            // Read section data directly from the raw byte array
                            int offset = (int)sectionOffset;
                            int size = (int)sectionSize;

                            if (offset + size <= data.Length && size > 0)
                            {
                                byte[] sectionData = new byte[size];
                                Array.Copy(data, offset, sectionData, 0, size);

                                ulong maxAddress = sectionAddress + (ulong)size;
                                var seg = new Segment(sectionAddress, maxAddress, sectionData, wordSizeBits);
                                result.Segments.Add(seg);
                            }
                        }
                    }
                }
            }

            return result;
        }
    }
}

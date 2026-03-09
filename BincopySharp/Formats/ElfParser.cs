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

        /// <summary>
        /// Parses ELF data from a byte array.
        /// </summary>
        /// <param name="data">The ELF file data.</param>
        /// <param name="wordSizeBytes">The word size in bytes.</param>
        /// <returns>A ParseResult containing the ELF segments and execution start address.</returns>
        public ParseResult ParseElf(byte[] data, int wordSizeBytes)
        {
            var result = new ParseResult();

            using (var stream = new MemoryStream(data))
            {
                IELF elfFile;
                try
                {
                    elfFile = ELFReader.Load(stream, true);
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

                // Iterate through segments
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

                        ulong sectionAddress = segmentAddress + sectionOffset - segmentOffset;

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

                            // Get section data
                            byte[] sectionData = section.GetContents();

                            if (sectionData != null && sectionData.Length > 0)
                            {
                                ulong address = sectionAddress;
                                ulong maxAddress = address + (ulong)sectionData.Length;
                                var seg = new Segment(address, maxAddress, sectionData, wordSizeBytes);
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

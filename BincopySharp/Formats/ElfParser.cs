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
    internal static class ElfParser
    {
        public static ParseResult ParseElf(byte[] data)
        {
            var result = new ParseResult();

            using (var stream = new MemoryStream(data))
            {
                IELF elfFile;
                try
                {
                    elfFile = ELFReader.Load(stream, true);
                }
                catch (ArgumentException ex) when (ex.Message.Contains("not a proper ELF"))
                {
                    // File is not ELF format at all - signal format detection to try next format
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
                    int segmentOffset = 0;
                    int segmentSize = 0;

                    if (segment is Segment<uint> seg32)
                    {
                        segmentAddress = seg32.PhysicalAddress;
                        segmentOffset = (int)seg32.Offset;
                        segmentSize = (int)seg32.FileSize;
                    }
                    else if (segment is Segment<ulong> seg64)
                    {
                        segmentAddress = seg64.PhysicalAddress;
                        segmentOffset = (int)seg64.Offset;
                        segmentSize = (int)seg64.FileSize;
                    }

                    // Skip BSS segments (FileSize == 0)
                    if (segmentSize == 0)
                    {
                        continue;
                    }

                    // Iterate through sections within this segment
                    foreach (var section in elfFile.Sections)
                    {
                        int sectionOffset = 0;
                        int sectionSize = 0;

                        if (section is Section<uint> sec32)
                        {
                            sectionOffset = (int)sec32.Offset;
                            sectionSize = (int)sec32.Size;
                        }
                        else if (section is Section<ulong> sec64)
                        {
                            sectionOffset = (int)sec64.Offset;
                            sectionSize = (int)sec64.Size;
                        }

                        if (sectionSize == 0)
                        {
                            continue;
                        }

                        // Check if section is within this segment
                        if ((sectionOffset >= segmentOffset) && (sectionOffset < segmentOffset + segmentSize))
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
                            ulong sectionAddress = segmentAddress + (ulong)sectionOffset - (ulong)segmentOffset;

                            // Read section data directly from the raw byte array
                            int offset = sectionOffset;
                            int size = sectionSize;

                            if (((offset + size) <= data.Length) && (size > 0))
                            {
                                byte[] sectionData = new byte[size];
                                Array.Copy(data, offset, sectionData, 0, size);

                                ulong maxAddress = sectionAddress + (ulong)size;
                                var seg = new Segment(sectionAddress, maxAddress, sectionData);
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

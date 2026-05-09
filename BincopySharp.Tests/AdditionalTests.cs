using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using BincopySharp;
using BincopySharp.Formats;

namespace BincopySharp.Tests
{
    /// <summary>
    /// Tests adicionales que no son port de Python: edge cases, cobertura de ramas,
    /// validaciÃ³n de serializers, bug fixes y cross-format.
    /// </summary>
    public class AdditionalTests
    {
        // ===== 33.1: Overflow at large addresses (>2GB address space) =====

        [Fact]
        public void TestAddressAbove2GB()
        {
            var bin = new BinFile();
            ulong address = 0x80000000; // 2GB boundary
            bin.Add(new byte[] { 0xAA, 0xBB }, address);

            Assert.Equal(address, bin.MinimumAddress);
            Assert.Equal(address + 2, bin.MaximumAddress);
            Assert.Equal(2, bin.Length);
            Assert.Equal(0xAAUL, bin[address]);
            Assert.Equal(0xBBUL, bin[address + 1]);
        }

        [Fact]
        public void TestAddressAt4GBBoundary()
        {
            var bin = new BinFile();
            ulong address = 0xFFFFFFFF; // 4GB - 1
            bin.Add(new byte[] { 0xDE, 0xAD }, address);

            Assert.Equal(address, bin.MinimumAddress);
            Assert.Equal(address + 2, bin.MaximumAddress);
            byte[] data = bin.AsBinary(address, address + 2);
            Assert.Equal(new byte[] { 0xDE, 0xAD }, data);
        }

        // ===== 33.2: Underflow in address operations =====

        [Fact]
        public void TestExcludeAtAddressZero()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02, 0x03, 0x04 }, 0);

            bin.Exclude(0, 2);
            Assert.Equal(2UL, bin.MinimumAddress);
            Assert.Equal(4UL, bin.MaximumAddress);
            Assert.Equal(new byte[] { 0x03, 0x04 }, bin.AsBinary(2, 4));
        }

        [Fact]
        public void TestCropAtAddressZero()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02, 0x03, 0x04 }, 0);

            bin.Crop(0, 2);
            Assert.Equal(0UL, bin.MinimumAddress);
            Assert.Equal(2UL, bin.MaximumAddress);
            Assert.Equal(new byte[] { 0x01, 0x02 }, bin.AsBinary(0, 2));
        }

        // ===== 33.3: Reference vs copy semantics in Add =====

        [Fact]
        public void TestAddDataIsCopied()
        {
            var bin = new BinFile();
            byte[] original = new byte[] { 0x01, 0x02, 0x03 };
            bin.Add(original, 0);

            // Modify the original array after Add
            original[0] = 0xFF;
            original[1] = 0xFF;
            original[2] = 0xFF;

            // BinFile should still have the original values
            Assert.Equal(0x01UL, bin[0]);
            Assert.Equal(0x02UL, bin[1]);
            Assert.Equal(0x03UL, bin[2]);
        }

        // ===== 33.4: Reference vs copy semantics in Exclude =====

        [Fact]
        public void TestExcludeDoesNotCorruptAdjacentData()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 }, 0);

            bin.Exclude(2, 4);

            Assert.Equal(2, bin.Segments.Count);
            Assert.Equal(4, bin.Length);
            Assert.Equal(0UL, bin.MinimumAddress);
            Assert.Equal(6UL, bin.MaximumAddress);
            // Left segment: [0x01, 0x02]
            Assert.Equal(new byte[] { 0x01, 0x02 }, bin.AsBinary(0, 2));
            // Right segment: [0x05, 0x06]
            Assert.Equal(new byte[] { 0x05, 0x06 }, bin.AsBinary(4, 6));
        }

        // ===== 33.5: Array mutation after Add =====

        [Fact]
        public void TestAsBinaryRangeReturnsCopy()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02, 0x03 }, 0);

            byte[] range = bin.AsBinary(0, 3);
            range[0] = 0xFF;

            // BinFile should be unaffected
            Assert.Equal(0x01UL, bin[0]);
        }

        [Fact]
        public void TestAsBinaryReturnsCopy()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02, 0x03 }, 0);

            byte[] binary = bin.AsBinary();
            binary[0] = 0xFF;

            // BinFile should be unaffected
            Assert.Equal(0x01UL, bin[0]);
        }

        // ===== 33.6: 64-bit addresses (0xFFFFFFFFFFFFFFFF) =====

        [Fact]
        public void TestMaxUlongAddressOverflows()
        {
            var bin = new BinFile();
            ulong maxAddr = 0xFFFFFFFFFFFFFFFF;

            // Adding at the absolute max address causes maximumAddress overflow (wraps to 0)
            // This is expected to throw because maximumAddress <= minimumAddress
            Assert.Throws<ArgumentException>(() => bin.Add(new byte[] { 0x42 }, maxAddr));
        }

        [Fact]
        public void TestNearMaxUlongAddress()
        {
            var bin = new BinFile();
            ulong addr = 0xFFFFFFFFFFFFFFFE; // Max - 1
            bin.Add(new byte[] { 0x42 }, addr);

            Assert.Equal(addr, bin.MinimumAddress);
            Assert.Equal(0xFFFFFFFFFFFFFFFFUL, bin.MaximumAddress); // addr + 1 = max ulong
            Assert.Equal(1, bin.Length);
            Assert.Single(bin.Segments);
            Assert.Equal(0x42UL, bin[addr]);
            Assert.Equal(new byte[] { 0x42 }, bin.AsBinary(addr, addr + 1));
        }

        [Fact]
        public void TestHighAddressAsBinary()
        {
            var bin = new BinFile();
            ulong addr = 0xFFFFFFFFFFFFFFF0;
            byte[] data = { 0x01, 0x02, 0x03, 0x04 };
            bin.Add(data, addr);

            Assert.Single(bin.Segments);
            Assert.Equal(addr, bin.MinimumAddress);
            Assert.Equal(addr + 4, bin.MaximumAddress);
            Assert.Equal(4, bin.Length);
            byte[] result = bin.AsBinary(addr, addr + 4);
            Assert.Equal(data, result);
        }

        [Fact]
        public void TestHighAddressInfo()
        {
            var bin = new BinFile();
            ulong addr = 0xFFFFFFFFFFFFFFF0;
            bin.Add(new byte[] { 0xAA }, addr);

            Assert.Single(bin.Segments);
            Assert.Equal(1, bin.Length);
            string info = bin.Info();
            Assert.Contains("fffffffffffffff0", info);
            Assert.Contains("fffffffffffffff1", info); // max address
        }

        // ===== 33.8: Null reference in optional parameters =====

        [Fact]
        public void TestAddEmptyByteArrayIsNoOp()
        {
            var bin = new BinFile();
            bin.Add(new byte[0], 0);

            Assert.Equal(0, bin.Length);
        }

        [Fact]
        public void TestAddNullStringThrows()
        {
            var bin = new BinFile();
            Assert.Throws<ArgumentNullException>(() => bin.Add((string)null));
        }

        [Fact]
        public void TestAddOverwriteNullThrows()
        {
            var bin = new BinFile();
            Assert.Throws<ArgumentNullException>(() => bin.Add(null, 0, overwrite: true));
        }

        // ===== 33.12: Absolute maximum address =====

        [Fact]
        public void TestSingleByteAtNearMaxAddress()
        {
            var bin = new BinFile();
            ulong addr = 0xFFFFFFFFFFFFFFFE; // Max - 1, so maximumAddress = Max (no overflow)
            bin.Add(new byte[] { 0x99 }, addr);

            Assert.Equal(addr, bin.MinimumAddress);
            Assert.Equal(0x99UL, bin[addr]);

            byte[] range = bin.AsBinary(addr, addr + 1);
            Assert.Equal(new byte[] { 0x99 }, range);
        }

        [Fact]
        public void TestAbsoluteMaxAddressOverflows()
        {
            var bin = new BinFile();
            ulong addr = 0xFFFFFFFFFFFFFFFF;

            // This must throw because addr + 1 overflows to 0
            Assert.Throws<ArgumentException>(() => bin.Add(new byte[] { 0x99 }, addr));
        }

        // ===== 33.13: Segment crossing 4GB boundary =====

        [Fact]
        public void TestSegmentCrossing4GBBoundary()
        {
            var bin = new BinFile();
            ulong addr = 0xFFFFFFF0;
            byte[] data = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
            bin.Add(data, addr);

            Assert.Single(bin.Segments);
            Assert.Equal(addr, bin.MinimumAddress);
            Assert.Equal(addr + 32, bin.MaximumAddress);
            Assert.Equal(32, bin.Length);

            // Verify exact data integrity across the 4GB boundary
            byte[] retrieved = bin.AsBinary(addr, addr + 32);
            Assert.Equal(data, retrieved);
            Assert.Equal(data, bin.AsBinary());
        }

        // ===== 33.14: Exclude that empties all data =====

        [Fact]
        public void TestExcludeEntireRange()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02, 0x03 }, 10);

            bin.Exclude(10, 13);

            Assert.Equal(0, bin.Length);
            Assert.Empty(bin.Segments);

            // All export methods should produce valid empty output
            Assert.Equal(new byte[0], bin.AsBinary());
            Assert.Equal("\n", bin.AsHexdump());

            // Serializers should produce valid (possibly empty) output without crashing
            string srec = bin.AsSrec();
            Assert.DoesNotContain("S1", srec); // no data records
            string ihex = bin.AsIhex();
            Assert.Contains(":00000001FF", ihex); // only EOF record

            // ToString should indicate empty
            string toString = bin.ToString();
            Assert.Contains("empty", toString.ToLower());
        }

        // ===== 33.15: Extreme chained operations =====

        [Fact]
        public void TestChainedOperations()
        {
            var bin = new BinFile();

            // Step 1-2: Two segments
            bin.Add(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 }, 0);
            bin.Add(new byte[] { 0xA0, 0xA1, 0xA2, 0xA3 }, 20);

            // Step 3: Fill gaps with 0x00 â†’ single segment [0..24)
            bin.Fill(0x00);
            Assert.Single(bin.Segments);
            Assert.Equal(0UL, bin.MinimumAddress);
            Assert.Equal(24UL, bin.MaximumAddress);

            // Step 4: Exclude [4..8) â†’ two segments [0..4), [8..24)
            bin.Exclude(4, 8);
            Assert.Equal(2, bin.Segments.Count);
            Assert.Equal(0UL, bin.MinimumAddress);
            Assert.Equal(24UL, bin.MaximumAddress);

            // Step 5: Crop [2..22) â†’ [2..4), [8..22)
            bin.Crop(2, 22);
            Assert.Equal(2, bin.Segments.Count);
            Assert.Equal(2UL, bin.MinimumAddress);
            Assert.Equal(22UL, bin.MaximumAddress);
            Assert.Equal(new byte[] { 0x03, 0x04 }, bin.AsBinary(2, 4));
            Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xA0, 0xA1 }, bin.AsBinary(8, 22));

            // Step 6: Overwrite at 10 â†’ bytes at 10,11 become BB,CC
            bin.Add(new byte[] { 0xBB, 0xCC }, 10, overwrite: true);
            Assert.Equal(0xBBUL, bin[10]);
            Assert.Equal(0xCCUL, bin[11]);

            // Step 7: Fill gap [4..8) with 0xDD â†’ single segment [2..22)
            bin.Fill(0xDD);
            Assert.Single(bin.Segments);
            Assert.Equal(2UL, bin.MinimumAddress);
            Assert.Equal(22UL, bin.MaximumAddress);
            Assert.Equal(20, bin.Length);

            // Verify exact final state byte by byte
            byte[] expected = {
                0x03, 0x04,                                     // [2..4) original data
                0xDD, 0xDD, 0xDD, 0xDD,                        // [4..8) filled gap
                0x00, 0x00,                                     // [8..10) from Fill step 3
                0xBB, 0xCC,                                     // [10..12) overwritten
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // [12..20) from Fill step 3
                0xA0, 0xA1                                      // [20..22) original data
            };
            Assert.Equal(expected, bin.AsBinary());
        }

        // ===== 33.16: Thousand small segments =====

        [Fact]
        public void TestThousandSmallSegments()
        {
            var bin = new BinFile();

            // Add 1000 segments of 1 byte at non-overlapping addresses spaced 10 apart
            for (int i = 0; i < 1000; i++)
            {
                ulong addr = (ulong)(i * 10);
                bin.Add(new byte[] { (byte)(addr & 0xFF) }, addr);
            }

            Assert.Equal(1000, bin.Segments.Count);
            Assert.Equal(0UL, bin.MinimumAddress);
            Assert.Equal(9991UL, bin.MaximumAddress); // last segment at 9990, 1 byte â†’ 9991
            Assert.Equal(1000, bin.Length); // 1000 segments Ã— 1 byte each

            // Verify first, middle, and last segments have correct data
            Assert.Equal(0x00UL, bin[0]);         // addr 0 â†’ 0x00
            Assert.Equal(0x88UL, bin[5000]);      // addr 5000 â†’ 5000 & 0xFF = 0x88
            Assert.Equal(0x06UL, bin[9990]);       // addr 9990 â†’ 9990 & 0xFF = 0x06

            // Verify segment boundaries
            Assert.Equal(0UL, bin.Segments[0].MinimumAddress);
            Assert.Equal(1UL, bin.Segments[0].MaximumAddress);
            Assert.Equal(9990UL, bin.Segments[999].MinimumAddress);
            Assert.Equal(9991UL, bin.Segments[999].MaximumAddress);

            // Binary output should be 9991 bytes (min=0 to max=9991, gaps filled with 0xFF)
            byte[] binary = bin.AsBinary();
            Assert.Equal(9991, binary.Length);
            Assert.Equal(0x00, binary[0]);     // data at addr 0
            Assert.Equal(0xFF, binary[1]);     // gap
            Assert.Equal(0xFF, binary[9]);     // gap
            Assert.Equal(0x0A, binary[10]);    // data at addr 10 â†’ 10 & 0xFF = 0x0A
        }

        // ===== 33.17: Cascading overwrite =====

        [Fact]
        public void TestCascadingOverwrite()
        {
            var bin = new BinFile();

            // Range A: addresses 0-9
            bin.Add(new byte[] { 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA }, 0);

            // Range B: addresses 5-14 (overlaps A)
            bin.Add(new byte[] { 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB }, 5, overwrite: true);

            // Range C: addresses 8-12 (overlaps B)
            bin.Add(new byte[] { 0xCC, 0xCC, 0xCC, 0xCC, 0xCC }, 8, overwrite: true);

            // Should be a single contiguous segment [0..15)
            Assert.Single(bin.Segments);
            Assert.Equal(0UL, bin.MinimumAddress);
            Assert.Equal(15UL, bin.MaximumAddress);
            Assert.Equal(15, bin.Length);

            // Verify exact final state
            byte[] expected = {
                0xAA, 0xAA, 0xAA, 0xAA, 0xAA, // [0..5) from A
                0xBB, 0xBB, 0xBB,               // [5..8) from B
                0xCC, 0xCC, 0xCC, 0xCC, 0xCC,   // [8..13) from C
                0xBB, 0xBB                       // [13..15) from B
            };
            Assert.Equal(expected, bin.AsBinary());
        }

        // ===== 33.18: AsBinary en gaps =====

        [Fact]
        public void TestAsBinaryAcrossGap()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02 }, 0);
            bin.Add(new byte[] { 0x03, 0x04 }, 5);

            // AsBinary across the gap (addresses 0-6)
            byte[] range = bin.AsBinary(0, 7);

            // 0,1 = data; 2,3,4 = 0xFF (gap); 5,6 = data
            Assert.Equal(0x01, range[0]);
            Assert.Equal(0x02, range[1]);
            Assert.Equal(0xFF, range[2]); // gap
            Assert.Equal(0xFF, range[3]); // gap
            Assert.Equal(0xFF, range[4]); // gap
            Assert.Equal(0x03, range[5]);
            Assert.Equal(0x04, range[6]);
        }

        [Fact]
        public void TestAddOverwriteInGapCreatesSegment()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02 }, 0);
            bin.Add(new byte[] { 0x05, 0x06 }, 10);

            // Add with overwrite in the gap creates a third segment
            bin.Add(new byte[] { 0xAA, 0xBB }, 5, overwrite: true);

            Assert.Equal(3, bin.Segments.Count);
            Assert.Equal(0UL, bin.MinimumAddress);
            Assert.Equal(12UL, bin.MaximumAddress);
            Assert.Equal(0x01UL, bin[0]);
            Assert.Equal(0x02UL, bin[1]);
            Assert.Equal(0xAAUL, bin[5]);
            Assert.Equal(0xBBUL, bin[6]);
            Assert.Equal(0x05UL, bin[10]);
            Assert.Equal(0x06UL, bin[11]);
        }

        // ===== 33.19: Crop to non-existent range =====

        [Fact]
        public void TestCropToNonExistentRange()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02, 0x03 }, 100);

            // Crop to a range completely outside the data
            bin.Crop(0, 10);

            Assert.Equal(0, bin.Length);
            Assert.Empty(bin.Segments);
            Assert.Equal(new byte[0], bin.AsBinary());
        }

        [Fact]
        public void TestCropToPartiallyOverlappingRange()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02, 0x03, 0x04 }, 100);

            // Crop to range that partially overlaps
            bin.Crop(102, 200);

            Assert.Single(bin.Segments);
            Assert.Equal(2, bin.Length);
            Assert.Equal(102UL,bin.MinimumAddress);
            Assert.Equal(104UL,bin.MaximumAddress);
            Assert.Equal(new byte[] { 0x03, 0x04 }, bin.AsBinary(102, 104));
            Assert.Equal(new byte[] { 0x03, 0x04 }, bin.AsBinary());
        }

        // ===== 33.20: All methods on empty BinFile =====

        [Fact]
        public void TestAllMethodsOnEmptyBinFile()
        {
            var bin = new BinFile();

            Assert.Equal(0, bin.Length);
            Assert.Empty(bin.Segments);

            // Export methods produce valid empty output
            Assert.Equal(new byte[0], bin.AsBinary());
            Assert.Equal("\n", bin.AsHexdump());
            Assert.Contains(":00000001FF", bin.AsIhex()); // only EOF record

            // Info returns "Data ranges:\n\n" for empty BinFile
            string info = bin.Info();
            Assert.Contains("Data ranges", info);
            // Layout returns "\n" for empty BinFile
            Assert.Equal("\n", bin.Layout());
            // ToString indicates empty
            Assert.Contains("empty", bin.ToString().ToLower());

            // Mutation operations are no-ops on empty BinFile
            bin.Fill(0x00);
            Assert.Equal(0, bin.Length);
            Assert.Empty(bin.Segments);

            bin.Exclude(0, 100);
            Assert.Equal(0, bin.Length);
            Assert.Empty(bin.Segments);

            bin.Crop(0, 100);
            Assert.Equal(0, bin.Length);
            Assert.Empty(bin.Segments);
        }

        // ===== 33.21: Add empty string and empty byte[] =====

        [Fact]
        public void TestAddEmptyStringThrowsOrIsNoOp()
        {
            var bin = new BinFile();

            // Empty string should throw UnsupportedFileFormatException
            // because the format detector can't detect a format from empty input
            Assert.Throws<UnsupportedFileFormatException>(() => bin.Add(""));
        }

        [Fact]
        public void TestAddEmptyByteArray()
        {
            var bin = new BinFile();
            bin.Add(new byte[0], 0);
            Assert.Equal(0, bin.Length);
        }

        [Fact]
        public void TestAddNullByteArray()
        {
            var bin = new BinFile();
            Assert.Throws<ArgumentNullException>(() => bin.Add((byte[])null, 0));
        }


        // ===== 34.7: Address range validation in serializers =====

        [Fact]
        public void TestSrecAddressAbove32BitWith32BitAddressLength()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01 }, 0x1_0000_0000);
            var ex = Assert.Throws<BincopyException>(() => bin.AsSrec(variant: Formats.SrecVariant.S37));
            Assert.Contains("SREC", ex.Message);
        }

        [Fact]
        public void TestSrecAddressAbove16BitWith16BitAddressLength()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01 }, 0x1_0000);
            var ex = Assert.Throws<BincopyException>(() => bin.AsSrec(variant: Formats.SrecVariant.S19));
            Assert.Contains("SREC", ex.Message);
        }

        [Fact]
        public void TestSrecExecutionStartAddressAboveRange()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01 }, 0x0000);
            bin.ExecutionStartAddress = 0x1_0000_0000;
            var ex = Assert.Throws<BincopyException>(() => bin.AsSrec(variant: Formats.SrecVariant.S37));
            Assert.Contains("SREC", ex.Message);
        }

        [Fact]
        public void TestIhexAddressAbove32Bit()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01 }, 0x1_0000_0000);
            var ex = Assert.Throws<BincopyException>(() => bin.AsIhex(variant: Formats.IhexVariant.I32Hex));
            Assert.Contains("4 GB", ex.Message);
        }

        [Fact]
        public void TestTiTxtAddressAbove32Bit()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01 }, 0x1_0000_0000);
            var ex = Assert.Throws<BincopyException>(() => bin.AsTiTxt());
            Assert.Contains("TI-TXT", ex.Message);
        }



        [Fact]
        public void TestOperatorPlusNull()
        {
            var a = new BinFile();
            a.Add(new byte[] { 0x01 }, 0);

            Assert.Throws<ArgumentNullException>(() => { var _ = a + (BinFile)null; });
            Assert.Throws<ArgumentNullException>(() => { var _ = (BinFile)null + a; });
        }


        [Fact]
        public void TestCropBadRange()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02, 0x03 }, 0);

            Assert.Throws<ArgumentException>(() => bin.Crop(5, 2));
        }


        [Fact]
        public void TestLayoutAllCharacters()
        {
            var bin = new BinFile();
            // Create a layout wide enough to show all 3 chars:
            // Segment at start (=), gap in middle ( ), partial at end (-)
            bin.Add(new byte[40], 0);
            bin.Add(new byte[10], 100);

            string layout = bin.Layout();
            Assert.Contains("=", layout);
            Assert.Contains(" ", layout);
        }



        [Fact]
        public void TestAddFileUnsupportedFormat()
        {
            var bin = new BinFile();
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(tempFile, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x01, 0x02, 0x03 });
                Assert.Throws<UnsupportedFileFormatException>(() => bin.AddFile(tempFile));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        // ===== Task 9.4: Missing test coverage =====


        /// <summary>
        /// Validates: Requirements 1.27, 2.27
        /// AddFile with a raw binary file should throw UnsupportedFileFormatException.
        /// </summary>
        [Fact]
        public void TestAddFileBinaryThrowsUnsupportedFormat()
        {
            var bin = new BinFile();
            string tempFile = Path.GetTempFileName();
            try
            {
                // Write random binary data that doesn't match any known format
                File.WriteAllBytes(tempFile, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x01, 0x02, 0x03 });
                Assert.Throws<UnsupportedFileFormatException>(() => bin.AddFile(tempFile));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }



        // ===== Bug 1.1: Stale cache after segment removal =====
        // These tests demonstrate that after Remove/Crop/Exclude, the system doesn't
        // reset _currentSegment or _currentSegmentIndex, so the next Add() uses the
        // fast path on a stale/deleted segment, corrupting or losing data.
        // EXPECTED TO FAIL on unfixed code.
        //
        // KEY INSIGHT: The fast-path condition is:
        //   _currentSegment != null && segment.MinimumAddress == _currentSegment.MaximumAddress
        // After Remove/Exclude/Crop, _currentSegment still points to the OLD segment object
        // (no longer in _segments). To trigger the bug, we must Add() at the exact address
        // that equals the stale _currentSegment.MaximumAddress.

        /// <summary>
        /// Validates: Requirements 1.1, 2.1
        /// After Add([0x01..0x06], 0), _currentSegment points to segment [0,6) with MaximumAddress=6.
        /// After Exclude(2,4), _currentSegment is stale (still [0,6) object, not in _segments).
        /// Add(newData, 6) triggers the fast path because 6 == stale MaximumAddress(6).
        /// The fast path appends to the stale segment object, which is NOT in _segments,
        /// so the new data is lost from the BinFile's perspective.
        /// </summary>
        [Fact]
        public void BugExploration_StaleCacheAfterExclude_ThenAddAtStaleMaxAddress()
        {
            var bin = new BinFile();
            // Add 6 bytes at address 0: [0x01, 0x02, 0x03, 0x04, 0x05, 0x06]
            // After this: _currentSegment = segment [0,6), MaximumAddress = 6
            bin.Add(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 }, 0);

            // Exclude addresses [2, 4) - removes bytes at addresses 2 and 3
            // After this: _segments = [segment[0,2), segment[4,6)]
            // BUT _currentSegment still points to the OLD [0,6) object (stale!)
            bin.Exclude(2, 4);
            Assert.Equal(2, bin.Segments.Count);

            // Add new data at address 6 - this equals the stale _currentSegment.MaximumAddress
            // On unfixed code: fast path triggers, appends to stale segment object â†’ data lost
            byte[] newData = new byte[] { 0xAA, 0xBB };
            bin.Add(newData, 6);

            // Verify the new data is actually in the BinFile at address 6
            // On unfixed code, this will fail because the data was appended to a stale object
            byte[] dataAt6 = bin.AsBinary(6, 8);
            Assert.Equal(newData, dataAt6);

            // Also verify the existing segments are intact
            Assert.Equal(new byte[] { 0x01, 0x02 }, bin.AsBinary(0, 2));
            Assert.Equal(new byte[] { 0x05, 0x06 }, bin.AsBinary(4, 6));
        }

        /// <summary>
        /// Validates: Requirements 1.1, 2.1
        /// After Add([0x01..0x08], 0), _currentSegment = segment [0,8), MaximumAddress=8.
        /// After Crop(0,3), _currentSegment is stale (still [0,8) object).
        /// Add(newData, 8) triggers the fast path because 8 == stale MaximumAddress(8).
        /// The fast path appends to the stale segment, so data is lost.
        /// </summary>
        [Fact]
        public void BugExploration_StaleCacheAfterCrop_ThenAddAtStaleMaxAddress()
        {
            var bin = new BinFile();
            // Add 8 bytes at address 0: [0x01..0x08]
            // After this: _currentSegment = segment [0,8), MaximumAddress = 8
            bin.Add(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 }, 0);

            // Crop to [0, 3) - keeps only addresses 0, 1, 2
            // _segments = [segment[0,3)], but _currentSegment still points to OLD [0,8)
            bin.Crop(0, 3);
            Assert.Single(bin.Segments);
            Assert.Equal(0UL,bin.MinimumAddress);
            Assert.Equal(3UL,bin.MaximumAddress);

            // Add new data at address 8 - equals stale _currentSegment.MaximumAddress
            // On unfixed code: fast path triggers, appends to stale segment â†’ data lost
            byte[] newData = new byte[] { 0xDD, 0xEE };
            bin.Add(newData, 8);

            // Should now have 2 segments: [0,3) and [8,10)
            Assert.Equal(2, bin.Segments.Count);

            // Verify the new data is at address 8
            byte[] dataAt8 = bin.AsBinary(8, 10);
            Assert.Equal(newData, dataAt8);

            // Verify the cropped data is still intact
            byte[] dataAt0 = bin.AsBinary(0, 3);
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, dataAt0);
        }



        // ===== Bug 1.5: IhexParser extended address reset =====
        // This test demonstrates that when an IHEX file contains both record type 02
        // (Extended Segment Address) and record type 04 (Extended Linear Address),
        // the parser does NOT reset the stale address variable when switching types.
        // When type 04 is encountered, extendedSegmentAddress should be reset to 0,
        // but it isn't - so the final data address = offset + extendedSegmentAddress*16 +
        // extendedLinearAddress (wrong), instead of offset + extendedLinearAddress (correct).
        // EXPECTED TO FAIL on unfixed code.

        /// <summary>
        /// Validates: Requirements 1.43, 2.43
        /// Construct IHEX with:
        ///   1. Record type 02: extendedSegmentAddress = 0x1000 (contributes 0x1000*16 = 0x10000)
        ///   2. Record type 04: extendedLinearAddress = 0x0001 (contributes 0x0001 &lt;&lt; 16 = 0x10000)
        ///   3. Data record at offset 0x0000 with byte 0xAA
        /// Correct address = 0x0000 + 0x00010000 = 0x00010000 (type 04 should reset type 02)
        /// Buggy address  = 0x0000 + 0x10000 + 0x00010000 = 0x00020000 (stale accumulation)
        /// On unfixed code: the data ends up at 0x00020000 instead of 0x00010000.
        /// </summary>
        [Fact]
        public void BugExploration_IhexExtendedAddressReset_Type04ShouldResetType02()
        {
            // Build IHEX string:
            // Record type 02: Extended Segment Address = 0x1000
            //   :02 0000 02 1000 EC
            //   Checksum: 256 - (0x02+0x00+0x00+0x02+0x10+0x00) & 0xFF = 256 - 0x14 = 0xEC
            // Record type 04: Extended Linear Address = 0x0001
            //   :02 0000 04 0001 F9
            //   Checksum: 256 - (0x02+0x00+0x00+0x04+0x00+0x01) & 0xFF = 256 - 0x07 = 0xF9
            // Data record: 1 byte 0xAA at offset 0x0000
            //   :01 0000 00 AA 55
            //   Checksum: 256 - (0x01+0x00+0x00+0x00+0xAA) & 0xFF = 256 - 0xAB = 0x55
            // EOF: :00000001FF
            string ihexData =
                ":020000021000EC\n" +   // Type 02: extendedSegmentAddress = 0x1000
                ":020000040001F9\n" +   // Type 04: extendedLinearAddress = 0x0001 << 16 = 0x00010000
                ":01000000AA55\n" +     // Data: 1 byte 0xAA at offset 0x0000
                ":00000001FF\n";        // EOF

            var bin = new BinFile();
            bin.Add(ihexData);

            // The correct address should be: offset(0) + extendedLinearAddress(0x00010000) = 0x00010000
            // Type 04 should have reset extendedSegmentAddress to 0.
            //
            // On unfixed code: address = offset(0) + extendedSegmentAddress(0x1000*16=0x10000)
            //                           + extendedLinearAddress(0x00010000) = 0x00020000
            // This is WRONG - the stale extendedSegmentAddress was not cleared.

            Assert.Single(bin.Segments);
            Assert.Equal(0x00010000UL,bin.MinimumAddress);
            Assert.Equal(0x00010001UL,bin.MaximumAddress);
            Assert.Equal(0xAAUL, bin[0x00010000]);
        }


        // ===== Bug 1.7: numberOfDataBytes=0 validation =====
        // These tests demonstrate that AsSrec() and AsIhex() do NOT validate
        // numberOfDataBytes at the API level. When numberOfDataBytes=0:
        // - With data: Segment.Chunks catches it with a confusing "Chunk size must be positive" error
        //   instead of a clear "numberOfDataBytes must be positive" from AsSrec/AsIhex
        // - With empty BinFile: silently succeeds (inconsistent behavior)
        // - With numberOfDataBytes=1 and a serializer that requires even counts: integer
        //   division could silently truncate, then Segment.Chunks throws the confusing error
        // EXPECTED TO FAIL on unfixed code - wrong exception type or missing validation.

        /// <summary>
        /// Validates: Requirements 1.19, 2.19
        /// Call AsSrec(numberOfDataBytes: 0) on a BinFile with data.
        /// Expected behavior after fix: ArgumentException thrown at the AsSrec level
        /// with a message about numberOfDataBytes, before reaching serialization internals.
        /// On unfixed code: the exception comes from Segment.Chunks with a confusing
        /// "Chunk size must be positive" message - the validation is at the wrong level.
        /// Uses a timeout to prevent potential infinite loop from hanging the test.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task BugExploration_AsSrecNumberOfDataBytesZero_ShouldThrowArgumentException()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02, 0x03, 0x04 }, 0);

            // Use Task.Run with a CancellationToken timeout to prevent infinite loop
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
            Exception caught = null;
            bool completed = false;

            try
            {
                var task = System.Threading.Tasks.Task.Run(() =>
                {
                    bin.AsSrec(numberOfDataBytes: 0);
                }, cts.Token);

                await System.Threading.Tasks.Task.WhenAny(
                    task,
                    System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(5), cts.Token));

                completed = task.IsCompleted;
                if (task.IsFaulted && task.Exception != null)
                {
                    caught = task.Exception.InnerException;
                }
            }
            catch (OperationCanceledException)
            {
                // Timeout - the call hung
                completed = false;
            }

            // On fixed code: ArgumentException should be thrown at the AsSrec level
            // with a message mentioning "numberOfDataBytes"
            // On unfixed code: exception comes from Segment.Chunks with wrong message
            Assert.True(completed, "AsSrec(numberOfDataBytes: 0) hung - likely infinite loop (no validation)");
            Assert.NotNull(caught);
            Assert.IsType<ArgumentException>(caught);
            // The error message should mention numberOfDataBytes, not "Chunk size"
            Assert.Contains("numberOfDataBytes", caught.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Validates: Requirements 1.39, 2.39
        /// Call AsIhex(numberOfDataBytes: 0) on a BinFile with data.
        /// Expected behavior after fix: ArgumentException thrown at the AsIhex level
        /// with a message about numberOfDataBytes, before reaching serialization internals.
        /// On unfixed code: the exception comes from Segment.Chunks with a confusing
        /// "Chunk size must be positive" message - the validation is at the wrong level.
        /// Uses a timeout to prevent potential infinite loop from hanging the test.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task BugExploration_AsIhexNumberOfDataBytesZero_ShouldThrowArgumentException()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02, 0x03, 0x04 }, 0);

            // Use Task.Run with a CancellationToken timeout to prevent infinite loop
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
            Exception caught = null;
            bool completed = false;

            try
            {
                var task = System.Threading.Tasks.Task.Run(() =>
                {
                    bin.AsIhex(numberOfDataBytes: 0);
                }, cts.Token);

                await System.Threading.Tasks.Task.WhenAny(
                    task,
                    System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(5), cts.Token));

                completed = task.IsCompleted;
                if (task.IsFaulted && task.Exception != null)
                {
                    caught = task.Exception.InnerException;
                }
            }
            catch (OperationCanceledException)
            {
                // Timeout - the call hung
                completed = false;
            }

            // On fixed code: ArgumentException should be thrown at the AsIhex level
            // with a message mentioning "numberOfDataBytes"
            // On unfixed code: exception comes from Segment.Chunks with wrong message
            Assert.True(completed, "AsIhex(numberOfDataBytes: 0) hung - likely infinite loop (no validation)");
            Assert.NotNull(caught);
            Assert.IsType<ArgumentException>(caught);
            // The error message should mention numberOfDataBytes, not "Chunk size"
            Assert.Contains("numberOfDataBytes", caught.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ===== Bug 1.8: Chunks XOR merge incorrectness =====
        // The Segments.Chunks() method uses XOR to merge overlapping alignment blocks:
        //   merged[i] = (byte)(low[i] ^ high[i] ^ alignmentPadding[i])
        // This XOR trick relies on the assumption that at each byte position, at most
        // one of low/high differs from the padding value. With the standard Segments
        // invariant (non-overlapping, non-adjacent), this holds for gaps. However,
        /// <summary>
        /// Two non-adjacent segments close enough that their alignment padding produces
        /// overlapping chunks. Segments.Chunks must merge the overlapping block correctly,
        /// preserving real data from both sides.
        ///
        /// Setup: seg1=[0,2] data=[0x11,0x22], seg2=[3,5] data=[0x44,0x55], gap at byte 2.
        /// alignment=4, size=4, padding=0xFF.
        ///
        /// seg1 chunks: append 2 padding bytes â†’ (addr=0, [0x11,0x22,0xFF,0xFF])  covers 0-3
        /// seg2 chunks: prepend 3 padding bytes, addrâ†’0 â†’ chunk1=(addr=0,[0xFF,0xFF,0xFF,0x44])
        ///                                                  chunk2=(addr=4,[0x55,0xFF,0xFF,0xFF])
        ///
        /// Overlap at addr=0: low=[0x11,0x22,0xFF,0xFF], high=[0xFF,0xFF,0xFF,0x44]
        /// Merge: pos0â†’0x11 (lowâ‰ pad), pos1â†’0x22 (lowâ‰ pad), pos2â†’0xFF (low=padâ†’use high=0xFF),
        ///        pos3â†’0x44 (low=padâ†’use high=0x44)
        /// Final chunks: (0,[0x11,0x22,0xFF,0x44]), (4,[0x55,0xFF,0xFF,0xFF])
        /// </summary>
        [Fact]
        public void Chunks_NonAdjacentSegmentsWithAlignmentPadding_MergesOverlappingBlockCorrectly()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x11, 0x22 }, 0); // seg1: bytes 0-1
            bin.Add(new byte[] { 0x44, 0x55 }, 3); // seg2: bytes 3-4, gap at byte 2

            Assert.Equal(2, bin.Segments.Count);

            var chunks = bin.Segments.Chunks(size: 4, alignment: 4, padding: 0xFF).ToList();

            // Two chunks: the merged overlap block at addr=0 and the tail at addr=4
            Assert.Equal(2, chunks.Count);
            Assert.Equal(0UL, chunks[0].Address);
            Assert.Equal(new byte[] { 0x11, 0x22, 0xFF, 0x44 }, chunks[0].Data);
            Assert.Equal(4UL, chunks[1].Address);
            Assert.Equal(new byte[] { 0x55, 0xFF, 0xFF, 0xFF }, chunks[1].Data);
        }


        // ===== Header API tests =====

        [Fact]
        public void HeaderBytes_SetAndGet_RoundTrips()
        {
            var bin = new BinFile();
            byte[] header = new byte[] { 0x01, 0x02, 0x03 };
            bin.HeaderBytes = header;
            Assert.Equal(header, bin.HeaderBytes);
        }

        [Fact]
        public void HeaderText_WithUtf8Encoding_DecodesCorrectly()
        {
            var bin = new BinFile(headerEncoding: "utf-8");
            bin.HeaderBytes = System.Text.Encoding.UTF8.GetBytes("hello");
            Assert.Equal("hello", bin.HeaderText);
        }

        [Fact]
        public void HeaderText_WithNullEncoding_ReturnsNull()
        {
            var bin = new BinFile(headerEncoding: null);
            bin.HeaderBytes = new byte[] { 0x01, 0x02 };
            Assert.Null(bin.HeaderText);
        }

        [Fact]
        public void OperatorPlus_PreservesHeaderBytesFromFirstOperand()
        {
            var a = new BinFile();
            a.Add(new byte[] { 0x01 }, 0);
            a.HeaderBytes = new byte[] { 0xAA, 0xBB };

            var b = new BinFile();
            b.Add(new byte[] { 0x02 }, 100);

            var result = a + b;
            Assert.Equal(new byte[] { 0xAA, 0xBB }, result.HeaderBytes);
        }

        [Fact]
        public void OperatorPlus_UsesHeaderBytesFromSecondOperandWhenFirstHasNone()
        {
            var a = new BinFile();
            a.Add(new byte[] { 0x01 }, 0);

            var b = new BinFile();
            b.Add(new byte[] { 0x02 }, 100);
            b.HeaderBytes = new byte[] { 0xCC, 0xDD };

            var result = a + b;
            Assert.Equal(new byte[] { 0xCC, 0xDD }, result.HeaderBytes);
        }


        [Fact]
        public void HeaderText_InvalidatedWhenHeaderBytesChanges()
        {
            var bin = new BinFile(headerEncoding: "utf-8");
            bin.HeaderBytes = System.Text.Encoding.UTF8.GetBytes("first");
            Assert.Equal("first", bin.HeaderText);
            bin.HeaderBytes = System.Text.Encoding.UTF8.GetBytes("second");
            Assert.Equal("second", bin.HeaderText);
        }

        [Fact]
        public void HeaderText_NullWhenNoBytesSet()
        {
            var bin = new BinFile(headerEncoding: "utf-8");
            Assert.Null(bin.HeaderText);
        }

        [Fact]
        public void HeaderText_Setter_EncodesStringToHeaderBytes()
        {
            var bin = new BinFile(headerEncoding: "utf-8");
            bin.HeaderText = "hello";
            Assert.Equal(System.Text.Encoding.UTF8.GetBytes("hello"), bin.HeaderBytes);
            Assert.Equal("hello", bin.HeaderText);
        }

        [Fact]
        public void HeaderText_Setter_NullClearsHeader()
        {
            var bin = new BinFile(headerEncoding: "utf-8");
            bin.HeaderText = "hello";
            bin.HeaderText = null;
            Assert.Null(bin.HeaderBytes);
            Assert.Null(bin.HeaderText);
        }

        [Fact]
        public void HeaderText_Setter_ThrowsWhenNoEncodingConfigured()
        {
            var bin = new BinFile(headerEncoding: null);
            Assert.Throws<InvalidOperationException>(() => bin.HeaderText = "hello");
        }
        // ===== Bug 1.9: Integer overflow in Layout and AsBinary =====
        // Layout: `int width = Math.Min(80, (int)size)` where size is ulong.
        // When MaximumAddress - MinimumAddress > int.MaxValue, the unchecked (int)size
        // cast wraps to a negative number, and Math.Min(80, negative) returns the
        // negative value. This causes the for-loop `for (int i = 0; i < width; i++)`
        // to never execute (width < 0), producing a broken layout string.
        //
        // AsBinary: `byte[] result = new byte[length]` where length is ulong.
        // When the range exceeds int.MaxValue bytes, the array allocation throws
        // an OverflowException or OutOfMemoryException instead of a descriptive
        // BincopyException explaining the range is too large.
        //
        // EXPECTED TO FAIL on unfixed code.

        /// <summary>
        /// Validates: Requirements 1.36, 2.36
        /// Create a BinFile with a small segment at address 0 and another at address 0x80000000.
        /// The span MaximumAddress - MinimumAddress = 0x80000001, which when cast to int via
        /// (int)size wraps to int.MinValue (-2147483647). Math.Min(80, negative) returns the
        /// negative value, so the for-loop `for (int i = 0; i &lt; width; i++)` never executes.
        /// Layout() should produce a valid layout with width clamped to 80.
        /// On unfixed code: width becomes negative, the visualization row is empty/missing.
        /// </summary>
        [Fact]
        public void BugExploration_LayoutIntegerOverflow_ShouldNotProduceNegativeWidth()
        {
            var bin = new BinFile();
            // Segment 1: small segment at address 0
            bin.Add(new byte[] { 0x01 }, 0);
            // Segment 2: small segment at address 0x80000000 (2^31)
            // span = 0x80000001, (int)0x80000001 = -2147483647 (wraps negative!)
            // Math.Min(80, -2147483647) = -2147483647 â†’ for-loop skipped
            bin.Add(new byte[] { 0x02 }, 0x80000000);

            // Layout() should produce a valid string with the visualization row
            string layout = bin.Layout();

            // The layout should have at least 2 lines:
            // Line 1: address header (e.g., "0x0                    0x80000001")
            // Line 2: visualization characters (=, -, or space), width should be 80
            string[] lines = layout.Split('\n', StringSplitOptions.None);

            // Should have the address header line
            Assert.True(lines.Length >= 2, $"Layout should have at least 2 lines, got {lines.Length}. Full output: '{layout}'");

            // The visualization line (second line) should have width 80 (the max, since size > 80)
            // On unfixed code: this line is empty because width was negative
            string vizLine = lines[1];
            Assert.True(vizLine.Length > 0, $"Visualization line should have positive length (width should be 80), got length {vizLine.Length}. Full output: '{layout}'");

            // Width should be exactly 80 since size (0x80000001) >> 80
            Assert.Equal(80, vizLine.Length);

            // The visualization should contain '=' for data and ' ' for gaps
            Assert.True(vizLine.Contains('=') || vizLine.Contains('-'),
                $"Visualization should contain data markers ('=' or '-'), got: '{vizLine}'");
        }

        /// <summary>
        /// Validates: Requirements 1.35, 2.35
        /// Call AsBinary with a range where the byte length exceeds int.MaxValue.
        /// The method should throw a descriptive BincopyException (not OverflowException
        /// or OutOfMemoryException) explaining the range is too large.
        /// On unfixed code: throws OverflowException from `new byte[length]` when
        /// length > int.MaxValue, or OutOfMemoryException - neither is descriptive.
        /// </summary>
        [Fact]
        public void BugExploration_AsBinaryIntegerOverflow_ShouldThrowDescriptiveException()
        {
            var bin = new BinFile();
            // Add a tiny segment so the BinFile is not empty
            bin.Add(new byte[] { 0x01 }, 0);

            // Request a range where endAddress - startAddress > int.MaxValue
            // This means length = endAddress - startAddress > int.MaxValue
            ulong startAddr = 0;
            ulong endAddr = (ulong)int.MaxValue + 2; // length = int.MaxValue + 2 > int.MaxValue

            // On fixed code: should throw BincopyException with a descriptive message
            // On unfixed code: throws OverflowException or OutOfMemoryException
            var ex = Assert.Throws<BincopyException>(() => bin.AsBinary(startAddr, endAddr));

            // The exception message should be descriptive about the range being too large
            Assert.NotNull(ex.Message);
            Assert.True(ex.Message.Length > 0, "Exception message should not be empty");
        }

        // ===== Bug 1.10: Null handling inconsistency and error handling =====
        // These tests demonstrate two error handling issues:
        // 1. Add(byte[] null) silently ignores null instead of throwing ArgumentNullException
        // 2. AddFile() with corrupted ELF swallows the original ELF parse exception and
        //    throws a generic UnsupportedFileFormatException, hiding the real error from users
        // EXPECTED TO FAIL on unfixed code.

        /// <summary>
        /// Validates: Requirements 1.18, 2.18, 1.42, 2.42
        /// Add(byte[] null) should throw ArgumentNullException.
        /// On unfixed code: the method silently returns (no-op) because the null check
        /// at line `if (data == null || data.Length == 0) return;` treats null as empty.
        /// </summary>
        [Fact]
        public void BugExploration_AddNullByteArray_ShouldThrowArgumentNullException()
        {
            var bin = new BinFile();

            // On fixed code: should throw ArgumentNullException
            // On unfixed code: silently returns - no exception thrown, test FAILS
            Assert.Throws<ArgumentNullException>(() => bin.Add(null, 0));
        }

        /// <summary>
        /// Validates: Requirements 1.17, 2.17
        /// AddFile() with a corrupted ELF file (valid magic bytes but truncated/invalid content)
        /// should propagate the original ELF parse error, NOT swallow it with a generic
        /// UnsupportedFileFormatException.
        ///
        /// On unfixed code: AddFile() catches BincopyException (base class) in the ELF try/catch,
        /// so any ELF parse error is swallowed. The method falls through to throw
        /// UnsupportedFileFormatException("Unable to detect file format"), hiding the real error.
        ///
        /// On fixed code: the ELF try/catch should only catch UnsupportedFileFormatException
        /// (not the base BincopyException), so ELF parse errors propagate with their original
        /// message and context.
        /// </summary>
        [Fact]
        public void BugExploration_AddFileCorruptedElf_ShouldPropagateOriginalException()
        {
            var bin = new BinFile();

            // Create a corrupted ELF file: valid magic bytes but truncated/invalid content
            // ELF magic: 0x7F, 'E', 'L', 'F' followed by garbage/truncated data
            byte[] corruptedElf = new byte[]
            {
                0x7F, 0x45, 0x4C, 0x46, // ELF magic: 0x7F, 'E', 'L', 'F'
                0x01,                     // EI_CLASS: 32-bit
                0x01,                     // EI_DATA: little-endian
                0x01,                     // EI_VERSION: current
                0x00,                     // EI_OSABI: UNIX System V
                0x00, 0x00, 0x00, 0x00,  // padding
                0x00, 0x00, 0x00, 0x00,  // padding
                // Truncated here - missing the rest of the ELF header
                // This should cause an ELF parse error, not "format not detected"
            };

            // Write to a temp file
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(tempFile, corruptedElf);

                // On fixed code: should throw an exception that is NOT UnsupportedFileFormatException
                // - it should propagate the original ELF parse error (e.g., BincopyException
                // with details about the corrupt ELF structure)
                //
                // On unfixed code: throws UnsupportedFileFormatException because the ELF
                // try/catch catches BincopyException (base class), swallowing the real error,
                // and falls through to the generic "Unable to detect file format" error.
                var ex = Assert.Throws<BincopyException>(() => bin.AddFile(tempFile));

                // The exception message should indicate an ELF parse error
                Assert.Contains("ELF", ex.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        // ===== Task 43: Fix Add(string) to respect format =====

        // 43.6: Add(string) with unrecognized format throws UnsupportedFileFormatException
        [Fact]
        public void Task43_6_AddStringUnrecognizedFormatThrows()
        {
            var bin = new BinFile();
            Assert.Throws<UnsupportedFileFormatException>(() => bin.Add("this is not a valid format"));
        }

        // 43.7: Add(string) with valid SREC first line but garbage after throws appropriate exception
        [Fact]
        public void Task43_7_AddStringValidSrecHeaderThenGarbageThrows()
        {
            var bin = new BinFile();
            // Valid S0 header record, then garbage
            string badSrec = "S0030000FC\nNOT_A_VALID_SREC_RECORD\n";
            Assert.Throws<InvalidRecordException>(() => bin.Add(badSrec));
        }


        // ===== #13: operator+ does not mutate operands =====

        [Fact]
        public void OperatorPlus_DoesNotMutateOperands()
        {
            var a = new BinFile();
            a.Add(new byte[] { 0x01, 0x02, 0x03, 0x04 }, address: 0x100);

            var b = new BinFile();
            b.Add(new byte[] { 0x10, 0x20, 0x30, 0x40 }, address: 0x200);

            byte[] aBeforeOp = a.AsBinary(0x100, 0x104);
            byte[] bBeforeOp = b.AsBinary(0x200, 0x204);
            ulong aMinBefore = a.MinimumAddress;
            ulong bMinBefore = b.MinimumAddress;

            var _ = a + b;

            Assert.Equal(aBeforeOp, a.AsBinary(0x100, 0x104));
            Assert.Equal(bBeforeOp, b.AsBinary(0x200, 0x204));
            Assert.Equal(aMinBefore, a.MinimumAddress);
            Assert.Equal(bMinBefore, b.MinimumAddress);
            Assert.Single(a.Segments);
            Assert.Single(b.Segments);
        }

        // ===== Aliasing / deep copy guarantees =====

        /// <summary>
        /// Mutating the byte[] passed to Add() must not affect the stored segment data.
        /// Segment copies the data on Add, so the caller retaining the array cannot corrupt the BinFile.
        /// </summary>
        [Fact]
        public void Aliasing_MutatingAddedArrayDoesNotAffectSegment()
        {
            var bin = new BinFile();
            byte[] data = { 0x01, 0x02, 0x03 };
            bin.Add(data, 0);

            data[0] = 0xFF;
            data[2] = 0xFF;

            Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, bin.AsBinary());
        }

        /// <summary>
        /// operator+ result must be independent of both operands.
        /// Modifying 'a' or 'b' after the operation must not change the result.
        /// </summary>
        [Fact]
        public void Aliasing_OperatorPlus_ModifyingOperandsAfterwardDoesNotAffectResult()
        {
            var a = new BinFile();
            a.Add(new byte[] { 0x01, 0x02 }, 0x100);

            var b = new BinFile();
            b.Add(new byte[] { 0x10, 0x20 }, 0x200);

            var c = a + b;
            byte[] cFromA = c.AsBinary(0x100, 0x102);
            byte[] cFromB = c.AsBinary(0x200, 0x202);

            a.Add(new byte[] { 0xFF, 0xFF }, 0x100, overwrite: true);
            b.Add(new byte[] { 0xFF, 0xFF }, 0x200, overwrite: true);

            Assert.Equal(cFromA, c.AsBinary(0x100, 0x102));
            Assert.Equal(cFromB, c.AsBinary(0x200, 0x202));
        }

        /// <summary>
        /// When Segments.Add() merges two adjacent segments, the merged segment
        /// must own a copy of both data buffers, not share them with the originals.
        /// Modifying the original segment's source data must not affect the merged result.
        /// </summary>
        [Fact]
        public void Aliasing_MergedSegmentIsIndependentOfOriginalArrays()
        {
            var bin = new BinFile();
            byte[] first = { 0x01, 0x02 };
            byte[] second = { 0x03, 0x04 };

            bin.Add(first, 0);
            bin.Add(second, 2);  // adjacent - will merge into one segment

            Assert.Single(bin.Segments);

            first[0] = 0xFF;
            second[1] = 0xFF;

            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, bin.AsBinary());
        }

        /// <summary>
        /// AsBinary() must return an independent copy each time.
        /// Mutating the returned array must not affect subsequent calls.
        /// </summary>
        [Fact]
        public void Aliasing_AsBinaryReturnsCopy()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02, 0x03 }, 0);

            byte[] result1 = bin.AsBinary();
            result1[0] = 0xFF;

            byte[] result2 = bin.AsBinary();
            Assert.Equal(0x01, result2[0]);
        }

        /// <summary>
        /// Data.ToArray() must return an independent copy each call.
        /// Mutating one copy must not affect another.
        /// </summary>
        [Fact]
        public void Aliasing_DataToArrayReturnsCopy()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0xAA, 0xBB, 0xCC }, 0);

            byte[] copy1 = bin.Segments[0].Data.ToArray();
            byte[] copy2 = bin.Segments[0].Data.ToArray();

            copy1[0] = 0xFF;

            Assert.Equal(0xAA, copy2[0]);
            Assert.Equal(0xAA, bin.Segments[0].Data[0]);
        }
    }
}

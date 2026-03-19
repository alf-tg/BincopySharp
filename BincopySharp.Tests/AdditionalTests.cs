using System;
using System.IO;
using System.Linq;
using Xunit;
using BincopySharp;
using BincopySharp.Formats;

namespace BincopySharp.Tests
{
    /// <summary>
    /// Tests adicionales que no son port de Python: edge cases, cobertura de ramas,
    /// validación de serializers, bug fixes y cross-format.
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
            Assert.Equal((ulong)2, bin.Length);
            Assert.Equal(0xAA, bin[address]);
            Assert.Equal(0xBB, bin[address + 1]);
        }

        [Fact]
        public void TestAddressAt4GBBoundary()
        {
            var bin = new BinFile();
            ulong address = 0xFFFFFFFF; // 4GB - 1
            bin.Add(new byte[] { 0xDE, 0xAD }, address);

            Assert.Equal(address, bin.MinimumAddress);
            Assert.Equal(address + 2, bin.MaximumAddress);
            byte[] data = bin.GetRange(address, address + 2);
            Assert.Equal(new byte[] { 0xDE, 0xAD }, data);
        }

        // ===== 33.2: Underflow in address operations =====

        [Fact]
        public void TestExcludeAtAddressZero()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02, 0x03, 0x04 }, 0);

            bin.Exclude(0, 2);
            Assert.Equal((ulong)2, bin.MinimumAddress);
            Assert.Equal((ulong)4, bin.MaximumAddress);
            Assert.Equal(new byte[] { 0x03, 0x04 }, bin.GetRange(2, 4));
        }

        [Fact]
        public void TestCropAtAddressZero()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02, 0x03, 0x04 }, 0);

            bin.Crop(0, 2);
            Assert.Equal((ulong)0, bin.MinimumAddress);
            Assert.Equal((ulong)2, bin.MaximumAddress);
            Assert.Equal(new byte[] { 0x01, 0x02 }, bin.GetRange(0, 2));
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
            Assert.Equal(0x01, bin[0]);
            Assert.Equal(0x02, bin[1]);
            Assert.Equal(0x03, bin[2]);
        }

        // ===== 33.4: Reference vs copy semantics in Exclude =====

        [Fact]
        public void TestExcludeDoesNotCorruptAdjacentData()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 }, 0);

            bin.Exclude(2, 4);

            Assert.Equal(2, bin.Segments.Count);
            Assert.Equal((ulong)4, bin.Length);
            Assert.Equal((ulong)0, bin.MinimumAddress);
            Assert.Equal((ulong)6, bin.MaximumAddress);
            // Left segment: [0x01, 0x02]
            Assert.Equal(new byte[] { 0x01, 0x02 }, bin.GetRange(0, 2));
            // Right segment: [0x05, 0x06]
            Assert.Equal(new byte[] { 0x05, 0x06 }, bin.GetRange(4, 6));
        }

        // ===== 33.5: Array mutation after Add =====

        [Fact]
        public void TestGetRangeReturnsCopy()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02, 0x03 }, 0);

            byte[] range = bin.GetRange(0, 3);
            range[0] = 0xFF;

            // BinFile should be unaffected
            Assert.Equal(0x01, bin[0]);
        }

        [Fact]
        public void TestAsBinaryReturnsCopy()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02, 0x03 }, 0);

            byte[] binary = bin.AsBinary();
            binary[0] = 0xFF;

            // BinFile should be unaffected
            Assert.Equal(0x01, bin[0]);
        }

        // ===== 33.6: 64-bit addresses (0xFFFFFFFFFFFFFFFF) =====

        [Fact]
        public void TestMaxUlongAddressOverflows()
        {
            var bin = new BinFile();
            ulong maxAddr = 0xFFFFFFFFFFFFFFFF;

            // Adding at the absolute max address causes maximumAddress overflow (wraps to 0)
            // This is expected to throw because maximumAddress <= minimumAddress
            Assert.ThrowsAny<ArgumentException>(() => bin.Add(new byte[] { 0x42 }, maxAddr));
        }

        [Fact]
        public void TestNearMaxUlongAddress()
        {
            var bin = new BinFile();
            ulong addr = 0xFFFFFFFFFFFFFFFE; // Max - 1
            bin.Add(new byte[] { 0x42 }, addr);

            Assert.Equal(addr, bin.MinimumAddress);
            Assert.Equal(0xFFFFFFFFFFFFFFFF, bin.MaximumAddress); // addr + 1 = max ulong
            Assert.Equal((ulong)1, bin.Length);
            Assert.Equal(1, bin.Segments.Count);
            Assert.Equal(0x42, bin[addr]);
            Assert.Equal(new byte[] { 0x42 }, bin.GetRange(addr, addr + 1));
        }

        [Fact]
        public void TestHighAddressGetRange()
        {
            var bin = new BinFile();
            ulong addr = 0xFFFFFFFFFFFFFFF0;
            byte[] data = { 0x01, 0x02, 0x03, 0x04 };
            bin.Add(data, addr);

            Assert.Equal(1, bin.Segments.Count);
            Assert.Equal(addr, bin.MinimumAddress);
            Assert.Equal(addr + 4, bin.MaximumAddress);
            Assert.Equal((ulong)4, bin.Length);
            byte[] result = bin.GetRange(addr, addr + 4);
            Assert.Equal(data, result);
        }

        [Fact]
        public void TestHighAddressInfo()
        {
            var bin = new BinFile();
            ulong addr = 0xFFFFFFFFFFFFFFF0;
            bin.Add(new byte[] { 0xAA }, addr);

            Assert.Equal(1, bin.Segments.Count);
            Assert.Equal((ulong)1, bin.Length);
            string info = bin.Info();
            Assert.Contains("fffffffffffffff0", info);
            Assert.Contains("fffffffffffffff1", info); // max address
        }

        // ===== 33.8: Null reference in optional parameters =====

        [Fact]
        public void TestAddNullByteArrayIsNoOp()
        {
            var bin = new BinFile();
            bin.Add((byte[])null, 0);

            Assert.Equal((ulong)0, bin.Length);
        }

        [Fact]
        public void TestAddEmptyByteArrayIsNoOp()
        {
            var bin = new BinFile();
            bin.Add(new byte[0], 0);

            Assert.Equal((ulong)0, bin.Length);
        }

        [Fact]
        public void TestAddNullStringThrows()
        {
            var bin = new BinFile();
            Assert.Throws<ArgumentNullException>(() => bin.Add((string)null));
        }

        [Fact]
        public void TestSetRangeNullThrows()
        {
            var bin = new BinFile();
            Assert.Throws<ArgumentNullException>(() => bin.SetRange(0, null));
        }

        // ===== 33.12: Absolute maximum address =====

        [Fact]
        public void TestSingleByteAtNearMaxAddress()
        {
            var bin = new BinFile();
            ulong addr = 0xFFFFFFFFFFFFFFFE; // Max - 1, so maximumAddress = Max (no overflow)
            bin.Add(new byte[] { 0x99 }, addr);

            Assert.Equal(addr, bin.MinimumAddress);
            Assert.Equal(0x99, bin[addr]);

            byte[] range = bin.GetRange(addr, addr + 1);
            Assert.Equal(new byte[] { 0x99 }, range);
        }

        [Fact]
        public void TestAbsoluteMaxAddressOverflows()
        {
            var bin = new BinFile();
            ulong addr = 0xFFFFFFFFFFFFFFFF;

            // This must throw because addr + 1 overflows to 0
            Assert.ThrowsAny<ArgumentException>(() => bin.Add(new byte[] { 0x99 }, addr));
        }

        // ===== 33.13: Segment crossing 4GB boundary =====

        [Fact]
        public void TestSegmentCrossing4GBBoundary()
        {
            var bin = new BinFile();
            ulong addr = 0xFFFFFFF0;
            byte[] data = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
            bin.Add(data, addr);

            Assert.Equal(1, bin.Segments.Count);
            Assert.Equal(addr, bin.MinimumAddress);
            Assert.Equal(addr + 32, bin.MaximumAddress);
            Assert.Equal((ulong)32, bin.Length);

            // Verify exact data integrity across the 4GB boundary
            byte[] retrieved = bin.GetRange(addr, addr + 32);
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

            Assert.Equal((ulong)0, bin.Length);
            Assert.Equal(0, bin.Segments.Count);

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

            // Step 3: Fill gaps with 0x00 → single segment [0..24)
            bin.Fill(0x00);
            Assert.Equal(1, bin.Segments.Count);
            Assert.Equal((ulong)0, bin.MinimumAddress);
            Assert.Equal((ulong)24, bin.MaximumAddress);

            // Step 4: Exclude [4..8) → two segments [0..4), [8..24)
            bin.Exclude(4, 8);
            Assert.Equal(2, bin.Segments.Count);
            Assert.Equal((ulong)0, bin.MinimumAddress);
            Assert.Equal((ulong)24, bin.MaximumAddress);

            // Step 5: Crop [2..22) → [2..4), [8..22)
            bin.Crop(2, 22);
            Assert.Equal(2, bin.Segments.Count);
            Assert.Equal((ulong)2, bin.MinimumAddress);
            Assert.Equal((ulong)22, bin.MaximumAddress);
            Assert.Equal(new byte[] { 0x03, 0x04 }, bin.GetRange(2, 4));
            Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xA0, 0xA1 }, bin.GetRange(8, 22));

            // Step 6: Overwrite at 10 → bytes at 10,11 become BB,CC
            bin.Add(new byte[] { 0xBB, 0xCC }, 10, overwrite: true);
            Assert.Equal(0xBB, bin[10]);
            Assert.Equal(0xCC, bin[11]);

            // Step 7: Fill gap [4..8) with 0xDD → single segment [2..22)
            bin.Fill(0xDD);
            Assert.Equal(1, bin.Segments.Count);
            Assert.Equal((ulong)2, bin.MinimumAddress);
            Assert.Equal((ulong)22, bin.MaximumAddress);
            Assert.Equal((ulong)20, bin.Length);

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
            Assert.Equal((ulong)0, bin.MinimumAddress);
            Assert.Equal((ulong)9991, bin.MaximumAddress); // last segment at 9990, 1 byte → 9991
            Assert.Equal((ulong)1000, bin.Length); // 1000 segments × 1 byte each

            // Verify first, middle, and last segments have correct data
            Assert.Equal((byte)0x00, bin[0]);         // addr 0 → 0x00
            Assert.Equal((byte)0x88, bin[5000]);      // addr 5000 → 5000 & 0xFF = 0x88
            Assert.Equal((byte)0x06, bin[9990]);       // addr 9990 → 9990 & 0xFF = 0x06

            // Verify segment boundaries
            Assert.Equal((ulong)0, bin.Segments[0].MinimumAddress);
            Assert.Equal((ulong)1, bin.Segments[0].MaximumAddress);
            Assert.Equal((ulong)9990, bin.Segments[999].MinimumAddress);
            Assert.Equal((ulong)9991, bin.Segments[999].MaximumAddress);

            // Binary output should be 9991 bytes (min=0 to max=9991, gaps filled with 0xFF)
            byte[] binary = bin.AsBinary();
            Assert.Equal(9991, binary.Length);
            Assert.Equal(0x00, binary[0]);     // data at addr 0
            Assert.Equal(0xFF, binary[1]);     // gap
            Assert.Equal(0xFF, binary[9]);     // gap
            Assert.Equal(0x0A, binary[10]);    // data at addr 10 → 10 & 0xFF = 0x0A
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
            Assert.Equal(1, bin.Segments.Count);
            Assert.Equal((ulong)0, bin.MinimumAddress);
            Assert.Equal((ulong)15, bin.MaximumAddress);
            Assert.Equal((ulong)15, bin.Length);

            // Verify exact final state
            byte[] expected = {
                0xAA, 0xAA, 0xAA, 0xAA, 0xAA, // [0..5) from A
                0xBB, 0xBB, 0xBB,               // [5..8) from B
                0xCC, 0xCC, 0xCC, 0xCC, 0xCC,   // [8..13) from C
                0xBB, 0xBB                       // [13..15) from B
            };
            Assert.Equal(expected, bin.AsBinary());
        }

        // ===== 33.18: GetRange/SetRange en gaps =====

        [Fact]
        public void TestGetRangeAcrossGap()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02 }, 0);
            bin.Add(new byte[] { 0x03, 0x04 }, 5);

            // GetRange across the gap (addresses 0-6)
            byte[] range = bin.GetRange(0, 7);

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
        public void TestSetRangeInGapCreatesSegment()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02 }, 0);
            bin.Add(new byte[] { 0x05, 0x06 }, 10);

            // SetRange in the gap creates a third segment
            bin.SetRange(5, new byte[] { 0xAA, 0xBB });

            Assert.Equal(3, bin.Segments.Count);
            Assert.Equal((ulong)0, bin.MinimumAddress);
            Assert.Equal((ulong)12, bin.MaximumAddress);
            Assert.Equal(0x01, bin[0]);
            Assert.Equal(0x02, bin[1]);
            Assert.Equal(0xAA, bin[5]);
            Assert.Equal(0xBB, bin[6]);
            Assert.Equal(0x05, bin[10]);
            Assert.Equal(0x06, bin[11]);
        }

        // ===== 33.19: Crop to non-existent range =====

        [Fact]
        public void TestCropToNonExistentRange()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02, 0x03 }, 100);

            // Crop to a range completely outside the data
            bin.Crop(0, 10);

            Assert.Equal((ulong)0, bin.Length);
            Assert.Equal(0, bin.Segments.Count);
            Assert.Equal(new byte[0], bin.AsBinary());
        }

        [Fact]
        public void TestCropToPartiallyOverlappingRange()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02, 0x03, 0x04 }, 100);

            // Crop to range that partially overlaps
            bin.Crop(102, 200);

            Assert.Equal(1, bin.Segments.Count);
            Assert.Equal((ulong)2, bin.Length);
            Assert.Equal((ulong)102, bin.MinimumAddress);
            Assert.Equal((ulong)104, bin.MaximumAddress);
            Assert.Equal(new byte[] { 0x03, 0x04 }, bin.GetRange(102, 104));
            Assert.Equal(new byte[] { 0x03, 0x04 }, bin.AsBinary());
        }

        // ===== 33.20: All methods on empty BinFile =====

        [Fact]
        public void TestAllMethodsOnEmptyBinFile()
        {
            var bin = new BinFile();

            Assert.Equal((ulong)0, bin.Length);
            Assert.Equal(0, bin.Segments.Count);

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
            Assert.Equal((ulong)0, bin.Length);
            Assert.Equal(0, bin.Segments.Count);

            bin.Exclude(0, 100);
            Assert.Equal((ulong)0, bin.Length);
            Assert.Equal(0, bin.Segments.Count);

            bin.Crop(0, 100);
            Assert.Equal((ulong)0, bin.Length);
            Assert.Equal(0, bin.Segments.Count);
        }

        // ===== 33.21: Add empty string and empty byte[] =====

        [Fact]
        public void TestAddEmptyStringThrowsOrIsNoOp()
        {
            var bin = new BinFile();

            // Empty string should throw UnsupportedFileFormatException
            // because the format detector can't detect a format from empty input
            Assert.ThrowsAny<Exception>(() => bin.Add(""));
        }

        [Fact]
        public void TestAddEmptyByteArray()
        {
            var bin = new BinFile();
            bin.Add(new byte[0], 0);
            Assert.Equal((ulong)0, bin.Length);
        }

        [Fact]
        public void TestAddNullByteArray()
        {
            var bin = new BinFile();
            bin.Add((byte[])null, 0);
            Assert.Equal((ulong)0, bin.Length);
        }

        // ===== 33.22: Extreme word size =====

        [Fact]
        public void TestWordSize64()
        {
            var bin = new BinFile(wordSizeBits: 64);

            // Add 16 bytes = 2 words of 8 bytes each
            byte[] data = { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
                            0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18 };
            bin.Add(data, 0);

            Assert.Equal((ulong)2, bin.Length); // 2 words
            Assert.Equal((ulong)0, bin.MinimumAddress);
            Assert.Equal((ulong)2, bin.MaximumAddress);

            byte[] retrieved = bin.GetRange(0, 2);
            Assert.Equal(data, retrieved);
        }

        [Fact]
        public void TestWordSize64Indexer()
        {
            var bin = new BinFile(wordSizeBits: 64);

            byte[] data = { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x11, 0x22 };
            bin.Add(data, 0);

            // Indexer accesses by word address, returns first byte of the word
            Assert.Equal(0xAA, bin[0]);
        }

        [Fact]
        public void TestInvalidWordSizeThrows()
        {
            Assert.Throws<ArgumentException>(() => new BinFile(wordSizeBits: 24));
            Assert.Throws<ArgumentException>(() => new BinFile(wordSizeBits: 0));
            Assert.Throws<ArgumentException>(() => new BinFile(wordSizeBits: 40));
            Assert.Throws<ArgumentException>(() => new BinFile(wordSizeBits: 128));
        }

        // ===== 34.7: Address range validation in serializers =====

        [Fact]
        public void TestSrecAddressAbove32BitWith32BitAddressLength()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01 }, 0x1_0000_0000UL);
            var ex = Assert.Throws<BincopyException>(() => bin.AsSrec(addressLengthBits: 32));
            Assert.Contains("SREC", ex.Message);
        }

        [Fact]
        public void TestSrecAddressAbove16BitWith16BitAddressLength()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01 }, 0x1_0000UL);
            var ex = Assert.Throws<BincopyException>(() => bin.AsSrec(addressLengthBits: 16));
            Assert.Contains("SREC", ex.Message);
        }

        [Fact]
        public void TestSrecExecutionStartAddressAboveRange()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01 }, 0x0000UL);
            bin.ExecutionStartAddress = 0x1_0000_0000UL;
            var ex = Assert.Throws<BincopyException>(() => bin.AsSrec(addressLengthBits: 32));
            Assert.Contains("SREC", ex.Message);
        }

        [Fact]
        public void TestIhexAddressAbove32Bit()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01 }, 0x1_0000_0000UL);
            var ex = Assert.Throws<BincopyException>(() => bin.AsIhex(addressLengthBits: 32));
            Assert.Contains("4 GB", ex.Message);
        }

        [Fact]
        public void TestTiTxtAddressAbove32Bit()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01 }, 0x1_0000_0000UL);
            var ex = Assert.Throws<BincopyException>(() => bin.AsTiTxt());
            Assert.Contains("TI-TXT", ex.Message);
        }

        [Fact]
        public void TestVmemAddressAbove32Bit()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01 }, 0x1_0000_0000UL);
            var ex = Assert.Throws<BincopyException>(() => bin.AsVerilogVmem());
            Assert.Contains("VMEM", ex.Message);
        }

        [Fact]
        public void TestOperatorPlusDifferentWordSize()
        {
            var a = new BinFile(wordSizeBits: 8);
            a.Add(new byte[] { 0x01 }, 0);
            var b = new BinFile(wordSizeBits: 16);
            b.Add(new byte[] { 0x02, 0x03 }, 0);

            Assert.Throws<ArgumentException>(() => { var _ = a + b; });
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
        public void TestHeaderSetterWrongTypeWithEncoding()
        {
            // Default encoding is utf-8, so Header expects a string
            var bin = new BinFile();
            Assert.Throws<ArgumentException>(() => bin.Header = new byte[] { 0x01, 0x02 });
        }

        [Fact]
        public void TestCropBadRange()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01, 0x02, 0x03 }, 0);

            Assert.Throws<ArgumentException>(() => bin.Crop(5, 2));
        }

        [Fact]
        public void TestAsArrayWordSize16()
        {
            var bin = new BinFile(wordSizeBits: 16);
            // 4 bytes = 2 words of 16 bits
            bin.Add(new byte[] { 0xAB, 0xCD, 0x12, 0x34 }, 0);

            string result = bin.AsArray();
            // Words should be assembled as big-endian: 0xABCD, 0x1234
            Assert.Equal("0xabcd, 0x1234", result);
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
        public void TestFillBadPaddingSize()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01 }, 0);
            bin.Add(new byte[] { 0x02 }, 10);

            Assert.Throws<ArgumentException>(() => bin.Fill(new byte[] { 0xFF, 0xFF }));
        }

        [Fact]
        public void TestAsBinaryBadPaddingArraySize()
        {
            var bin = new BinFile();
            bin.Add(new byte[] { 0x01 }, 0);

            Assert.Throws<ArgumentException>(() => bin.AsBinary(null, null, new byte[] { 0xFF, 0xFF }));
        }

        [Fact]
        public void TestSegmentsAddMismatchedWordSize()
        {
            var segments = new Segments(8);
            var segment = new Segment(0, 2, new byte[] { 0x01, 0x02 }, 16);

            Assert.Throws<ArgumentException>(() => segments.Add(segment));
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

        [Fact]
        public void TestBinarySerializerSerializeStringThrows()
        {
            var serializer = new BinarySerializer();
            var segments = new Segments(8);
            var options = new SerializerOptions();

            Assert.Throws<NotSupportedException>(() => serializer.Serialize(segments, options));
        }
    }
}

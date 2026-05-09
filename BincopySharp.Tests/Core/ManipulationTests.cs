using System;
using System.IO;
using System.Text;
using System.Linq;
using Xunit;

namespace BincopySharp.Tests
{
    public class ManipulationTests
    {
        private readonly string _testFilesPath;

        public ManipulationTests()
        {
            _testFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestFiles");
        }

        private string GetTestFilePath(string filename)
        {
            return Path.Combine(_testFilesPath, filename);
        }

        [Fact]
        public void Exclude_VariousRanges_ProducesCorrectSegments()
        {
            // Test 1: Exclude 2-4
            var binFile = new BinFile();
            string inS19Content = File.ReadAllText(GetTestFilePath("in.s19"));
            binFile.AddSrec(inS19Content);
            binFile.Exclude(2, 4);

            string expected1 = File.ReadAllText(GetTestFilePath("in_exclude_2_4.s19"));
            Assert.Equal(expected1, binFile.AsSrec(32, Formats.SrecVariant.S19));

            // Test 2: Exclude 3-1024
            binFile = new BinFile();
            binFile.AddSrec(inS19Content);
            binFile.Exclude(3, 1024);

            string expected2 = File.ReadAllText(GetTestFilePath("in_exclude_3_1024.s19"));
            Assert.Equal(expected2, binFile.AsSrec(32, Formats.SrecVariant.S19));

            // Test 3: Exclude 0-9
            binFile = new BinFile();
            binFile.AddSrec(inS19Content);
            binFile.Exclude(0, 9);

            string expected3 = File.ReadAllText(GetTestFilePath("in_exclude_0_9.s19"));
            Assert.Equal(expected3, binFile.AsSrec(32, Formats.SrecVariant.S19));

            // Test 4: Exclude from empty_main
            binFile = new BinFile();
            string emptyMainS19 = File.ReadAllText(GetTestFilePath("empty_main.s19"));
            binFile.AddSrec(emptyMainS19);
            binFile.Exclude(0x400240, 0x400600);

            byte[] expected4 = File.ReadAllBytes(GetTestFilePath("empty_main_mod.bin"));
            Assert.Equal(expected4, binFile.AsBinary(padding: 0x00));

            // Test 5: Exclude various parts of segments
            binFile = new BinFile();
            binFile.Add(Encoding.ASCII.GetBytes("111111"), address: 8);
            binFile.Add(Encoding.ASCII.GetBytes("222222"), address: 16);
            binFile.Add(Encoding.ASCII.GetBytes("333333"), address: 24);

            binFile.Exclude(7, 8);
            binFile.Exclude(15, 16);
            binFile.Exclude(23, 24);

            byte[] expected5 =
            [
                (byte)'1', (byte)'1', (byte)'1', (byte)'1', (byte)'1', (byte)'1',  // "111111"
                0xff, 0xff,  // 2 bytes gap
                (byte)'2', (byte)'2', (byte)'2', (byte)'2', (byte)'2', (byte)'2',  // "222222"
                0xff, 0xff,  // 2 bytes gap
                (byte)'3', (byte)'3', (byte)'3', (byte)'3', (byte)'3', (byte)'3'   // "333333"
            ];
            Assert.Equal(expected5, binFile.AsBinary());
            Assert.Equal(3, binFile.Segments.Count);

            binFile.Exclude(20, 24);
            Assert.Equal(
                Encoding.ASCII.GetBytes("111111")
                    .Concat(new byte[] { 0xff, 0xff })
                    .Concat(Encoding.ASCII.GetBytes("2222"))
                    .Concat(Enumerable.Repeat((byte)0xff, 4))
                    .Concat(Encoding.ASCII.GetBytes("333333"))
                    .ToArray(),
                binFile.AsBinary());
            Assert.Equal(3, binFile.Segments.Count);

            binFile.Exclude(12, 24);
            Assert.Equal(
                Encoding.ASCII.GetBytes("1111")
                    .Concat(Enumerable.Repeat((byte)0xff, 12))
                    .Concat(Encoding.ASCII.GetBytes("333333"))
                    .ToArray(),
                binFile.AsBinary());
            Assert.Equal(2, binFile.Segments.Count);

            binFile.Exclude(11, 25);
            Assert.Equal(
                Encoding.ASCII.GetBytes("111")
                    .Concat(Enumerable.Repeat((byte)0xff, 14))
                    .Concat(Encoding.ASCII.GetBytes("33333"))
                    .ToArray(),
                binFile.AsBinary());
            Assert.Equal(2, binFile.Segments.Count);

            binFile.Exclude(11, 26);
            Assert.Equal(
                Encoding.ASCII.GetBytes("111")
                    .Concat(Enumerable.Repeat((byte)0xff, 15))
                    .Concat(Encoding.ASCII.GetBytes("3333"))
                    .ToArray(),
                binFile.AsBinary());
            Assert.Equal(2, binFile.Segments.Count);

            binFile.Exclude(27, 29);
            Assert.Equal(
                Encoding.ASCII.GetBytes("111")
                    .Concat(Enumerable.Repeat((byte)0xff, 15))
                    .Concat(Encoding.ASCII.GetBytes("3"))
                    .Concat(new byte[] { 0xff, 0xff })
                    .Concat(Encoding.ASCII.GetBytes("3"))
                    .ToArray(),
                binFile.AsBinary());
            Assert.Equal(3, binFile.Segments.Count);

            // Exclude negative address range and empty address range
            binFile = new BinFile();
            binFile.Add(Encoding.ASCII.GetBytes("111111"));

            Assert.Throws<ArgumentException>(() =>
            {
                binFile.Exclude(4, 2);
            });

            binFile.Exclude(2, 2);
            Assert.Equal(Encoding.ASCII.GetBytes("111111"), binFile.AsBinary());
        }

        [Fact]
        public void Crop_KeepsOnlyRequestedRange()
        {
            // Test 1: Crop 2-4
            var binFile = new BinFile();
            binFile.AddSrecFile(GetTestFilePath("in.s19"));
            binFile.Crop(2, 4);

            string expected = File.ReadAllText(GetTestFilePath("in_crop_2_4.s19"));
            Assert.Equal(expected, binFile.AsSrec(32, Formats.SrecVariant.S19));

            // Test 2: Crop then exclude should result in empty
            binFile.Exclude(2, 4);
            Assert.Empty(binFile.AsBinary());
        }

        [Fact]
        public void Fill_WithGappedData_MergesIntoContiguousSegment()
        {
            var binFile = new BinFile();

            // Fill empty file
            binFile.Fill();
            Assert.Empty(binFile.AsBinary());

            // Add some data and fill again
            binFile.Add([0x01, 0x02, 0x03, 0x04], address: 0);
            binFile.Add([0x01, 0x02, 0x03, 0x04], address: 8);
            binFile.Fill();
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0xff, 0xff, 0xff, 0xff, 0x01, 0x02, 0x03, 0x04 }, binFile.AsBinary());
        }

        [Fact]
        public void Fill_WithMaxBytesLimit_OnlyFillsSmallGaps()
        {
            var binFile = new BinFile();
            binFile.Add([0x01], address: 0);
            binFile.Add([0x02], address: 2);
            binFile.Add([0x03], address: 5);
            binFile.Add([0x04], address: 9);
            binFile.Fill(0xaa, maxBytes: 2);

            Assert.Equal(2, binFile.Segments.Count);
            Assert.Equal(0UL, binFile.Segments[0].MinimumAddress);
            Assert.Equal(new byte[] { 0x01, 0xaa, 0x02, 0xaa, 0xaa, 0x03 }, binFile.Segments[0].Data.ToArray());
            Assert.Equal(9UL, binFile.Segments[1].MinimumAddress);
            Assert.Equal(new byte[] { 0x04 }, binFile.Segments[1].Data.ToArray());
        }

        [Fact]
        public void Add_WithOverwrite_ReplacesExistingData()
        {
            var binFile = new BinFile();

            // Overwrite in empty file.
            binFile.Add(Encoding.ASCII.GetBytes("1234"), address: 512, overwrite: true);
            Assert.Equal(Encoding.ASCII.GetBytes("1234"), binFile.AsBinary(minimumAddress: 512));

            // Test setting data with multiple existing segments.
            binFile.Add(Encoding.ASCII.GetBytes("123456"), address: 1024);
            binFile.Add(Encoding.ASCII.GetBytes("99"), address: 1026, overwrite: true);
            Assert.Equal(
                Encoding.ASCII.GetBytes("1234")
                    .Concat(Enumerable.Repeat((byte)0xff, 508))
                    .Concat(Encoding.ASCII.GetBytes("129956"))
                    .ToArray(),
                binFile.AsBinary(minimumAddress: 512));

            // Test setting data crossing the original segment limits.
            binFile.Add(Encoding.ASCII.GetBytes("abc"), address: 1022, overwrite: true);
            binFile.Add(Encoding.ASCII.GetBytes("def"), address: 1029, overwrite: true);
            Assert.Equal(
                Encoding.ASCII.GetBytes("1234")
                    .Concat(Enumerable.Repeat((byte)0xff, 506))
                    .Concat(Encoding.ASCII.GetBytes("abc2995def"))
                    .ToArray(),
                binFile.AsBinary(minimumAddress: 512));

            // Overwrite a segment and write outside it.
            binFile.Add(Encoding.ASCII.GetBytes("111111111111"), address: 1021, overwrite: true);
            Assert.Equal(
                Encoding.ASCII.GetBytes("1234")
                    .Concat(Enumerable.Repeat((byte)0xff, 505))
                    .Concat(Encoding.ASCII.GetBytes("111111111111"))
                    .ToArray(),
                binFile.AsBinary(minimumAddress: 512));

            // Overwrite multiple segments (all segments in this test).
            byte[] ones = Enumerable.Repeat((byte)'1', 1024).ToArray();
            binFile.Add(ones, address: 256, overwrite: true);
            Assert.Equal(ones, binFile.AsBinary(minimumAddress: 256));
        }

        [Fact]
        public void Indexer_GetAndSet_ReadsAndWritesByteAtAddress()
        {
            var binFile = new BinFile();

            binFile.Add([0x01, 0x02, 0x03, 0x04], address: 1);

            // Get all data
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, binFile.AsBinary());

            // Address 0 is out of range
            Assert.Throws<IndexOutOfRangeException>(() => { var _ = binFile[0]; });

            Assert.Equal(1UL, binFile[1]);
            Assert.Equal(2UL, binFile[2]);
            Assert.Equal(3UL, binFile[3]);
            Assert.Equal(4UL, binFile[4]);

            // Address 5 is out of range
            Assert.Throws<IndexOutOfRangeException>(() => { var _ = binFile[5]; });

            // Range [3, 5)
            Assert.Equal(new byte[] { 0x03, 0x04 }, binFile.AsBinary(minimumAddress: 3, maximumAddress: 5));
            // Range [3, 6) - clipped to actual data
            Assert.Equal(new byte[] { 0x03, 0x04 }, binFile.AsBinary(minimumAddress: 3, maximumAddress: 6));

            // Set range [1, 3)
            binFile.Add([0x05, 0x06], 1, overwrite: true);
            Assert.Equal(new byte[] { 0x05, 0x06, 0x03, 0x04 }, binFile.AsBinary());

            // Set from address 3 onwards
            binFile.Add([0x07, 0x08, 0x09], 3, overwrite: true);
            Assert.Equal(new byte[] { 0x05, 0x06, 0x07, 0x08, 0x09 }, binFile.AsBinary());

            // Set range [3, 5)
            binFile.Add([0x0a, 0x0b], 3, overwrite: true);
            Assert.Equal(new byte[] { 0x05, 0x06, 0x0a, 0x0b, 0x09 }, binFile.AsBinary());

            // Set single byte at address 2
            binFile.Add([0x0c], 2, overwrite: true);
            Assert.Equal(new byte[] { 0x05, 0x0c, 0x0a, 0x0b, 0x09 }, binFile.AsBinary());

            // Set all data from minimum address
            binFile.Add([0x01, 0x02, 0x03, 0x04, 0x05], binFile.MinimumAddress, overwrite: true);
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 }, binFile.AsBinary());

            // Set single byte at address 0 (extends data)
            binFile[0] = 0;
            Assert.Equal(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 }, binFile.AsBinary());

            // Set single byte at address 7 (creates gap)
            binFile[7] = 7;
            Assert.Equal(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0xff, 0x07 }, binFile.AsBinary());
            Assert.Equal(255UL, binFile[6]);
            Assert.Equal(new byte[] { 0xff }, binFile.AsBinary(minimumAddress: 6, maximumAddress: 7));
            Assert.Equal(new byte[] { 0xff, 0x07 }, binFile.AsBinary(minimumAddress: 6, maximumAddress: 8));
            Assert.Equal(new byte[] { 0x05, 0xff, 0x07 }, binFile.AsBinary(minimumAddress: 5, maximumAddress: 8));

            // Add data at high address to test get performance.
            binFile[0x10000000] = 0x12;
            Assert.Equal(new byte[] { 0xff, 0x12 },
                binFile.AsBinary(minimumAddress: 0x10000000 - 1));
        }

        [Fact]
        public void OperatorPlusEquals_TwoFiles_MergesContent()
        {
            var binFile = new BinFile();
            var binFile12 = new BinFile();
            binFile.Add([0x00]);
            binFile12.Add([0x01], address: 1);

            // Save original data for aliasing verification
            byte[] originalBinFileData = binFile.AsBinary();
            byte[] originalBinFile12Data = binFile12.AsBinary();

            // Use += operator to add files
            binFile += binFile12;

            Assert.Equal(new byte[] { 0x00, 0x01 }, binFile.AsBinary());

            // Verify original binFile12 is not mutated after operator+
            Assert.Equal(originalBinFile12Data, binFile12.AsBinary());

            // Segment data is read-only (Data is ReadOnlySpan<byte>),
            // so aliasing mutation is not possible through the public API.
        }

        [Fact]
        public void Exclude_OutsideOrAtBoundary_DoesNotAffectData()
        {
            var binFile = new BinFile();
            binFile.Add(Encoding.ASCII.GetBytes("1234"), address: 10);
            binFile.Exclude(8, 10);
            binFile.Exclude(14, 15);

            Assert.Equal(Encoding.ASCII.GetBytes("1234"), binFile.AsBinary());
            Assert.Single(binFile.Segments);

            binFile.Exclude(8, 11);
            binFile.Exclude(13, 15);

            Assert.Equal(Encoding.ASCII.GetBytes("23"), binFile.AsBinary());
            Assert.Single(binFile.Segments);
        }

        [Fact]
        public void Add_DataIsCopiedOnAdd_MutatingOriginalArrayHasNoEffect()
        {
            var bin = new BinFile();
            byte[] original = [0x01, 0x02, 0x03];
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

        [Fact]
        public void Exclude_MiddleOfSegment_LeavesAdjacentDataIntact()
        {
            var bin = new BinFile();
            bin.Add([0x01, 0x02, 0x03, 0x04, 0x05, 0x06], 0);

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

        [Fact]
        public void Exclude_EntireRange_ResultsInEmptyBinFile()
        {
            var bin = new BinFile();
            bin.Add([0x01, 0x02, 0x03], 10);

            bin.Exclude(10, 13);

            Assert.Equal(0, bin.Length);
            Assert.Empty(bin.Segments);

            // All export methods should produce valid empty output
            Assert.Empty(bin.AsBinary());
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

        [Fact]
        public void ChainedOperations_AddFillExcludeCropOverwrite_ProducesCorrectState()
        {
            var bin = new BinFile();

            // Step 1-2: Two segments
            bin.Add([0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08], 0);
            bin.Add([0xA0, 0xA1, 0xA2, 0xA3], 20);

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
            bin.Add([0xBB, 0xCC], 10, overwrite: true);
            Assert.Equal(0xBBUL, bin[10]);
            Assert.Equal(0xCCUL, bin[11]);

            // Step 7: Fill gap [4..8) with 0xDD â†’ single segment [2..22)
            bin.Fill(0xDD);
            Assert.Single(bin.Segments);
            Assert.Equal(2UL, bin.MinimumAddress);
            Assert.Equal(22UL, bin.MaximumAddress);
            Assert.Equal(20, bin.Length);

            // Verify exact final state byte by byte
            byte[] expected = [
                0x03, 0x04,                                     // [2..4) original data
                0xDD, 0xDD, 0xDD, 0xDD,                        // [4..8) filled gap
                0x00, 0x00,                                     // [8..10) from Fill step 3
                0xBB, 0xCC,                                     // [10..12) overwritten
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // [12..20) from Fill step 3
                0xA0, 0xA1                                      // [20..22) original data
            ];
            Assert.Equal(expected, bin.AsBinary());
        }

        [Fact]
        public void Add_CascadingOverlappingWrites_LastWriteWins()
        {
            var bin = new BinFile();

            // Range A: addresses 0-9
            bin.Add([0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA], 0);

            // Range B: addresses 5-14 (overlaps A)
            bin.Add([0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB], 5, overwrite: true);

            // Range C: addresses 8-12 (overlaps B)
            bin.Add([0xCC, 0xCC, 0xCC, 0xCC, 0xCC], 8, overwrite: true);

            // Should be a single contiguous segment [0..15)
            Assert.Single(bin.Segments);
            Assert.Equal(0UL, bin.MinimumAddress);
            Assert.Equal(15UL, bin.MaximumAddress);
            Assert.Equal(15, bin.Length);

            // Verify exact final state
            byte[] expected = [
                0xAA, 0xAA, 0xAA, 0xAA, 0xAA, // [0..5) from A
                0xBB, 0xBB, 0xBB,               // [5..8) from B
                0xCC, 0xCC, 0xCC, 0xCC, 0xCC,   // [8..13) from C
                0xBB, 0xBB                       // [13..15) from B
            ];
            Assert.Equal(expected, bin.AsBinary());
        }

        [Fact]
        public void AsBinary_RangeSpanningGap_FillsGapWithPaddingByte()
        {
            var bin = new BinFile();
            bin.Add([0x01, 0x02], 0);
            bin.Add([0x03, 0x04], 5);

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
        public void Add_WithOverwriteInGap_CreatesThirdSegment()
        {
            var bin = new BinFile();
            bin.Add([0x01, 0x02], 0);
            bin.Add([0x05, 0x06], 10);

            // Add with overwrite in the gap creates a third segment
            bin.Add([0xAA, 0xBB], 5, overwrite: true);

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

        [Fact]
        public void Crop_ToRangeOutsideData_ResultsInEmptyBinFile()
        {
            var bin = new BinFile();
            bin.Add([0x01, 0x02, 0x03], 100);

            // Crop to a range completely outside the data
            bin.Crop(0, 10);

            Assert.Equal(0, bin.Length);
            Assert.Empty(bin.Segments);
            Assert.Empty(bin.AsBinary());
        }

        [Fact]
        public void Crop_ToPartiallyOverlappingRange_KeepsIntersection()
        {
            var bin = new BinFile();
            bin.Add([0x01, 0x02, 0x03, 0x04], 100);

            // Crop to range that partially overlaps
            bin.Crop(102, 200);

            Assert.Single(bin.Segments);
            Assert.Equal(2, bin.Length);
            Assert.Equal(102UL,bin.MinimumAddress);
            Assert.Equal(104UL,bin.MaximumAddress);
            Assert.Equal(new byte[] { 0x03, 0x04 }, bin.AsBinary(102, 104));
            Assert.Equal(new byte[] { 0x03, 0x04 }, bin.AsBinary());
        }

        [Fact]
        public void Add_NullWithOverwrite_ThrowsArgumentNullException()
        {
            var bin = new BinFile();
            Assert.Throws<ArgumentNullException>(() => bin.Add(null, 0, overwrite: true));
        }

        [Fact]
        public void OperatorPlus_WithNullOperand_ThrowsArgumentNullException()
        {
            var a = new BinFile();
            a.Add([0x01], 0);

            Assert.Throws<ArgumentNullException>(() => { var _ = a + (BinFile)null; });
            Assert.Throws<ArgumentNullException>(() => { var _ = (BinFile)null + a; });
        }

        [Fact]
        public void Crop_EndBeforeStart_ThrowsArgumentException()
        {
            var bin = new BinFile();
            bin.Add([0x01, 0x02, 0x03], 0);

            Assert.Throws<ArgumentException>(() => bin.Crop(5, 2));
        }

        [Fact]
        public void OperatorPlus_DoesNotMutateOperands()
        {
            var a = new BinFile();
            a.Add([0x01, 0x02, 0x03, 0x04], address: 0x100);

            var b = new BinFile();
            b.Add([0x10, 0x20, 0x30, 0x40], address: 0x200);

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
    }
}

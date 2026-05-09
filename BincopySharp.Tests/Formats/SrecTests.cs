using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using BincopySharp;
using BincopySharp.Formats;
using Formats = BincopySharp.Formats;

namespace BincopySharp.Tests
{
    public class SrecTests
    {
        private readonly string _testFilesPath;

        public SrecTests()
        {
            _testFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestFiles");
        }

        private string GetTestFilePath(string filename)
        {
            return Path.Combine(_testFilesPath, filename);
        }

        [Fact]
        public void AddSrec_RoundTrip_PreservesContent()
        {
            // Test 1: Load SREC and convert back
            var binFile = new BinFile();
            string inS19Content = File.ReadAllText(GetTestFilePath("in.s19"));
            binFile.AddSrec(inS19Content);

            string result = binFile.AsSrec(28, Formats.SrecVariant.S19);
            Assert.Equal(inS19Content, result);

            // Test 2: Load SREC and convert to binary
            binFile = new BinFile();
            string emptyMainS19 = File.ReadAllText(GetTestFilePath("empty_main.s19"));
            binFile.AddSrec(emptyMainS19);

            byte[] emptyMainBin = File.ReadAllBytes(GetTestFilePath("empty_main.bin"));
            byte[] resultBin = binFile.AsBinary(padding: 0x00);
            Assert.Equal(emptyMainBin, resultBin);

            // Test 3: Add and overwrite data
            binFile = new BinFile();
            binFile.AddSrecFile(GetTestFilePath("empty_main_rearranged.s19"));
            binFile.AddSrecFile(GetTestFilePath("empty_main_rearranged.s19"), overwrite: true);

            resultBin = binFile.AsBinary(padding: 0x00);
            Assert.Equal(emptyMainBin, resultBin);

            // Test 4: Bad CRC should throw exception
            binFile = new BinFile();
            var ex = Assert.Throws<InvalidRecordException>(() =>
            {
                binFile.AddSrecFile(GetTestFilePath("bad_crc.s19"));
            });

            Assert.Equal("Expected crc '25' in record S2144002640000000002000000060000001800000022, but got '22'", ex.Message);
        }

        [Fact]
        public void AddSrec_InvalidInput_ThrowsExpectedException()
        {
            // Test: Invalid SREC records should throw exceptions
            var binFile = new BinFile();

            // Too short record
            Assert.Throws<BincopyException>(() =>
            {
                binFile.AddSrec("");
            });

            // Bad first character
            Assert.Throws<InvalidRecordException>(() =>
            {
                binFile.AddSrec("T0000011");
            });

            // Bad type (invalid character in type field)
            var exBadType = Assert.Throws<InvalidRecordException>(() =>
            {
                binFile.AddSrec("S.0200FF");
            });
            Assert.Equal("Expected record type 0..3 or 5..9, but got '.'", exBadType.Message);

            // Bad CRC
            var ex = Assert.Throws<InvalidRecordException>(() =>
            {
                binFile.AddSrec("S1020011");
            });
            Assert.Equal("Expected crc 'FD' in record S1020011, but got '11'", ex.Message);
        }

        [Fact]
        public void AsSrec_With65535DataRecords_EmitsRecord5()
        {
            var binFile = new BinFile();

            // Add 65535 bytes of zeros
            binFile.Add(new byte[65535]);
            string records = binFile.AsSrec(numberOfDataBytes: 1);

            int lineCount = records.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.Equal(65536, lineCount);
            Assert.Contains("S503FFFFFE", records);
        }

        [Fact]
        public void AsSrec_With65536DataRecords_EmitsRecord6()
        {
            var binFile = new BinFile();

            // Add 65536 bytes of zeros
            binFile.Add(new byte[65536]);
            string records = binFile.AsSrec(numberOfDataBytes: 1);

            int lineCount = records.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.Equal(65537, lineCount);
            Assert.Contains("S604010000FA", records);
        }

        [Fact]
        public void AsSrec_S28VariantWithExecutionStart_EmitsRecord8()
        {
            var binFile = new BinFile();

            binFile.Add([0x00]);
            binFile.ExecutionStartAddress = 0x123456;
            string records = binFile.AsSrec(variant: Formats.SrecVariant.S28);

            Assert.Equal("S20500000000FA\n" +
                        "S5030001FB\n" +
                        "S8041234565F\n", records);
        }

        [Fact]
        public void AddSrec_WithNonSortedSegments_MergesAndSortsOutput()
        {
            var binFile = new BinFile();

            string nonSorted = File.ReadAllText(GetTestFilePath("non_sorted_segments.s19"));
            binFile.AddSrec(nonSorted);

            string expected = File.ReadAllText(GetTestFilePath("non_sorted_segments_merged_and_sorted.s19"));
            Assert.Equal(expected, binFile.AsSrec());
        }

        [Fact]
        public void AddSrec_WithBlankLines_ParsesSuccessfully()
        {
            var binFile = new BinFile();

            string inBlankLinesS19 = File.ReadAllText(GetTestFilePath("in_blank_lines.s19"));
            binFile.AddSrec(inBlankLinesS19);

            string inS19 = File.ReadAllText(GetTestFilePath("in.s19"));
            Assert.Equal(inS19, binFile.AsSrec(28, Formats.SrecVariant.S19));
        }

        [Fact]
        public void AsSrec_S37VariantWithAddressAbove32Bit_Throws()
        {
            var bin = new BinFile();
            bin.Add([0x01], 0x1_0000_0000);
            var ex = Assert.Throws<BincopyException>(() => bin.AsSrec(variant: Formats.SrecVariant.S37));
            Assert.Contains("SREC", ex.Message);
        }

        [Fact]
        public void AsSrec_S19VariantWithAddressAbove16Bit_Throws()
        {
            var bin = new BinFile();
            bin.Add([0x01], 0x1_0000);
            var ex = Assert.Throws<BincopyException>(() => bin.AsSrec(variant: Formats.SrecVariant.S19));
            Assert.Contains("SREC", ex.Message);
        }

        [Fact]
        public void AsSrec_S37VariantWithExecutionStartAboveRange_Throws()
        {
            var bin = new BinFile();
            bin.Add([0x01], 0x0000);
            bin.ExecutionStartAddress = 0x1_0000_0000;
            var ex = Assert.Throws<BincopyException>(() => bin.AsSrec(variant: Formats.SrecVariant.S37));
            Assert.Contains("SREC", ex.Message);
        }

        [Fact]
        public void Add_ValidSrecHeaderThenGarbage_ThrowsInvalidRecordException()
        {
            var bin = new BinFile();
            // Valid S0 header record, then garbage
            string badSrec = "S0030000FC\nNOT_A_VALID_SREC_RECORD\n";
            Assert.Throws<InvalidRecordException>(() => bin.Add(badSrec));
        }
    }
}

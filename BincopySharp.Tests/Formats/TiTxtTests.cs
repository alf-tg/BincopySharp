using System;
using System.IO;
using Xunit;
using BincopySharp;

namespace BincopySharp.Tests
{
    public class TiTxtTests
    {
        private readonly string _testFilesPath;

        public TiTxtTests()
        {
            _testFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestFiles");
        }

        private string GetTestFilePath(string filename)
        {
            return Path.Combine(_testFilesPath, filename);
        }

        [Fact]
        public void AddTiTxt_RoundTrip_PreservesContent()
        {
            // Test 1: Load TI-TXT and convert back
            var binFile = new BinFile();
            string inTxtContent = File.ReadAllText(GetTestFilePath("in.s19.txt"));
            binFile.AddTiTxt(inTxtContent);

            string result = binFile.AsTiTxt();
            Assert.Equal(inTxtContent, result);

            // Test 2: Load TI-TXT and convert to binary
            binFile = new BinFile();
            string emptyMainTxt = File.ReadAllText(GetTestFilePath("empty_main.s19.txt"));
            binFile.AddTiTxt(emptyMainTxt);

            byte[] emptyMainBin = File.ReadAllBytes(GetTestFilePath("empty_main.bin"));
            byte[] resultBin = binFile.AsBinary(padding: 0x00);
            Assert.Equal(emptyMainBin, resultBin);

            // Test 3: Add and overwrite data
            binFile = new BinFile();
            binFile.AddTiTxtFile(GetTestFilePath("empty_main_rearranged.s19.txt"));
            binFile.AddTiTxtFile(GetTestFilePath("empty_main_rearranged.s19.txt"), overwrite: true);

            resultBin = binFile.AsBinary(padding: 0x00);
            Assert.Equal(emptyMainBin, resultBin);

            // Test 4: Empty file
            var empty = new BinFile();
            binFile = new BinFile();
            binFile.AddTiTxtFile(GetTestFilePath("empty.txt"));
            Assert.Equal(empty.AsTiTxt(), binFile.AsTiTxt());
        }

        [Fact]
        public void AddTiTxt_InvalidInput_ThrowsExpectedException()
        {
            var testCases = new[]
            {
                ("bad_ti_txt_address_value.txt", "Bad section address"),
                ("bad_ti_txt_bad_q.txt", "Bad file terminator"),
                ("bad_ti_txt_data_value.txt", "Bad data"),
                ("bad_ti_txt_record_short.txt", "Missing section address"),
                ("bad_ti_txt_record_long.txt", "Bad line length"),
                ("bad_ti_txt_no_offset.txt", "Missing section address"),
                ("bad_ti_txt_no_q.txt", "Missing file terminator"),
                ("bad_ti_txt_blank_line.txt", "Bad line length")
            };

            foreach (var (filename, expectedMessage) in testCases)
            {
                var binFile = new BinFile();
                var ex = Assert.Throws<BincopyException>(() =>
                {
                    binFile.AddTiTxtFile(GetTestFilePath(filename));
                });
                Assert.Equal(expectedMessage, ex.Message);
            }
        }

        [Fact]
        public void AsTiTxt_OutputMatchesTiTxtFiles_ForAllTestFormats()
        {
            var filenames = new[]
            {
                "in.s19",
                "empty_main.s19",
                "convert.s19",
                "out.s19",
                "non_sorted_segments.s19",
                "non_sorted_segments_merged_and_sorted.s19",
                "in.hex",
                "empty_main.hex",
                "convert.hex",
                "out.hex"
            };

            foreach (var file1 in filenames)
            {
                string file2 = file1 + ".txt";

                try
                {
                    var bin1 = new BinFile();
                    bin1.AddFile(GetTestFilePath(file1));

                    var bin2 = new BinFile();
                    bin2.AddFile(GetTestFilePath(file2));

                    Assert.Equal(bin1.AsTiTxt(), bin2.AsTiTxt());
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error comparing {file1} to {file2}: {ex.Message}", ex);
                }
            }
        }

        [Fact]
        public void AsTiTxt_WithAddressAbove32Bit_Throws()
        {
            var bin = new BinFile();
            bin.Add([0x01], 0x1_0000_0000);
            var ex = Assert.Throws<BincopyException>(() => bin.AsTiTxt());
            Assert.Contains("TI-TXT", ex.Message);
        }
    }
}

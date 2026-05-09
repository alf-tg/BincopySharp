using System;
using System.IO;
using Xunit;

namespace BincopySharp.Tests
{
    public class BinFileTests
    {
        private readonly string _testFilesPath;

        public BinFileTests()
        {
            _testFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestFiles");
        }

        private string GetTestFilePath(string filename)
        {
            return Path.Combine(_testFilesPath, filename);
        }

        [Fact]
        public void ToString_WithData_ReturnsNonNullString()
        {
            var binFile = new BinFile();

            string inS19 = File.ReadAllText(GetTestFilePath("in.s19"));
            binFile.AddSrec(inS19);

            string result = binFile.ToString();
            Assert.NotNull(result);
        }

        [Fact]
        public void MinimumMaximumAddress_AndLength_ReflectActualSegments()
        {
            var binFile = new BinFile();

            // Get the minimum address from an empty file (should throw)
            Assert.Throws<InvalidOperationException>(() =>
            {
                var _ = binFile.MinimumAddress;
            });

            // Get the maximum address from an empty file (should throw)
            Assert.Throws<InvalidOperationException>(() =>
            {
                var _ = binFile.MaximumAddress;
            });

            // Get the length of an empty file
            Assert.Equal(0, binFile.Length);

            // Get from a small file
            string inS19Content = File.ReadAllText(GetTestFilePath("in.s19"));
            binFile.AddSrec(inS19Content);

            Assert.Equal(0UL,binFile.MinimumAddress);
            Assert.Equal(70UL,binFile.MaximumAddress);
            Assert.Equal(70, binFile.Length);

            // Add a second segment to the file
            binFile.Add([0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01], address: 80);

            Assert.Equal(0UL,binFile.MinimumAddress);
            Assert.Equal(89UL,binFile.MaximumAddress);
            Assert.Equal(79, binFile.Length);
        }

        [Fact]
        public void Add_SrecAndHexStrings_ParsesAndRoundTrips()
        {
            // Test 1: Add SREC string
            var binFile = new BinFile();
            string inS19Content = File.ReadAllText(GetTestFilePath("in.s19"));
            binFile.Add(inS19Content);
            Assert.Equal(inS19Content, binFile.AsSrec(28, Formats.SrecVariant.S19));

            // Test 2: Add Intel HEX string
            binFile = new BinFile();
            string inHexContent = File.ReadAllText(GetTestFilePath("in.hex"));
            binFile.Add(inHexContent);
            Assert.Equal(inHexContent, binFile.AsIhex());

            // Test 3: Invalid data should throw UnsupportedFileFormatException
            binFile = new BinFile();
            Assert.Throws<UnsupportedFileFormatException>(() =>
            {
                binFile.Add("invalid data");
            });

            // Test 4: SREC with invalid data after valid record
            binFile = new BinFile();
            var ex = Assert.Throws<InvalidRecordException>(() =>
            {
                binFile.Add("S214400420ED044000E8B7FFFFFFF4660F1F440000EE\n" +
                           "invalid data");
            });
            Assert.Equal("Record 'invalid data' not starting with an 'S'", ex.Message);

            // Test 5: Intel HEX with invalid data after valid record
            binFile = new BinFile();
            ex = Assert.Throws<InvalidRecordException>(() =>
            {
                binFile.Add(":020000040040BA\n" +
                           "invalid data");
            });
            Assert.Equal("Record 'invalid data' not starting with a ':'", ex.Message);

            // Test 6: Junk data
            Assert.Throws<UnsupportedFileFormatException>(() =>
            {
                binFile.Add("junk");
            });
        }

        [Fact]
        public void EmptyBinFile_AllMethods_ProduceValidOutput()
        {
            var bin = new BinFile();

            Assert.Equal(0, bin.Length);
            Assert.Empty(bin.Segments);

            // Export methods produce valid empty output
            Assert.Empty(bin.AsBinary());
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
    }
}

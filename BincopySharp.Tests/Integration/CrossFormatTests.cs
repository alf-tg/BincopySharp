using System;
using System.IO;
using Xunit;
using BincopySharp;
using Formats = BincopySharp.Formats;

namespace BincopySharp.Tests
{
    public class CrossFormatTests
    {
        private readonly string _testFilesPath;

        public CrossFormatTests()
        {
            _testFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestFiles");
        }

        private string GetTestFilePath(string filename)
        {
            return Path.Combine(_testFilesPath, filename);
        }

        [Fact]
        public void AddMixedFormats_AllConversions_ProduceCorrectOutput()
        {
            var binFile = new BinFile();

            // Add Intel HEX
            string inHexContent = File.ReadAllText(GetTestFilePath("in.hex"));
            binFile.AddIhex(inHexContent);

            // Add SREC
            string inS19Content = File.ReadAllText(GetTestFilePath("in.s19"));
            binFile.AddSrec(inS19Content);

            // Add binary
            byte[] binary1 = File.ReadAllBytes(GetTestFilePath("binary1.bin"));
            binFile.Add(binary1, address: 1024);

            // Verify Intel HEX output
            string expectedHex = File.ReadAllText(GetTestFilePath("out.hex"));
            Assert.Equal(expectedHex, binFile.AsIhex());

            // Verify SREC output
            string expectedS19 = File.ReadAllText(GetTestFilePath("out.s19"));
            Assert.Equal(expectedS19, binFile.AsSrec(variant: Formats.SrecVariant.S19));

            // Fill and verify binary output
            binFile.Fill(0x00);
            byte[] expectedBin = File.ReadAllBytes(GetTestFilePath("out.bin"));
            Assert.Equal(expectedBin, binFile.AsBinary());
        }

        [Fact]
        public void AddSrec_ThenExportAsIhex_MatchesExpectedFile()
        {
            var binFile = new BinFile();

            string emptyMainS19 = File.ReadAllText(GetTestFilePath("empty_main.s19"));
            binFile.AddSrec(emptyMainS19);

            string expected = File.ReadAllText(GetTestFilePath("empty_main.hex"));
            Assert.Equal(expected, binFile.AsIhex());
        }
    }
}

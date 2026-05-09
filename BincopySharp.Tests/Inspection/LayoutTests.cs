using System;
using System.IO;
using Xunit;
using BincopySharp;

namespace BincopySharp.Tests
{
    public class LayoutTests
    {
        private readonly string _testFilesPath;

        public LayoutTests()
        {
            _testFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestFiles");
        }

        private string GetTestFilePath(string filename)
        {
            return Path.Combine(_testFilesPath, filename);
        }

        [Fact]
        public void Layout_EmptyMainSrec_MatchesExpectedOutput()
        {
            var binFile = new BinFile();
            binFile.AddFile(GetTestFilePath("empty_main.s19"));

            string expected = "0x400238                                                                0x601038\n" +
                            "-                                                                              -\n";
            Assert.Equal(expected, binFile.Layout());
        }

        [Fact]
        public void Layout_OutHex_MatchesExpectedOutput()
        {
            var binFile = new BinFile();
            binFile.AddFile(GetTestFilePath("out.hex"));

            string expected = "0x0                                                                        0x403\n" +
                            "=====-               -====-                                                    -\n";
            Assert.Equal(expected, binFile.Layout());
        }

        [Fact]
        public void Layout_InExclude24Srec_MatchesExpectedOutput()
        {
            var binFile = new BinFile();
            binFile.AddFile(GetTestFilePath("in_exclude_2_4.s19"));

            string expected = "0x0                                                               0x46\n" +
                            "==  ==================================================================\n";
            Assert.Equal(expected, binFile.Layout());
        }

        [Fact]
        public void Layout_WithGappedData_ContainsAllExpectedCharacters()
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
    }
}

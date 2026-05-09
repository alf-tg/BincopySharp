using System;
using System.IO;
using Xunit;
using BincopySharp;

namespace BincopySharp.Tests
{
    public class InfoTests
    {
        private readonly string _testFilesPath;

        public InfoTests()
        {
            _testFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestFiles");
        }

        private string GetTestFilePath(string filename)
        {
            return Path.Combine(_testFilesPath, filename);
        }

        [Fact]
        public void Info_WithKnownSrecFile_MatchesExpectedOutput()
        {
            var binFile = new BinFile();
            string srecContent = File.ReadAllText(GetTestFilePath("empty_main.s19"));
            binFile.AddSrec(srecContent);

            string expected = "Header:                  \"bincopy/empty_main.s19\"\n" +
                              "Execution start address: 0x00400400\n" +
                              "Data ranges:\n" +
                              "\n" +
                              "    0x00400238 - 0x004002b4 (124 bytes)\n" +
                              "    0x004002b8 - 0x0040033e (134 bytes)\n" +
                              "    0x00400340 - 0x004003c2 (130 bytes)\n" +
                              "    0x004003d0 - 0x00400572 (418 bytes)\n" +
                              "    0x00400574 - 0x0040057d (9 bytes)\n" +
                              "    0x00400580 - 0x004006ac (300 bytes)\n" +
                              "    0x00600e10 - 0x00601038 (552 bytes)\n";

            Assert.Equal(expected, binFile.Info());
        }
    }
}

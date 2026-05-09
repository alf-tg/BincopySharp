using System;
using System.IO;
using System.Text;
using Xunit;
using BincopySharp;

namespace BincopySharp.Tests
{
    public class HexdumpTests
    {
        private readonly string _testFilesPath;

        public HexdumpTests()
        {
            _testFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestFiles");
        }

        private string GetTestFilePath(string filename)
        {
            return Path.Combine(_testFilesPath, filename);
        }

        [Fact]
        public void AsHexdump_WithNonAlignedSegments_MatchesExpectedOutput()
        {
            var binFile = new BinFile();
            binFile.Add(Encoding.ASCII.GetBytes("12"), address: 17);
            binFile.Add(Encoding.ASCII.GetBytes("34"), address: 26);
            binFile.Add(Encoding.ASCII.GetBytes("5678"), address: 30);
            binFile.Add(Encoding.ASCII.GetBytes("9"), address: 47);

            string expected = File.ReadAllText(GetTestFilePath("hexdump.txt"));
            string result = binFile.AsHexdump();
            Assert.Equal(expected, result);
        }

        [Fact]
        public void AsHexdump_WithSparseHighAddresses_MatchesExpectedOutput()
        {
            var binFile = new BinFile();
            binFile.Add(Encoding.ASCII.GetBytes("34"), address: 0x150);
            binFile.Add(Encoding.ASCII.GetBytes("3"), address: 0x163);
            binFile.Add([0x01], address: 0x260);
            binFile.Add(Encoding.ASCII.GetBytes("3"), address: 0x263);

            string expected = File.ReadAllText(GetTestFilePath("hexdump2.txt"));
            string result = binFile.AsHexdump();
            Assert.Equal(expected, result);
        }

        [Fact]
        public void AsHexdump_WithGaps_ShowsEllipsis()
        {
            var binFile = new BinFile();
            binFile.Add(Encoding.ASCII.GetBytes("1"), address: 0);
            // One line gap as "...".
            binFile.Add(Encoding.ASCII.GetBytes("3"), address: 32);
            // Two lines gap as "...".
            binFile.Add(Encoding.ASCII.GetBytes("6"), address: 80);

            string expected = File.ReadAllText(GetTestFilePath("hexdump3.txt"));
            string result = binFile.AsHexdump();
            Assert.Equal(expected, result);
        }

        [Fact]
        public void AsHexdump_EmptyBinFile_ReturnsNewline()
        {
            var binFile = new BinFile();
            Assert.Equal("\n", binFile.AsHexdump());
        }
    }
}

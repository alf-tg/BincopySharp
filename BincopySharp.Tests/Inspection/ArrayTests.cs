using System;
using System.IO;
using Xunit;
using BincopySharp;

namespace BincopySharp.Tests
{
    public class ArrayTests
    {
        private readonly string _testFilesPath;

        public ArrayTests()
        {
            _testFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestFiles");
        }

        private string GetTestFilePath(string filename)
        {
            return Path.Combine(_testFilesPath, filename);
        }

        [Fact]
        public void AsArray_WithKnownData_MatchesExpectedOutput()
        {
            var binFile = new BinFile();
            string inHexContent = File.ReadAllText(GetTestFilePath("in.hex"));
            binFile.AddIhex(inHexContent);

            string expected = File.ReadAllText(GetTestFilePath("in.i"));
            string result = binFile.AsArray() + "\n";
            Assert.Equal(expected, result);
        }
    }
}

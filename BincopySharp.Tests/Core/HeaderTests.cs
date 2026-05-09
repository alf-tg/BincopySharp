using System;
using System.IO;
using Xunit;

namespace BincopySharp.Tests
{
    public class HeaderTests
    {
        private readonly string _testFilesPath;

        public HeaderTests()
        {
            _testFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestFiles");
        }

        private string GetTestFilePath(string filename)
        {
            return Path.Combine(_testFilesPath, filename);
        }

        [Fact]
        public void ExecutionStartAddress_SetAndGet_RoundTrips()
        {
            var binFile = new BinFile();
            string emptyMainS19 = File.ReadAllText(GetTestFilePath("empty_main.s19"));
            binFile.AddSrec(emptyMainS19);

            Assert.Equal(0x00400400UL,binFile.ExecutionStartAddress);

            binFile.ExecutionStartAddress = 0x00400401;
            Assert.Equal(0x00400401UL,binFile.ExecutionStartAddress);
        }

        [Fact]
        public void HeaderBytes_SetAndGet_RoundTrips()
        {
            var bin = new BinFile();
            byte[] header = [0x01, 0x02, 0x03];
            bin.HeaderBytes = header;
            Assert.Equal(header, bin.HeaderBytes);
        }

        [Fact]
        public void HeaderText_WithUtf8Encoding_DecodesCorrectly()
        {
            var bin = new BinFile(headerEncoding: "utf-8")
            {
                HeaderBytes = System.Text.Encoding.UTF8.GetBytes("hello")
            };
            Assert.Equal("hello", bin.HeaderText);
        }

        [Fact]
        public void HeaderText_WithNullEncoding_ReturnsNull()
        {
            var bin = new BinFile(headerEncoding: null)
            {
                HeaderBytes = [0x01, 0x02]
            };
            Assert.Null(bin.HeaderText);
        }

        [Fact]
        public void OperatorPlus_PreservesHeaderBytesFromFirstOperand()
        {
            var a = new BinFile();
            a.Add([0x01], 0);
            a.HeaderBytes = [0xAA, 0xBB];

            var b = new BinFile();
            b.Add([0x02], 100);

            var result = a + b;
            Assert.Equal(new byte[] { 0xAA, 0xBB }, result.HeaderBytes);
        }

        [Fact]
        public void OperatorPlus_UsesHeaderBytesFromSecondOperandWhenFirstHasNone()
        {
            var a = new BinFile();
            a.Add([0x01], 0);

            var b = new BinFile();
            b.Add([0x02], 100);
            b.HeaderBytes = [0xCC, 0xDD];

            var result = a + b;
            Assert.Equal(new byte[] { 0xCC, 0xDD }, result.HeaderBytes);
        }

        [Fact]
        public void HeaderText_InvalidatedWhenHeaderBytesChanges()
        {
            var bin = new BinFile(headerEncoding: "utf-8")
            {
                HeaderBytes = System.Text.Encoding.UTF8.GetBytes("first")
            };
            Assert.Equal("first", bin.HeaderText);
            bin.HeaderBytes = System.Text.Encoding.UTF8.GetBytes("second");
            Assert.Equal("second", bin.HeaderText);
        }

        [Fact]
        public void HeaderText_NullWhenNoBytesSet()
        {
            var bin = new BinFile(headerEncoding: "utf-8");
            Assert.Null(bin.HeaderText);
        }

        [Fact]
        public void HeaderText_Setter_EncodesStringToHeaderBytes()
        {
            var bin = new BinFile(headerEncoding: "utf-8")
            {
                HeaderText = "hello"
            };
            Assert.Equal(System.Text.Encoding.UTF8.GetBytes("hello"), bin.HeaderBytes);
            Assert.Equal("hello", bin.HeaderText);
        }

        [Fact]
        public void HeaderText_Setter_NullClearsHeader()
        {
            var bin = new BinFile(headerEncoding: "utf-8")
            {
                HeaderText = "hello"
            };
            bin.HeaderText = null;
            Assert.Null(bin.HeaderBytes);
            Assert.Null(bin.HeaderText);
        }

        [Fact]
        public void HeaderText_Setter_ThrowsWhenNoEncodingConfigured()
        {
            var bin = new BinFile(headerEncoding: null);
            Assert.Throws<InvalidOperationException>(() => bin.HeaderText = "hello");
        }
    }
}

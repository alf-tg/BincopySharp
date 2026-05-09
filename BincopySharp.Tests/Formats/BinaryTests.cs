using System;
using System.IO;
using System.Text;
using System.Linq;
using Xunit;
using BincopySharp;

namespace BincopySharp.Tests
{
    public class BinaryTests
    {
        private readonly string _testFilesPath;

        public BinaryTests()
        {
            _testFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestFiles");
        }

        private string GetTestFilePath(string filename)
        {
            return Path.Combine(_testFilesPath, filename);
        }

        [Fact]
        public void AsBinary_VariousOperations_ProducesCorrectOutput()
        {
            // Add data to 0..2.
            var binFile = new BinFile();
            byte[] binary1 = File.ReadAllBytes(GetTestFilePath("binary1.bin"));
            binFile.Add(binary1);

            byte[] result = binFile.AsBinary();
            Assert.Equal(binary1, result);

            // Add and overwrite data to 15..179.
            binFile = new BinFile();
            binFile.AddBinaryFile(GetTestFilePath("binary2.bin"), address: 15);
            binFile.AddBinaryFile(GetTestFilePath("binary2.bin"), address: 15, overwrite: true);

            // Cannot add overlapping segments.
            byte[] binary2 = File.ReadAllBytes(GetTestFilePath("binary2.bin"));
            Assert.Throws<AddDataException>(() =>
            {
                binFile.Add(binary2, address: 20);
            });

            // Exclude the overlapping part and add.
            binFile.Exclude(20, 1024);
            binFile.Add(binary2, address: 20);

            byte[] binary3 = File.ReadAllBytes(GetTestFilePath("binary3.bin"));
            result = binFile.AsBinary(minimumAddress: 0, padding: 0x00);
            Assert.Equal(binary3, result);

            // Exclude first byte and read it to test adjacent add before.
            binFile.Exclude(0, 1);
            binFile.Add([(byte)'1']);

            byte[] reference = new byte[binary3.Length];
            reference[0] = (byte)'1';
            Array.Copy(binary3, 1, reference, 1, binary3.Length - 1);
            result = binFile.AsBinary(minimumAddress: 0, padding: 0x00);
            Assert.Equal(reference, result);

            // Basic checks.
            Assert.Equal(0UL,binFile.MinimumAddress);
            Assert.Equal(184UL,binFile.MaximumAddress);
            Assert.Equal(170, binFile.Length);

            // Dump with start address beyond end of binary.
            Assert.Empty(binFile.AsBinary(minimumAddress: 512));

            // Dump with start address at maximum address.
            Assert.Empty(binFile.AsBinary(minimumAddress: 184));

            // Dump with start address one before maximum address.
            Assert.Equal(new byte[] { (byte)'\n' }, binFile.AsBinary(minimumAddress: 183));

            // Dump with start address one after minimum address.
            byte[] refSlice = new byte[reference.Length - 1];
            Array.Copy(reference, 1, refSlice, 0, reference.Length - 1);
            Assert.Equal(refSlice, binFile.AsBinary(minimumAddress: 1, padding: 0x00));

            // Dump with start address 16 and end address 18.
            Assert.Equal(new byte[] { 0x32, 0x30 }, binFile.AsBinary(minimumAddress: 16, maximumAddress: 18));

            // Dump with start and end addresses 16.
            Assert.Empty(binFile.AsBinary(minimumAddress: 16, maximumAddress: 16));

            // Dump with end beyond end of binary.
            Assert.Equal(reference, binFile.AsBinary(maximumAddress: 1024, padding: 0x00));

            // Dump with end before start.
            Assert.Empty(binFile.AsBinary(minimumAddress: 2, maximumAddress: 0));
        }

        [Fact]
        public void AddFile_SrecAndHexFormats_ParsesCorrectly()
        {
            // Test 1: Add SREC file
            var binFile = new BinFile();
            binFile.AddFile(GetTestFilePath("empty_main_rearranged.s19"));

            byte[] expected = File.ReadAllBytes(GetTestFilePath("empty_main.bin"));
            byte[] result = binFile.AsBinary(padding: 0x00);
            Assert.Equal(expected, result);

            // Test 2: Add Intel HEX file
            binFile = new BinFile();
            binFile.AddFile(GetTestFilePath("in.hex"));

            string inHexContent = File.ReadAllText(GetTestFilePath("in.hex"));
            Assert.Equal(inHexContent, binFile.AsIhex());

            // Test 3: Unsupported file format
            binFile = new BinFile();
            Assert.Throws<UnsupportedFileFormatException>(() =>
            {
                binFile.AddFile(GetTestFilePath("hexdump.txt"));
            });
        }

        [Fact]
        public void AddFile_WithOverwrite_PreservesContent()
        {
            // Test 1: Initialize with single file
            var binFile = new BinFile();
            binFile.AddFile(GetTestFilePath("empty_main_rearranged.s19"));
            byte[] expected = File.ReadAllBytes(GetTestFilePath("empty_main.bin"));
            Assert.Equal(expected, binFile.AsBinary(padding: 0x00));

            // Test 2: Initialize with multiple files and overwrite
            binFile = new BinFile();
            binFile.AddFile(GetTestFilePath("in.hex"));
            binFile.AddFile(GetTestFilePath("in.hex"), overwrite: true);
            string inHexContent = File.ReadAllText(GetTestFilePath("in.hex"));
            Assert.Equal(inHexContent, binFile.AsIhex());

            // Test 3: Initialize with unsupported file format
            binFile = new BinFile();
            Assert.Throws<UnsupportedFileFormatException>(() =>
            {
                binFile.AddFile(GetTestFilePath("hexdump.txt"));
            });
        }

        [Fact]
        public void AsBinary_WithRange_ReturnsCopy()
        {
            var bin = new BinFile();
            bin.Add([0x01, 0x02, 0x03], 0);

            byte[] range = bin.AsBinary(0, 3);
            range[0] = 0xFF;

            // BinFile should be unaffected
            Assert.Equal(0x01UL, bin[0]);
        }

        [Fact]
        public void Add_NullString_ThrowsArgumentNullException()
        {
            var bin = new BinFile();
            Assert.Throws<ArgumentNullException>(() => bin.Add((string)null));
        }

        [Fact]
        public void Add_EmptyString_ThrowsUnsupportedFileFormatException()
        {
            var bin = new BinFile();

            // Empty string should throw UnsupportedFileFormatException
            // because the format detector can't detect a format from empty input
            Assert.Throws<UnsupportedFileFormatException>(() => bin.Add(""));
        }

        [Fact]
        public void Add_EmptyByteArray_LeavesLengthZero()
        {
            var bin = new BinFile();
            bin.Add([], 0);
            Assert.Equal(0, bin.Length);
        }

        [Fact]
        public void Add_NullByteArray_ThrowsArgumentNullException()
        {
            var bin = new BinFile();
            Assert.Throws<ArgumentNullException>(() => bin.Add((byte[])null, 0));
        }

        [Fact]
        public void AddFile_UnrecognizedBinaryFile_ThrowsUnsupportedFileFormatException()
        {
            var bin = new BinFile();
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(tempFile, [0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x01, 0x02, 0x03]);
                Assert.Throws<UnsupportedFileFormatException>(() => bin.AddFile(tempFile));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void Add_UnrecognizedStringFormat_ThrowsUnsupportedFileFormatException()
        {
            var bin = new BinFile();
            Assert.Throws<UnsupportedFileFormatException>(() => bin.Add("this is not a valid format"));
        }
    }
}

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using BincopySharp;
using Formats = BincopySharp.Formats;

namespace BincopySharp.Tests
{
    public class IhexTests
    {
        private readonly string _testFilesPath;

        public IhexTests()
        {
            _testFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestFiles");
        }

        private string GetTestFilePath(string filename)
        {
            return Path.Combine(_testFilesPath, filename);
        }

        [Fact]
        public void AddIhex_InvalidInput_ThrowsExpectedException()
        {
            var binFile = new BinFile();

            // Empty data
            var ex1 = Assert.Throws<BincopyException>(() =>
            {
                binFile.AddIhex("");
            });
            Assert.Equal("Cannot parse empty Intel HEX data", ex1.Message);

            // Bad first character
            var ex2 = Assert.Throws<InvalidRecordException>(() =>
            {
                binFile.AddIhex(".0011110022");
            });
            Assert.Equal("Record '.0011110022' not starting with a ':'", ex2.Message);

            // Bad checksum
            var ex3 = Assert.Throws<InvalidRecordException>(() =>
            {
                binFile.AddIhex(":0011110022");
            });
            Assert.Equal("Expected checksum 'DE' in record :0011110022, but got '22'", ex3.Message);
        }

        [Fact]
        public void AddIhex_RoundTrip_PreservesContent()
        {
            // Test 1: Load Intel HEX and convert back
            var binFile = new BinFile();
            string inHexContent = File.ReadAllText(GetTestFilePath("in.hex"));
            binFile.AddIhex(inHexContent);

            string result = binFile.AsIhex();
            Assert.Equal(inHexContent, result);

            // Test 2: Add and overwrite the data
            binFile = new BinFile();
            binFile.AddIhexFile(GetTestFilePath("in.hex"));
            binFile.AddIhexFile(GetTestFilePath("in.hex"), overwrite: true);

            result = binFile.AsIhex();
            Assert.Equal(inHexContent, result);
        }

        [Fact]
        public void AsIhex_I8HexVariant_OutputsCorrectRecords()
        {
            // I8HEX files use only record types 00 and 01 (16 bit addresses).
            var binFile = new BinFile();

            binFile.AddIhex(":0100000001FE\n" +
                           ":0101000002FC\n" +
                           ":01FFFF0003FE\n" +
                           ":0400000300000000F9\n" + // Will not be part of I8HEX output.
                           ":00000001FF\n");

            var segments = new List<(ulong, byte[])>();
            foreach (var segment in binFile.Segments)
            {
                segments.Add((segment.MinimumAddress, segment.Data.ToArray()));
            }

            Assert.Equal(3, segments.Count);
            Assert.Equal(0UL,segments[0].Item1);
            Assert.Equal(new byte[] { 0x01 }, segments[0].Item2);
            Assert.Equal(0x100UL,segments[1].Item1);
            Assert.Equal(new byte[] { 0x02 }, segments[1].Item2);
            Assert.Equal(0xffffUL,segments[2].Item1);
            Assert.Equal(new byte[] { 0x03 }, segments[2].Item2);

            string result = binFile.AsIhex(variant: Formats.IhexVariant.I8Hex);
            Assert.Equal(":0100000001FE\n" +
                        ":0101000002FC\n" +
                        ":01FFFF0003FE\n" +
                        ":00000001FF\n", result);
        }

        [Fact]
        public void AsIhex_I8HexVariantWithAddressAbove64k_Throws()
        {
            var binFile = new BinFile();
            binFile.Add([0x00], address: 65536);

            var ex = Assert.Throws<BincopyException>(() =>
            {
                binFile.AsIhex(variant: Formats.IhexVariant.I8Hex);
            });

            Assert.Equal("Cannot address more than 64 kB in I8HEX files (16 bits addresses)", ex.Message);
        }

        [Fact]
        public void AsIhex_I16HexVariant_OutputsCorrectRecords()
        {
            // I16HEX files use only record types 00 through 03 (20 bit addresses).
            var binFile = new BinFile();

            binFile.AddIhex(":0100000001FE\n" +
                           ":01F00000020D\n" +
                           ":01FFFF0003FE\n" +
                           ":02000002C0003C\n" +
                           ":0110000005EA\n" +
                           ":02000002FFFFFE\n" +
                           ":0100000006F9\n" +
                           ":01FFFF0007FA\n" +
                           ":020000021000EC\n" +
                           ":0100000004FB\n" +
                           ":0400000500000000F7\n" + // Converted to 03 in I16HEX output.
                           ":00000001FF\n");

            var segments = new List<(ulong, byte[])>();
            foreach (var segment in binFile.Segments)
            {
                segments.Add((segment.MinimumAddress, segment.Data.ToArray()));
            }

            Assert.Equal(6, segments.Count);
            Assert.Equal(0UL,segments[0].Item1);
            Assert.Equal(new byte[] { 0x01 }, segments[0].Item2);
            Assert.Equal(0xf000UL,segments[1].Item1);
            Assert.Equal(new byte[] { 0x02 }, segments[1].Item2);
            Assert.Equal(0xffffUL,segments[2].Item1);
            Assert.Equal(new byte[] { 0x03, 0x04 }, segments[2].Item2); // 3 at 0xffff and 4 at 16 * 0x1000 = 0x10000.
            Assert.Equal((ulong)(16 * 0xc000 + 0x1000), segments[3].Item1);
            Assert.Equal(new byte[] { 0x05 }, segments[3].Item2);
            Assert.Equal((ulong)(16 * 0xffff), segments[4].Item1);
            Assert.Equal(new byte[] { 0x06 }, segments[4].Item2);
            Assert.Equal((ulong)(17 * 0xffff), segments[5].Item1);
            Assert.Equal(new byte[] { 0x07 }, segments[5].Item2);

            string result = binFile.AsIhex(variant: Formats.IhexVariant.I16Hex);
            Assert.Equal(":0100000001FE\n" +
                        ":01F00000020D\n" +
                        ":02FFFF000304F9\n" +
                        ":02000002C0003C\n" +
                        ":0110000005EA\n" +
                        ":02000002F0000C\n" +
                        ":01FFF000060A\n" +
                        ":02000002FFFFFE\n" +
                        ":01FFFF0007FA\n" +
                        ":0400000300000000F9\n" +
                        ":00000001FF\n", result);
        }

        [Fact]
        public void AsIhex_I16HexVariantWithAddressAbove1MB_Throws()
        {
            var binFile = new BinFile();
            binFile.Add([0x00], address: 17 * 65535 + 1);

            var ex = Assert.Throws<BincopyException>(() =>
            {
                binFile.AsIhex(variant: Formats.IhexVariant.I16Hex);
            });

            Assert.Equal("Cannot address more than 1 MB in I16HEX files (20 bits addresses)", ex.Message);
        }

        [Fact]
        public void AsIhex_I16HexVariantWithExecutionStartAbove1MB_Throws()
        {
            var binFile = new BinFile();
            binFile.Add([0x00]);
            binFile.ExecutionStartAddress = 0x100000;

            var ex = Assert.Throws<BincopyException>(() =>
            {
                binFile.AsIhex(variant: Formats.IhexVariant.I16Hex);
            });

            Assert.Equal("Cannot set execution start address above 1 MB in I16HEX files (20 bits addresses)", ex.Message);
        }

        [Fact]
        public void AsIhex_I32HexVariant_OutputsCorrectRecords()
        {
            // I32HEX files use only record types 00, 01, 04, and 05 (32 bit addresses).
            var binFile = new BinFile();

            binFile.AddIhex(":0100000001FE\n" +
                           ":01FFFF0002FF\n" +
                           ":02000004FFFFFC\n" +
                           ":0100000004FB\n" +
                           ":01FFFF0005FC\n" +
                           ":020000040001F9\n" +
                           ":0100000003FC\n" +
                           ":0400000500000000F7\n" +
                           ":00000001FF\n");

            string result = binFile.AsIhex();
            Assert.Equal(":0100000001FE\n" +
                        ":02FFFF000203FB\n" +
                        ":02000004FFFFFC\n" +
                        ":0100000004FB\n" +
                        ":01FFFF0005FC\n" +
                        ":0400000500000000F7\n" +
                        ":00000001FF\n", result);

            Assert.Equal(0UL,binFile.MinimumAddress);
            Assert.Equal(0x100000000UL, binFile.MaximumAddress);
            Assert.Equal(0UL,binFile.ExecutionStartAddress);
            Assert.Equal(1UL, binFile[0]);
            Assert.Equal(2UL, binFile[0xffff]);
            Assert.Equal(3UL, binFile[0x10000]);
            Assert.Equal(4UL, binFile[0xffff0000]);
            Assert.Equal(new byte[] { 0xff, 0xff }, binFile.AsBinary(0xffff0002, 0xffff0004));
            Assert.Equal(new byte[] { 0x05 }, binFile.AsBinary(0xffffffff, 0x100000000));
        }

        [Fact]
        public void AsIhex_I32HexVariantWithAddressAbove4GB_Throws()
        {
            var binFile = new BinFile();
            binFile.Add([0x00], address: 0x100000000);

            var ex = Assert.Throws<BincopyException>(() =>
            {
                binFile.AsIhex(variant: Formats.IhexVariant.I32Hex);
            });

            Assert.Equal("Cannot address more than 4 GB in I32HEX files (32 bits addresses)", ex.Message);
        }

        [Fact]
        public void AsIhex_I32HexVariantWithExecutionStartAbove4GB_Throws()
        {
            var binFile = new BinFile();
            binFile.Add([0x00]);
            binFile.ExecutionStartAddress = 0x100000000;

            var ex = Assert.Throws<BincopyException>(() =>
            {
                binFile.AsIhex(variant: Formats.IhexVariant.I32Hex);
            });

            Assert.Equal("Cannot set execution start address above 4 GB in I32HEX files (32 bits addresses)", ex.Message);
        }

        [Fact]
        public void IhexChecksum_KnownValues_CalculatesCorrectly()
        {
            Assert.Equal(0x1e, BincopySharp.Utilities.IhexChecksumCalculator.Calculate("0300300002337a"));
            Assert.Equal(0, BincopySharp.Utilities.IhexChecksumCalculator.Calculate("00000000"));
        }

        [Fact]
        public void AddIhex_WithRoundTripFile_PreservesFormat()
        {
            var binFile = new BinFile();

            string inHex = File.ReadAllText(GetTestFilePath("issue_4_in.hex"));
            binFile.AddIhex(inHex);

            string expected = File.ReadAllText(GetTestFilePath("issue_4_out.hex"));
            Assert.Equal(expected, binFile.AsIhex());
        }

        [Fact]
        public void AddIhex_WithBlankLines_ParsesSuccessfully()
        {
            var binFile = new BinFile();

            string inBlankLinesHex = File.ReadAllText(GetTestFilePath("in_blank_lines.hex"));
            binFile.AddIhex(inBlankLinesHex);

            string inHex = File.ReadAllText(GetTestFilePath("in.hex"));
            Assert.Equal(inHex, binFile.AsIhex());
        }

        [Fact]
        public void AddIhex_RecordType3_SetsExecutionStartAddress()
        {
            var binFile = new BinFile();
            binFile.AddIhex(":0400000302030405EB");

            Assert.Equal(0x02030405UL,binFile.ExecutionStartAddress);
        }

        [Fact]
        public void AddIhex_RecordType5_SetsExecutionStartAddress()
        {
            var binFile = new BinFile();
            binFile.AddIhex(":0400000501020304ED");

            Assert.Equal(0x01020304UL,binFile.ExecutionStartAddress);
        }

        [Fact]
        public void AddIhex_RecordType6_ThrowsInvalidRecordException()
        {
            var binFile = new BinFile();

            var ex = Assert.Throws<InvalidRecordException>(() =>
            {
                binFile.AddIhex(":00000006FA");
            });

            Assert.Equal("Expected type 0..5 in record :00000006FA, but got 6", ex.Message);
        }

        [Fact]
        public void AsIhex_I32HexVariantWithAddressAbove32Bit_ThrowsWithMessage()
        {
            var bin = new BinFile();
            bin.Add([0x01], 0x1_0000_0000);
            var ex = Assert.Throws<BincopyException>(() => bin.AsIhex(variant: Formats.IhexVariant.I32Hex));
            Assert.Contains("4 GB", ex.Message);
        }
    }
}

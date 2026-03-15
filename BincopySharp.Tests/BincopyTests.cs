using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using BincopySharp;

namespace BincopySharp.Tests
{
    /// <summary>
    /// Port of test_bincopy.py from Python bincopy library version 20.1.1.
    /// Each test maintains 1:1 mapping with original Python tests.
    /// </summary>
    public class BincopyTests
    {
        private readonly string _testFilesPath;

        public BincopyTests()
        {
            _testFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestFiles");
        }

        private string GetTestFilePath(string filename)
        {
            return Path.Combine(_testFilesPath, filename);
        }

        private void AssertFilesEqual(string actualPath, string expectedPath)
        {
            byte[] actual = File.ReadAllBytes(actualPath);
            byte[] expected = File.ReadAllBytes(expectedPath);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TestSrec()
        {
            // Test 1: Load SREC and convert back
            var binFile = new BinFile();
            string inS19Content = File.ReadAllText(GetTestFilePath("in.s19"));
            binFile.AddSrec(inS19Content);
            
            string result = binFile.AsSrec(28, 16);
            Assert.Equal(inS19Content, result);

            // Test 2: Load SREC and convert to binary
            binFile = new BinFile();
            string emptyMainS19 = File.ReadAllText(GetTestFilePath("empty_main.s19"));
            binFile.AddSrec(emptyMainS19);
            
            byte[] emptyMainBin = File.ReadAllBytes(GetTestFilePath("empty_main.bin"));
            byte[] resultBin = binFile.AsBinary(padding: 0x00);
            Assert.Equal(emptyMainBin, resultBin);

            // Test 3: Add and overwrite data
            binFile = new BinFile();
            binFile.AddSrecFile(GetTestFilePath("empty_main_rearranged.s19"));
            binFile.AddSrecFile(GetTestFilePath("empty_main_rearranged.s19"), overwrite: true);
            
            resultBin = binFile.AsBinary(padding: 0x00);
            Assert.Equal(emptyMainBin, resultBin);

            // Test 4: Bad CRC should throw exception
            binFile = new BinFile();
            var ex = Assert.Throws<InvalidRecordException>(() =>
            {
                binFile.AddSrecFile(GetTestFilePath("bad_crc.s19"));
            });
            
            Assert.Equal("expected crc '25' in record S2144002640000000002000000060000001800000022, but got '22'", ex.Message);
        }

        [Fact]
        public void TestBadSrec()
        {            
            // Test: Invalid SREC records should throw exceptions
            var binFile = new BinFile();
            
            // Too short record
            Assert.ThrowsAny<BincopyException>(() =>
            {
                binFile.AddSrec("");
            });

            // Bad first character
            Assert.ThrowsAny<BincopyException>(() =>
            {
                binFile.AddSrec("T0000011");
            });

            // Bad type (invalid character in type field)
            var exBadType = Assert.ThrowsAny<BincopyException>(() =>
            {
                binFile.AddSrec("S.0200FF");
            });
            Assert.Equal("expected record type 0..3 or 5..9, but got '.'", exBadType.Message);

            // Bad CRC
            var ex = Assert.ThrowsAny<BincopyException>(() =>
            {
                binFile.AddSrec("S1020011");
            });
            Assert.Equal("expected crc 'FD' in record S1020011, but got '11'", ex.Message);
        }

        [Fact]
        public void TestTiTxt()
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
        public void TestBadTiTxt()
        {
            var testCases = new[]
            {
                ("bad_ti_txt_address_value.txt", "bad section address"),
                ("bad_ti_txt_bad_q.txt", "bad file terminator"),
                ("bad_ti_txt_data_value.txt", "bad data"),
                ("bad_ti_txt_record_short.txt", "missing section address"),
                ("bad_ti_txt_record_long.txt", "bad line length"),
                ("bad_ti_txt_no_offset.txt", "missing section address"),
                ("bad_ti_txt_no_q.txt", "missing file terminator"),
                ("bad_ti_txt_blank_line.txt", "bad line length")
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
        public void TestCompareTiTxt()
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
        public void TestBadIhex()
        {
            var binFile = new BinFile();
            
            // Empty data
            var ex1 = Assert.ThrowsAny<BincopyException>(() =>
            {
                binFile.AddIhex("");
            });
            Assert.Equal("Cannot parse empty Intel HEX data", ex1.Message);

            // Bad first character
            var ex2 = Assert.ThrowsAny<BincopyException>(() =>
            {
                binFile.AddIhex(".0011110022");
            });
            Assert.Equal("Record '.0011110022' not starting with a ':'", ex2.Message);

            // Bad checksum
            var ex3 = Assert.ThrowsAny<BincopyException>(() =>
            {
                binFile.AddIhex(":0011110022");
            });
            Assert.Equal("Expected checksum 'DE' in record :0011110022, but got '22'", ex3.Message);
        }

        [Fact]
        public void TestIhex()
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
        public void TestI8hex()
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
                segments.Add((segment.MinimumAddress, segment.Data));
            }

            Assert.Equal(3, segments.Count);
            Assert.Equal((ulong)0, segments[0].Item1);
            Assert.Equal(new byte[] { 0x01 }, segments[0].Item2);
            Assert.Equal((ulong)0x100, segments[1].Item1);
            Assert.Equal(new byte[] { 0x02 }, segments[1].Item2);
            Assert.Equal((ulong)0xffff, segments[2].Item1);
            Assert.Equal(new byte[] { 0x03 }, segments[2].Item2);

            string result = binFile.AsIhex(addressLengthBits: 16);
            Assert.Equal(":0100000001FE\n" +
                        ":0101000002FC\n" +
                        ":01FFFF0003FE\n" +
                        ":00000001FF\n", result);
        }

        [Fact]
        public void TestI8hexAddressAbove64k()
        {
            var binFile = new BinFile();
            binFile.AddBinary(new byte[] { 0x00 }, address: 65536);

            var ex = Assert.Throws<BincopyException>(() =>
            {
                binFile.AsIhex(addressLengthBits: 16);
            });

            Assert.Equal("cannot address more than 64 kB in I8HEX files (16 bits addresses)", ex.Message);
        }

        [Fact]
        public void TestI16hex()
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
                segments.Add((segment.MinimumAddress, segment.Data));
            }

            Assert.Equal(6, segments.Count);
            Assert.Equal((ulong)0, segments[0].Item1);
            Assert.Equal(new byte[] { 0x01 }, segments[0].Item2);
            Assert.Equal((ulong)0xf000, segments[1].Item1);
            Assert.Equal(new byte[] { 0x02 }, segments[1].Item2);
            Assert.Equal((ulong)0xffff, segments[2].Item1);
            Assert.Equal(new byte[] { 0x03, 0x04 }, segments[2].Item2); // 3 at 0xffff and 4 at 16 * 0x1000 = 0x10000.
            Assert.Equal(16UL * 0xc000 + 0x1000, segments[3].Item1);
            Assert.Equal(new byte[] { 0x05 }, segments[3].Item2);
            Assert.Equal(16UL * 0xffff, segments[4].Item1);
            Assert.Equal(new byte[] { 0x06 }, segments[4].Item2);
            Assert.Equal(17UL * 0xffff, segments[5].Item1);
            Assert.Equal(new byte[] { 0x07 }, segments[5].Item2);

            string result = binFile.AsIhex(addressLengthBits: 24);
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
        public void TestI16hexAddressAbove1meg()
        {
            var binFile = new BinFile();
            binFile.AddBinary(new byte[] { 0x00 }, address: 17 * 65535 + 1);

            var ex = Assert.Throws<BincopyException>(() =>
            {
                binFile.AsIhex(addressLengthBits: 24);
            });

            Assert.Equal("cannot address more than 1 MB in I16HEX files (20 bits addresses)", ex.Message);
        }

        [Fact]
        public void TestI32hex()
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
            
            Assert.Equal((ulong)0, binFile.MinimumAddress);
            Assert.Equal(0x100000000UL, binFile.MaximumAddress);
            Assert.Equal((ulong)0, binFile.ExecutionStartAddress);
            Assert.Equal(1, binFile[0]);
            Assert.Equal(2, binFile[0xffff]);
            Assert.Equal(3, binFile[0x10000]);
            Assert.Equal(4, binFile[0xffff0000UL]);
            Assert.Equal(new byte[] { 0xff, 0xff }, binFile.GetRange(0xffff0002UL, 0xffff0004UL));
            Assert.Equal(new byte[] { 0x05 }, binFile.GetRange(0xffffffffUL, 0x100000000UL));
        }

        [Fact]
        public void TestI32hexAddressAbove4gig()
        {
            var binFile = new BinFile();
            binFile.AddBinary(new byte[] { 0x00 }, address: 0x100000000UL);

            var ex = Assert.Throws<BincopyException>(() =>
            {
                binFile.AsIhex(addressLengthBits: 32);
            });

            Assert.Equal("cannot address more than 4 GB in I32HEX files (32 bits addresses)", ex.Message);
        }

        [Fact]
        public void TestBinary()
        {
            // Add data to 0..2.
            var binFile = new BinFile();
            byte[] binary1 = File.ReadAllBytes(GetTestFilePath("binary1.bin"));
            binFile.AddBinary(binary1);
            
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
                binFile.AddBinary(binary2, address: 20);
            });

            // Exclude the overlapping part and add.
            binFile.Exclude(20, 1024);
            binFile.AddBinary(binary2, address: 20);

            byte[] binary3 = File.ReadAllBytes(GetTestFilePath("binary3.bin"));
            result = binFile.AsBinary(minimumAddress: 0, padding: 0x00);
            Assert.Equal(binary3, result);

            // Exclude first byte and read it to test adjacent add before.
            binFile.Exclude(0, 1);
            binFile.AddBinary(new byte[] { (byte)'1' });

            byte[] reference = new byte[binary3.Length];
            reference[0] = (byte)'1';
            Array.Copy(binary3, 1, reference, 1, binary3.Length - 1);
            result = binFile.AsBinary(minimumAddress: 0, padding: 0x00);
            Assert.Equal(reference, result);

            // Basic checks.
            Assert.Equal((ulong)0, binFile.MinimumAddress);
            Assert.Equal((ulong)184, binFile.MaximumAddress);
            Assert.Equal((ulong)170, binFile.Length);

            // Dump with start address beyond end of binary.
            Assert.Equal(Array.Empty<byte>(), binFile.AsBinary(minimumAddress: 512));

            // Dump with start address at maximum address.
            Assert.Equal(Array.Empty<byte>(), binFile.AsBinary(minimumAddress: 184));

            // Dump with start address one before maximum address.
            Assert.Equal(new byte[] { (byte)'\n' }, binFile.AsBinary(minimumAddress: 183));

            // Dump with start address one after minimum address.
            byte[] refSlice = new byte[reference.Length - 1];
            Array.Copy(reference, 1, refSlice, 0, reference.Length - 1);
            Assert.Equal(refSlice, binFile.AsBinary(minimumAddress: 1, padding: 0x00));

            // Dump with start address 16 and end address 18.
            Assert.Equal(new byte[] { 0x32, 0x30 }, binFile.AsBinary(minimumAddress: 16, maximumAddress: 18));

            // Dump with start and end addresses 16.
            Assert.Equal(Array.Empty<byte>(), binFile.AsBinary(minimumAddress: 16, maximumAddress: 16));

            // Dump with end beyond end of binary.
            Assert.Equal(reference, binFile.AsBinary(maximumAddress: 1024, padding: 0x00));

            // Dump with end before start.
            Assert.Equal(Array.Empty<byte>(), binFile.AsBinary(minimumAddress: 2, maximumAddress: 0));
        }

        [Fact]
        public void TestAddFile()
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
        public void TestArray()
        {
            var binFile = new BinFile();
            string inHexContent = File.ReadAllText(GetTestFilePath("in.hex"));
            binFile.AddIhex(inHexContent);

            string expected = File.ReadAllText(GetTestFilePath("in.i"));
            string result = binFile.AsArray() + "\n";
            Assert.Equal(expected, result);
        }

        [Fact]
        public void TestHexdump1()
        {
            var binFile = new BinFile();
            binFile.AddBinary(Encoding.ASCII.GetBytes("12"), address: 17);
            binFile.AddBinary(Encoding.ASCII.GetBytes("34"), address: 26);
            binFile.AddBinary(Encoding.ASCII.GetBytes("5678"), address: 30);
            binFile.AddBinary(Encoding.ASCII.GetBytes("9"), address: 47);

            string expected = File.ReadAllText(GetTestFilePath("hexdump.txt"));
            string result = binFile.AsHexdump();
            Assert.Equal(expected, result);
        }

        [Fact]
        public void TestHexdump2()
        {
            var binFile = new BinFile();
            binFile.AddBinary(Encoding.ASCII.GetBytes("34"), address: 0x150);
            binFile.AddBinary(Encoding.ASCII.GetBytes("3"), address: 0x163);
            binFile.AddBinary(new byte[] { 0x01 }, address: 0x260);
            binFile.AddBinary(Encoding.ASCII.GetBytes("3"), address: 0x263);

            string expected = File.ReadAllText(GetTestFilePath("hexdump2.txt"));
            string result = binFile.AsHexdump();
            Assert.Equal(expected, result);
        }

        [Fact]
        public void TestHexdumpGaps()
        {
            var binFile = new BinFile();
            binFile.AddBinary(Encoding.ASCII.GetBytes("1"), address: 0);
            // One line gap as "...".
            binFile.AddBinary(Encoding.ASCII.GetBytes("3"), address: 32);
            // Two lines gap as "...".
            binFile.AddBinary(Encoding.ASCII.GetBytes("6"), address: 80);

            string expected = File.ReadAllText(GetTestFilePath("hexdump3.txt"));
            string result = binFile.AsHexdump();
            Assert.Equal(expected, result);
        }

        [Fact]
        public void TestHexdumpEmpty()
        {
            var binFile = new BinFile();
            Assert.Equal("\n", binFile.AsHexdump());
        }

        [Fact]
        public void TestSrecIhexBinary()
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
            binFile.AddBinary(binary1, address: 1024);

            // Verify Intel HEX output
            string expectedHex = File.ReadAllText(GetTestFilePath("out.hex"));
            Assert.Equal(expectedHex, binFile.AsIhex());

            // Verify SREC output
            string expectedS19 = File.ReadAllText(GetTestFilePath("out.s19"));
            Assert.Equal(expectedS19, binFile.AsSrec(addressLengthBits: 16));

            // Fill and verify binary output
            binFile.Fill(0x00);
            byte[] expectedBin = File.ReadAllBytes(GetTestFilePath("out.bin"));
            Assert.Equal(expectedBin, binFile.AsBinary());
        }

        [Fact]
        public void TestExclude()
        {
            // Test 1: Exclude 2-4
            var binFile = new BinFile();
            string inS19Content = File.ReadAllText(GetTestFilePath("in.s19"));
            binFile.AddSrec(inS19Content);
            binFile.Exclude(2, 4);

            string expected1 = File.ReadAllText(GetTestFilePath("in_exclude_2_4.s19"));
            Assert.Equal(expected1, binFile.AsSrec(32, 16));

            // Test 2: Exclude 3-1024
            binFile = new BinFile();
            binFile.AddSrec(inS19Content);
            binFile.Exclude(3, 1024);

            string expected2 = File.ReadAllText(GetTestFilePath("in_exclude_3_1024.s19"));
            Assert.Equal(expected2, binFile.AsSrec(32, 16));

            // Test 3: Exclude 0-9
            binFile = new BinFile();
            binFile.AddSrec(inS19Content);
            binFile.Exclude(0, 9);

            string expected3 = File.ReadAllText(GetTestFilePath("in_exclude_0_9.s19"));
            Assert.Equal(expected3, binFile.AsSrec(32, 16));

            // Test 4: Exclude from empty_main
            binFile = new BinFile();
            string emptyMainS19 = File.ReadAllText(GetTestFilePath("empty_main.s19"));
            binFile.AddSrec(emptyMainS19);
            binFile.Exclude(0x400240, 0x400600);

            byte[] expected4 = File.ReadAllBytes(GetTestFilePath("empty_main_mod.bin"));
            Assert.Equal(expected4, binFile.AsBinary(padding: 0x00));

            // Test 5: Exclude various parts of segments
            binFile = new BinFile();
            binFile.AddBinary(Encoding.ASCII.GetBytes("111111"), address: 8);
            binFile.AddBinary(Encoding.ASCII.GetBytes("222222"), address: 16);
            binFile.AddBinary(Encoding.ASCII.GetBytes("333333"), address: 24);

            binFile.Exclude(7, 8);
            binFile.Exclude(15, 16);
            binFile.Exclude(23, 24);

            byte[] expected5 = new byte[] 
            { 
                (byte)'1', (byte)'1', (byte)'1', (byte)'1', (byte)'1', (byte)'1',  // "111111"
                0xff, 0xff,  // 2 bytes gap
                (byte)'2', (byte)'2', (byte)'2', (byte)'2', (byte)'2', (byte)'2',  // "222222"
                0xff, 0xff,  // 2 bytes gap
                (byte)'3', (byte)'3', (byte)'3', (byte)'3', (byte)'3', (byte)'3'   // "333333"
            };
            Assert.Equal(expected5, binFile.AsBinary());
            Assert.Equal(3, binFile.Segments.Count);

            binFile.Exclude(20, 24);
            Assert.Equal(
                Encoding.ASCII.GetBytes("111111")
                    .Concat(new byte[] { 0xff, 0xff })
                    .Concat(Encoding.ASCII.GetBytes("2222"))
                    .Concat(Enumerable.Repeat((byte)0xff, 4))
                    .Concat(Encoding.ASCII.GetBytes("333333"))
                    .ToArray(),
                binFile.AsBinary());
            Assert.Equal(3, binFile.Segments.Count);

            binFile.Exclude(12, 24);
            Assert.Equal(
                Encoding.ASCII.GetBytes("1111")
                    .Concat(Enumerable.Repeat((byte)0xff, 12))
                    .Concat(Encoding.ASCII.GetBytes("333333"))
                    .ToArray(),
                binFile.AsBinary());
            Assert.Equal(2, binFile.Segments.Count);

            binFile.Exclude(11, 25);
            Assert.Equal(
                Encoding.ASCII.GetBytes("111")
                    .Concat(Enumerable.Repeat((byte)0xff, 14))
                    .Concat(Encoding.ASCII.GetBytes("33333"))
                    .ToArray(),
                binFile.AsBinary());
            Assert.Equal(2, binFile.Segments.Count);

            binFile.Exclude(11, 26);
            Assert.Equal(
                Encoding.ASCII.GetBytes("111")
                    .Concat(Enumerable.Repeat((byte)0xff, 15))
                    .Concat(Encoding.ASCII.GetBytes("3333"))
                    .ToArray(),
                binFile.AsBinary());
            Assert.Equal(2, binFile.Segments.Count);

            binFile.Exclude(27, 29);
            Assert.Equal(
                Encoding.ASCII.GetBytes("111")
                    .Concat(Enumerable.Repeat((byte)0xff, 15))
                    .Concat(Encoding.ASCII.GetBytes("3"))
                    .Concat(new byte[] { 0xff, 0xff })
                    .Concat(Encoding.ASCII.GetBytes("3"))
                    .ToArray(),
                binFile.AsBinary());
            Assert.Equal(3, binFile.Segments.Count);

            // Exclude negative address range and empty address range
            binFile = new BinFile();
            binFile.AddBinary(Encoding.ASCII.GetBytes("111111"));

            var ex = Assert.Throws<BincopyException>(() =>
            {
                binFile.Exclude(4, 2);
            });
            Assert.Equal("bad address range", ex.Message);

            binFile.Exclude(2, 2);
            Assert.Equal(Encoding.ASCII.GetBytes("111111"), binFile.AsBinary());
        }

        [Fact]
        public void TestCrop()
        {
            // Test 1: Crop 2-4
            var binFile = new BinFile();
            binFile.AddSrecFile(GetTestFilePath("in.s19"));
            binFile.Crop(2, 4);

            string expected = File.ReadAllText(GetTestFilePath("in_crop_2_4.s19"));
            Assert.Equal(expected, binFile.AsSrec(32, 16));

            // Test 2: Crop then exclude should result in empty
            binFile.Exclude(2, 4);
            Assert.Equal(Array.Empty<byte>(), binFile.AsBinary());
        }

        [Fact]
        public void TestSegmentsList()
        {
            var binFile = new BinFile();
            binFile.AddBinary(new byte[] { 0x00 }, address: 0);
            binFile.AddBinary(new byte[] { 0x01, 0x02 }, address: 10);
            binFile.AddBinary(new byte[] { 0x03 }, address: 12);
            binFile.AddBinary(new byte[] { 0x04 }, address: 1000);

            var segments = new List<(ulong Address, byte[] Data)>();
            foreach (var segment in binFile.Segments)
            {
                segments.Add((segment.MinimumAddress, segment.Data));
            }

            Assert.Equal(3, segments.Count);
            Assert.Equal((ulong)0, segments[0].Address);
            Assert.Equal(new byte[] { 0x00 }, segments[0].Data);
            Assert.Equal((ulong)10, segments[1].Address);
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, segments[1].Data);
            Assert.Equal((ulong)1000, segments[2].Address);
            Assert.Equal(new byte[] { 0x04 }, segments[2].Data);
        }

        [Fact]
        public void TestInfo()
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

        [Fact]
        public void TestBinary16()
        {
            var binFile = new BinFile(wordSizeBytes: 2);  // 16 bits = 2 bytes
            binFile.AddBinary(new byte[] { 0x35, 0x30, 0x36, 0x30, 0x37, 0x30 }, address: 5);
            binFile.AddBinary(new byte[] { 0x61, 0x30, 0x62, 0x30, 0x63, 0x30 }, address: 10);

            // Basic checks
            Assert.Equal((ulong)5, binFile.MinimumAddress);
            Assert.Equal((ulong)13, binFile.MaximumAddress);
            Assert.Equal((ulong)6, binFile.Length);

            // Dump with start address beyond end of binary
            Assert.Equal(Array.Empty<byte>(), binFile.AsBinary(minimumAddress: 14));

            // Dump with start address at maximum address
            Assert.Equal(Array.Empty<byte>(), binFile.AsBinary(minimumAddress: 13));

            // Dump with start address one before maximum address
            Assert.Equal(new byte[] { (byte)'c', (byte)'0' }, binFile.AsBinary(minimumAddress: 12));

            // Dump parts of both segments
            byte[] expected = new byte[] { 0x36, 0x30, 0x37, 0x30, 0xff, 0xff, 0xff, 0xff, 0x61, 0x30 };
            Assert.Equal(expected, binFile.AsBinary(minimumAddress: 6, maximumAddress: 11));

            // Iterate over segments
            var segments = new List<(ulong, byte[])>();
            foreach (var segment in binFile.Segments)
            {
                segments.Add((segment.Address, segment.Data));
            }
            Assert.Equal(2, segments.Count);
            Assert.Equal((ulong)5, segments[0].Item1);
            Assert.Equal(new byte[] { 0x35, 0x30, 0x36, 0x30, 0x37, 0x30 }, segments[0].Item2);
            Assert.Equal((ulong)10, segments[1].Item1);
            Assert.Equal(new byte[] { 0x61, 0x30, 0x62, 0x30, 0x63, 0x30 }, segments[1].Item2);

            // Chunks of segments
            var chunks = new List<(ulong, byte[])>();
            foreach (var chunk in binFile.Segments.Chunks(size: 2))
            {
                chunks.Add((chunk.Address, chunk.Data));
            }
            Assert.Equal(4, chunks.Count);
            Assert.Equal((ulong)5, chunks[0].Item1);
            Assert.Equal(new byte[] { 0x35, 0x30, 0x36, 0x30 }, chunks[0].Item2);
            Assert.Equal((ulong)7, chunks[1].Item1);
            Assert.Equal(new byte[] { 0x37, 0x30 }, chunks[1].Item2);
            Assert.Equal((ulong)10, chunks[2].Item1);
            Assert.Equal(new byte[] { 0x61, 0x30, 0x62, 0x30 }, chunks[2].Item2);
            Assert.Equal((ulong)12, chunks[3].Item1);
            Assert.Equal(new byte[] { 0x63, 0x30 }, chunks[3].Item2);

            // Hexdump output
            string expectedHexdump = "00000000                                 35 30 36 30 37 30  |          506070|\n" +
                                    "00000008              61 30 62 30  63 30                    |    a0b0c0      |\n";
            Assert.Equal(expectedHexdump, binFile.AsHexdump());
        }

        [Fact]
        public void TestAdd()
        {
            // Test 1: Add SREC string
            var binFile = new BinFile();
            string inS19Content = File.ReadAllText(GetTestFilePath("in.s19"));
            binFile.Add(inS19Content);
            Assert.Equal(inS19Content, binFile.AsSrec(28, 16));

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
            var ex = Assert.ThrowsAny<BincopyException>(() =>
            {
                binFile.Add("S214400420ED044000E8B7FFFFFFF4660F1F440000EE\n" +
                           "invalid data");
            });
            Assert.Equal("Record 'invalid data' not starting with an 'S'", ex.Message);

            // Test 5: Intel HEX with invalid data after valid record
            binFile = new BinFile();
            ex = Assert.ThrowsAny<BincopyException>(() =>
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
        public void TestInitFiles()
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
        public void TestMinimumMaximumLength()
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
            Assert.Equal((ulong)0, binFile.Length);

            // Get from a small file
            string inS19Content = File.ReadAllText(GetTestFilePath("in.s19"));
            binFile.AddSrec(inS19Content);

            Assert.Equal((ulong)0, binFile.MinimumAddress);
            Assert.Equal((ulong)70, binFile.MaximumAddress);
            Assert.Equal((ulong)70, binFile.Length);

            // Add a second segment to the file
            binFile.AddBinary(new byte[] { 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01 }, address: 80);

            Assert.Equal((ulong)0, binFile.MinimumAddress);
            Assert.Equal((ulong)89, binFile.MaximumAddress);
            Assert.Equal((ulong)79, binFile.Length);
        }

        [Fact]
        public void TestIterateSegments()
        {
            var binFile = new BinFile();
            string inS19Content = File.ReadAllText(GetTestFilePath("in.s19"));
            binFile.AddSrec(inS19Content);

            int count = 0;
            foreach (var segment in binFile.Segments)
            {
                count++;
            }

            Assert.Equal(1, count);
            Assert.Equal(1, binFile.Segments.Count);
        }

        [Fact]
        public void TestChunksList()
        {
            var binFile = new BinFile();
            binFile.AddBinary(new byte[] { 0x00, 0x00, 0x01, 0x01, 0x02 }, address: 0);
            binFile.AddBinary(new byte[] { 0x04, 0x05, 0x05, 0x06, 0x06, 0x07 }, address: 9);
            binFile.AddBinary(new byte[] { 0x09 }, address: 19);
            binFile.AddBinary(new byte[] { 0x0a }, address: 21);

            byte[] expectedBinary = new byte[]
            {
                0x00, 0x00, 0x01, 0x01, 0x02, 0xff, 0xff, 0xff,
                0xff, 0x04, 0x05, 0x05, 0x06, 0x06, 0x07, 0xff,
                0xff, 0xff, 0xff, 0x09, 0xff, 0x0a
            };
            Assert.Equal(expectedBinary, binFile.AsBinary());

            // Size 8, alignment 1
            var chunks = new List<(ulong, byte[])>();
            foreach (var chunk in binFile.Segments.Chunks(size: 8))
            {
                chunks.Add((chunk.Address, chunk.Data));
            }
            Assert.Equal(4, chunks.Count);
            Assert.Equal((ulong)0, chunks[0].Item1);
            Assert.Equal(new byte[] { 0x00, 0x00, 0x01, 0x01, 0x02 }, chunks[0].Item2);
            Assert.Equal((ulong)9, chunks[1].Item1);
            Assert.Equal(new byte[] { 0x04, 0x05, 0x05, 0x06, 0x06, 0x07 }, chunks[1].Item2);
            Assert.Equal((ulong)19, chunks[2].Item1);
            Assert.Equal(new byte[] { 0x09 }, chunks[2].Item2);
            Assert.Equal((ulong)21, chunks[3].Item1);
            Assert.Equal(new byte[] { 0x0a }, chunks[3].Item2);

            // Size 8, alignment 2
            chunks.Clear();
            foreach (var chunk in binFile.Segments.Chunks(size: 8, alignment: 2))
            {
                chunks.Add((chunk.Address, chunk.Data));
            }
            Assert.Equal(5, chunks.Count);
            Assert.Equal((ulong)0, chunks[0].Item1);
            Assert.Equal(new byte[] { 0x00, 0x00, 0x01, 0x01, 0x02 }, chunks[0].Item2);
            Assert.Equal((ulong)9, chunks[1].Item1);
            Assert.Equal(new byte[] { 0x04 }, chunks[1].Item2);
            Assert.Equal((ulong)10, chunks[2].Item1);
            Assert.Equal(new byte[] { 0x05, 0x05, 0x06, 0x06, 0x07 }, chunks[2].Item2);
            Assert.Equal((ulong)19, chunks[3].Item1);
            Assert.Equal(new byte[] { 0x09 }, chunks[3].Item2);
            Assert.Equal((ulong)21, chunks[4].Item1);
            Assert.Equal(new byte[] { 0x0a }, chunks[4].Item2);

            // Size 8, alignment 4
            chunks.Clear();
            foreach (var chunk in binFile.Segments.Chunks(size: 8, alignment: 4))
            {
                chunks.Add((chunk.Address, chunk.Data));
            }
            Assert.Equal(5, chunks.Count);
            Assert.Equal((ulong)0, chunks[0].Item1);
            Assert.Equal(new byte[] { 0x00, 0x00, 0x01, 0x01, 0x02 }, chunks[0].Item2);
            Assert.Equal((ulong)9, chunks[1].Item1);
            Assert.Equal(new byte[] { 0x04, 0x05, 0x05 }, chunks[1].Item2);
            Assert.Equal((ulong)12, chunks[2].Item1);
            Assert.Equal(new byte[] { 0x06, 0x06, 0x07 }, chunks[2].Item2);
            Assert.Equal((ulong)19, chunks[3].Item1);
            Assert.Equal(new byte[] { 0x09 }, chunks[3].Item2);
            Assert.Equal((ulong)21, chunks[4].Item1);
            Assert.Equal(new byte[] { 0x0a }, chunks[4].Item2);

            // Size 8, alignment 8
            chunks.Clear();
            foreach (var chunk in binFile.Segments.Chunks(size: 8, alignment: 8))
            {
                chunks.Add((chunk.Address, chunk.Data));
            }
            Assert.Equal(4, chunks.Count);
            Assert.Equal((ulong)0, chunks[0].Item1);
            Assert.Equal(new byte[] { 0x00, 0x00, 0x01, 0x01, 0x02 }, chunks[0].Item2);
            Assert.Equal((ulong)9, chunks[1].Item1);
            Assert.Equal(new byte[] { 0x04, 0x05, 0x05, 0x06, 0x06, 0x07 }, chunks[1].Item2);
            Assert.Equal((ulong)19, chunks[2].Item1);
            Assert.Equal(new byte[] { 0x09 }, chunks[2].Item2);
            Assert.Equal((ulong)21, chunks[3].Item1);
            Assert.Equal(new byte[] { 0x0a }, chunks[3].Item2);

            // Size 4, alignment 1
            chunks.Clear();
            foreach (var chunk in binFile.Segments.Chunks(size: 4))
            {
                chunks.Add((chunk.Address, chunk.Data));
            }
            Assert.Equal(6, chunks.Count);
            Assert.Equal((ulong)0, chunks[0].Item1);
            Assert.Equal(new byte[] { 0x00, 0x00, 0x01, 0x01 }, chunks[0].Item2);
            Assert.Equal((ulong)4, chunks[1].Item1);
            Assert.Equal(new byte[] { 0x02 }, chunks[1].Item2);
            Assert.Equal((ulong)9, chunks[2].Item1);
            Assert.Equal(new byte[] { 0x04, 0x05, 0x05, 0x06 }, chunks[2].Item2);
            Assert.Equal((ulong)13, chunks[3].Item1);
            Assert.Equal(new byte[] { 0x06, 0x07 }, chunks[3].Item2);
            Assert.Equal((ulong)19, chunks[4].Item1);
            Assert.Equal(new byte[] { 0x09 }, chunks[4].Item2);
            Assert.Equal((ulong)21, chunks[5].Item1);
            Assert.Equal(new byte[] { 0x0a }, chunks[5].Item2);

            // Size 4, alignment 2
            chunks.Clear();
            foreach (var chunk in binFile.Segments.Chunks(size: 4, alignment: 2))
            {
                chunks.Add((chunk.Address, chunk.Data));
            }
            Assert.Equal(7, chunks.Count);
            Assert.Equal((ulong)0, chunks[0].Item1);
            Assert.Equal(new byte[] { 0x00, 0x00, 0x01, 0x01 }, chunks[0].Item2);
            Assert.Equal((ulong)4, chunks[1].Item1);
            Assert.Equal(new byte[] { 0x02 }, chunks[1].Item2);
            Assert.Equal((ulong)9, chunks[2].Item1);
            Assert.Equal(new byte[] { 0x04 }, chunks[2].Item2);
            Assert.Equal((ulong)10, chunks[3].Item1);
            Assert.Equal(new byte[] { 0x05, 0x05, 0x06, 0x06 }, chunks[3].Item2);
            Assert.Equal((ulong)14, chunks[4].Item1);
            Assert.Equal(new byte[] { 0x07 }, chunks[4].Item2);
            Assert.Equal((ulong)19, chunks[5].Item1);
            Assert.Equal(new byte[] { 0x09 }, chunks[5].Item2);
            Assert.Equal((ulong)21, chunks[6].Item1);
            Assert.Equal(new byte[] { 0x0a }, chunks[6].Item2);

            // Size 4, alignment 4
            chunks.Clear();
            foreach (var chunk in binFile.Segments.Chunks(size: 4, alignment: 4))
            {
                chunks.Add((chunk.Address, chunk.Data));
            }
            Assert.Equal(6, chunks.Count);
            Assert.Equal((ulong)0, chunks[0].Item1);
            Assert.Equal(new byte[] { 0x00, 0x00, 0x01, 0x01 }, chunks[0].Item2);
            Assert.Equal((ulong)4, chunks[1].Item1);
            Assert.Equal(new byte[] { 0x02 }, chunks[1].Item2);
            Assert.Equal((ulong)9, chunks[2].Item1);
            Assert.Equal(new byte[] { 0x04, 0x05, 0x05 }, chunks[2].Item2);
            Assert.Equal((ulong)12, chunks[3].Item1);
            Assert.Equal(new byte[] { 0x06, 0x06, 0x07 }, chunks[3].Item2);
            Assert.Equal((ulong)19, chunks[4].Item1);
            Assert.Equal(new byte[] { 0x09 }, chunks[4].Item2);
            Assert.Equal((ulong)21, chunks[5].Item1);
            Assert.Equal(new byte[] { 0x0a }, chunks[5].Item2);
        }

        [Fact]
        public void TestChunksBadArguments()
        {
            var binFile = new BinFile();

            // Size 4 is not a multiple of alignment 3
            var ex = Assert.Throws<BincopyException>(() =>
            {
                var chunks = binFile.Segments.Chunks(size: 4, alignment: 3).ToList();
            });
            Assert.Equal("size 4 is not a multiple of alignment 3", ex.Message);

            // Size 4 is not a multiple of alignment 8
            ex = Assert.Throws<BincopyException>(() =>
            {
                var chunks = binFile.Segments.Chunks(size: 4, alignment: 8).ToList();
            });
            Assert.Equal("size 4 is not a multiple of alignment 8", ex.Message);

            // Padding must be a word value (size 1)
            ex = Assert.Throws<BincopyException>(() =>
            {
                var chunks = binFile.Segments.Chunks(padding: new byte[] { 0xff, 0xff }).ToList();
            });
            Assert.Equal("padding must be a word value (size 1), got 2 bytes", ex.Message);
        }

        [Fact]
        public void TestSegment()
        {
            var binFile = new BinFile();
            binFile.AddBinary(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 }, address: 2);

            // Size 4, alignment 4
            var chunks = new List<(ulong, byte[])>();
            foreach (var chunk in binFile.Segments[0].Chunks(size: 4, alignment: 4))
            {
                chunks.Add(chunk);
            }
            Assert.Equal(2, chunks.Count);
            Assert.Equal((ulong)2, chunks[0].Item1);
            Assert.Equal(new byte[] { 0x00, 0x01 }, chunks[0].Item2);
            Assert.Equal((ulong)4, chunks[1].Item1);
            Assert.Equal(new byte[] { 0x02, 0x03, 0x04 }, chunks[1].Item2);

            // Bad arguments - size 4 is not a multiple of alignment 8
            var ex = Assert.Throws<BincopyException>(() =>
            {
                var result = binFile.Segments[0].Chunks(size: 4, alignment: 8).ToList();
            });
            Assert.Equal("size 4 is not a multiple of alignment 8", ex.Message);

            // Missing segment
            ex = Assert.Throws<BincopyException>(() =>
            {
                var result = binFile.Segments[1].Chunks(size: 4, alignment: 8).ToList();
            });
            Assert.Equal("segment does not exist", ex.Message);
        }

        [Fact]
        public void TestAddFiles()
        {
            var binFile = new BinFile();
            var binFile12 = new BinFile();
            binFile.AddBinary(new byte[] { 0x00 });
            binFile12.AddBinary(new byte[] { 0x01 }, address: 1);
            
            // Use += operator to add files
            binFile += binFile12;
            
            Assert.Equal(new byte[] { 0x00, 0x01 }, binFile.AsBinary());
        }

        [Fact]
        public void TestExecutionStartAddress()
        {
            var binFile = new BinFile();
            string emptyMainS19 = File.ReadAllText(GetTestFilePath("empty_main.s19"));
            binFile.AddSrec(emptyMainS19);

            Assert.Equal((ulong)0x00400400, binFile.ExecutionStartAddress);

            binFile.ExecutionStartAddress = 0x00400401;
            Assert.Equal((ulong)0x00400401, binFile.ExecutionStartAddress);
        }

        [Fact]
        public void TestAddIhexRecordType3()
        {
            var binFile = new BinFile();
            binFile.AddIhex(":0400000302030405EB");
            
            Assert.Equal((ulong)0x02030405, binFile.ExecutionStartAddress);
        }

        [Fact]
        public void TestAddIhexRecordType5()
        {
            var binFile = new BinFile();
            binFile.AddIhex(":0400000501020304ED");
            
            Assert.Equal((ulong)0x01020304, binFile.ExecutionStartAddress);
        }

        [Fact]
        public void TestAddIhexBadRecordType6()
        {
            var binFile = new BinFile();
            
            var ex = Assert.Throws<InvalidRecordException>(() =>
            {
                binFile.AddIhex(":00000006FA");
            });
            
            Assert.Equal("Expected type 0..5 in record :00000006FA, but got 6", ex.Message);
        }

        [Fact]
        public void TestAsIhexBadAddressLengthBits()
        {
            var binFile = new BinFile();
            binFile.AddBinary(new byte[] { 0x00 });
            
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                binFile.AsIhex(addressLengthBits: 8);
            });
            
            Assert.Equal("Expected address length 16, 24 or 32, but got 8 (Parameter 'AddressLengthBits')", ex.Message);
        }

        [Fact]
        public void TestAsSrecBadAddressLength()
        {
            var binFile = new BinFile();
            
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                binFile.AsSrec(addressLengthBits: 40);
            });
            
            Assert.Equal("Expected address length 16, 24 or 32, but got 40 (Parameter 'AddressLengthBits')", ex.Message);
        }

        [Fact]
        public void TestAsSrecRecord5()
        {
            var binFile = new BinFile();
            
            // Add 65535 bytes of zeros
            binFile.AddBinary(new byte[65535]);
            string records = binFile.AsSrec(numberOfDataBytes: 1);
            
            int lineCount = records.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.Equal(65536, lineCount);
            Assert.Contains("S503FFFFFE", records);
        }

        [Fact]
        public void TestAsSrecRecord6()
        {
            var binFile = new BinFile();
            
            // Add 65536 bytes of zeros
            binFile.AddBinary(new byte[65536]);
            string records = binFile.AsSrec(numberOfDataBytes: 1);
            
            int lineCount = records.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.Equal(65537, lineCount);
            Assert.Contains("S604010000FA", records);
        }

        [Fact]
        public void TestAsSrecRecord8()
        {
            var binFile = new BinFile();
            
            binFile.AddBinary(new byte[] { 0x00 });
            binFile.ExecutionStartAddress = 0x123456;
            string records = binFile.AsSrec(addressLengthBits: 24);
            
            Assert.Equal("S20500000000FA\n" +
                        "S5030001FB\n" +
                        "S8041234565F\n", records);
        }

        [Fact]
        public void TestWordSize()
        {
            var binFile = new BinFile(wordSizeBytes: 2);  // 16 bits = 2 bytes
            
            string in16BitsWord = File.ReadAllText(GetTestFilePath("in_16bits_word.s19"));
            binFile.AddSrec(in16BitsWord);
            
            string out16BitsWord = File.ReadAllText(GetTestFilePath("out_16bits_word.s19"));
            Assert.Equal(out16BitsWord, binFile.AsSrec(30, 24));
        }

        [Fact]
        public void TestWordSizeDefaultPadding()
        {
            var binFile = new BinFile(wordSizeBytes: 2);  // 16 bits = 2 bytes
            
            string inHex = File.ReadAllText(GetTestFilePath("in_16bits_word_padding.hex"));
            binFile.AddIhex(inHex);
            
            byte[] expected = File.ReadAllBytes(GetTestFilePath("out_16bits_word_padding.bin"));
            Assert.Equal(expected, binFile.AsBinary());
        }

        [Fact]
        public void TestIhexCrc()
        {
            Assert.Equal(0x1e, BincopySharp.Utilities.ChecksumCalculator.CalculateIhexChecksum("0300300002337a"));
            Assert.Equal(0, BincopySharp.Utilities.ChecksumCalculator.CalculateIhexChecksum("00000000"));
        }

        [Fact]
        public void TestIssue41()
        {
            var binFile = new BinFile();
            
            string inHex = File.ReadAllText(GetTestFilePath("issue_4_in.hex"));
            binFile.AddIhex(inHex);
            
            string expected = File.ReadAllText(GetTestFilePath("issue_4_out.hex"));
            Assert.Equal(expected, binFile.AsIhex());
        }

        [Fact]
        public void TestIssue42()
        {
            var binFile = new BinFile();
            
            string emptyMainS19 = File.ReadAllText(GetTestFilePath("empty_main.s19"));
            binFile.AddSrec(emptyMainS19);
            
            string expected = File.ReadAllText(GetTestFilePath("empty_main.hex"));
            Assert.Equal(expected, binFile.AsIhex());
        }

        [Fact]
        public void TestNonSortedSegments()
        {
            var binFile = new BinFile();
            
            string nonSorted = File.ReadAllText(GetTestFilePath("non_sorted_segments.s19"));
            binFile.AddSrec(nonSorted);
            
            string expected = File.ReadAllText(GetTestFilePath("non_sorted_segments_merged_and_sorted.s19"));
            Assert.Equal(expected, binFile.AsSrec());
        }

        [Fact]
        public void TestFill()
        {
            var binFile = new BinFile();
            
            // Fill empty file
            binFile.Fill();
            Assert.Equal(Array.Empty<byte>(), binFile.AsBinary());
            
            // Add some data and fill again
            binFile.AddBinary(new byte[] { 0x01, 0x02, 0x03, 0x04 }, address: 0);
            binFile.AddBinary(new byte[] { 0x01, 0x02, 0x03, 0x04 }, address: 8);
            binFile.Fill();
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0xff, 0xff, 0xff, 0xff, 0x01, 0x02, 0x03, 0x04 }, binFile.AsBinary());
        }

        [Fact]
        public void TestFillMaxWords()
        {
            var binFile = new BinFile();
            binFile.AddBinary(new byte[] { 0x01 }, address: 0);
            binFile.AddBinary(new byte[] { 0x02 }, address: 2);
            binFile.AddBinary(new byte[] { 0x03 }, address: 5);
            binFile.AddBinary(new byte[] { 0x04 }, address: 9);
            binFile.Fill(0xaa, maxWords: 2);
            
            Assert.Equal(2, binFile.Segments.Count);
            Assert.Equal((ulong)0, binFile.Segments[0].Address);
            Assert.Equal(new byte[] { 0x01, 0xaa, 0x02, 0xaa, 0xaa, 0x03 }, binFile.Segments[0].Data);
            Assert.Equal((ulong)9, binFile.Segments[1].Address);
            Assert.Equal(new byte[] { 0x04 }, binFile.Segments[1].Data);
        }

        [Fact]
        public void TestHeaderDefaultEncoding()
        {
            var binFile = new BinFile();
            binFile.AddFile(GetTestFilePath("empty_main.s19"));
            
            Assert.Equal("bincopy/empty_main.s19", binFile.Header);
            
            binFile.Header = "bincopy/empty_main.s20";
            Assert.Equal("bincopy/empty_main.s20", binFile.Header);
        }

        [Fact(Skip = "Performance test - takes too long")]
        public void TestPerformance()
        {
            var binFile = new BinFile();
            
            // Add a 1MB consecutive binary
            byte[] chunk = new byte[1024];
            for (int i = 0; i < 1024; i++)
            {
                chunk[i] = (byte)'1';
            }
            
            for (int i = 0; i < 1024; i++)
            {
                binFile.AddBinary(chunk, (ulong)(1024 * i));
            }
            
            Assert.Equal((ulong)0, binFile.MinimumAddress);
            Assert.Equal((ulong)(1024 * 1024), binFile.MaximumAddress);
            
            string ihex = binFile.AsIhex();
            string srec = binFile.AsSrec();
            
            binFile = new BinFile();
            binFile.AddIhex(ihex);
            
            binFile = new BinFile();
            binFile.AddSrec(srec);
        }

        [Fact]
        public void TestVerilogVmem()
        {
            var binFile = new BinFile();
            
            string in8Vmem = File.ReadAllText(GetTestFilePath("in-8.vmem"));
            binFile.AddVerilogVmem(in8Vmem);
            
            Assert.Equal(in8Vmem, binFile.AsVerilogVmem());
            
            binFile = new BinFile(wordSizeBytes: 4);  // 32 bits = 4 bytes
            
            string in32Vmem = File.ReadAllText(GetTestFilePath("in-32.vmem"));
            binFile.AddVerilogVmem(in32Vmem);
            
            Assert.Equal(in32Vmem, binFile.AsVerilogVmem());
            
            binFile = new BinFile();
            
            string emptyMain8Vmem = File.ReadAllText(GetTestFilePath("empty_main-8.vmem"));
            binFile.AddVerilogVmem(emptyMain8Vmem);
            
            byte[] emptyMainBin = File.ReadAllBytes(GetTestFilePath("empty_main.bin"));
            Assert.Equal(emptyMainBin, binFile.AsBinary(padding: 0x00));
        }

        [Fact]
        public void TestSegmentLen()
        {
            int length = 0x100;
            int wordSizeBytes = 1;
            var segment = new Segment(0, (ulong)length, new byte[length], wordSizeBytes);

            Assert.Equal((ulong)length, segment.WordCount);
        }

        [Fact]
        public void TestSegmentLen16()
        {
            int length = 0x100;
            int wordSizeBytes = 2;
            var segment = new Segment(0, (ulong)length, new byte[length * wordSizeBytes], wordSizeBytes);
            
            Assert.Equal((ulong)length, segment.WordCount);
        }

        [Fact]
        public void TestBadWordSize()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                new BinFile(wordSizeBytes: 7);
            });
            
            Assert.Equal("Word size must be 1, 2, 4, or 8 bytes, but got 7 (Parameter 'wordSizeBytes')", ex.Message);
        }

        [Fact]
        public void TestIgnoreBlankLinesHex()
        {
            var binFile = new BinFile();
            
            string inBlankLinesHex = File.ReadAllText(GetTestFilePath("in_blank_lines.hex"));
            binFile.AddIhex(inBlankLinesHex);
            
            string inHex = File.ReadAllText(GetTestFilePath("in.hex"));
            Assert.Equal(inHex, binFile.AsIhex());
        }

        [Fact]
        public void TestIgnoreBlankLinesSrec()
        {
            var binFile = new BinFile();
            
            string inBlankLinesS19 = File.ReadAllText(GetTestFilePath("in_blank_lines.s19"));
            binFile.AddSrec(inBlankLinesS19);
            
            string inS19 = File.ReadAllText(GetTestFilePath("in.s19"));
            Assert.Equal(inS19, binFile.AsSrec(28, 16));
        }

        [Fact]
        public void TestPrint()
        {
            var binFile = new BinFile();
            
            string inS19 = File.ReadAllText(GetTestFilePath("in.s19"));
            binFile.AddSrec(inS19);

            string result = binFile.ToString();
            Assert.NotNull(result);
        }

        [Fact]
        public void TestExcludeEdgeCases()
        {
            var binFile = new BinFile();
            binFile.AddBinary(Encoding.ASCII.GetBytes("1234"), address: 10);
            binFile.Exclude(8, 10);
            binFile.Exclude(14, 15);
            
            Assert.Equal(Encoding.ASCII.GetBytes("1234"), binFile.AsBinary());
            Assert.Equal(1, binFile.Segments.Count);
            
            binFile.Exclude(8, 11);
            binFile.Exclude(13, 15);
            
            Assert.Equal(Encoding.ASCII.GetBytes("23"), binFile.AsBinary());
            Assert.Equal(1, binFile.Segments.Count);
        }

        [Fact]
        public void TestLayoutEmptyMain()
        {
            var binFile = new BinFile();
            binFile.AddFile(GetTestFilePath("empty_main.s19"));
            
            string expected = "0x400238                                                                0x601038\n" +
                            "-                                                                              -\n";
            Assert.Equal(expected, binFile.Layout());
        }

        [Fact]
        public void TestLayoutOut()
        {
            var binFile = new BinFile();
            binFile.AddFile(GetTestFilePath("out.hex"));
            
            string expected = "0x0                                                                        0x403\n" +
                            "=====-               -====-                                                    -\n";
            Assert.Equal(expected, binFile.Layout());
        }

        [Fact]
        public void TestLayoutInExclude24()
        {
            var binFile = new BinFile();
            binFile.AddFile(GetTestFilePath("in_exclude_2_4.s19"));
            
            string expected = "0x0                                                               0x46\n" +
                            "==  ==================================================================\n";
            Assert.Equal(expected, binFile.Layout());
        }

        [Fact]
        public void TestOverwrite()
        {
            var binFile = new BinFile();

            // Overwrite in empty file.
            binFile.AddBinary(Encoding.ASCII.GetBytes("1234"), address: 512, overwrite: true);
            Assert.Equal(Encoding.ASCII.GetBytes("1234"), binFile.AsBinary(minimumAddress: 512));

            // Test setting data with multiple existing segments.
            binFile.AddBinary(Encoding.ASCII.GetBytes("123456"), address: 1024);
            binFile.AddBinary(Encoding.ASCII.GetBytes("99"), address: 1026, overwrite: true);
            Assert.Equal(
                Encoding.ASCII.GetBytes("1234")
                    .Concat(Enumerable.Repeat((byte)0xff, 508))
                    .Concat(Encoding.ASCII.GetBytes("129956"))
                    .ToArray(),
                binFile.AsBinary(minimumAddress: 512));

            // Test setting data crossing the original segment limits.
            binFile.AddBinary(Encoding.ASCII.GetBytes("abc"), address: 1022, overwrite: true);
            binFile.AddBinary(Encoding.ASCII.GetBytes("def"), address: 1029, overwrite: true);
            Assert.Equal(
                Encoding.ASCII.GetBytes("1234")
                    .Concat(Enumerable.Repeat((byte)0xff, 506))
                    .Concat(Encoding.ASCII.GetBytes("abc2995def"))
                    .ToArray(),
                binFile.AsBinary(minimumAddress: 512));

            // Overwrite a segment and write outside it.
            binFile.AddBinary(Encoding.ASCII.GetBytes("111111111111"), address: 1021, overwrite: true);
            Assert.Equal(
                Encoding.ASCII.GetBytes("1234")
                    .Concat(Enumerable.Repeat((byte)0xff, 505))
                    .Concat(Encoding.ASCII.GetBytes("111111111111"))
                    .ToArray(),
                binFile.AsBinary(minimumAddress: 512));

            // Overwrite multiple segments (all segments in this test).
            byte[] ones = Enumerable.Repeat((byte)'1', 1024).ToArray();
            binFile.AddBinary(ones, address: 256, overwrite: true);
            Assert.Equal(ones, binFile.AsBinary(minimumAddress: 256));
        }

        [Fact]
        public void TestSetGetItem()
        {
            var binFile = new BinFile();

            binFile.AddBinary(new byte[] { 0x01, 0x02, 0x03, 0x04 }, address: 1);

            // Get all data
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, binFile.AsBinary());

            // Address 0 is out of range
            Assert.ThrowsAny<Exception>(() => { var _ = binFile[0]; });

            Assert.Equal(1, binFile[1]);
            Assert.Equal(2, binFile[2]);
            Assert.Equal(3, binFile[3]);
            Assert.Equal(4, binFile[4]);

            // Address 5 is out of range
            Assert.ThrowsAny<Exception>(() => { var _ = binFile[5]; });

            // Range [3, 5)
            Assert.Equal(new byte[] { 0x03, 0x04 }, binFile.AsBinary(minimumAddress: 3, maximumAddress: 5));
            // Range [3, 6) — clipped to actual data
            Assert.Equal(new byte[] { 0x03, 0x04 }, binFile.AsBinary(minimumAddress: 3, maximumAddress: 6));

            // Set range [1, 3)
            binFile.SetRange(1, new byte[] { 0x05, 0x06 });
            Assert.Equal(new byte[] { 0x05, 0x06, 0x03, 0x04 }, binFile.AsBinary());

            // Set from address 3 onwards
            binFile.SetRange(3, new byte[] { 0x07, 0x08, 0x09 });
            Assert.Equal(new byte[] { 0x05, 0x06, 0x07, 0x08, 0x09 }, binFile.AsBinary());

            // Set range [3, 5)
            binFile.SetRange(3, new byte[] { 0x0a, 0x0b });
            Assert.Equal(new byte[] { 0x05, 0x06, 0x0a, 0x0b, 0x09 }, binFile.AsBinary());

            // Set single byte at address 2
            binFile.SetRange(2, new byte[] { 0x0c });
            Assert.Equal(new byte[] { 0x05, 0x0c, 0x0a, 0x0b, 0x09 }, binFile.AsBinary());

            // Set all data from minimum address
            binFile.SetRange(binFile.MinimumAddress, new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 });
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 }, binFile.AsBinary());

            // Set single byte at address 0 (extends data)
            binFile[0] = 0;
            Assert.Equal(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 }, binFile.AsBinary());

            // Set single byte at address 7 (creates gap)
            binFile[7] = 7;
            Assert.Equal(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0xff, 0x07 }, binFile.AsBinary());
            Assert.Equal(255, binFile[6]);
            Assert.Equal(new byte[] { 0xff }, binFile.AsBinary(minimumAddress: 6, maximumAddress: 7));
            Assert.Equal(new byte[] { 0xff, 0x07 }, binFile.AsBinary(minimumAddress: 6, maximumAddress: 8));
            Assert.Equal(new byte[] { 0x05, 0xff, 0x07 }, binFile.AsBinary(minimumAddress: 5, maximumAddress: 8));

            // Add data at high address to test get performance.
            binFile[0x10000000] = 0x12;
            Assert.Equal(new byte[] { 0xff, 0x12 },
                binFile.AsBinary(minimumAddress: 0x10000000UL - 1));
        }

        [Fact]
        public void TestSetGetItem16()
        {
            var binFile = new BinFile(wordSizeBytes: 2);

            binFile.AddBinary(new byte[] { 0x01, 0x02, 0x03, 0x04 }, address: 1);

            // Get all data
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, binFile.AsBinary());

            // Address 0 is out of range
            Assert.ThrowsAny<Exception>(() => { var _ = binFile[0]; });

            // Word at address 1 = {0x01, 0x02}, word at address 2 = {0x03, 0x04}
            Assert.Equal(new byte[] { 0x01, 0x02 }, binFile.AsBinary(minimumAddress: 1, maximumAddress: 2));
            Assert.Equal(new byte[] { 0x03, 0x04 }, binFile.AsBinary(minimumAddress: 2, maximumAddress: 3));

            // Address 3 is out of range
            Assert.ThrowsAny<Exception>(() => { var _ = binFile[3]; });

            // Range [1, 3)
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, binFile.AsBinary(minimumAddress: 1, maximumAddress: 3));
            // Range [1, 4) — clipped to actual data
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, binFile.AsBinary(minimumAddress: 1, maximumAddress: 4));

            // Set range [1, 2)
            binFile.SetRange(1, new byte[] { 0x05, 0x06 });
            Assert.Equal(new byte[] { 0x05, 0x06, 0x03, 0x04 }, binFile.AsBinary());

            // Set from address 2 onwards
            binFile.SetRange(2, new byte[] { 0x07, 0x08, 0x09, 0xa0 });
            Assert.Equal(new byte[] { 0x05, 0x06, 0x07, 0x08, 0x09, 0xa0 }, binFile.AsBinary());

            // Set word at address 5 (creates gap)
            binFile.SetRange(5, new byte[] { 0x17, 0x18 });
            Assert.Equal(new byte[] { 0x05, 0x06, 0x07, 0x08, 0x09, 0xa0, 0xff, 0xff, 0x17, 0x18 }, binFile.AsBinary());
            Assert.Equal(new byte[] { 0xff, 0xff }, binFile.AsBinary(minimumAddress: 4, maximumAddress: 5));
            Assert.Equal(new byte[] { 0x09, 0xa0, 0xff, 0xff, 0x17, 0x18 }, binFile.AsBinary(minimumAddress: 3, maximumAddress: 8));
        }

        [Fact]
        public void TestWordSizeCustomPadding()
        {
            var binFile = new BinFile(wordSizeBytes: 2);  // 16 bits = 2 bytes
            
            string inHex = File.ReadAllText(GetTestFilePath("in_16bits_word_padding.hex"));
            binFile.AddIhex(inHex);
            
            byte[] expected = File.ReadAllBytes(GetTestFilePath("out_16bits_word_padding_0xff00.bin"));
            Assert.Equal(expected, binFile.AsBinary(null, null, new byte[] { 0xff, 0x00 }));
        }

        [Fact]
        public void TestFillWordSize16()
        {
            var binFile = new BinFile(wordSizeBytes: 2);  // 16 bits = 2 bytes
            binFile.AddBinary(new byte[] { 0x01, 0x02 }, address: 0);
            binFile.AddBinary(new byte[] { 0x03, 0x04 }, address: 2);
            binFile.AddBinary(new byte[] { 0x05, 0x06 }, address: 5);
            binFile.AddBinary(new byte[] { 0x07, 0x08 }, address: 9);
            binFile.Fill(new byte[] { 0xaa, 0xaa }, maxWords: 2);
            
            Assert.Equal(2, binFile.Segments.Count);
            Assert.Equal((ulong)0, binFile.Segments[0].Address);
            Assert.Equal(new byte[] { 0x01, 0x02, 0xaa, 0xaa, 0x03, 0x04, 0xaa, 0xaa, 0xaa, 0xaa, 0x05, 0x06 }, 
                        binFile.Segments[0].Data);
            Assert.Equal((ulong)9, binFile.Segments[1].Address);
            Assert.Equal(new byte[] { 0x07, 0x08 }, binFile.Segments[1].Data);

            // Fill the rest with the default value.
            binFile.Fill();
            Assert.Equal(1, binFile.Segments.Count);
            Assert.Equal(
                new byte[] { 0x01, 0x02, 0xaa, 0xaa, 0x03, 0x04, 0xaa, 0xaa, 0xaa, 0xaa, 0x05, 0x06, 0xff, 0xff, 0xff, 0xff,
                            0xff, 0xff, 0x07, 0x08 },
                binFile.AsBinary());
        }

        [Fact]
        public void TestChunkPadding()
        {
            string records = ":02000004000AF0\n" +
                           ":10B8440000000000000000009630000007770000B0\n";
            var hexfile = new BinFile();
            hexfile.AddIhex(records);
            int align = 8;
            int size = 16;
            var chunks = hexfile.Segments.Chunks(size: size, alignment: align, padding: new byte[] { 0xff }).ToList();
            
            Assert.All(chunks, c => Assert.Equal(0UL, c.Address % (ulong)align));
            
            Assert.All(chunks, c => Assert.Equal(0, c.Data.Length % align));
        }

        [Fact]
        public void TestMergeChunks()
        {
            string records = ":0A0000001010101010101010101056\n" +
                           ":0A000E001010101010101010101048\n";
            var hexfile = new BinFile();
            hexfile.AddIhex(records);
            int align = 8;
            int size = 16;
            var chunks = hexfile.Segments.Chunks(size: size, alignment: align, padding: new byte[] { 0xff }).ToList();
            
            var lastChunk = chunks[chunks.Count - 1];
            Assert.Equal(8UL, lastChunk.Address);
            Assert.Equal(new byte[] { 0x10, 0x10, 0xff, 0xff, 0xff, 0xff, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10 }, 
                        lastChunk.Data);
        }

        [Fact]
        public void TestMergeChunks16()
        {
            string records = ":1000000010101010101010101010101010101010F0\n" +
                           ":10000A0010101010101010101010101010101010E6\n";
            var hexfile = new BinFile(wordSizeBytes: 2);
            hexfile.AddIhex(records);
            int align = 6;
            int size = 12;
            var chunks = hexfile.Segments.Chunks(size: size, alignment: align, padding: new byte[] { 0xff, 0xff }).ToList();
            
            var lastChunk = chunks[chunks.Count - 1];
            Assert.Equal(6UL, lastChunk.Address);
            Assert.Equal(new byte[] { 0x10, 0x10, 0x10, 0x10, 0xff, 0xff, 0xff, 0xff, 0x10, 0x10, 0x10, 0x10, 
                                     0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10 }, 
                        lastChunk.Data);
        }

        [Fact]
        public void TestHeaderNoEncoding()
        {
            var binfile = new BinFile(headerEncoding: null);
            binfile.AddFile(GetTestFilePath("empty_main.s19"));

            Assert.Equal(System.Text.Encoding.UTF8.GetBytes("bincopy/empty_main.s19"), (byte[])binfile.Header);

            binfile.Header = System.Text.Encoding.UTF8.GetBytes("bincopy/empty_main.s20");
            Assert.Equal(System.Text.Encoding.UTF8.GetBytes("bincopy/empty_main.s20"), (byte[])binfile.Header);

            binfile.Header = new byte[] { 0x01, 0x80, 0x88, 0xaa, 0x90 };
            Assert.Equal(new byte[] { 0x01, 0x80, 0x88, 0xaa, 0x90 }, (byte[])binfile.Header);

            var ex = Assert.Throws<ArgumentException>(() => binfile.Header = "bincopy/empty_main.s21");
            Assert.Equal("expected a byte array, but got System.String", ex.Message);
        }

        [Fact]
        public void TestSrecNoHeaderEncoding()
        {
            var binfile = new BinFile(headerEncoding: null);

            binfile.AddSrec("S0080000018088AA90B4");

            Assert.Equal("S0080000018088AA90B4", binfile.AsSrec().Split('\n')[0]);
        }

        [Fact]
        public void TestAddMicrochipHexRecord()
        {
            var binfile = new BinFile();
            binfile.AddMicrochipHex(":02000E00E4C943");
            Assert.Equal(0x0007UL, binfile.MinimumAddress);

            byte[] data = binfile.AsBinary(minimumAddress: binfile.MinimumAddress, 
                                          maximumAddress: binfile.MinimumAddress + 1);
            ushort firstWord = BitConverter.ToUInt16(data, 0);
            Assert.Equal(0xC9E4, firstWord);
        }

        [Fact]
        public void TestMicrochipHex()
        {
            var binfile = new BinFile();

            string content = File.ReadAllText("TestFiles/in.hex");
            binfile.AddMicrochipHex(content);

            string content2 = File.ReadAllText("TestFiles/in.hex");
            Assert.Equal(binfile.AsMicrochipHex(), content2);

            // Add and overwrite the data.
            binfile = new BinFile();
            binfile.AddMicrochipHexFile("TestFiles/in.hex");
            binfile.AddMicrochipHexFile("TestFiles/in.hex", overwrite: true);

            string content3 = File.ReadAllText("TestFiles/in.hex");
            Assert.Equal(binfile.AsMicrochipHex(), content3);
        }

        [Fact]
        public void TestAddElf()
        {
            var bf = new BinFile();
            bf.AddElfFile("TestFiles/elf.out");

            string expected = File.ReadAllText("TestFiles/elf.s19");
            Assert.Equal(expected, bf.AsSrec());
        }

        [Fact]
        public void TestAddElfBlinky()
        {
            var bf = new BinFile();
            bf.AddElfFile("TestFiles/evkbimxrt1050_iled_blinky_sdram.axf");
            string actualSrec = bf.AsSrec();

            bf = new BinFile();
            bf.AddSrecFile("TestFiles/evkbimxrt1050_iled_blinky_sdram.s19");
            string expectedSrec = bf.AsSrec();

            Assert.Equal(expectedSrec, actualSrec);
        }

        [Fact]
        public void TestAddElfGcc()
        {
            var bf = new BinFile();
            bf.AddElfFile("TestFiles/elf/gcc.elf");

            byte[] expected = File.ReadAllBytes("TestFiles/elf/gcc.bin");
            Assert.Equal(expected, bf.AsBinary());
        }

        [Fact]
        public void TestAddElfIar()
        {
            var bf = new BinFile();
            bf.AddElfFile("TestFiles/elf/iar.out");

            byte[] expected = File.ReadAllBytes("TestFiles/elf/iar.bin");
            Assert.Equal(expected, bf.AsBinary());
        }

        [Fact]
        public void TestAddElfKeil()
        {
            var bf = new BinFile();
            bf.AddElfFile("TestFiles/elf/keil.out");

            byte[] expected = File.ReadAllBytes("TestFiles/elf/keil.bin");
            Assert.Equal(expected, bf.AsBinary());
        }

        [Fact]
        public void TestAddElfModifyOverwrite()
        {
            var bf = new BinFile();
            bf.AddElfFile("TestFiles/elf.out");

            byte[] data = "test"u8.ToArray();
            ulong address = bf.MinimumAddress;
            bf.AddBinary(data, address: address, overwrite: true);
            Assert.Equal(data, bf.AsBinary(minimumAddress: address, maximumAddress: address + (ulong)data.Length));
        }
    }
}

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
    /// Port of test_bincopy.py from Python bincopy library.
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

        // Python: test_srec
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
            
            Assert.Contains("expected crc '25'", ex.Message);
            Assert.Contains("but got '22'", ex.Message);
            Assert.Contains("S2144002640000000002000000060000001800000022", ex.Message);
        }

        // Python: test_bad_srec
        [Fact]
        public void TestBadSrec()
        {
            // Note: pack_srec and unpack_srec are internal utilities in C#
            // These tests verify error handling through the public API
            
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
            Assert.Contains("expected record type 0..3 or 5..9", exBadType.Message);
            Assert.Contains("but got '.'", exBadType.Message);

            // Bad CRC
            var ex = Assert.ThrowsAny<BincopyException>(() =>
            {
                binFile.AddSrec("S1020011");
            });
            Assert.Contains("expected crc 'FD'", ex.Message);
            Assert.Contains("but got '11'", ex.Message);
        }

        // Python: test_ti_txt
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

        // Python: test_bad_ti_txt
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
                Assert.Contains(expectedMessage, ex.Message, StringComparison.OrdinalIgnoreCase);
            }
        }

        // Python: test_compare_ti_txt
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
            Assert.Contains("Cannot parse empty Intel HEX data", ex1.Message);

            // Bad first character
            var ex2 = Assert.ThrowsAny<BincopyException>(() =>
            {
                binFile.AddIhex(".0011110022");
            });
            Assert.Contains("not starting with a ':'", ex2.Message);

            // Bad checksum
            var ex3 = Assert.ThrowsAny<BincopyException>(() =>
            {
                binFile.AddIhex(":0011110022");
            });
            Assert.Contains("Expected checksum 'DE'", ex3.Message);
            Assert.Contains("but got '22'", ex3.Message);
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

        // Python: test_i8hex_address_above_64k
        [Fact]
        public void TestI8hexAddressAbove64k()
        {
            var binFile = new BinFile();
            binFile.AddBinary(new byte[] { 0x00 }, address: 65536);

            var ex = Assert.Throws<BincopyException>(() =>
            {
                binFile.AsIhex(addressLengthBits: 16);
            });

            Assert.Contains("cannot address more than 64 kB in I8HEX files (16 bits addresses)", ex.Message);
        }

        // Python: test_i16hex
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

        // Python: test_i16hex_address_above_1meg
        [Fact]
        public void TestI16hexAddressAbove1meg()
        {
            var binFile = new BinFile();
            binFile.AddBinary(new byte[] { 0x00 }, address: 17 * 65535 + 1);

            var ex = Assert.Throws<BincopyException>(() =>
            {
                binFile.AsIhex(addressLengthBits: 24);
            });

            Assert.Contains("cannot address more than 1 MB in I16HEX files (20 bits addresses)", ex.Message);
        }

        // Python: test_i32hex
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

        // Python: test_i32hex_address_above_4gig
        [Fact]
        public void TestI32hexAddressAbove4gig()
        {
            var binFile = new BinFile();
            binFile.AddBinary(new byte[] { 0x00 }, address: 0x100000000UL);

            var ex = Assert.Throws<BincopyException>(() =>
            {
                binFile.AsIhex(addressLengthBits: 32);
            });

            Assert.Contains("cannot address more than 4 GB in I32HEX files (32 bits addresses)", ex.Message);
        }

        // Python: test_binary
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

        // Python: test_add_file
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

        // Python: test_array
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

        // Python: test_hexdump_1
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

        // Python: test_hexdump_2
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

        // Python: test_hexdump_gaps
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

        // Python: test_hexdump_empty
        [Fact]
        public void TestHexdumpEmpty()
        {
            var binFile = new BinFile();
            Assert.Equal("\n", binFile.AsHexdump());
        }

        // Python: test_srec_ihex_binary
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

        // Python: test_exclude_crop (part 1 - exclude tests)
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

        // Python: test_exclude_crop (part 2 - crop tests)
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

        // Python: test_segments_list
        [Fact]
        public void TestSegmentsList()
        {
            var binFile = new BinFile();
            binFile.AddBinary(new byte[] { 0x00 }, address: 0);
            binFile.AddBinary(new byte[] { 0x01, 0x02 }, address: 5);
            binFile.AddBinary(new byte[] { 0x03, 0x04, 0x05 }, address: 12);

            var segments = new List<(ulong Address, byte[] Data)>();
            foreach (var segment in binFile.Segments)
            {
                segments.Add((segment.MinimumAddress, segment.Data));
            }

            Assert.Equal(3, segments.Count);
            Assert.Equal((ulong)0, segments[0].Address);
            Assert.Equal(new byte[] { 0x00 }, segments[0].Data);
            Assert.Equal((ulong)5, segments[1].Address);
            Assert.Equal(new byte[] { 0x01, 0x02 }, segments[1].Data);
            Assert.Equal((ulong)12, segments[2].Address);
            Assert.Equal(new byte[] { 0x03, 0x04, 0x05 }, segments[2].Data);
        }

        // Python: test_info
        [Fact]
        public void TestInfo()
        {
            var binFile = new BinFile();
            binFile.AddBinary(new byte[] { 0x00 }, address: 0);
            binFile.AddBinary(new byte[] { 0x01, 0x02 }, address: 5);
            binFile.AddBinary(new byte[] { 0x03, 0x04, 0x05 }, address: 12);

            string info = binFile.Info();

            Assert.Contains("Data ranges:", info);
            Assert.Contains("0x00000000 - 0x00000001", info);
            Assert.Contains("0x00000005 - 0x00000007", info);
            Assert.Contains("0x0000000c - 0x0000000f", info);
        }

        // Python: test_binary_16
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

        // Python: test_add
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
            Assert.Contains("not starting with an 'S'", ex.Message);
            Assert.Contains("invalid data", ex.Message);

            // Test 5: Intel HEX with invalid data after valid record
            binFile = new BinFile();
            ex = Assert.ThrowsAny<BincopyException>(() =>
            {
                binFile.Add(":020000040040BA\n" +
                           "invalid data");
            });
            Assert.Contains("not starting with a ':'", ex.Message);
            Assert.Contains("invalid data", ex.Message);

            // Test 6: Junk data
            Assert.Throws<UnsupportedFileFormatException>(() =>
            {
                binFile.Add("junk");
            });
        }

        // Python: test_init_files
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

        // Python: test_minimum_maximum_length
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

        // Python: test_iterate_segments
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

        // Python: test_chunks_list
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

        // Python: test_chunks_bad_arguments
        [Fact]
        public void TestChunksBadArguments()
        {
            var binFile = new BinFile();

            // Size 4 is not a multiple of alignment 3
            var ex = Assert.Throws<BincopyException>(() =>
            {
                var chunks = binFile.Segments.Chunks(size: 4, alignment: 3).ToList();
            });
            Assert.Contains("size 4 is not a multiple of alignment 3", ex.Message);

            // Size 4 is not a multiple of alignment 8
            ex = Assert.Throws<BincopyException>(() =>
            {
                var chunks = binFile.Segments.Chunks(size: 4, alignment: 8).ToList();
            });
            Assert.Contains("size 4 is not a multiple of alignment 8", ex.Message);

            // Padding must be a word value (size 1)
            ex = Assert.Throws<BincopyException>(() =>
            {
                var chunks = binFile.Segments.Chunks(padding: new byte[] { 0xff, 0xff }).ToList();
            });
            Assert.Contains("padding must be a word value (size 1)", ex.Message);
        }

        // Python: test_segment
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
            Assert.Contains("size 4 is not a multiple of alignment 8", ex.Message);

            // Missing segment
            ex = Assert.Throws<BincopyException>(() =>
            {
                var result = binFile.Segments[1].Chunks(size: 4, alignment: 8).ToList();
            });
            Assert.Contains("segment does not exist", ex.Message);
        }

        // Python: test_add_files
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

        // Python: test_execution_start_address
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

        // Python: test_add_ihex_record_type_3
        [Fact]
        public void TestAddIhexRecordType3()
        {
            var binFile = new BinFile();
            binFile.AddIhex(":0400000302030405EB");
            
            Assert.Equal((ulong)0x02030405, binFile.ExecutionStartAddress);
        }

        // Python: test_add_ihex_record_type_5
        [Fact]
        public void TestAddIhexRecordType5()
        {
            var binFile = new BinFile();
            binFile.AddIhex(":0400000501020304ED");
            
            Assert.Equal((ulong)0x01020304, binFile.ExecutionStartAddress);
        }

        // Python: test_add_ihex_bad_record_type_6
        [Fact]
        public void TestAddIhexBadRecordType6()
        {
            var binFile = new BinFile();
            
            var ex = Assert.Throws<InvalidRecordException>(() =>
            {
                binFile.AddIhex(":00000006FA");
            });
            
            Assert.Contains("expected type", ex.Message.ToLower());
            Assert.Contains("but got 6", ex.Message);
        }

        // Python: test_as_ihex_bad_address_length_bits
        [Fact]
        public void TestAsIhexBadAddressLengthBits()
        {
            var binFile = new BinFile();
            binFile.AddBinary(new byte[] { 0x00 });
            
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                binFile.AsIhex(addressLengthBits: 8);
            });
            
            Assert.Contains("expected address length 16, 24 or 32, but got 8", ex.Message.ToLower());
        }

        // Python: test_as_srec_bad_address_length
        [Fact]
        public void TestAsSrecBadAddressLength()
        {
            var binFile = new BinFile();
            
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                binFile.AsSrec(addressLengthBits: 40);
            });
            
            // C# validates address length directly (better than Python which calculates type first)
            // Python: type_ = str((40 // 8) - 1) = '4', then raises "expected data record type 1..3, but got 4"
            // C#: validates address length directly, raises "Expected address length 16, 24 or 32, but got 40"
            Assert.Contains("Expected address length 16, 24 or 32, but got 40", ex.Message);
        }

        // Python: test_as_srec_record_5
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

        // Python: test_as_srec_record_6
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

        // Python: test_as_srec_record_8
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

        // Python: test_word_size
        [Fact]
        public void TestWordSize()
        {
            var binFile = new BinFile(wordSizeBytes: 2);  // 16 bits = 2 bytes
            
            string in16BitsWord = File.ReadAllText(GetTestFilePath("in_16bits_word.s19"));
            binFile.AddSrec(in16BitsWord);
            
            string out16BitsWord = File.ReadAllText(GetTestFilePath("out_16bits_word.s19"));
            Assert.Equal(out16BitsWord, binFile.AsSrec(30, 24));
        }

        // Python: test_word_size_default_padding
        [Fact]
        public void TestWordSizeDefaultPadding()
        {
            var binFile = new BinFile(wordSizeBytes: 2);  // 16 bits = 2 bytes
            
            string inHex = File.ReadAllText(GetTestFilePath("in_16bits_word_padding.hex"));
            binFile.AddIhex(inHex);
            
            byte[] expected = File.ReadAllBytes(GetTestFilePath("out_16bits_word_padding.bin"));
            Assert.Equal(expected, binFile.AsBinary());
        }

        // Python: test_ihex_crc
        [Fact]
        public void TestIhexCrc()
        {
            Assert.Equal(0x1e, BincopySharp.Utilities.ChecksumCalculator.CalculateIhexChecksum("0300300002337a"));
            Assert.Equal(0, BincopySharp.Utilities.ChecksumCalculator.CalculateIhexChecksum("00000000"));
        }

        // Python: test_issue_4_1
        [Fact]
        public void TestIssue41()
        {
            var binFile = new BinFile();
            
            string inHex = File.ReadAllText(GetTestFilePath("issue_4_in.hex"));
            binFile.AddIhex(inHex);
            
            string expected = File.ReadAllText(GetTestFilePath("issue_4_out.hex"));
            Assert.Equal(expected, binFile.AsIhex());
        }

        // Python: test_issue_4_2
        [Fact]
        public void TestIssue42()
        {
            var binFile = new BinFile();
            
            string emptyMainS19 = File.ReadAllText(GetTestFilePath("empty_main.s19"));
            binFile.AddSrec(emptyMainS19);
            
            string expected = File.ReadAllText(GetTestFilePath("empty_main.hex"));
            Assert.Equal(expected, binFile.AsIhex());
        }

        // Python: test_non_sorted_segments
        [Fact]
        public void TestNonSortedSegments()
        {
            var binFile = new BinFile();
            
            string nonSorted = File.ReadAllText(GetTestFilePath("non_sorted_segments.s19"));
            binFile.AddSrec(nonSorted);
            
            string expected = File.ReadAllText(GetTestFilePath("non_sorted_segments_merged_and_sorted.s19"));
            Assert.Equal(expected, binFile.AsSrec());
        }

        // Python: test_fill
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

        // Python: test_fill_max_words
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

        // Python: test_header_default_encoding
        [Fact]
        public void TestHeaderDefaultEncoding()
        {
            var binFile = new BinFile();
            binFile.AddFile(GetTestFilePath("empty_main.s19"));
            
            Assert.Equal("bincopy/empty_main.s19", binFile.Header);
            
            binFile.Header = "bincopy/empty_main.s20";
            Assert.Equal("bincopy/empty_main.s20", binFile.Header);
        }

        // Python: test_performance
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

        // Python: test_verilog_vmem
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

        // Python: test_segment_len
        [Fact]
        public void TestSegmentLen()
        {
            int length = 0x100;
            int wordSizeBytes = 1;
            var segment = new Segment(0, (ulong)length, new byte[length], wordSizeBytes);
            
            // Python's len(segment) returns number of words
            // When wordSizeBytes=1, WordCount == Length
            Assert.Equal((ulong)length, segment.WordCount);
        }

        // Python: test_segment_len_16
        [Fact]
        public void TestSegmentLen16()
        {
            int length = 0x100;
            int wordSizeBytes = 2;
            var segment = new Segment(0, (ulong)length, new byte[length * wordSizeBytes], wordSizeBytes);
            
            // Python's len(segment) returns number of words, not bytes
            Assert.Equal((ulong)length, segment.WordCount);
        }

        // Python: test_bad_word_size
        [Fact]
        public void TestBadWordSize()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                new BinFile(wordSizeBytes: 7);
            });
            
            Assert.Contains("Word size must be 1, 2, 4, or 8 bytes", ex.Message);
            Assert.Contains("but got 7", ex.Message);
        }

        // Python: test_ignore_blank_lines_hex
        [Fact]
        public void TestIgnoreBlankLinesHex()
        {
            var binFile = new BinFile();
            
            string inBlankLinesHex = File.ReadAllText(GetTestFilePath("in_blank_lines.hex"));
            binFile.AddIhex(inBlankLinesHex);
            
            string inHex = File.ReadAllText(GetTestFilePath("in.hex"));
            Assert.Equal(inHex, binFile.AsIhex());
        }

        // Python: test_ignore_blank_lines_srec
        [Fact]
        public void TestIgnoreBlankLinesSrec()
        {
            var binFile = new BinFile();
            
            string inBlankLinesS19 = File.ReadAllText(GetTestFilePath("in_blank_lines.s19"));
            binFile.AddSrec(inBlankLinesS19);
            
            string inS19 = File.ReadAllText(GetTestFilePath("in.s19"));
            Assert.Equal(inS19, binFile.AsSrec(28, 16));
        }

        // Python: test_print
        [Fact]
        public void TestPrint()
        {
            var binFile = new BinFile();
            
            string inS19 = File.ReadAllText(GetTestFilePath("in.s19"));
            binFile.AddSrec(inS19);
            
            // Python test just prints the binfile (calls __str__)
            // In C# we verify ToString() doesn't throw
            string result = binFile.ToString();
            Assert.NotNull(result);
        }

        // Python: test_exclude_edge_cases
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

        // Python: test_layout_empty_main
        [Fact]
        public void TestLayoutEmptyMain()
        {
            var binFile = new BinFile();
            binFile.AddFile(GetTestFilePath("empty_main.s19"));
            
            // Python: "0x400238" + 64 spaces + "0x601038" = 80 chars
            // Python: "-" + 78 spaces + "-" = 80 chars
            string expected = "0x400238                                                                0x601038\n" +
                            "-                                                                              -\n";
            Assert.Equal(expected, binFile.Layout());
        }

        // Python: test_layout_out
        [Fact]
        public void TestLayoutOut()
        {
            var binFile = new BinFile();
            binFile.AddFile(GetTestFilePath("out.hex"));
            
            string expected = "0x0                                                                        0x403\n" +
                            "=====-               -====-                                                    -\n";
            Assert.Equal(expected, binFile.Layout());
        }

        // Python: test_layout_in_exclude_2_4
        [Fact]
        public void TestLayoutInExclude24()
        {
            var binFile = new BinFile();
            binFile.AddFile(GetTestFilePath("in_exclude_2_4.s19"));
            
            string expected = "0x0                                                               0x46\n" +
                            "==  ==================================================================\n";
            Assert.Equal(expected, binFile.Layout());
        }

        // Python: test_overwrite
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

        // Python: test_set_get_item
        [Fact]
        public void TestSetGetItem()
        {
            var binFile = new BinFile();

            binFile.AddBinary(new byte[] { 0x01, 0x02, 0x03, 0x04 }, address: 1);

            // binfile[:] == b'\x01\x02\x03\x04'
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, binFile.AsBinary());

            // binfile[0] raises IndexError
            Assert.ThrowsAny<Exception>(() => { var _ = binFile[0]; });

            Assert.Equal(1, binFile[1]);
            Assert.Equal(2, binFile[2]);
            Assert.Equal(3, binFile[3]);
            Assert.Equal(4, binFile[4]);

            // binfile[5] raises IndexError
            Assert.ThrowsAny<Exception>(() => { var _ = binFile[5]; });

            // binfile[3:5] == b'\x03\x04'
            Assert.Equal(new byte[] { 0x03, 0x04 }, binFile.AsBinary(minimumAddress: 3, maximumAddress: 5));
            // binfile[3:6] == b'\x03\x04'
            Assert.Equal(new byte[] { 0x03, 0x04 }, binFile.AsBinary(minimumAddress: 3, maximumAddress: 6));

            // binfile[1:3] = b'\x05\x06'
            binFile.SetRange(1, new byte[] { 0x05, 0x06 });
            Assert.Equal(new byte[] { 0x05, 0x06, 0x03, 0x04 }, binFile.AsBinary());

            // binfile[3:] = b'\x07\x08\x09'
            binFile.SetRange(3, new byte[] { 0x07, 0x08, 0x09 });
            Assert.Equal(new byte[] { 0x05, 0x06, 0x07, 0x08, 0x09 }, binFile.AsBinary());

            // binfile[3:5] = b'\x0a\x0b'
            binFile.SetRange(3, new byte[] { 0x0a, 0x0b });
            Assert.Equal(new byte[] { 0x05, 0x06, 0x0a, 0x0b, 0x09 }, binFile.AsBinary());

            // binfile[2:] = b'\x0c'
            binFile.SetRange(2, new byte[] { 0x0c });
            Assert.Equal(new byte[] { 0x05, 0x0c, 0x0a, 0x0b, 0x09 }, binFile.AsBinary());

            // binfile[:] = b'\x01\x02\x03\x04\x05'
            binFile.SetRange(binFile.MinimumAddress, new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 });
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 }, binFile.AsBinary());

            // binfile[0] = 0
            binFile[0] = 0;
            Assert.Equal(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 }, binFile.AsBinary());

            // binfile[7] = 7
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

        // Python: test_set_get_item_16
        [Fact]
        public void TestSetGetItem16()
        {
            var binFile = new BinFile(wordSizeBytes: 2);

            binFile.AddBinary(new byte[] { 0x01, 0x02, 0x03, 0x04 }, address: 1);

            // binfile[:] == b'\x01\x02\x03\x04'
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, binFile.AsBinary());

            // binfile[0] raises IndexError
            Assert.ThrowsAny<Exception>(() => { var _ = binFile[0]; });

            // In 16-bit mode, Python binfile[1] returns 0x0102 (word value)
            // C# indexer returns byte at byte offset, so we use AsBinary for word-level access
            Assert.Equal(new byte[] { 0x01, 0x02 }, binFile.AsBinary(minimumAddress: 1, maximumAddress: 2));
            Assert.Equal(new byte[] { 0x03, 0x04 }, binFile.AsBinary(minimumAddress: 2, maximumAddress: 3));

            // binfile[3] raises IndexError
            Assert.ThrowsAny<Exception>(() => { var _ = binFile[3]; });

            // binfile[1:3] == b'\x01\x02\x03\x04'
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, binFile.AsBinary(minimumAddress: 1, maximumAddress: 3));
            // binfile[1:4] == b'\x01\x02\x03\x04'
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, binFile.AsBinary(minimumAddress: 1, maximumAddress: 4));

            // binfile[1:2] = b'\x05\x06'
            binFile.SetRange(1, new byte[] { 0x05, 0x06 });
            Assert.Equal(new byte[] { 0x05, 0x06, 0x03, 0x04 }, binFile.AsBinary());

            // binfile[2:] = b'\x07\x08\x09\xa0'
            binFile.SetRange(2, new byte[] { 0x07, 0x08, 0x09, 0xa0 });
            Assert.Equal(new byte[] { 0x05, 0x06, 0x07, 0x08, 0x09, 0xa0 }, binFile.AsBinary());

            // binfile[5] = 0x1718
            binFile.SetRange(5, new byte[] { 0x17, 0x18 });
            Assert.Equal(new byte[] { 0x05, 0x06, 0x07, 0x08, 0x09, 0xa0, 0xff, 0xff, 0x17, 0x18 }, binFile.AsBinary());
            Assert.Equal(new byte[] { 0xff, 0xff }, binFile.AsBinary(minimumAddress: 4, maximumAddress: 5));
            Assert.Equal(new byte[] { 0x09, 0xa0, 0xff, 0xff, 0x17, 0x18 }, binFile.AsBinary(minimumAddress: 3, maximumAddress: 8));
        }

        // Python: test_word_size_custom_padding
        [Fact]
        public void TestWordSizeCustomPadding()
        {
            var binFile = new BinFile(wordSizeBytes: 2);  // 16 bits = 2 bytes
            
            string inHex = File.ReadAllText(GetTestFilePath("in_16bits_word_padding.hex"));
            binFile.AddIhex(inHex);
            
            byte[] expected = File.ReadAllBytes(GetTestFilePath("out_16bits_word_padding_0xff00.bin"));
            Assert.Equal(expected, binFile.AsBinary(null, null, new byte[] { 0xff, 0x00 }));
        }

        // Python: test_fill_word_size_16
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

        // Python: test_chunk_padding
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
            
            // Assert: not any(c.address % align for c in chunks)
            Assert.All(chunks, c => Assert.Equal(0UL, c.Address % (ulong)align));
            
            // Assert: not any(len(c) % align for c in chunks)
            Assert.All(chunks, c => Assert.Equal(0, c.Data.Length % align));
        }

        // Python: test_merge_chunks
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
            
            // Python: assert list(chunks[-1]) == [8, b'\x10\x10\xff\xff\xff\xff\x10\x10\x10\x10\x10\x10\x10\x10\x10\x10']
            var lastChunk = chunks[chunks.Count - 1];
            Assert.Equal(8UL, lastChunk.Address);
            Assert.Equal(new byte[] { 0x10, 0x10, 0xff, 0xff, 0xff, 0xff, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10 }, 
                        lastChunk.Data);
        }

        // Python: test_merge_chunks_16
        [Fact]
        public void TestMergeChunks16()
        {
            string records = ":1000000010101010101010101010101010101010F0\n" +
                           ":10000A0010101010101010101010101010101010E6\n";
            var hexfile = new BinFile(wordSizeBytes: 2);  // word_size_bits=16
            hexfile.AddIhex(records);
            int align = 6;
            int size = 12;
            var chunks = hexfile.Segments.Chunks(size: size, alignment: align, padding: new byte[] { 0xff, 0xff }).ToList();
            
            // Python: assert list(chunks[-1]) == [6, b'\x10\x10\x10\x10\xff\xff\xff\xff\x10\x10\x10\x10\x10\x10\x10\x10\x10\x10\x10\x10\x10\x10\x10\x10']
            var lastChunk = chunks[chunks.Count - 1];
            Assert.Equal(6UL, lastChunk.Address);
            Assert.Equal(new byte[] { 0x10, 0x10, 0x10, 0x10, 0xff, 0xff, 0xff, 0xff, 0x10, 0x10, 0x10, 0x10, 
                                     0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10 }, 
                        lastChunk.Data);
        }

        // Python: test_header_no_encoding
        [Fact]
        public void TestHeaderNoEncoding()
        {
            var binfile = new BinFile(headerEncoding: null);
            binfile.AddFile("TestFiles/empty_main.s19");

            Assert.Equal(System.Text.Encoding.UTF8.GetBytes("bincopy/empty_main.s19"), (byte[])binfile.Header);

            binfile.Header = System.Text.Encoding.UTF8.GetBytes("bincopy/empty_main.s20");
            Assert.Equal(System.Text.Encoding.UTF8.GetBytes("bincopy/empty_main.s20"), (byte[])binfile.Header);

            binfile.Header = new byte[] { 0x01, 0x80, 0x88, 0xaa, 0x90 };
            Assert.Equal(new byte[] { 0x01, 0x80, 0x88, 0xaa, 0x90 }, (byte[])binfile.Header);

            // Python: with self.assertRaises(TypeError) as cm:
            //             binfile.header = u'bincopy/empty_main.s21'
            var ex = Assert.Throws<ArgumentException>(() => binfile.Header = "bincopy/empty_main.s21");
            Assert.Contains("expected a byte array", ex.Message);
        }

        // Python: test_srec_no_header_encoding
        [Fact]
        public void TestSrecNoHeaderEncoding()
        {
            var binfile = new BinFile(headerEncoding: null);

            binfile.AddSrec("S0080000018088AA90B4");

            Assert.Equal("S0080000018088AA90B4", binfile.AsSrec().Split('\n')[0]);
        }

        // Python: test_add_microchip_hex_record
        [Fact]
        public void TestAddMicrochipHexRecord()
        {
            var binfile = new BinFile();
            binfile.AddMicrochipHex(":02000E00E4C943");
            Assert.Equal(0x0007UL, binfile.MinimumAddress);
            
            // Python: binfile[:binfile.minimum_address + 1]
            // This is equivalent to binfile.as_binary(binfile.minimum_address, binfile.minimum_address + 1)
            // which returns 1 word (2 bytes) from address 0x0007
            byte[] data = binfile.AsBinary(minimumAddress: binfile.MinimumAddress, 
                                          maximumAddress: binfile.MinimumAddress + 1);
            ushort firstWord = BitConverter.ToUInt16(data, 0);
            Assert.Equal(0xC9E4, firstWord);
        }

        // Python: test_microchip_hex
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

        // Python: test_add_elf
        [Fact]
        public void TestAddElf()
        {
            var bf = new BinFile();
            bf.AddElfFile("TestFiles/elf.out");

            string expected = File.ReadAllText("TestFiles/elf.s19");
            Assert.Equal(expected, bf.AsSrec());
        }

        // Python: test_add_elf_blinky
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

        // Python: test_add_elf_gcc
        [Fact]
        public void TestAddElfGcc()
        {
            var bf = new BinFile();
            bf.AddElfFile("TestFiles/elf/gcc.elf");

            byte[] expected = File.ReadAllBytes("TestFiles/elf/gcc.bin");
            Assert.Equal(expected, bf.AsBinary());
        }

        // Python: test_add_elf_iar
        [Fact]
        public void TestAddElfIar()
        {
            var bf = new BinFile();
            bf.AddElfFile("TestFiles/elf/iar.out");

            byte[] expected = File.ReadAllBytes("TestFiles/elf/iar.bin");
            Assert.Equal(expected, bf.AsBinary());
        }

        // Python: test_add_elf_keil
        [Fact]
        public void TestAddElfKeil()
        {
            var bf = new BinFile();
            bf.AddElfFile("TestFiles/elf/keil.out");

            byte[] expected = File.ReadAllBytes("TestFiles/elf/keil.bin");
            Assert.Equal(expected, bf.AsBinary());
        }
    }
}

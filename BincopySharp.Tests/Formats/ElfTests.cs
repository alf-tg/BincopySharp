using System;
using System.IO;
using Xunit;
using BincopySharp;

namespace BincopySharp.Tests
{
    public class ElfTests
    {
        private readonly string _testFilesPath;

        public ElfTests()
        {
            _testFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestFiles");
        }

        private string GetTestFilePath(string filename)
        {
            return Path.Combine(_testFilesPath, filename);
        }

        [Fact]
        public void AddElfFile_ConvertedToSrec_MatchesExpectedOutput()
        {
            var bf = new BinFile();
            bf.AddElfFile(GetTestFilePath("elf.out"));

            string expected = File.ReadAllText(GetTestFilePath("elf.s19"));
            Assert.Equal(expected, bf.AsSrec());
        }

        [Fact]
        public void AddElfFile_BlinkyFirmware_MatchesSrecReference()
        {
            var bf = new BinFile();
            bf.AddElfFile(GetTestFilePath("evkbimxrt1050_iled_blinky_sdram.axf"));
            string actualSrec = bf.AsSrec();

            bf = new BinFile();
            bf.AddSrecFile(GetTestFilePath("evkbimxrt1050_iled_blinky_sdram.s19"));
            string expectedSrec = bf.AsSrec();

            Assert.Equal(expectedSrec, actualSrec);
        }

        [Fact]
        public void AddElfFile_GccElf_MatchesBinaryReference()
        {
            var bf = new BinFile();
            bf.AddElfFile(Path.Combine(_testFilesPath, "elf", "gcc.elf"));

            byte[] expected = File.ReadAllBytes(Path.Combine(_testFilesPath, "elf", "gcc.bin"));
            Assert.Equal(expected, bf.AsBinary());
        }

        [Fact]
        public void AddElfFile_IarElf_MatchesBinaryReference()
        {
            var bf = new BinFile();
            bf.AddElfFile(Path.Combine(_testFilesPath, "elf", "iar.out"));

            byte[] expected = File.ReadAllBytes(Path.Combine(_testFilesPath, "elf", "iar.bin"));
            Assert.Equal(expected, bf.AsBinary());
        }

        [Fact]
        public void AddElfFile_KeilElf_MatchesBinaryReference()
        {
            var bf = new BinFile();
            bf.AddElfFile(Path.Combine(_testFilesPath, "elf", "keil.out"));

            byte[] expected = File.ReadAllBytes(Path.Combine(_testFilesPath, "elf", "keil.bin"));
            Assert.Equal(expected, bf.AsBinary());
        }

        [Fact]
        public void AddElfFile_ThenOverwriteData_ReflectsNewData()
        {
            var bf = new BinFile();
            bf.AddElfFile(GetTestFilePath("elf.out"));

            byte[] data = "test"u8.ToArray();
            ulong address = bf.MinimumAddress;
            bf.Add(data, address: address, overwrite: true);
            Assert.Equal(data, bf.AsBinary(minimumAddress: address, maximumAddress: address + (ulong)data.Length));
        }
    }
}

using System;
using System.Linq;
using Xunit;

namespace BincopySharp.Tests
{
    public class AddressTests
    {
        [Fact]
        public void Add_At2GBBoundary_StoresAndRetrievesCorrectly()
        {
            var bin = new BinFile();
            ulong address = 0x80000000; // 2GB boundary
            bin.Add([0xAA, 0xBB], address);

            Assert.Equal(address, bin.MinimumAddress);
            Assert.Equal(address + 2, bin.MaximumAddress);
            Assert.Equal(2, bin.Length);
            Assert.Equal(0xAAUL, bin[address]);
            Assert.Equal(0xBBUL, bin[address + 1]);
        }

        [Fact]
        public void Add_At4GBBoundary_StoresAndRetrievesCorrectly()
        {
            var bin = new BinFile();
            ulong address = 0xFFFFFFFF; // 4GB - 1
            bin.Add([0xDE, 0xAD], address);

            Assert.Equal(address, bin.MinimumAddress);
            Assert.Equal(address + 2, bin.MaximumAddress);
            byte[] data = bin.AsBinary(address, address + 2);
            Assert.Equal(new byte[] { 0xDE, 0xAD }, data);
        }

        [Fact]
        public void Exclude_AtAddressZero_LeavesRemainingData()
        {
            var bin = new BinFile();
            bin.Add([0x01, 0x02, 0x03, 0x04], 0);

            bin.Exclude(0, 2);
            Assert.Equal(2UL, bin.MinimumAddress);
            Assert.Equal(4UL, bin.MaximumAddress);
            Assert.Equal(new byte[] { 0x03, 0x04 }, bin.AsBinary(2, 4));
        }

        [Fact]
        public void Crop_AtAddressZero_KeepsOnlyRequestedRange()
        {
            var bin = new BinFile();
            bin.Add([0x01, 0x02, 0x03, 0x04], 0);

            bin.Crop(0, 2);
            Assert.Equal(0UL, bin.MinimumAddress);
            Assert.Equal(2UL, bin.MaximumAddress);
            Assert.Equal(new byte[] { 0x01, 0x02 }, bin.AsBinary(0, 2));
        }

        [Fact]
        public void Add_AtAbsoluteMaxUlongAddress_Throws()
        {
            var bin = new BinFile();
            ulong maxAddr = 0xFFFFFFFFFFFFFFFF;

            // Adding at the absolute max address causes maximumAddress overflow (wraps to 0)
            // This is expected to throw because maximumAddress <= minimumAddress
            Assert.Throws<ArgumentException>(() => bin.Add([0x42], maxAddr));
        }

        [Fact]
        public void Add_AtNearMaxUlongAddress_StoresCorrectly()
        {
            var bin = new BinFile();
            ulong addr = 0xFFFFFFFFFFFFFFFE; // Max - 1
            bin.Add([0x42], addr);

            Assert.Equal(addr, bin.MinimumAddress);
            Assert.Equal(0xFFFFFFFFFFFFFFFFUL, bin.MaximumAddress); // addr + 1 = max ulong
            Assert.Equal(1, bin.Length);
            Assert.Single(bin.Segments);
            Assert.Equal(0x42UL, bin[addr]);
            Assert.Equal(new byte[] { 0x42 }, bin.AsBinary(addr, addr + 1));
        }

        [Fact]
        public void AsBinary_AtVeryHighAddress_ReturnsCorrectData()
        {
            var bin = new BinFile();
            ulong addr = 0xFFFFFFFFFFFFFFF0;
            byte[] data = [0x01, 0x02, 0x03, 0x04];
            bin.Add(data, addr);

            Assert.Single(bin.Segments);
            Assert.Equal(addr, bin.MinimumAddress);
            Assert.Equal(addr + 4, bin.MaximumAddress);
            Assert.Equal(4, bin.Length);
            byte[] result = bin.AsBinary(addr, addr + 4);
            Assert.Equal(data, result);
        }

        [Fact]
        public void Info_AtVeryHighAddress_ContainsCorrectAddresses()
        {
            var bin = new BinFile();
            ulong addr = 0xFFFFFFFFFFFFFFF0;
            bin.Add([0xAA], addr);

            Assert.Single(bin.Segments);
            Assert.Equal(1, bin.Length);
            string info = bin.Info();
            Assert.Contains("fffffffffffffff0", info);
            Assert.Contains("fffffffffffffff1", info); // max address
        }

        [Fact]
        public void AsBinary_SingleByteAtNearMaxAddress_ReturnsCorrectData()
        {
            var bin = new BinFile();
            ulong addr = 0xFFFFFFFFFFFFFFFE; // Max - 1, so maximumAddress = Max (no overflow)
            bin.Add([0x99], addr);

            Assert.Equal(addr, bin.MinimumAddress);
            Assert.Equal(0x99UL, bin[addr]);

            byte[] range = bin.AsBinary(addr, addr + 1);
            Assert.Equal(new byte[] { 0x99 }, range);
        }

        [Fact]
        public void Add_AtUlongMaxAddress_ThrowsArgumentException()
        {
            var bin = new BinFile();
            ulong addr = 0xFFFFFFFFFFFFFFFF;

            // This must throw because addr + 1 overflows to 0
            Assert.Throws<ArgumentException>(() => bin.Add([0x99], addr));
        }

        [Fact]
        public void Add_SegmentCrossing4GBBoundary_StoresDataIntact()
        {
            var bin = new BinFile();
            ulong addr = 0xFFFFFFF0;
            byte[] data = [.. Enumerable.Range(0, 32).Select(i => (byte)i)];
            bin.Add(data, addr);

            Assert.Single(bin.Segments);
            Assert.Equal(addr, bin.MinimumAddress);
            Assert.Equal(addr + 32, bin.MaximumAddress);
            Assert.Equal(32, bin.Length);

            // Verify exact data integrity across the 4GB boundary
            byte[] retrieved = bin.AsBinary(addr, addr + 32);
            Assert.Equal(data, retrieved);
            Assert.Equal(data, bin.AsBinary());
        }
    }
}

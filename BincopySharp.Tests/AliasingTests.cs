using System;
using Xunit;
using BincopySharp;

namespace BincopySharp.Tests
{
    public class AliasingTests
    {
        [Fact]
        public void Aliasing_MutatingAddedArrayDoesNotAffectSegment()
        {
            var bin = new BinFile();
            byte[] data = [0x01, 0x02, 0x03];
            bin.Add(data, 0);

            data[0] = 0xFF;
            data[2] = 0xFF;

            Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, bin.AsBinary());
        }

        [Fact]
        public void Aliasing_OperatorPlus_ModifyingOperandsAfterwardDoesNotAffectResult()
        {
            var a = new BinFile();
            a.Add([0x01, 0x02], 0x100);

            var b = new BinFile();
            b.Add([0x10, 0x20], 0x200);

            var c = a + b;
            byte[] cFromA = c.AsBinary(0x100, 0x102);
            byte[] cFromB = c.AsBinary(0x200, 0x202);

            a.Add([0xFF, 0xFF], 0x100, overwrite: true);
            b.Add([0xFF, 0xFF], 0x200, overwrite: true);

            Assert.Equal(cFromA, c.AsBinary(0x100, 0x102));
            Assert.Equal(cFromB, c.AsBinary(0x200, 0x202));
        }

        [Fact]
        public void Aliasing_MergedSegmentIsIndependentOfOriginalArrays()
        {
            var bin = new BinFile();
            byte[] first = [0x01, 0x02];
            byte[] second = [0x03, 0x04];

            bin.Add(first, 0);
            bin.Add(second, 2);  // adjacent - will merge into one segment

            Assert.Single(bin.Segments);

            first[0] = 0xFF;
            second[1] = 0xFF;

            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, bin.AsBinary());
        }

        [Fact]
        public void Aliasing_AsBinaryReturnsCopy()
        {
            var bin = new BinFile();
            bin.Add([0x01, 0x02, 0x03], 0);

            byte[] result1 = bin.AsBinary();
            result1[0] = 0xFF;

            byte[] result2 = bin.AsBinary();
            Assert.Equal(0x01, result2[0]);
        }

        [Fact]
        public void Aliasing_DataToArrayReturnsCopy()
        {
            var bin = new BinFile();
            bin.Add([0xAA, 0xBB, 0xCC], 0);

            byte[] copy1 = bin.Segments[0].Data.ToArray();
            byte[] copy2 = bin.Segments[0].Data.ToArray();

            copy1[0] = 0xFF;

            Assert.Equal(0xAA, copy2[0]);
            Assert.Equal(0xAA, bin.Segments[0].Data[0]);
        }
    }
}

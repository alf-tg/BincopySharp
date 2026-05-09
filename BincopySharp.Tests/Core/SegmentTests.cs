using System.IO;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BincopySharp.Tests
{
    public class SegmentTests
    {
        private readonly string _testFilesPath;

        public SegmentTests()
        {
            _testFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "TestFiles");
        }

        private string GetTestFilePath(string filename)
        {
            return Path.Combine(_testFilesPath, filename);
        }

        [Fact]
        public void Segments_AfterMultipleAdds_HasCorrectBoundariesAndData()
        {
            var binFile = new BinFile();
            binFile.Add([0x00], address: 0);
            binFile.Add([0x01, 0x02], address: 10);
            binFile.Add([0x03], address: 12);
            binFile.Add([0x04], address: 1000);

            var segments = new List<(ulong Address, byte[] Data)>();
            foreach (var segment in binFile.Segments)
            {
                segments.Add((segment.MinimumAddress, segment.Data.ToArray()));
            }

            Assert.Equal(3, segments.Count);
            Assert.Equal(0UL,segments[0].Address);
            Assert.Equal(new byte[] { 0x00 }, segments[0].Data);
            Assert.Equal(10UL,segments[1].Address);
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, segments[1].Data);
            Assert.Equal(1000UL,segments[2].Address);
            Assert.Equal(new byte[] { 0x04 }, segments[2].Data);
        }

        [Fact]
        public void Segments_IteratedFromSrecFile_CountsCorrectly()
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
            Assert.Single(binFile.Segments);
        }

        [Fact]
        public void Chunks_VariousSizesAndAlignments_ProducesCorrectRanges()
        {
            var binFile = new BinFile();
            binFile.Add([0x00, 0x00, 0x01, 0x01, 0x02], address: 0);
            binFile.Add([0x04, 0x05, 0x05, 0x06, 0x06, 0x07], address: 9);
            binFile.Add([0x09], address: 19);
            binFile.Add([0x0a], address: 21);

            byte[] expectedBinary =
            [
                0x00, 0x00, 0x01, 0x01, 0x02, 0xff, 0xff, 0xff,
                0xff, 0x04, 0x05, 0x05, 0x06, 0x06, 0x07, 0xff,
                0xff, 0xff, 0xff, 0x09, 0xff, 0x0a
            ];
            Assert.Equal(expectedBinary, binFile.AsBinary());

            // Size 8, alignment 1
            var chunks = new List<(ulong, byte[])>();
            foreach (var (Address, Data) in binFile.Segments.Chunks(size: 8))
            {
                chunks.Add((Address, Data));
            }
            Assert.Equal(4, chunks.Count);
            Assert.Equal(0UL,chunks[0].Item1);
            Assert.Equal(new byte[] { 0x00, 0x00, 0x01, 0x01, 0x02 }, chunks[0].Item2);
            Assert.Equal(9UL,chunks[1].Item1);
            Assert.Equal(new byte[] { 0x04, 0x05, 0x05, 0x06, 0x06, 0x07 }, chunks[1].Item2);
            Assert.Equal(19UL,chunks[2].Item1);
            Assert.Equal(new byte[] { 0x09 }, chunks[2].Item2);
            Assert.Equal(21UL,chunks[3].Item1);
            Assert.Equal(new byte[] { 0x0a }, chunks[3].Item2);

            // Size 8, alignment 2
            chunks.Clear();
            foreach (var (Address, Data) in binFile.Segments.Chunks(size: 8, alignment: 2))
            {
                chunks.Add((Address, Data));
            }
            Assert.Equal(5, chunks.Count);
            Assert.Equal(0UL,chunks[0].Item1);
            Assert.Equal(new byte[] { 0x00, 0x00, 0x01, 0x01, 0x02 }, chunks[0].Item2);
            Assert.Equal(9UL,chunks[1].Item1);
            Assert.Equal(new byte[] { 0x04 }, chunks[1].Item2);
            Assert.Equal(10UL,chunks[2].Item1);
            Assert.Equal(new byte[] { 0x05, 0x05, 0x06, 0x06, 0x07 }, chunks[2].Item2);
            Assert.Equal(19UL,chunks[3].Item1);
            Assert.Equal(new byte[] { 0x09 }, chunks[3].Item2);
            Assert.Equal(21UL,chunks[4].Item1);
            Assert.Equal(new byte[] { 0x0a }, chunks[4].Item2);

            // Size 8, alignment 4
            chunks.Clear();
            foreach (var (Address, Data) in binFile.Segments.Chunks(size: 8, alignment: 4))
            {
                chunks.Add((Address, Data));
            }
            Assert.Equal(5, chunks.Count);
            Assert.Equal(0UL,chunks[0].Item1);
            Assert.Equal(new byte[] { 0x00, 0x00, 0x01, 0x01, 0x02 }, chunks[0].Item2);
            Assert.Equal(9UL,chunks[1].Item1);
            Assert.Equal(new byte[] { 0x04, 0x05, 0x05 }, chunks[1].Item2);
            Assert.Equal(12UL,chunks[2].Item1);
            Assert.Equal(new byte[] { 0x06, 0x06, 0x07 }, chunks[2].Item2);
            Assert.Equal(19UL,chunks[3].Item1);
            Assert.Equal(new byte[] { 0x09 }, chunks[3].Item2);
            Assert.Equal(21UL,chunks[4].Item1);
            Assert.Equal(new byte[] { 0x0a }, chunks[4].Item2);

            // Size 8, alignment 8
            chunks.Clear();
            foreach (var (Address, Data) in binFile.Segments.Chunks(size: 8, alignment: 8))
            {
                chunks.Add((Address, Data));
            }
            Assert.Equal(4, chunks.Count);
            Assert.Equal(0UL,chunks[0].Item1);
            Assert.Equal(new byte[] { 0x00, 0x00, 0x01, 0x01, 0x02 }, chunks[0].Item2);
            Assert.Equal(9UL,chunks[1].Item1);
            Assert.Equal(new byte[] { 0x04, 0x05, 0x05, 0x06, 0x06, 0x07 }, chunks[1].Item2);
            Assert.Equal(19UL,chunks[2].Item1);
            Assert.Equal(new byte[] { 0x09 }, chunks[2].Item2);
            Assert.Equal(21UL,chunks[3].Item1);
            Assert.Equal(new byte[] { 0x0a }, chunks[3].Item2);

            // Size 4, alignment 1
            chunks.Clear();
            foreach (var (Address, Data) in binFile.Segments.Chunks(size: 4))
            {
                chunks.Add((Address, Data));
            }
            Assert.Equal(6, chunks.Count);
            Assert.Equal(0UL,chunks[0].Item1);
            Assert.Equal(new byte[] { 0x00, 0x00, 0x01, 0x01 }, chunks[0].Item2);
            Assert.Equal(4UL,chunks[1].Item1);
            Assert.Equal(new byte[] { 0x02 }, chunks[1].Item2);
            Assert.Equal(9UL,chunks[2].Item1);
            Assert.Equal(new byte[] { 0x04, 0x05, 0x05, 0x06 }, chunks[2].Item2);
            Assert.Equal(13UL,chunks[3].Item1);
            Assert.Equal(new byte[] { 0x06, 0x07 }, chunks[3].Item2);
            Assert.Equal(19UL,chunks[4].Item1);
            Assert.Equal(new byte[] { 0x09 }, chunks[4].Item2);
            Assert.Equal(21UL,chunks[5].Item1);
            Assert.Equal(new byte[] { 0x0a }, chunks[5].Item2);

            // Size 4, alignment 2
            chunks.Clear();
            foreach (var (Address, Data) in binFile.Segments.Chunks(size: 4, alignment: 2))
            {
                chunks.Add((Address, Data));
            }
            Assert.Equal(7, chunks.Count);
            Assert.Equal(0UL,chunks[0].Item1);
            Assert.Equal(new byte[] { 0x00, 0x00, 0x01, 0x01 }, chunks[0].Item2);
            Assert.Equal(4UL,chunks[1].Item1);
            Assert.Equal(new byte[] { 0x02 }, chunks[1].Item2);
            Assert.Equal(9UL,chunks[2].Item1);
            Assert.Equal(new byte[] { 0x04 }, chunks[2].Item2);
            Assert.Equal(10UL,chunks[3].Item1);
            Assert.Equal(new byte[] { 0x05, 0x05, 0x06, 0x06 }, chunks[3].Item2);
            Assert.Equal(14UL,chunks[4].Item1);
            Assert.Equal(new byte[] { 0x07 }, chunks[4].Item2);
            Assert.Equal(19UL,chunks[5].Item1);
            Assert.Equal(new byte[] { 0x09 }, chunks[5].Item2);
            Assert.Equal(21UL,chunks[6].Item1);
            Assert.Equal(new byte[] { 0x0a }, chunks[6].Item2);

            // Size 4, alignment 4
            chunks.Clear();
            foreach (var (Address, Data) in binFile.Segments.Chunks(size: 4, alignment: 4))
            {
                chunks.Add((Address, Data));
            }
            Assert.Equal(6, chunks.Count);
            Assert.Equal(0UL,chunks[0].Item1);
            Assert.Equal(new byte[] { 0x00, 0x00, 0x01, 0x01 }, chunks[0].Item2);
            Assert.Equal(4UL,chunks[1].Item1);
            Assert.Equal(new byte[] { 0x02 }, chunks[1].Item2);
            Assert.Equal(9UL,chunks[2].Item1);
            Assert.Equal(new byte[] { 0x04, 0x05, 0x05 }, chunks[2].Item2);
            Assert.Equal(12UL,chunks[3].Item1);
            Assert.Equal(new byte[] { 0x06, 0x06, 0x07 }, chunks[3].Item2);
            Assert.Equal(19UL,chunks[4].Item1);
            Assert.Equal(new byte[] { 0x09 }, chunks[4].Item2);
            Assert.Equal(21UL,chunks[5].Item1);
            Assert.Equal(new byte[] { 0x0a }, chunks[5].Item2);
        }

        [Fact]
        public void Chunks_SizeNotMultipleOfAlignment_Throws()
        {
            var binFile = new BinFile();

            // Size 4 is not a multiple of alignment 3
            var ex = Assert.Throws<BincopyException>(() =>
            {
                var chunks = binFile.Segments.Chunks(size: 4, alignment: 3).ToList();
            });
            Assert.Equal("Size 4 is not a multiple of alignment 3", ex.Message);

            // Size 4 is not a multiple of alignment 8
            ex = Assert.Throws<BincopyException>(() =>
            {
                var chunks = binFile.Segments.Chunks(size: 4, alignment: 8).ToList();
            });
            Assert.Equal("Size 4 is not a multiple of alignment 8", ex.Message);

        }

        [Fact]
        public void Segment_Chunks_ProducesCorrectRangesAndThrowsOnBadArgs()
        {
            var binFile = new BinFile();
            binFile.Add([0x00, 0x01, 0x02, 0x03, 0x04], address: 2);

            // Size 4, alignment 4
            var chunks = new List<(ulong, byte[])>();
            foreach (var chunk in binFile.Segments[0].Chunks(size: 4, alignment: 4))
            {
                chunks.Add(chunk);
            }
            Assert.Equal(2, chunks.Count);
            Assert.Equal(2UL,chunks[0].Item1);
            Assert.Equal(new byte[] { 0x00, 0x01 }, chunks[0].Item2);
            Assert.Equal(4UL,chunks[1].Item1);
            Assert.Equal(new byte[] { 0x02, 0x03, 0x04 }, chunks[1].Item2);

            // Bad arguments - size 4 is not a multiple of alignment 8
            var ex = Assert.Throws<BincopyException>(() =>
            {
                var result = binFile.Segments[0].Chunks(size: 4, alignment: 8).ToList();
            });
            Assert.Equal("Size 4 is not a multiple of alignment 8", ex.Message);

            // Missing segment
            ex = Assert.Throws<BincopyException>(() =>
            {
                var result = binFile.Segments[1].Chunks(size: 4, alignment: 8).ToList();
            });
            Assert.Equal("Segment does not exist", ex.Message);
        }

        [Fact]
        public void Chunks_WithPadding_AlignsBoundariesCorrectly()
        {
            string records = ":02000004000AF0\n" +
                           ":10B8440000000000000000009630000007770000B0\n";
            var hexfile = new BinFile();
            hexfile.AddIhex(records);
            int align = 8;
            int size = 16;
            var chunks = hexfile.Segments.Chunks(size: size, alignment: align, padding: 0xff).ToList();

            Assert.All(chunks, c => Assert.Equal(0UL, c.Address % (ulong)align));

            Assert.All(chunks, c => Assert.Equal(0, c.Data.Length % align));
        }

        [Fact]
        public void Chunks_AdjacentSegmentsWithGap_MergesIntoAlignedBlocks()
        {
            string records = ":0A0000001010101010101010101056\n" +
                           ":0A000E001010101010101010101048\n";
            var hexfile = new BinFile();
            hexfile.AddIhex(records);
            int align = 8;
            int size = 16;
            var chunks = hexfile.Segments.Chunks(size: size, alignment: align, padding: 0xff).ToList();

            Assert.Equal(2, chunks.Count);
            Assert.Equal(0UL, chunks[0].Address);
            Assert.Equal(new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0xff, 0xff, 0xff, 0xff, 0x10, 0x10 },
                        chunks[0].Data);
            Assert.Equal(16UL, chunks[1].Address);
            Assert.Equal(new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10 },
                        chunks[1].Data);
        }

        [Fact]
        public void Add_ThousandSmallSegments_AllAddressableCorrectly()
        {
            var bin = new BinFile();

            // Add 1000 segments of 1 byte at non-overlapping addresses spaced 10 apart
            for (int i = 0; i < 1000; i++)
            {
                ulong addr = (ulong)(i * 10);
                bin.Add([(byte)(addr & 0xFF)], addr);
            }

            Assert.Equal(1000, bin.Segments.Count);
            Assert.Equal(0UL, bin.MinimumAddress);
            Assert.Equal(9991UL, bin.MaximumAddress); // last segment at 9990, 1 byte â†’ 9991
            Assert.Equal(1000, bin.Length); // 1000 segments Ã— 1 byte each

            // Verify first, middle, and last segments have correct data
            Assert.Equal(0x00UL, bin[0]);         // addr 0 â†’ 0x00
            Assert.Equal(0x88UL, bin[5000]);      // addr 5000 â†’ 5000 & 0xFF = 0x88
            Assert.Equal(0x06UL, bin[9990]);       // addr 9990 â†’ 9990 & 0xFF = 0x06

            // Verify segment boundaries
            Assert.Equal(0UL, bin.Segments[0].MinimumAddress);
            Assert.Equal(1UL, bin.Segments[0].MaximumAddress);
            Assert.Equal(9990UL, bin.Segments[999].MinimumAddress);
            Assert.Equal(9991UL, bin.Segments[999].MaximumAddress);

            // Binary output should be 9991 bytes (min=0 to max=9991, gaps filled with 0xFF)
            byte[] binary = bin.AsBinary();
            Assert.Equal(9991, binary.Length);
            Assert.Equal(0x00, binary[0]);     // data at addr 0
            Assert.Equal(0xFF, binary[1]);     // gap
            Assert.Equal(0xFF, binary[9]);     // gap
            Assert.Equal(0x0A, binary[10]);    // data at addr 10 â†’ 10 & 0xFF = 0x0A
        }

        [Fact]
        public void Chunks_NonAdjacentSegmentsWithAlignmentPadding_MergesOverlappingBlockCorrectly()
        {
            var bin = new BinFile();
            bin.Add([0x11, 0x22], 0); // seg1: bytes 0-1
            bin.Add([0x44, 0x55], 3); // seg2: bytes 3-4, gap at byte 2

            Assert.Equal(2, bin.Segments.Count);

            var chunks = bin.Segments.Chunks(size: 4, alignment: 4, padding: 0xFF).ToList();

            // Two chunks: the merged overlap block at addr=0 and the tail at addr=4
            Assert.Equal(2, chunks.Count);
            Assert.Equal(0UL, chunks[0].Address);
            Assert.Equal(new byte[] { 0x11, 0x22, 0xFF, 0x44 }, chunks[0].Data);
            Assert.Equal(4UL, chunks[1].Address);
            Assert.Equal(new byte[] { 0x55, 0xFF, 0xFF, 0xFF }, chunks[1].Data);
        }
    }
}

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using BincopySharp;
using BincopySharp.Formats;

namespace BincopySharp.Tests
{
    public class RegressionTests
    {
        [Fact]
        public void Exclude_ThenAddAdjacentData_DataIsPresent()
        {
            var bin = new BinFile();
            bin.Add([0x01, 0x02, 0x03, 0x04, 0x05, 0x06], 0);

            bin.Exclude(2, 4);
            Assert.Equal(2, bin.Segments.Count);

            byte[] newData = [0xAA, 0xBB];
            bin.Add(newData, 6);

            Assert.Equal(newData, bin.AsBinary(6, 8));
            Assert.Equal(new byte[] { 0x01, 0x02 }, bin.AsBinary(0, 2));
            Assert.Equal(new byte[] { 0x05, 0x06 }, bin.AsBinary(4, 6));
        }

        [Fact]
        public void Crop_ThenAddBeyondNewEnd_CreatesNewSegment()
        {
            var bin = new BinFile();
            bin.Add([0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08], 0);

            bin.Crop(0, 3);
            Assert.Single(bin.Segments);
            Assert.Equal(0UL, bin.MinimumAddress);
            Assert.Equal(3UL, bin.MaximumAddress);

            byte[] newData = [0xDD, 0xEE];
            bin.Add(newData, 8);

            Assert.Equal(2, bin.Segments.Count);
            Assert.Equal(newData, bin.AsBinary(8, 10));
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, bin.AsBinary(0, 3));
        }

        [Fact]
        public void IhexType04Record_ResetsType02ExtendedSegmentAddress()
        {
            // Type 04 (extended linear address) must clear any previously set type 02
            // (extended segment address), not accumulate both offsets.
            string ihexData =
                ":020000021000EC\n" +   // type 02: segment base = 0x1000 * 16 = 0x10000
                ":020000040001F9\n" +   // type 04: linear base = 0x0001 << 16 = 0x10000
                ":01000000AA55\n" +     // data at offset 0 → correct address = 0x00010000
                ":00000001FF\n";

            var bin = new BinFile();
            bin.Add(ihexData);

            Assert.Single(bin.Segments);
            Assert.Equal(0x00010000UL, bin.MinimumAddress);
            Assert.Equal(0x00010001UL, bin.MaximumAddress);
            Assert.Equal(0xAAUL, bin[0x00010000]);
        }

        [Fact]
        public async Task AsSrec_ZeroDataBytes_ThrowsArgumentException()
        {
            var bin = new BinFile();
            bin.Add([0x01, 0x02, 0x03, 0x04], 0);

            // Wrap in a timed task to guard against infinite loop on unfixed code.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Exception caught = null;
            bool completed = false;

            try
            {
                var task = Task.Run(() =>
                {
                    bin.AsSrec(numberOfDataBytes: 0);
                }, cts.Token);

                await Task.WhenAny(
                    task,
                    Task.Delay(TimeSpan.FromSeconds(5), cts.Token));

                completed = task.IsCompleted;
                if (task.IsFaulted && task.Exception != null)
                {
                    caught = task.Exception.InnerException;
                }

            }
            catch (OperationCanceledException)
            {
                completed = false;
            }

            Assert.True(completed, "AsSrec(numberOfDataBytes: 0) hung - likely infinite loop");
            Assert.NotNull(caught);
            Assert.IsType<ArgumentException>(caught);
            Assert.Contains("numberOfDataBytes", caught.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AsIhex_ZeroDataBytes_ThrowsArgumentException()
        {
            var bin = new BinFile();
            bin.Add([0x01, 0x02, 0x03, 0x04], 0);

            // Wrap in a timed task to guard against infinite loop on unfixed code.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Exception caught = null;
            bool completed = false;

            try
            {
                var task = Task.Run(() =>
                {
                    bin.AsIhex(numberOfDataBytes: 0);
                }, cts.Token);

                await Task.WhenAny(
                    task,
                    Task.Delay(TimeSpan.FromSeconds(5), cts.Token));

                completed = task.IsCompleted;
                if (task.IsFaulted && task.Exception != null)
                {
                    caught = task.Exception.InnerException;
                }

            }
            catch (OperationCanceledException)
            {
                completed = false;
            }

            Assert.True(completed, "AsIhex(numberOfDataBytes: 0) hung - likely infinite loop");
            Assert.NotNull(caught);
            Assert.IsType<ArgumentException>(caught);
            Assert.Contains("numberOfDataBytes", caught.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Layout_LargeAddressSpan_ProducesCorrectWidth()
        {
            var bin = new BinFile();
            bin.Add([0x01], 0);
            bin.Add([0x02], 0x80000000);

            string layout = bin.Layout();
            string[] lines = layout.Split('\n', StringSplitOptions.None);

            Assert.True(lines.Length >= 2, $"Expected at least 2 lines, got {lines.Length}");
            Assert.Equal(80, lines[1].Length);
            Assert.True(lines[1].Contains('=') || lines[1].Contains('-'),
                $"Visualization line should contain data markers, got: '{lines[1]}'");
        }

        [Fact]
        public void AsBinary_RangeExceedsIntMax_ThrowsBincopyException()
        {
            var bin = new BinFile();
            bin.Add([0x01], 0);

            ulong endAddr = (ulong)int.MaxValue + 2;
            var ex = Assert.Throws<BincopyException>(() => bin.AsBinary(0, endAddr));
            Assert.True(ex.Message.Length > 0);
        }

        [Fact]
        public void Add_NullByteArray_ThrowsArgumentNullException()
        {
            var bin = new BinFile();
            Assert.Throws<ArgumentNullException>(() => bin.Add(null, 0));
        }

        [Fact]
        public void AddFile_CorruptedElf_ThrowsDescriptiveException()
        {
            var bin = new BinFile();

            byte[] corruptedElf =
            [
                0x7F, 0x45, 0x4C, 0x46, // ELF magic
                0x01,                    // EI_CLASS: 32-bit
                0x01,                    // EI_DATA: little-endian
                0x01,                    // EI_VERSION: current
                0x00,                    // EI_OSABI
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                // truncated — missing the rest of the ELF header
            ];

            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(tempFile, corruptedElf);
                var ex = Assert.Throws<BincopyException>(() => bin.AddFile(tempFile));
                Assert.Contains("ELF", ex.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}

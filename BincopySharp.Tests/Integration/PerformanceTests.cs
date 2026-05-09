using System;
using System.IO;
using Xunit;
using BincopySharp;

namespace BincopySharp.Tests
{
    public class PerformanceTests
    {
        [Fact]
        public void Serialization_OneMegabyte_CompletesWithinReasonableTime()
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
                binFile.Add(chunk, (ulong)(1024 * i));
            }

            Assert.Equal(0UL,binFile.MinimumAddress);
            Assert.Equal((ulong)(1024 * 1024), binFile.MaximumAddress);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            string ihex = binFile.AsIhex();
            var ihexSerializeTime = sw.Elapsed;

            sw.Restart();
            string srec = binFile.AsSrec();
            var srecSerializeTime = sw.Elapsed;

            sw.Restart();
            string tiTxt = binFile.AsTiTxt();
            var tiTxtSerializeTime = sw.Elapsed;

            // Parse back and verify round-trip correctness
            sw.Restart();
            var binFile2 = new BinFile();
            binFile2.AddIhex(ihex);
            var ihexParseTime = sw.Elapsed;
            Assert.Equal(binFile.AsBinary(), binFile2.AsBinary());

            sw.Restart();
            var binFile3 = new BinFile();
            binFile3.AddSrec(srec);
            var srecParseTime = sw.Elapsed;
            Assert.Equal(binFile.AsBinary(), binFile3.AsBinary());

            sw.Restart();
            var binFile4 = new BinFile();
            binFile4.AddTiTxt(tiTxt);
            var tiTxtParseTime = sw.Elapsed;
            Assert.Equal(binFile.AsBinary(), binFile4.AsBinary());

            Console.WriteLine($"IHEX   serialize: {ihexSerializeTime.TotalMilliseconds:F1}ms  parse: {ihexParseTime.TotalMilliseconds:F1}ms");
            Console.WriteLine($"SREC   serialize: {srecSerializeTime.TotalMilliseconds:F1}ms  parse: {srecParseTime.TotalMilliseconds:F1}ms");
            Console.WriteLine($"TI-TXT serialize: {tiTxtSerializeTime.TotalMilliseconds:F1}ms  parse: {tiTxtParseTime.TotalMilliseconds:F1}ms");
        }
    }
}

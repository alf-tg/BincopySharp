using System.Collections.Generic;

namespace BincopySharp.Formats
{
    internal class ParseResult
    {
        public List<Segment> Segments { get; set; }
        public ulong? ExecutionStartAddress { get; set; }
        public byte[]? Header { get; set; }

        public ParseResult()
        {
            Segments = new List<Segment>();
        }
    }
}
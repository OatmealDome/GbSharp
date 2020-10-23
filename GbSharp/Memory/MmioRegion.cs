using System;
using System.Collections.Generic;

namespace GbSharp.Memory
{
    internal class MmioRegion : MemoryRegion
    {
        private readonly Func<byte> ReadFunc;
        private readonly Action<byte> WriteFunc;

        public MmioRegion(Func<byte> readFunc, Action<byte> writeFunc)
        {
            ReadFunc = readFunc;
            WriteFunc = writeFunc;
        }

        public override IEnumerable<Tuple<int, int>> GetHandledRanges()
        {
            // Return nothing, RegisterMmio handles this for us
            return null;
        }

        public override byte Read(int offset)
        {
            return ReadFunc();
        }

        public override void Write(int offset, byte val)
        {
            WriteFunc(val);
        }

    }
}

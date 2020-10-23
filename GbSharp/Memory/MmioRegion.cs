using System;

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

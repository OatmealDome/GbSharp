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

        public override byte Read(ushort offset)
        {
            return ReadFunc();
        }

        public override void Write(ushort offset, byte val)
        {
            WriteFunc(val);
        }

    }
}

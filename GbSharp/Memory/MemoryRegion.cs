using System;
using System.Collections.Generic;

namespace GbSharp.Memory
{
    abstract internal class MemoryRegion
    {
        public abstract IEnumerable<Tuple<int, int>> GetHandledRanges();

        public abstract byte Read(int address);

        public abstract void Write(int address, byte val);

    }
}

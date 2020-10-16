using System;
using System.Collections.Generic;
using System.Text;

namespace GbSharp.Memory
{
    abstract internal class MemoryRegion
    {
        public abstract byte Read(ushort offset);

        public abstract void Write(ushort offset, byte val);
    }
}

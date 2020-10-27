using System;
using System.Collections.Generic;

namespace GbSharp.Memory
{
    abstract internal class MemoryRegion
    {
        protected HardwareType HardwareType;

        public abstract IEnumerable<Tuple<int, int>> GetHandledRanges();

        public virtual void SetHardwareType(HardwareType type)
        {
            HardwareType = type;
        }

        public abstract byte Read(int address);

        public abstract void Write(int address, byte val);

    }
}

using System;
using System.Collections.Generic;

namespace GbSharp.Memory.Rom
{
    class BootRomRegion : MemoryRegion
    {
        public static readonly int POTENTIAL_BOOT_ROM_SIZE = 0x900;

        private byte[] Rom;

        public BootRomRegion(byte[] rom)
        {
            Rom = rom;
        }

        public override IEnumerable<Tuple<int, int>> GetHandledRanges()
        {
            // Return nothing, we're a special case in GbMemory
            return null;
        }

        public override byte Read(int offset)
        {
            return Rom[offset];
        }

        public override void Write(int offset, byte val)
        {
            // Should never happen
        }

    }
}

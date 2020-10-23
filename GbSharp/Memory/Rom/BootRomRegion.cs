using System;
using System.Collections.Generic;

namespace GbSharp.Memory.Rom
{
    class BootRomRegion : MemoryRegion
    {
        public static readonly int BOOT_ROM_SIZE = 0x100;

        private byte[] Rom;

        public BootRomRegion(byte[] rom)
        {
            if (rom.Length != BOOT_ROM_SIZE)
            {
                throw new Exception("Invalid boot ROM");
            }

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

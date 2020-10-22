using System;

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

        public override byte Read(ushort offset)
        {
            return Rom[offset];
        }

        public override void Write(ushort offset, byte val)
        {
            // Should never happen
        }

    }
}

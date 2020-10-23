using GbSharp.Memory.Ram;
using GbSharp.Memory.Rom;
using System;
using System.Collections.Generic;

namespace GbSharp.Memory
{
    class GbMemory
    {
        // General DMG memory map
        // 0x0000 to 0x3FFF: ROM bank 0 (unswitchable)
        // 0x4000 to 0x7FFF: ROM bank X (switchable)
        // 0x8000 to 0x9FFF: Video RAM
        // 0xA000 to 0xBFFF: Save RAM
        // 0xC000 to 0xCFFF: Work RAM
        // 0xD000 to 0xDFFF: Work RAM
        // 0xE000 to 0xFDFF: Echo Work RAM
        // 0xFE00 to 0xFE9F: OAM
        // 0xFEA0 to 0xFEFF: Unused
        // 0xFF00 to 0xFF7F: MMIO
        // 0xFF80 to 0xFFFE: High Speed RAM
        // 0xFFFF          : IE Register

        private readonly Dictionary<int, MemoryRegion> MemoryMap;
        private BootRomRegion BootRomRegion;
        private bool BootRomAccessible;

        public GbMemory()
        {
            MemoryMap = new Dictionary<int, MemoryRegion>();
            BootRomAccessible = false;

            RegisterRegion(new WorkRamRegion());
            RegisterRegion(new HighSpeedRamRegion());

            RegisterMmio(0xFF50, () => (byte)(BootRomAccessible ? 0 : 1), x => BootRomAccessible = false);
        }

        public void RegisterRegion(MemoryRegion region)
        {
            IEnumerable<Tuple<int, int>> ranges = region.GetHandledRanges();

            foreach (Tuple<int, int> range in ranges)
            {
                for (int i = 0; i < range.Item2; i++)
                {
                    MemoryMap[range.Item1 + i] = region;
                }
            }
        }

        public void RegisterMmio(int address, Func<byte> readFunc, Action<byte> writeFunc)
        {
            MemoryMap[address] = new MmioRegion(readFunc, writeFunc);
        }

        public void RegisterBootRom(byte[] bootRom)
        {
            BootRomRegion = new BootRomRegion(bootRom);
            BootRomAccessible = true;
        }

        public byte Read(int address)
        {
            if (BootRomAccessible)
            {
                if (MathUtil.InRange(address, 0x0, BootRomRegion.BOOT_ROM_SIZE))
                {
                    return BootRomRegion.Read(address);
                }
            }

            if (MemoryMap.TryGetValue(address, out MemoryRegion region))
            {
                return region.Read(address);
            }
            else
            {
                Console.WriteLine($"Invalid read to address {address:x}");
                return 0xFF;
            }
        }

        public void Write(int address, byte val)
        {
            if (MemoryMap.TryGetValue(address, out MemoryRegion region))
            {
                region.Write(address, val);
            }
            else
            {
                Console.WriteLine($"Invalid write to address {address:x}, value {val:x}");
            }
        }

    }
}

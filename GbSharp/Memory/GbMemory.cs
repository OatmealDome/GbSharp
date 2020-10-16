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

        private readonly Dictionary<Tuple<ushort, int>, MemoryRegion> MemoryRegions;

        public GbMemory()
        {
            MemoryRegions = new Dictionary<Tuple<ushort, int>, MemoryRegion>();

            WorkRamRegion workRam = new WorkRamRegion();
            RegisterRegion(0xC000, 0x2000, workRam);
            RegisterRegion(0xE000, 0x1E00, workRam); // Echo

            HighSpeedRamRegion highSpeedRam = new HighSpeedRamRegion();
            RegisterRegion(0xFF80, 0x7F, highSpeedRam);
        }

        public void RegisterRegion(ushort address, int size, MemoryRegion region)
        {
            MemoryRegions.Add(new Tuple<ushort, int>(address, size), region);
        }

        private Tuple<ushort, MemoryRegion> GetRegion(ushort address)
        {
            foreach (Tuple<ushort, int> regionSpan in MemoryRegions.Keys)
            {
                if (MathUtil.InRange(address, regionSpan.Item1, regionSpan.Item2))
                {
                    ushort offset = (ushort)(address - regionSpan.Item1);

                    return new Tuple<ushort, MemoryRegion>(offset, MemoryRegions[regionSpan]);
                }
            }

            return null;
        }

        public byte Read(ushort address)
        {
            Tuple<ushort, MemoryRegion> regionPair = GetRegion(address);

            if (regionPair != null)
            {
                return regionPair.Item2.Read(regionPair.Item1);
            }
            else
            {
                Console.WriteLine($"Invalid read to address {address}");
                return 0xFF;
            }
        }

        public void Write(ushort address, byte val)
        {
            Tuple<ushort, MemoryRegion> regionPair = GetRegion(address);

            if (regionPair != null)
            {
                regionPair.Item2.Write(regionPair.Item1, val);
            }
            else
            {
                Console.WriteLine($"Invalid write to address {address}, value {val}");
            }
        }
        
    }
}

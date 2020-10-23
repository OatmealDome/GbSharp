using System;
using System.Collections.Generic;

namespace GbSharp.Memory.Ram
{
    internal class HighSpeedRamRegion : MemoryRegion
    {
        private static readonly int HRAM_START = 0xFF80;
        private static readonly int HRAM_SIZE = 0x7F;

        private byte[] HighSpeedRam;

        public HighSpeedRamRegion()
        {
            HighSpeedRam = new byte[HRAM_SIZE];
        }

        public override IEnumerable<Tuple<int, int>> GetHandledRanges()
        {
            return new List<Tuple<int, int>>()
            {
                new Tuple<int, int>(HRAM_START, HRAM_SIZE)
            };
        }

        public override byte Read(int address)
        {
            return HighSpeedRam[address - HRAM_START];
        }

        public override void Write(int address, byte val)
        {
            HighSpeedRam[address - HRAM_START] = val;
        }

    }
}

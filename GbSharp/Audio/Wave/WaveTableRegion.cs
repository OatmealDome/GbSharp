using GbSharp.Memory;
using System;
using System.Collections.Generic;

namespace GbSharp.Audio.Wave
{
    class WaveTableRegion : MemoryRegion
    {
        private static int WAVE_TABLE_START = 0xFF30;
        private static int WAVE_TABLE_SIZE = 0x10;

        private byte[] WaveTable;

        public WaveTableRegion()
        {
            WaveTable = new byte[WAVE_TABLE_SIZE];
        }

        public override IEnumerable<Tuple<int, int>> GetHandledRanges()
        {
            return new List<Tuple<int, int>>()
            {
                new Tuple<int, int>(WAVE_TABLE_START, WAVE_TABLE_SIZE)
            };
        }

        public override byte Read(int address)
        {
            return ReadDirect(address & 0xF);
        }

        public override void Write(int address, byte val)
        {
            WriteDirect(address & 0xF, val);
        }

        public byte ReadDirect(int address)
        {
            return WaveTable[address];
        }

        public void WriteDirect(int address, byte val)
        {
            WaveTable[address] = val;
        }

    }
}

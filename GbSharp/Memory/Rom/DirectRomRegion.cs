using System;
using System.Collections.Generic;

namespace GbSharp.Memory.Rom
{
    class DirectRomRegion : MemoryRegion
    {
        private readonly int ROM_START = 0x0;
        private readonly int ROM_SIZE = 0x8000;

        private readonly byte[] Rom;

        public DirectRomRegion(byte[] rom)
        {
            Rom = rom;
        }

        public override IEnumerable<Tuple<int, int>> GetHandledRanges()
        {
            return new List<Tuple<int, int>>()
            {
                new Tuple<int, int>(ROM_START, ROM_SIZE)
            };
        }

        public override byte Read(int offset)
        {
            return Rom[offset];
        }

        public override void Write(int offset, byte val)
        {
            // for simple ROMs (no mapper), writing isn't allowed
            // something probably went very wrong
        }

    }
}

using System.Collections.Generic;

namespace GbSharp.Memory
{
    internal class WorkRamRegion : MemoryRegion
    {
        // 0xC000 to 0xCFFF: Work RAM (non-switchable, bank 0)
        // 0xD000 to 0xDFFF: Work RAM (switchable, banks 1 ~ 7)
        // 0xE000 to 0xEFFF: Echo Work RAM (bank 0)
        // 0xF000 to 0xFDFF: Echo Work RAM (switchable bank)

        private static readonly int BANK_SIZE = 0x1000;

        private static readonly int DMG_BANK_COUNT = 2;
        private static readonly int CGB_BANK_COUNT = 8;

        private List<byte[]> Banks;
        private int CurrentSwitchableBank;

        public WorkRamRegion()
        {
            Banks = new List<byte[]>();
            CurrentSwitchableBank = 1;

            // TODO: detect Game Boy Color and use CGB_BANK_COUNT if needed
            for (int i = 0; i < DMG_BANK_COUNT; i++)
            {
                Banks.Add(new byte[BANK_SIZE]);
            }
        }

        private bool OffsetInSwitchableBank(ushort offset)
        {
            return MathUtil.InRange(offset, (ushort)BANK_SIZE, BANK_SIZE - 1);
        }

        public override byte Read(ushort offset)
        {
            int bankIdx = 0;
            if (OffsetInSwitchableBank(offset))
            {
                bankIdx = CurrentSwitchableBank;
                offset -= (ushort)BANK_SIZE;
            }

            return Banks[bankIdx][offset];
        }

        public override void Write(ushort offset, byte val)
        {
            int bankIdx = 0;
            if (OffsetInSwitchableBank(offset))
            {
                bankIdx = CurrentSwitchableBank;
                offset -= (ushort)BANK_SIZE;
            }

            Banks[bankIdx][offset] = val;
        }

    }
}

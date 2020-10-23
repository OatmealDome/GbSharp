using System.Collections.Generic;

namespace GbSharp.Memory
{
    internal class WorkRamRegion : MemoryRegion
    {
        // 0xC000 to 0xCFFF: Work RAM (non-switchable, bank 0)
        // 0xD000 to 0xDFFF: Work RAM (switchable, banks 1 ~ 7)
        // 0xE000 to 0xEFFF: Echo Work RAM (bank 0)
        // 0xF000 to 0xFDFF: Echo Work RAM (switchable bank)

        public static readonly int WORK_RAM_START = 0xC000;
        public static readonly int WORK_RAM_BANK_START = 0xD000;
        public static readonly int ECHO_RAM_START = 0xE000;
        public static readonly int BANK_SIZE = 0x1000;

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

        private bool OffsetInSwitchableBank(int address)
        {
            return MathUtil.InRange(address, WORK_RAM_BANK_START, BANK_SIZE);
        }

        public override byte Read(int address)
        {
            int bankIdx = 0;

            if (OffsetInSwitchableBank(address))
            {
                bankIdx = CurrentSwitchableBank;
            }

            address &= 0xFFF; // mask off top bits so this is a bank offset

            return Banks[bankIdx][address];
        }

        public override void Write(int address, byte val)
        {
            int bankIdx = 0;

            if (OffsetInSwitchableBank(address))
            {
                bankIdx = CurrentSwitchableBank;
            }

            address &= 0xFFF; // mask off top bits so this is a bank offset

            Banks[bankIdx][address] = val;
        }

    }
}

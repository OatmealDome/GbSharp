using System;
using System.Collections.Generic;

namespace GbSharp.Memory
{
    internal class WorkRamRegion : MemoryRegion
    {
        // 0xC000 to 0xCFFF: Work RAM (non-switchable, bank 0)
        // 0xD000 to 0xDFFF: Work RAM (switchable, banks 1 ~ 7)
        // 0xE000 to 0xEFFF: Echo Work RAM (bank 0)
        // 0xF000 to 0xFDFF: Echo Work RAM (switchable bank)

        private static readonly int WORK_RAM_START = 0xC000;
        private static readonly int WORK_RAM_BANK_START = 0xD000;
        private static readonly int ECHO_RAM_START = 0xE000;
        private static readonly int BANK_SIZE = 0x1000;
        private static readonly int ECHO_RAM_SIZE = 0x1E00;

        private static readonly int MAX_BANK_COUNT = 8;

        private List<byte[]> Banks;
        private int CurrentSwitchableBank;

        public WorkRamRegion(GbMemory memory)
        {
            Banks = new List<byte[]>();
            CurrentSwitchableBank = 1;

            for (int i = 0; i < MAX_BANK_COUNT; i++)
            {
                Banks.Add(new byte[BANK_SIZE]);
            }

            memory.RegisterMmio(0xFF4D, () =>
            {
                return (byte)(CurrentSwitchableBank | 0xF8);
            }, (x) =>
            {
                // Ignore writes on DMG
                if (HardwareType == HardwareType.Cgb)
                {
                    CurrentSwitchableBank = x & 0x7;

                    // WRAM bank 0 is always at 0xC000
                    if (CurrentSwitchableBank == 0)
                    {
                        CurrentSwitchableBank = 1;
                    }
                }
            });
        }

        public override IEnumerable<Tuple<int, int>> GetHandledRanges()
        {
            return new List<Tuple<int, int>>()
            {
                new Tuple<int, int>(WORK_RAM_START, BANK_SIZE * 2),
                new Tuple<int, int>(ECHO_RAM_START, ECHO_RAM_SIZE)
            };
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

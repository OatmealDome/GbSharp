namespace GbSharp.Memory.Rom
{
    class Mbc3RomRegion : CartridgeRomRegion
    {
        // MBC3 Registers
        private int RomBank;
        private int MultiPurposeSelect;
        // TODO: RTC Latch and Registers

        public Mbc3RomRegion(byte[] rom) : base(rom)
        {
            RomBank = 1;
            MultiPurposeSelect = 0;
        }

        public override byte Read(int address)
        {
            if (MathUtil.InRange(address, ROM_START, ROM_BANK_SIZE))
            {
                return Rom[address];
            }
            else if (MathUtil.InRange(address, ROM_START + ROM_BANK_SIZE, ROM_BANK_SIZE))
            {
                return Rom[RomBank << 14 | address & 0x3FFF];
            }
            else if (MathUtil.InRange(address, EXTERNAL_RAM_START, EXTERNAL_RAM_SIZE))
            {
                // RAM enable flag is used for both external RAM and the RTC
                if (!RamEnabled)
                {
                    return 0xFF;
                }

                if (MultiPurposeSelect <= 3)
                {
                    return Ram[MultiPurposeSelect << 13 | address & 0x1FFF];
                }
                else // RTC Register
                {
                    // TODO
                    return 0x00;
                }
            }

            return 0xFF;
        }

        public override void Write(int address, byte val)
        {
            if (MathUtil.InRange(address, 0x0, 0x2000))
            {
                RamEnabled = MathUtil.IsBitSet(val, 1);
            }
            else if (MathUtil.InRange(address, 0x2000, 0x2000))
            {
                RomBank = val;

                // This register is not allowed to be zero
                if (RomBank == 0)
                {
                    RomBank = 1;
                }
            }
            else if (MathUtil.InRange(address, 0x4000, 0x2000))
            {
                MultiPurposeSelect = val;
            }
            else if (MathUtil.InRange(address, 0x6000, 0x2000))
            {
                // TODO: RTC Latch
            }
            else if (MathUtil.InRange(address, EXTERNAL_RAM_START, EXTERNAL_RAM_SIZE))
            {
                if (!RamEnabled)
                {
                    return;
                }

                if (MultiPurposeSelect <= 3)
                {
                    Ram[MultiPurposeSelect << 13 | address & 0x1FFF] = val;
                }
                else // RTC Register
                {
                    // TODO
                }
            }
        }

    }
}

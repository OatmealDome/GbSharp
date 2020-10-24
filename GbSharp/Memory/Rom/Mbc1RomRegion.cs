namespace GbSharp.Memory.Rom
{
    class Mbc1RomRegion : CartridgeRomRegion
    {
        // MBC1 Registers
        private int CurrentBankPrimary;
        private int CurrentBankSecondary;
        private int Mode;

        // TODO: This doesn't support multicart MBC1 ROMs.
        public Mbc1RomRegion(byte[] rom) : base(rom)
        {
            CurrentBankPrimary = 1;
            CurrentBankSecondary = 0;
            Mode = 0;
        }

        private int GetRamOffset(int address)
        {
            if (Mode == 0)
            {
                return address & 0xFFF;
            }
            else
            {
                return (CurrentBankSecondary << 13) | (address & 0x1FFF);
            }
        }

        public override byte Read(int address)
        {
            int romBank = 0;

            if (MathUtil.InRange(address, ROM_START, ROM_BANK_SIZE))
            {
                if (Mode == 1)
                {
                    romBank = CurrentBankSecondary << 5;
                }
            }
            else if (MathUtil.InRange(address, ROM_START + ROM_BANK_SIZE, ROM_BANK_SIZE))
            {
                romBank = CurrentBankSecondary << 5 | CurrentBankPrimary;
                address &= 0x3FFF; // mask top two bits so it's a bank offset
            }
            else if (MathUtil.InRange(address, EXTERNAL_RAM_START, EXTERNAL_RAM_SIZE))
            {
                return Ram[GetRamOffset(address)];
            }

            return Rom[romBank << 14 | address];
        }

        public override void Write(int address, byte val)
        {
            if (MathUtil.InRange(address, 0x0, 0x2000))
            {
                RamEnabled = MathUtil.IsBitSet(val, 1);
            }
            else if (MathUtil.InRange(address, 0x2000, 0x2000))
            {
                CurrentBankPrimary = val & 0x1F;

                // This register is not allowed to be zero
                if (CurrentBankPrimary == 0)
                {
                    CurrentBankPrimary = 1;
                }
            }
            else if (MathUtil.InRange(address, 0x4000, 0x2000))
            {
                CurrentBankSecondary = val & 0x3;
            }
            else if (MathUtil.InRange(address, 0x6000, 0x2000))
            {
                Mode = val & 0x1;
            }
            else if (MathUtil.InRange(address, EXTERNAL_RAM_START, EXTERNAL_RAM_SIZE))
            {
                if (RamEnabled)
                {
                    Ram[GetRamOffset(address)] = val;
                }
            }
        }

    }
}

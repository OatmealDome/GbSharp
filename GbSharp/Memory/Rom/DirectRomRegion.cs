namespace GbSharp.Memory.Rom
{
    class DirectRomRegion : CartridgeRomRegion
    {
        public DirectRomRegion(byte[] rom) : base(rom)
        {

        }

        public override byte Read(int address)
        {
            if (MathUtil.InRange(address, EXTERNAL_RAM_START, EXTERNAL_RAM_SIZE))
            {
                return Ram[address & 0x1FFF];
            }
            else
            {
                return Rom[address];
            }
        }

        public override void Write(int address, byte val)
        {
            if (MathUtil.InRange(address, EXTERNAL_RAM_START, EXTERNAL_RAM_SIZE))
            {
                Ram[address & 0x1FFF] = val;
            }

            // for simple ROMs (no mapper), writing isn't allowed
            // something probably went very wrong
        }

    }
}

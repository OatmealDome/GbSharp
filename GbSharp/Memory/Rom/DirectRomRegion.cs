namespace GbSharp.Memory.Rom
{
    class DirectRomRegion : MemoryRegion
    {
        private readonly byte[] Rom;

        public DirectRomRegion(byte[] rom)
        {
            Rom = rom;
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

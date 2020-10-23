namespace GbSharp.Memory.Rom
{
    class DirectRomRegion : MemoryRegion
    {
        private readonly byte[] Rom;

        public DirectRomRegion(byte[] rom)
        {
            Rom = rom;
        }

        public override byte Read(ushort offset)
        {
            return Rom[offset];
        }

        public override void Write(ushort offset, byte val)
        {
            // for simple ROMs (no mapper), writing isn't allowed
            // something probably went very wrong
        }

    }
}

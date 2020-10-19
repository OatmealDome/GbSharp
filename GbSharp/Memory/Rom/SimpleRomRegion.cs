namespace GbSharp.Memory.Rom
{
    class SimpleRomRegion : MemoryRegion
    {
        private readonly byte[] Rom;

        public SimpleRomRegion(byte[] rom)
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

namespace GbSharp.Memory
{
    internal class HighSpeedRamRegion : MemoryRegion
    {
        private byte[] HighSpeedRam;

        public HighSpeedRamRegion()
        {
            HighSpeedRam = new byte[0x7F];
        }

        public override byte Read(ushort offset)
        {
            return HighSpeedRam[offset];
        }

        public override void Write(ushort offset, byte val)
        {
            HighSpeedRam[offset] = val;
        }

    }
}

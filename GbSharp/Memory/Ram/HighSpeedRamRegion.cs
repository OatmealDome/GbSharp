namespace GbSharp.Memory.Ram
{
    internal class HighSpeedRamRegion : MemoryRegion
    {
        public static readonly int HRAM_START = 0xFF80;
        public static readonly int HRAM_SIZE = 0x7F;

        private byte[] HighSpeedRam;

        public HighSpeedRamRegion()
        {
            HighSpeedRam = new byte[HRAM_SIZE];
        }

        public override byte Read(ushort address)
        {
            return HighSpeedRam[address - HRAM_START];
        }

        public override void Write(ushort address, byte val)
        {
            HighSpeedRam[address - HRAM_START] = val;
        }

    }
}

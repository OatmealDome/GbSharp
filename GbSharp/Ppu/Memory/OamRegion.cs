using GbSharp.Memory;

namespace GbSharp.Ppu.Memory
{
    class OamRegion : MemoryRegion
    {
        public static readonly int OAM_SIZE = 0x100;

        private byte[] Oam;
        private bool Locked;

        public OamRegion()
        {
            Oam = new byte[OAM_SIZE];
        }

        public void Lock()
        {
            Locked = true;
        }

        public void Unlock()
        {
            Locked = false;
        }

        public override byte Read(ushort offset)
        {
            // Reading OAM from the CPU during certain modes is disallowed
            if (Locked)
            {
                return 0xFF;
            }

            return ReadDirect(offset);
        }

        public override void Write(ushort offset, byte val)
        {
            // Writes to OAM from the CPU during certain modes are ignored
            if (!Locked)
            {
                WriteDirect(offset, val);
            }
        }

        public byte ReadDirect(ushort offset)
        {
            return Oam[offset];
        }

        public void WriteDirect(ushort offset, byte val)
        {
            Oam[offset] = val;
        }

    }
}

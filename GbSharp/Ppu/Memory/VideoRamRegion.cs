using GbSharp.Memory;

namespace GbSharp.Ppu.Memory
{
    class VideoRamRegion : MemoryRegion
    {
        public static readonly int VIDEO_RAM_SIZE = 0x2000;

        private byte[] BankOne;
        private byte[] BankTwo; // CGB
        private int CurrentSwitchableBank;
        private bool Locked;

        public VideoRamRegion()
        {
            BankOne = new byte[VIDEO_RAM_SIZE];
            BankTwo = new byte[VIDEO_RAM_SIZE];
            CurrentSwitchableBank = 0;
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
            // Reading VRAM from the CPU during certain modes is disallowed
            if (Locked)
            {
                return 0xFF;
            }

            return ReadDirect(offset);
        }

        public override void Write(ushort offset, byte val)
        {
            // Writes to VRAM from the CPU during certain modes are ignored
            if (!Locked)
            {
                WriteDirect(offset, val);
            }
        }

        private byte[] GetCurrentBank()
        {
            if (CurrentSwitchableBank == 0)
            {
                return BankOne;
            }
            else
            {
                return BankTwo;
            }
        }

        public byte ReadDirect(ushort offset)
        {
            return GetCurrentBank()[offset];
        }

        public void WriteDirect(ushort offset, byte val)
        {
            GetCurrentBank()[offset] = val;
        }

    }
}

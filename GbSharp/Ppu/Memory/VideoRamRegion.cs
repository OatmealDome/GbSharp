using GbSharp.Memory;
using System.Collections.Generic;

namespace GbSharp.Ppu.Memory
{
    class VideoRamRegion : MemoryRegion
    {
        public static readonly int VIDEO_RAM_SIZE = 0x2000;

        private List<byte[]> Banks;
        private int CurrentSwitchableBank;
        private bool Locked;

        public VideoRamRegion()
        {
            Banks = new List<byte[]>()
            {
                new byte[VIDEO_RAM_SIZE],
                new byte[VIDEO_RAM_SIZE] // CGB
            };
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

        public byte ReadDirect(ushort offset)
        {
            return Banks[CurrentSwitchableBank][offset];
        }

        public void WriteDirect(ushort offset, byte val)
        {
            Banks[CurrentSwitchableBank][offset] = val;
        }

    }
}

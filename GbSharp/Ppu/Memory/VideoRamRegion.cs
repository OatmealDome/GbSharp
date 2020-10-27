using GbSharp.Memory;
using System;
using System.Collections.Generic;

namespace GbSharp.Ppu.Memory
{
    class VideoRamRegion : MemoryRegion
    {
        private static readonly int VIDEO_RAM_START = 0x8000;
        private static readonly int VIDEO_RAM_SIZE = 0x2000;

        private List<byte[]> Banks;
        private int CurrentSwitchableBank;
        private bool Locked;

        public VideoRamRegion(GbMemory memory)
        {
            Banks = new List<byte[]>()
            {
                new byte[VIDEO_RAM_SIZE],
                new byte[VIDEO_RAM_SIZE] // CGB
            };
            CurrentSwitchableBank = 0;

            memory.RegisterMmio(0xFF4F, () =>
            {
                return (byte)CurrentSwitchableBank;
            }, (x) =>
            {
                // ignore if not CGB
                if (HardwareType == HardwareType.Cgb)
                {
                    CurrentSwitchableBank = MathUtil.GetBit(x, 0);
                }
            });
        }

        public void Lock()
        {
            Locked = true;
        }

        public void Unlock()
        {
            Locked = false;
        }

        public override IEnumerable<Tuple<int, int>> GetHandledRanges()
        {
            return new List<Tuple<int, int>>()
            {
                new Tuple<int, int>(VIDEO_RAM_START, VIDEO_RAM_SIZE)
            };
        }

        public override byte Read(int address)
        {
            // Reading VRAM from the CPU during certain modes is disallowed
            if (Locked)
            {
                return 0xFF;
            }

            return ReadDirect(address & 0x1FFF);
        }

        public override void Write(int address, byte val)
        {
            // Writes to VRAM from the CPU during certain modes are ignored
            if (!Locked)
            {
                WriteDirect(address & 0x1FFF, val);
            }
        }

        public byte ReadDirect(int offset, int bank = -1)
        {
            if (bank == -1)
            {
                bank = CurrentSwitchableBank;
            }

            return Banks[bank][offset];
        }

        public void WriteDirect(int offset, byte val, int bank = -1)
        {
            if (bank == -1)
            {
                bank = CurrentSwitchableBank;
            }

            Banks[bank][offset] = val;
        }

    }
}

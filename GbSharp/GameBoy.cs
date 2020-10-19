using GbSharp.Cpu;
using GbSharp.Memory;
using GbSharp.Memory.Rom;

namespace GbSharp
{
    public class GameBoy
    {
        private readonly GbMemory MemoryMap;
        private readonly GbCpu Cpu;

        public GameBoy()
        {
            MemoryMap = new GbMemory();
            Cpu = new GbCpu(MemoryMap);
        }

        public void LoadRom(byte[] rom)
        {
            SimpleRomRegion romRegion = new SimpleRomRegion(rom);
            MemoryMap.RegisterRegion(0x0, 0x8000, romRegion);
        }

    }
}

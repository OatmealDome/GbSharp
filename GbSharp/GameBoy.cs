// #define FRAME_TIME_LOGGING

using GbSharp.Cpu;
using GbSharp.Memory;
using GbSharp.Memory.Rom;
using GbSharp.Ppu;
using System;
using System.Diagnostics;

namespace GbSharp
{
    public class GameBoy
    {
        private readonly GbMemory MemoryMap;
        private readonly GbCpu Cpu;
        private readonly GbPpu Ppu;

#if FRAME_TIME_LOGGING
        private double[] RunningAverages = new double[120];
        private int CurrentAverageIdx = 0;
        private int AddedAvgs = 0;
#endif

        public GameBoy()
        {
            MemoryMap = new GbMemory();
            Cpu = new GbCpu(MemoryMap);
            Ppu = new GbPpu(Cpu, MemoryMap);
        }

        public void RunFrame()
        {
#if FRAME_TIME_LOGGING
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
#endif

            int cycles = 0;

            // At 1,048,576 M-cycles/s and 59.7 frames/s, the Game Boy runs approx
            // 17,564.087 M-cycles per second. Here, this number is rounded to the
            // nearest whole number, so the emulator will be very slightly slower
            // than a real Game Boy.
            while (cycles < 17564)
            {
                int lastCpuCycles = Cpu.Step();

                for (int i = 0; i < (lastCpuCycles * 4); i++)
                {
                    Ppu.Update();
                }

                cycles += lastCpuCycles;
            }

#if FRAME_TIME_LOGGING
            stopwatch.Stop();

            RunningAverages[CurrentAverageIdx] = stopwatch.ElapsedMilliseconds;
            CurrentAverageIdx++;
            if (CurrentAverageIdx == RunningAverages.Length)
            {
                CurrentAverageIdx = 0;
            }

            if (AddedAvgs != RunningAverages.Length)
            {
                AddedAvgs++;
            }
            else
            {
                double avg = 0;
                for (int i = 0; i < RunningAverages.Length; i++)
                {
                    avg += RunningAverages[i];
                }

                avg /= RunningAverages.Length;

                avg = Math.Round(avg, 3);

                Console.WriteLine($"frame time avg {avg}ms");
            }
#endif
        }

        public void LoadRom(byte[] rom, byte[] bootRom = null)
        {
            if (bootRom != null)
            {
                MemoryMap.RegisterBootRom(bootRom);
            }
            else
            {
                Cpu.SetDefaultStateAfterBootRom();
            }

            MemoryMap.RegisterRegion(CartridgeRomRegion.CreateRomRegion(rom));
        }

        public byte[] GetPixelOutput()
        {
            return Ppu.GetPixelOutput();
        }
        
    }
}

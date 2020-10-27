// #define FRAME_TIME_LOGGING

using GbSharp.Audio;
using GbSharp.Controller;
using GbSharp.Cpu;
using GbSharp.Memory;
using GbSharp.Memory.Rom;
using GbSharp.Ppu;
using GbSharp.Timer;
using System;
using System.Diagnostics;

namespace GbSharp
{
    public class GameBoy
    {
        private readonly GbMemory MemoryMap;
        private readonly GbCpu Cpu;
        private readonly GbTimer Timer;
        private readonly GbPpu Ppu;
        private readonly GbApu Apu;
        private readonly GbController Controller;

        private HardwareType HardwareType;

        public delegate void NotifySamplesReady(float[] samples);
        public event NotifySamplesReady SamplesReady;

#if FRAME_TIME_LOGGING
        private double[] RunningAverages = new double[120];
        private int CurrentAverageIdx = 0;
        private int AddedAvgs = 0;
#endif

        public GameBoy(HardwareType type = HardwareType.AutoSelect)
        {
            MemoryMap = new GbMemory();
            Cpu = new GbCpu(MemoryMap);
            Timer = new GbTimer(Cpu, MemoryMap);
            Ppu = new GbPpu(Cpu, MemoryMap);
            Apu = new GbApu(MemoryMap);
            Controller = new GbController(Cpu, MemoryMap);

            SetHardwareType(type);
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

                Timer.Tick(lastCpuCycles);

                for (int i = 0; i < (lastCpuCycles * 4); i++)
                {
                    Ppu.Update();
                    
                    if (Apu.Tick())
                    {
                        // Samples are ready for output
                        SamplesReady?.Invoke(Apu.GetSampleBuffer());
                    }
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

        private void SetHardwareType(HardwareType type)
        {
            HardwareType = type;

            MemoryMap.SetHardwareType(type);
            Cpu.SetHardwareType(type);
            Ppu.SetHardwareType(type);
        }

        public void LoadRom(byte[] rom, byte[] bootRom = null)
        {
            CartridgeRomRegion romRegion = CartridgeRomRegion.CreateRomRegion(rom);

            if (HardwareType == HardwareType.AutoSelect)
            {
                SetHardwareType(romRegion.GetBestSupportedHardware());
            }

            if (bootRom != null)
            {
                MemoryMap.RegisterBootRom(bootRom);
            }
            else
            {
                Cpu.SetDefaultStateAfterBootRom();
            }

            MemoryMap.RegisterRegion(romRegion);
        }

        public void UpdateControllerKeyState(ControllerKey key, bool state)
        {
            Controller.UpdateKeyState(key, state);
        }

        public byte[] GetPixelOutput()
        {
            return Ppu.GetPixelOutput();
        }
        
    }
}

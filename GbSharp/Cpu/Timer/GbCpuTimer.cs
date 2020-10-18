using GbSharp.Memory;

namespace GbSharp.Cpu.Timer
{
    class GbCpuTimer
    {
        private bool Enabled;
        private TimerSpeed Speed;

        private byte Divider;
        private byte Counter;
        private byte Modulo;

        private int CycleCount;
        private bool OverflowOccurred;

        private readonly GbCpu Cpu;

        public GbCpuTimer(GbCpu cpu, GbMemory memory)
        {
            memory.RegisterMmio(0xFF04, () => Divider, (x) => Divider = 0);
            memory.RegisterMmio(0xFF05, () => Counter, (x) => Counter = x);
            memory.RegisterMmio(0xFF06, () => Modulo, (x) => Modulo = x);
            memory.RegisterMmio(0xFF07, GetControl, SetControl);

            CycleCount = 0;
            OverflowOccurred = true;

            Cpu = cpu;
        }

        private byte GetControl()
        {
            byte value = (byte)Speed;

            if (Enabled)
            {
                MathUtil.SetBit(ref value, 2);
            }

            return value;
        }

        private void SetControl(byte value)
        {
            Enabled = MathUtil.IsBitSet(value, 2);
            Speed = (TimerSpeed)(value & 0x3);
        }

        /// <summary>
        /// Ticks the clock.
        /// </summary>
        /// <param name="cycles">The number of M-Cycles that has passed.</param>
        public void Tick(int cycles)
        {
            Divider++;

            if (OverflowOccurred)
            {
                Cpu.RaiseInterrupt(2);

                Counter = Modulo;

                OverflowOccurred = false;
            }

            CycleCount += cycles;

            int cyclesToNextTick = 0;
            switch (Speed)
            {
                case TimerSpeed.Zero:
                    cyclesToNextTick = 256;
                    break;
                case TimerSpeed.One:
                    cyclesToNextTick = 4;
                    break;
                case TimerSpeed.Two:
                    cyclesToNextTick = 16;
                    break;
                case TimerSpeed.Three:
                    cyclesToNextTick = 64;
                    break;
            }

            if (CycleCount >= cyclesToNextTick)
            {
                Counter++;

                CycleCount -= cyclesToNextTick;

                if (Counter == 0)
                {
                    OverflowOccurred = true;
                }
            }
        }

    }
}

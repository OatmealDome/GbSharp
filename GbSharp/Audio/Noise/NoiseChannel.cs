using GbSharp.Memory;

namespace GbSharp.Audio.Noise
{
    class NoiseChannel : AudioChannel
    {
        private int LfsrState;
        private int CurrentEnvelopePeriod;
        private int TicksToNextFrequencyClock;
        private int TicksToNextFrequencyClockDefault;

        // NR42 (Envelope: Start Volume, Envelope Mode, Period)
        private int StartVolume;
        private bool IncreaseEnvelope;
        private int EnvelopePeriod; // also called steps in official manual

        // NR43 (Clock Shift, Width, Divisor)
        private int ClockShift;
        private bool LfsrWidthIsSevenBit;
        private int DivisorCode;

        protected override bool MultiplyVolumeAfterTick => true;

        public NoiseChannel(GbMemory memory, int startAddress) : base(memory)
        {
            // Unused register
            MemoryMap.RegisterMmio(startAddress, () =>
            {
                return 0xFF;
            }, (x) =>
            {
                // nothing
            });

            MemoryMap.RegisterMmio(startAddress + 1, () =>
            {
                // length cannot be read
                return 0x3F;
            }, (x) =>
            {
                LengthData = x & 0x3F;
                LengthCounter = 64 - LengthData;
            });

            MemoryMap.RegisterMmio(startAddress + 2, () =>
            {
                byte b = (byte)(StartVolume << 4);

                if (IncreaseEnvelope)
                {
                    MathUtil.SetBit(ref b, 3);
                }

                b |= (byte)EnvelopePeriod;

                return b;
            }, (x) =>
            {
                DacEnabled = (x & 0xF8) != 0; // DAC enabled when upper 5 bits set
                StartVolume = x >> 4;
                IncreaseEnvelope = MathUtil.IsBitSet(x, 3);
                EnvelopePeriod = x & 0x7; // lower 3 bits

                // Quirk: a period of 0 is treated as 8
                if (EnvelopePeriod == 0)
                {
                    EnvelopePeriod = 8;
                }
            });

            MemoryMap.RegisterMmio(startAddress + 3, () =>
            {
                byte b = (byte)(ClockShift << 4);

                if (LfsrWidthIsSevenBit)
                {
                    MathUtil.SetBit(ref b, 3);
                }

                b |= (byte)DivisorCode;

                return b;
            }, (x) =>
            {
                ClockShift = x >> 4;
                LfsrWidthIsSevenBit = MathUtil.IsBitSet(x, 3);
                DivisorCode = x & 0x7; // lower 3 bits

                ResetFrequencyClocks();
            });

            MemoryMap.RegisterMmio(startAddress + 4, () =>
            {
                byte b = 0;

                if (LengthEnabled)
                {
                    MathUtil.SetBit(ref b, 6);
                }

                return b;
            }, (x) =>
            {
                bool WillBeEnabled = MathUtil.IsBitSet(x, 7);
                LengthEnabled = MathUtil.IsBitSet(x, 6);

                if (WillBeEnabled)
                {
                    EnableChannel();
                }
                else if (!WillBeEnabled)
                {
                    Enabled = false;
                }
            });
        }

        private void ResetFrequencyClocks()
        {
            int divisor = 0;
            switch (DivisorCode)
            {
                case 0:
                    divisor = 8;
                    break;
                case 1:
                    divisor = 16;
                    break;
                case 2:
                    divisor = 32;
                    break;
                case 3:
                    divisor = 48;
                    break;
                case 4:
                    divisor = 64;
                    break;
                case 5:
                    divisor = 80;
                    break;
                case 6:
                    divisor = 96;
                    break;
                case 7:
                    divisor = 112;
                    break;
            }

            TicksToNextFrequencyClockDefault = divisor << ClockShift;
        }

        protected override void EnableChannel()
        {
            Enabled = true;

            if (LengthCounter == 0)
            {
                LengthCounter = 64;
            }

            LfsrState = 0x7FFF; // 15 bits set to 1
            TicksToNextFrequencyClock = TicksToNextFrequencyClockDefault;
            CurrentEnvelopePeriod = EnvelopePeriod;
            Volume = StartVolume;
        }

        protected override void TickChannel()
        {
            TicksToNextFrequencyClock--;

            if (TicksToNextFrequencyClock == 0)
            {
                int xorResult = LfsrState & 0x1 ^ ((LfsrState & 0x2) >> 1);

                LfsrState = LfsrState >> 1;

                LfsrState |= xorResult << 14;

                if (LfsrWidthIsSevenBit)
                {
                    LfsrState = (LfsrState & 0x7FBF) | (xorResult << 6);
                }

                TicksToNextFrequencyClock = TicksToNextFrequencyClockDefault;
            }

            Sample = ~(LfsrState & 0x1);
        }

        protected override void ClockVolumeOrEnvelope()
        {
            if (EnvelopePeriod == 0)
            {
                return;
            }

            CurrentEnvelopePeriod--;

            if (CurrentEnvelopePeriod == 0)
            {
                CurrentEnvelopePeriod = EnvelopePeriod;

                if (IncreaseEnvelope && Volume < 15)
                {
                    Volume++;
                }
                else if (!IncreaseEnvelope && Volume > 0)
                {
                    Volume--;
                }
            }
        }

    }
}

using GbSharp.Memory;

namespace GbSharp.Audio.Square
{
    class SquareChannel : AudioChannel
    {
        private byte[][] DutyCycles = new byte[][]
        {
            new byte[] { 0, 0, 0, 0,  0, 0, 0, 1 },
            new byte[] { 1, 0, 0, 0,  0, 0, 0, 1 },
            new byte[] { 1, 0, 0, 0,  0, 1, 1, 1 },
            new byte[] { 0, 1, 1, 1,  1, 1, 1, 0 }
        };

        private bool HasSweep;
        private int CurrentDutyCycleIdx;
        private int CurrentEnvelopePeriod;
        private int TicksToNextFrequencyClock;
        private int TicksToNextFrequencyClockDefault;

        // NR10 (Sweep)
        private int SweepPeriod;
        private bool SweepNegate;
        private int SweepShift;

        // NR11 (Duty, length)
        private int DutyCycle;

        // NR12 (Envelope: Start Volume, Envelope Mode, Period)
        private int StartVolume;
        private bool IncreaseEnvelope;
        private int EnvelopePeriod; // also called steps in official manual

        // MR13 and MR14 (Frequency LSB and MSB)
        private int Frequency;

        protected override bool MultiplyVolumeAfterTick => true;

        public SquareChannel(GbMemory memory, int startAddress, bool hasSweep) : base(memory)
        {
            HasSweep = hasSweep;

            if (HasSweep)
            {
                MemoryMap.RegisterMmio(startAddress, () =>
                {
                    byte b = (byte)(SweepPeriod << 4);

                    if (SweepNegate)
                    {
                        MathUtil.SetBit(ref b, 3);
                    }

                    SweepShift |= SweepShift;

                    return b;
                }, (x) =>
                {
                    SweepPeriod = x >> 4;
                    SweepNegate = MathUtil.IsBitSet(x, 3);
                    SweepShift = x & 0x7; // lower 3 bits

                    // Quirk: a period of 0 is treated as 8
                    if (SweepPeriod == 0)
                    {
                        SweepPeriod = 8;
                    }
                });
            }

            MemoryMap.RegisterMmio(startAddress + 1, () =>
            {
                // length cannot be read
                return (byte)((DutyCycle << 6) | 0x3F);
            }, (x) =>
            {
                DutyCycle = x >> 6;
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
                return (byte)(Frequency & 0xFF);
            }, (x) =>
            {
                ResetFrequencyClocks((Frequency & 0x700) | x);
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

                int frequencyMsb = (x << 8) & 0x700;

                ResetFrequencyClocks(frequencyMsb | (Frequency & 0xFF));

                if (WillBeEnabled)
                {
                    EnableChannel();
                }
            });
        }

        private void ResetFrequencyClocks(int newFrequency)
        {
            if (Frequency == newFrequency)
            {
                return;
            }

            Frequency = newFrequency;
            TicksToNextFrequencyClockDefault = ((2048 - Frequency) * 4);
        }

        protected override void EnableChannel()
        {
            Enabled = true;

            if (LengthCounter == 0)
            {
                LengthCounter = 64;
            }

            TicksToNextFrequencyClock = TicksToNextFrequencyClockDefault;
            CurrentEnvelopePeriod = EnvelopePeriod;
            Volume = StartVolume;

            // TODO: sweep
        }

        protected override void TickChannel()
        {
            if (HasSweep)
            {
                // TODO
            }

            TicksToNextFrequencyClock--;

            if (TicksToNextFrequencyClock == 0)
            {
                CurrentDutyCycleIdx++;

                if (CurrentDutyCycleIdx == 8)
                {
                    CurrentDutyCycleIdx = 0;
                }

                TicksToNextFrequencyClock = TicksToNextFrequencyClockDefault;
            }

            Sample = DutyCycles[DutyCycle][CurrentDutyCycleIdx];
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

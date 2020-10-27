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
        private bool SweepEnabled;
        private int CurrentSweepPeriod;
        private uint FrequencyShadowRegister;
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
        private uint Frequency;

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

                uint frequencyMsb = (uint)((x << 8) & 0x700);

                ResetFrequencyClocks(frequencyMsb | (Frequency & 0xFF));

                if (WillBeEnabled)
                {
                    EnableChannel();
                }
            });
        }

        private void ResetFrequencyClocks(uint newFrequency)
        {
            Frequency = newFrequency;
            TicksToNextFrequencyClockDefault = (int)((2048 - Frequency) * 4);
        }

        private uint CalculateNewFrequencyForSweep()
        {
            uint newFrequency = FrequencyShadowRegister >> SweepShift;

            if (!SweepNegate)
            {
                newFrequency = FrequencyShadowRegister + newFrequency;
            }
            else
            {
                newFrequency = FrequencyShadowRegister - newFrequency;
            }

            if (newFrequency > 2047)
            {
                Enabled = false;
            }

            return newFrequency;
        }

        protected override void EnableChannel()
        {
            Enabled = true;

            if (LengthCounter == 0)
            {
                LengthCounter = 64;
            }

            CurrentDutyCycleIdx = 0;
            TicksToNextFrequencyClock = TicksToNextFrequencyClockDefault;
            CurrentEnvelopePeriod = EnvelopePeriod;
            Volume = StartVolume;

            if (HasSweep)
            {
                SweepEnabled = SweepPeriod != 0 || SweepShift != 0;
                
                CurrentSweepPeriod = SweepPeriod;

                // Quirk: a period of 0 is treated as 8
                if (SweepPeriod == 0)
                {
                    CurrentSweepPeriod = 8;
                }

                FrequencyShadowRegister = Frequency;

                if (SweepShift != 0)
                {
                    CalculateNewFrequencyForSweep();
                }
            }
        }

        protected override void TickChannel()
        {
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

        protected override void ClockPreChannelTick()
        {
            base.ClockPreChannelTick();

            if (!HasSweep)
            {
                return;
            }

            // Sweep
            if (FrameSequencer == 2 || FrameSequencer == 6)
            {
                CurrentSweepPeriod--;

                if (CurrentSweepPeriod == 0)
                {
                    CurrentSweepPeriod = SweepPeriod;

                    // Quirk: a period of 0 is treated as 8
                    if (SweepPeriod == 0)
                    {
                        CurrentSweepPeriod = 8;
                    }

                    if (SweepEnabled && SweepPeriod != 0)
                    {
                        uint frequency = CalculateNewFrequencyForSweep();
                        if (Enabled && SweepShift != 0) // if still enabled, no overflow occurred
                        {
                            FrequencyShadowRegister = frequency;
                            ResetFrequencyClocks(frequency);

                            CalculateNewFrequencyForSweep();
                        }
                    }
                }
            }
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

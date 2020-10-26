using GbSharp.Memory;

namespace GbSharp.Audio.Wave
{
    class WaveChannel : AudioChannel
    {
        private int CurrentNybble;
        private int SampleBuffer;
        private int TicksToNextFrequencyClock;
        private int TicksToNextFrequencyClockDefault;

        // NR12 (Envelope: Start Volume, Envelope Mode, Period)
        private int StartVolume;

        // MR13 and MR14 (Frequency LSB and MSB)
        private int Frequency;

        private WaveTableRegion WaveTableRegion;

        // "Volume" isn't really volume, since all it does is shift the sample
        protected override bool MultiplyVolumeAfterTick => false;

        public WaveChannel(GbMemory memory, int startAddress) : base(memory)
        {
            WaveTableRegion = new WaveTableRegion();

            MemoryMap.RegisterRegion(WaveTableRegion);

            MemoryMap.RegisterMmio(startAddress, () =>
            {
                byte b = 0;

                if (DacEnabled)
                {
                    MathUtil.SetBit(ref b, 7);
                }

                return b;
            }, (x) =>
            {
                DacEnabled = MathUtil.IsBitSet(x, 7);
            });

            MemoryMap.RegisterMmio(startAddress + 1, () =>
            {
                // length cannot be read
                return 0xFF;
            }, (x) =>
            {
                LengthData = x;
                LengthCounter = 256 - LengthData;
            });

            MemoryMap.RegisterMmio(startAddress + 2, () =>
            {
                byte b = (byte)(StartVolume << 5);

                return b;
            }, (x) =>
            {
                StartVolume = x >> 5;
            });

            MemoryMap.RegisterMmio(startAddress + 3, () =>
            {
                // LSB of frequency can't be read
                return 0xFF;
            }, (x) =>
            {
                ResetFrequencyClocks(Frequency & 0x700 | x);
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

                int frequencyMsb = x << 8 & 0x700;

                ResetFrequencyClocks(frequencyMsb | Frequency & 0xFF);

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
            TicksToNextFrequencyClockDefault = (2048 - Frequency) * 2;
        }

        protected override void EnableChannel()
        {
            Enabled = true;

            if (LengthCounter == 0)
            {
                LengthCounter = 256;
            }

            TicksToNextFrequencyClock = TicksToNextFrequencyClockDefault;
            CurrentNybble = 0;
            Volume = StartVolume;

            // Quirk: the sample buffer isn't cleared, so the first sample output
            // when the channel is enabled will be the high nybble of the last
            // played sample byte
        }

        protected override void TickChannel()
        {
            TicksToNextFrequencyClock--;

            if (TicksToNextFrequencyClock == 0)
            {
                CurrentNybble++;

                if (CurrentNybble == 32)
                {
                    CurrentNybble = 0;
                }

                int byteOfs = CurrentNybble / 2;
                SampleBuffer = WaveTableRegion.ReadDirect(byteOfs);

                TicksToNextFrequencyClock = TicksToNextFrequencyClockDefault;
            }

            bool isHigh = CurrentNybble % 2 == 0;

            if (isHigh)
            {
                Sample = SampleBuffer >> 4;
            }
            else
            {
                Sample = SampleBuffer & 0xF;
            }
        }

        protected override void ClockVolumeOrEnvelope()
        {
            int shift = 0; // (00=0%, 01=100%, 10=50%, 11=25%)
            switch (Volume)
            {
                case 0:
                    shift = 4;
                    break;
                case 1:
                    shift = 0;
                    break;
                case 2:
                    shift = 1;
                    break;
                case 3:
                    shift = 2;
                    break;
                default:
                    break;
            }

            Sample = Sample >> shift;
        }

    }
}

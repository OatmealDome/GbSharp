using GbSharp.Audio.Square;
using GbSharp.Memory;

namespace GbSharp.Audio
{
    class GbApu
    {
        private SquareChannel SquareOne;
        private SquareChannel SquareTwo;
        private WaveChannel WaveChannel;

        private float[] SampleBuffer;
        private int SampleBufferIdx;
        private int CyclesToNextSample = 0;

        // Audio-In and Volume
        private bool AudioInLeftEnable;
        private bool AudioInRightEnable;
        private int VolumeLeft;
        private int VolumeRight;

        // Sound Output Channels
        private bool[][] ChannelOutputEnabled;

        // Sound On/Off
        private bool SoundEnabled;

        private readonly GbMemory MemoryMap;

        public GbApu(GbMemory memory)
        {
            MemoryMap = memory;

            ResetGeneralRegisters();
            ResetChannels();

            MemoryMap.RegisterMmio(0xFF24, () =>
            {
                byte b = 0;

                if (AudioInLeftEnable)
                {
                    MathUtil.SetBit(ref b, 7);
                }

                b |= (byte)(VolumeLeft << 4);

                if (AudioInRightEnable)
                {
                    MathUtil.SetBit(ref b, 3);
                }

                b |= (byte)VolumeRight;

                return b;
            }, (x) =>
            {
                AudioInLeftEnable = MathUtil.IsBitSet(x, 7);
                VolumeLeft = (x >> 4) & 0x7;
                AudioInRightEnable = MathUtil.IsBitSet(x, 3);
                VolumeRight = x & 0x7;
            });

            MemoryMap.RegisterMmio(0xFF25, () =>
            {
                byte b = 0;

                for (int i = 0; i < 4; i++)
                {
                    if (ChannelOutputEnabled[(int)OutputTerminal.Left][3 - i])
                    {
                        MathUtil.SetBit(ref b, 7 - i);
                    }

                    if (ChannelOutputEnabled[(int)OutputTerminal.Right][3 - i])
                    {
                        MathUtil.SetBit(ref b, 3 - i);
                    }
                }

                return b;
            }, (x) =>
            {
                for (int i = 0; i < 4; i++)
                {
                    ChannelOutputEnabled[(int)OutputTerminal.Left][3 - i] = MathUtil.IsBitSet(x, 7 - i);
                    ChannelOutputEnabled[(int)OutputTerminal.Right][3 - i] = MathUtil.IsBitSet(x, 3 - i);
                }
            });

            MemoryMap.RegisterMmio(0xFF26, () =>
            {
                byte b = 0;

                if (SoundEnabled)
                {
                    MathUtil.SetBit(ref b, 7);
                }

                if (SquareTwo.IsEnabled())
                {
                    MathUtil.SetBit(ref b, 1);
                }

                if (SquareOne.IsEnabled())
                {
                    MathUtil.SetBit(ref b, 0);
                }

                return b;
            }, (x) =>
            {
                // Writes to all lower bits are ignored

                bool SoundWillBeEnabled = MathUtil.IsBitSet(x, 7);

                if (!SoundWillBeEnabled && SoundEnabled)
                {
                    // Destroy all registers
                    ResetGeneralRegisters();
                    ResetChannels();
                }

                SoundEnabled = SoundWillBeEnabled;
            });
        }

        private void ResetGeneralRegisters()
        {
            SampleBuffer = new float[1024];
            CyclesToNextSample = 95; // assumed 44100 Hz (CD quality)

            AudioInLeftEnable = false;
            VolumeLeft = 0;
            AudioInRightEnable = false;
            VolumeRight = 0;

            ChannelOutputEnabled = new bool[][]
            {
                new bool[] { false, false, false, false },
                new bool[] { false, false, false, false }
            };
        }

        private void ResetChannels()
        {
            // TODO: this isn't even remotely correct
            SquareOne = new SquareChannel(MemoryMap, 0xFF10, true);
            SquareTwo = new SquareChannel(MemoryMap, 0xFF15, false);
            WaveChannel = new WaveChannel(MemoryMap, 0xFF1A);
        }

        public bool Tick()
        {
            SquareOne.Tick();
            SquareTwo.Tick();
            WaveChannel.Tick();

            CyclesToNextSample--;

            if (CyclesToNextSample == 0)
            {
                MixChannelsForOutput(OutputTerminal.Left);
                MixChannelsForOutput(OutputTerminal.Right);

                SampleBufferIdx += 2;
                CyclesToNextSample = 95;

                if (SampleBufferIdx == SampleBuffer.Length)
                {
                    SampleBufferIdx = 0;

                    return true;
                }
            }

            return false;
        }

        private void MixChannelsForOutput(OutputTerminal outputTerminal)
        {
            int bufferIdx = SampleBufferIdx;

            if (outputTerminal == OutputTerminal.Right)
            {
                bufferIdx += 1;
            }

            int terminalVolume;

            if (outputTerminal == OutputTerminal.Left)
            {
                terminalVolume = VolumeLeft;
            }
            else
            {
                terminalVolume = VolumeRight;
            }

            SampleBuffer[bufferIdx] = 0.0f;

            if (ChannelOutputEnabled[(int)outputTerminal][0])
            {
                SampleBuffer[bufferIdx] += SampleToFloat(SquareOne.GetSample(), terminalVolume);
            }

            if (ChannelOutputEnabled[(int)outputTerminal][1])
            {
                SampleBuffer[bufferIdx] += SampleToFloat(SquareTwo.GetSample(), terminalVolume);
            }

            if (ChannelOutputEnabled[(int)outputTerminal][2])
            {
                SampleBuffer[bufferIdx] += SampleToFloat(WaveChannel.GetSample(), terminalVolume);
            }
        }

        private float SampleToFloat(int sample, int volume)
        {
            return sample * (volume + 1) / 100f;
        }

        public float[] GetSampleBuffer()
        {
            return SampleBuffer;
        }

    }
}

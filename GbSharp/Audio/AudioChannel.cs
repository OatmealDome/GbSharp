using GbSharp.Memory;

namespace GbSharp.Audio
{
    abstract class AudioChannel
    {
        protected bool Enabled;
        protected int FrameSequencer;
        private int CyclesToNextSequencerClock;

        protected bool DacEnabled;

        protected int Volume;

        protected bool LengthEnabled;
        protected int LengthData;
        protected int LengthCounter;

        protected int Sample;

        protected abstract bool MultiplyVolumeAfterTick
        {
            get;
        }

        protected readonly GbMemory MemoryMap;

        protected AudioChannel(GbMemory memory)
        {
            FrameSequencer = 0;
            CyclesToNextSequencerClock = 8192;

            MemoryMap = memory;
        }

        public void Tick()
        {
            if (!Enabled)
            {
                Sample = 0;

                return;
            }

            TickChannel();

            CyclesToNextSequencerClock--;

            if (CyclesToNextSequencerClock == 0)
            {
                FrameSequencer++;

                if (FrameSequencer == 8)
                {
                    FrameSequencer = 0;
                }

                if (LengthEnabled)
                {
                    ClockLength();
                }

                if (FrameSequencer == 7)
                {
                    ClockVolumeOrEnvelope();
                }

                CyclesToNextSequencerClock = 8192;
            }

            if (DacEnabled)
            {
                if (MultiplyVolumeAfterTick)
                {
                Sample *= Volume;
            }
            }
            else
            {
                Sample = 0;
            }
        }

        public bool IsEnabled()
        {
            return Enabled;
        }

        public int GetSample()
        {
            return Sample;
        }

        private void ClockLength()
        {
            if (FrameSequencer % 2 != 0)
            {
                return;
            }

            LengthCounter--;

            if (LengthCounter == 0)
            {
                Enabled = false;
            }
        }

        protected abstract void EnableChannel();

        protected abstract void TickChannel();

        protected abstract void ClockVolumeOrEnvelope();

    }
}

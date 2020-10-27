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
            CyclesToNextSequencerClock--;

            bool sequencerTicked = false;

            if (CyclesToNextSequencerClock == 0)
            {
                FrameSequencer++;

                if (FrameSequencer == 8)
                {
                    FrameSequencer = 0;
                }

                ClockPreChannelTick();

                CyclesToNextSequencerClock = 8192;

                sequencerTicked = true;
            }

            TickChannel();

            if (sequencerTicked)
            {
                if (LengthEnabled && FrameSequencer % 2 == 0)
                {
                    ClockLength();
                }

                if (FrameSequencer == 7)
                {
                    ClockVolumeOrEnvelope();
                }
            }

            if (DacEnabled && Enabled)
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

        protected virtual void ClockPreChannelTick()
        {
            ;
        }

        private void ClockLength()
        {
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

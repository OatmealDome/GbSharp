using GbSharp.Cpu;
using GbSharp.Memory;
using System.Collections.Generic;

namespace GbSharp.Controller
{
    class GbController
    {
        private bool ReadingButtons;

        // Buttons
        private Dictionary<ControllerKey, bool> KeyStates;

        private GbCpu Cpu;
        private GbMemory MemoryMap;

        public GbController(GbCpu cpu, GbMemory memory)
        {
            ReadingButtons = false;

            // Initial states
            KeyStates = new Dictionary<ControllerKey, bool>();
            KeyStates[ControllerKey.DPadDown] = false;
            KeyStates[ControllerKey.DPadUp] = false;
            KeyStates[ControllerKey.DPadLeft] = false;
            KeyStates[ControllerKey.DPadRight] = false;
            KeyStates[ControllerKey.Start] = false;
            KeyStates[ControllerKey.Select] = false;
            KeyStates[ControllerKey.B] = false;
            KeyStates[ControllerKey.A] = false;

            Cpu = cpu;
            MemoryMap = memory;

            // The controller register actually starts with all bits pulled high, and
            // pulls a bit low if the flag is selected or the corresponding key is pressed.
            MemoryMap.RegisterMmio(0xFF00, () =>
            {
                byte b = 0xFF;

                if (ReadingButtons)
                {
                    MathUtil.ClearBit(ref b, 5);

                    if (KeyStates[ControllerKey.Start])
                    {
                        MathUtil.ClearBit(ref b, 3);
                    }

                    if (KeyStates[ControllerKey.Select])
                    {
                        MathUtil.ClearBit(ref b, 2);
                    }

                    if (KeyStates[ControllerKey.B])
                    {
                        MathUtil.ClearBit(ref b, 1);
                    }

                    if (KeyStates[ControllerKey.A])
                    {
                        MathUtil.ClearBit(ref b, 0);
                    }
                }
                else // D-Pad
                {
                    MathUtil.ClearBit(ref b, 4);

                    if (KeyStates[ControllerKey.DPadDown])
                    {
                        MathUtil.ClearBit(ref b, 3);
                    }

                    if (KeyStates[ControllerKey.DPadUp])
                    {
                        MathUtil.ClearBit(ref b, 2);
                    }

                    if (KeyStates[ControllerKey.DPadLeft])
                    {
                        MathUtil.ClearBit(ref b, 1);
                    }

                    if (KeyStates[ControllerKey.DPadRight])
                    {
                        MathUtil.ClearBit(ref b, 0);
                    }
                }

                return b;
            }, (x) =>
            {
                if (!MathUtil.IsBitSet(x, 5)) // Buttons
                {
                    ReadingButtons = true;
                }
                else if (!MathUtil.IsBitSet(x, 4)) // D-Pad
                {
                    ReadingButtons = false;
                }
            });
        }

        public void UpdateKeyState(ControllerKey key, bool state)
        {
            bool priorState = KeyStates[key];
            KeyStates[key] = state;

            // Raise an interrupt if a key becomes pressed
            if (!priorState && state)
            {
                Cpu.RaiseInterrupt(4);
            }
        }

    }
}

namespace GbSharp.Ppu.Palette
{
    class ColourPalette
    {
        private static LcdColour[] DmgColours = new LcdColour[]
        {
            new LcdColour(255, 255, 255), // White
            new LcdColour(169, 169, 169), // Light Grey
            new LcdColour(84, 84, 84), // Dark Grey
            new LcdColour(0, 0, 0) // Black
        };

        private LcdColour[] Colours;
        private byte DmgRegisterValue;

        public ColourPalette()
        {
            Colours = new LcdColour[4];

            // Default to all white
            Colours[0] = new LcdColour(255, 255, 255);
            Colours[1] = new LcdColour(255, 255, 255);
            Colours[2] = new LcdColour(255, 255, 255);
            Colours[3] = new LcdColour(255, 255, 255);
        }

        public LcdColour GetColour(int idx)
        {
            return Colours[idx];
        }

        public void SetColour(int idx, LcdColour colour)
        {
            Colours[idx] = colour;
        }

        public void SetFromDmgRegister(byte register, bool isCgb)
        {
            // Don't actually set any colours if the register is being written to in CGB mode.
            // The CGB Pokemon games read this register's value later in order to upload the
            // equivalent colours to CGB palette memory.
            if (!isCgb)
            {
                Colours[0] = DmgColours[register & 0x3];
                Colours[1] = DmgColours[(register >> 2) & 0x3];
                Colours[2] = DmgColours[(register >> 4) & 0x3];
                Colours[3] = DmgColours[register >> 6];
            }

            DmgRegisterValue = register;
        }

        public byte GetDmgRegister()
        {
            return DmgRegisterValue;
        }

    }
}

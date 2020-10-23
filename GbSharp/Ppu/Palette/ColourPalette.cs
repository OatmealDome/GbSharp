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

        public ColourPalette()
        {
            Colours = new LcdColour[4];

            // Default to all white
            Colours[0] = DmgColours[0];
            Colours[1] = DmgColours[0];
            Colours[2] = DmgColours[0];
            Colours[3] = DmgColours[0];
        }

        public LcdColour GetColour(int idx)
        {
            return Colours[idx];
        }

        public void SetColour(int idx, LcdColour colour)
        {
            Colours[idx] = colour;
        }

        public void SetFromDmgRegister(byte register)
        {
            Colours[0] = DmgColours[register & 0x3];
            Colours[1] = DmgColours[(register >> 2) & 0x3];
            Colours[2] = DmgColours[(register >> 4) & 0x3];
            Colours[3] = DmgColours[register >> 6];
        }

        public byte GetDmgRegister()
        {
            int getColorIdx(LcdColour colour)
            {
                if (colour == DmgColours[0])
                {
                    return 0;
                }
                else if (colour == DmgColours[1])
                {
                    return 1;
                }
                else if (colour == DmgColours[2])
                {
                    return 2;
                }
                else if (colour == DmgColours[3])
                {
                    return 3;
                }

                return 0;
            }

            return (byte)(getColorIdx(Colours[3]) << 6 | getColorIdx(Colours[2]) << 4 | getColorIdx(Colours[1]) << 2 | getColorIdx(Colours[0]));
        }

    }
}

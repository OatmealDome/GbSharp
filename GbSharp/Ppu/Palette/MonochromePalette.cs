namespace GbSharp.Ppu.Palette
{
    class MonochromePalette
    {
        public MonochromeColour ColourZero
        {
            get;
            set;
        }

        public MonochromeColour ColourOne
        {
            get;
            set;
        }
        
        public MonochromeColour ColourTwo
        {
            get;
            set;
        }

        public MonochromeColour ColourThree
        {
            get;
            set;
        }

        public MonochromePalette()
        {
            ColourZero = MonochromeColour.White;
            ColourOne = MonochromeColour.White;
            ColourTwo = MonochromeColour.White;
            ColourThree = MonochromeColour.White;
        }

        /// <summary>
        /// Sets this MonochromePalette's values from the register's assigned value.
        /// </summary>
        /// <param name="register">The value assigned to the register.</param>
        public void SetFromRegister(byte register)
        {
            ColourZero = (MonochromeColour)(register & 0x3);
            ColourOne = (MonochromeColour)((register >> 2) & 0x3);
            ColourTwo = (MonochromeColour)((register >> 4) & 0x3);
            ColourThree = (MonochromeColour)(register >> 6);
        }

        /// <summary>
        /// Converts this MonochromePalette into a register for MMIO.
        /// </summary>
        /// <returns>A byte representing this MonochromePalette.</returns>
        public byte ToRegister()
        {
            return (byte)((int)ColourThree << 6 | (int)ColourTwo << 4 | (int)ColourOne << 2 | (int)ColourZero);
        }

    }
}

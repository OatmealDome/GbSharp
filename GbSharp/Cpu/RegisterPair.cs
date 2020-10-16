namespace GbSharp.Cpu
{
    class RegisterPair
    {
        public byte High;
        public byte Low;

        public ushort Value
        {
            get
            {
                return (ushort)((High << 8) | Low);
            }
            set
            {
                High = (byte)(value >> 8);
                Low = (byte)(value & 0xFF);
            }
        }

        public RegisterPair()
        {
            High = 0;
            Low = 0;
        }

    }
}

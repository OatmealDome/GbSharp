namespace GbSharp
{
    internal class MathUtil
    {
        public static bool InRange(ushort value, ushort start, int size)
        {
            return value >= start && value < start + size;
        }

        public static bool IsBitSet(byte b, int bit)
        {
            // for example, checking Carry on CpuFlags:
            // F      1001 0000
            // mask   0001 0000
            // AND    0001 0000
            int mask = 1 << bit;
            return (b & mask) == mask;
        }

        public static void SetBit(ref byte b, int bit)
        {
            // for example, setting Carry on CpuFlags:
            // F    1000 0000
            // mask 0001 0000
            // XOR  1001 0000
            b |= (byte)(1 << bit);
        }

        public static void ClearBit(ref byte b, int bit)
        {
            // for example, clearing Carry on CpuFlags:
            // F      1001 0000
            // mask   0001 0000
            // invert 1110 0000
            // AND    1000 0000
            b &= (byte)~(1 << bit);
        }

        public static int GetBit(byte b, int bit)
        {
            return IsBitSet(b, bit) ? 1 : 0;
        }

    }
}

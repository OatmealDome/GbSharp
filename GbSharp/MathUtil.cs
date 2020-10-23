using System.Runtime.CompilerServices;

namespace GbSharp
{
    internal class MathUtil
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InRange(int value, int start, int size)
        {
            return value >= start && value < start + size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBitSet(byte b, int bit)
        {
            // for example, checking Carry on CpuFlags:
            // F      1001 0000
            // mask   0001 0000
            // AND    0001 0000
            int mask = 1 << bit;
            return (b & mask) == mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBit(ref byte b, int bit)
        {
            // for example, setting Carry on CpuFlags:
            // F    1000 0000
            // mask 0001 0000
            // XOR  1001 0000
            b |= (byte)(1 << bit);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClearBit(ref byte b, int bit)
        {
            // for example, clearing Carry on CpuFlags:
            // F      1001 0000
            // mask   0001 0000
            // invert 1110 0000
            // AND    1000 0000
            b &= (byte)~(1 << bit);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetBit(byte b, int bit)
        {
            return (b >> bit) & 0x1;
        }

    }
}

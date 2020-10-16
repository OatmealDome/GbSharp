namespace GbSharp
{
    internal class MathUtil
    {
        public static bool InRange(ushort value, ushort start, ushort end)
        {
            return value >= start && value < end + 1;
        }

        public static bool InRange(ushort value, ushort start, int size)
        {
            return InRange(value, start, start + size);
        }

    }
}

using GbSharp.Memory;

namespace GbSharp.Cpu
{
    internal class IeRegisterRegion : MemoryRegion
    {
        public byte Value
        {
            get;
            set;
        }

        public override byte Read(ushort offset)
        {
            return Value;
        }

        public override void Write(ushort offset, byte val)
        {
            Value = val;
        }

    }
}

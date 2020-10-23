namespace GbSharp.Memory
{
    abstract internal class MemoryRegion
    {
        public abstract byte Read(int address);

        public abstract void Write(int address, byte val);

    }
}

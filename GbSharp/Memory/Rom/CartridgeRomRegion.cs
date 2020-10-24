using System;
using System.Collections.Generic;

namespace GbSharp.Memory.Rom
{
    abstract class CartridgeRomRegion : MemoryRegion
    {
        protected static readonly int ROM_START = 0x0;
        protected static readonly int ROM_BANK_SIZE = 0x4000;
        protected static readonly int ROM_SIZE = 0x8000;
        protected static readonly int EXTERNAL_RAM_START = 0xA000;
        protected static readonly int EXTERNAL_RAM_SIZE = 0x2000;

        protected readonly byte[] Rom;
        protected readonly byte[] Ram;

        protected bool RamEnabled;

        protected CartridgeRomRegion(byte[] rom)
        {
            Rom = rom;
            Ram = new byte[GetRamSize()]; // TODO: load and save this area

            RamEnabled = false;
        }

        public override IEnumerable<Tuple<int, int>> GetHandledRanges()
        {
            return new List<Tuple<int, int>>()
            {
                new Tuple<int, int>(ROM_START, ROM_SIZE),
                new Tuple<int, int>(EXTERNAL_RAM_START, EXTERNAL_RAM_SIZE)
            };
        }

        protected int GetRamSize()
        {
            switch (Rom[0x149])
            {
                case 0x00:
                    return 0;
                case 0x01: // 2kb
                    return 0x800;
                case 0x02: // 8kb
                    return 0x2000;
                case 0x03: // 32kb (4 banks of 8kb)
                    return 0x2000 * 4;
                case 0x04: // 128kb (16 banks of 8kb)
                    return 0x2000 * 16;
                case 0x5: // 64kb (8 banks of 8kb)
                    return 0x2000 * 8;
                default:
                    return 0;
            }
        }

        public static CartridgeRomRegion CreateRomRegion(byte[] rom)
        {
            byte type = rom[0x147];
            switch (type)
            {
                case 0x00: // ROM only
                case 0x08: // ROM + RAM
                case 0x09: // ROM + RAM + BATTERY
                    return new DirectRomRegion(rom);
                case 0x01: // MBC1 ROM
                case 0x02: // MBC1 ROM + RAM
                case 0x03: // MBC1 ROM + RAM + BATTERY
                    return new Mbc1RomRegion(rom);
                case 0x0F: // MBC3 + TIMER + BATTERY
                case 0x10: // MBC3 ROM + TIMER + RAM + BATTERY
                case 0x11: // MBC3 ROM
                case 0x12: // MBC3 ROM + RAM
                case 0x13: // MBC3 ROM + RAM + BATTERY
                    return new Mbc3RomRegion(rom);
                default:
                    throw new Exception($"Unsupported cartridge type {type:x}");
            }
        }

    }
}

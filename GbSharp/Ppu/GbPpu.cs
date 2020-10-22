using GbSharp.Cpu;
using GbSharp.Memory;
using GbSharp.Ppu.Memory;
using GbSharp.Ppu.Palette;

namespace GbSharp.Ppu
{
    class GbPpu
    {
        private int CurrentScanlineCyclePosition;
        private PpuMode CurrentMode;
        private VideoRamRegion VideoRamRegion;
        private OamRegion OamRegion;
        private byte[] RawOutput; // RGBA8_UNorm

        private GbCpu Cpu;
        private GbMemory MemoryMap;

        // LCD Control (0xFF40)
        private bool EnableLcd;
        private bool UseAlternateWindowTileMap;
        private bool EnableWindow;
        private bool UseAlternateTileData;
        private bool UseAlternateBgTileMap;
        private bool UseDoubleHeightObjectSize;
        private bool EnableObjects;
        private bool PrioritizeBgAndWindow;

        // LCD Status (0xFF41)
        private bool CoincidenceInterruptEnabled;
        private bool OamScanInterruptEnabled;
        private bool VBlankInterruptEnabled;
        private bool HBlankInterruptEnabled;

        // LCD Position and Scrolling
        private byte BgScrollY;
        private byte BgScrollX;
        private byte CurrentScanline;
        private byte ScanlineCompare;
        private byte WindowY;
        private byte WindowX;

        // LCD Monochrome Palettes
        private MonochromePalette BgPalette;
        private MonochromePalette ObjectZeroPalette;
        private MonochromePalette ObjectOnePalette;

        // LCD OAM DMA
        private byte OamDmaStart;

        public GbPpu(GbCpu cpu, GbMemory memory)
        {
            CurrentScanlineCyclePosition = 0;
            CurrentMode = PpuMode.OamScan;
            VideoRamRegion = new VideoRamRegion();
            OamRegion = new OamRegion();
            RawOutput = new byte[160 * 144 * 4];

            EnableLcd = false;
            UseAlternateWindowTileMap = false;
            EnableWindow = false;
            UseAlternateTileData = false;
            UseAlternateBgTileMap = false;
            UseDoubleHeightObjectSize = false;
            EnableObjects = false;
            PrioritizeBgAndWindow = false;

            BgScrollX = 0;
            BgScrollY = 0;
            CurrentScanline = 0;
            ScanlineCompare = 0;
            WindowX = 0;
            WindowY = 0;

            BgPalette = new MonochromePalette();
            ObjectZeroPalette = new MonochromePalette();
            ObjectOnePalette = new MonochromePalette();

            Cpu = cpu;
            MemoryMap = memory;

            MemoryMap.RegisterRegion(0x8000, VideoRamRegion.VIDEO_RAM_SIZE, VideoRamRegion);
            MemoryMap.RegisterRegion(0xFE00, OamRegion.OAM_SIZE, OamRegion);

            MemoryMap.RegisterMmio(0xFF40, () =>
            {
                byte b = 0;

                if (EnableLcd)
                {
                    MathUtil.SetBit(ref b, 7);
                }

                if (UseAlternateWindowTileMap)
                {
                    MathUtil.SetBit(ref b, 6);
                }

                if (EnableWindow)
                {
                    MathUtil.SetBit(ref b, 5);
                }

                if (UseAlternateTileData)
                {
                    MathUtil.SetBit(ref b, 4);
                }

                if (UseAlternateBgTileMap)
                {
                    MathUtil.SetBit(ref b, 3);
                }

                if (UseDoubleHeightObjectSize)
                {
                    MathUtil.SetBit(ref b, 2);
                }

                if (EnableObjects)
                {
                    MathUtil.SetBit(ref b, 1);
                }

                if (PrioritizeBgAndWindow)
                {
                    MathUtil.SetBit(ref b, 0);
                }

                return b;
            }, (x) =>
            {
                EnableLcd = MathUtil.IsBitSet(x, 7);
                UseAlternateWindowTileMap = MathUtil.IsBitSet(x, 6);
                EnableWindow = MathUtil.IsBitSet(x, 5);
                UseAlternateTileData = MathUtil.IsBitSet(x, 4);
                UseAlternateBgTileMap = MathUtil.IsBitSet(x, 3);
                UseDoubleHeightObjectSize = MathUtil.IsBitSet(x, 2);
                EnableObjects = MathUtil.IsBitSet(x, 1);
                PrioritizeBgAndWindow = MathUtil.IsBitSet(x, 0);
            });

            MemoryMap.RegisterMmio(0xFF41, () =>
            {
                byte b = 0;

                if (CoincidenceInterruptEnabled)
                {
                    MathUtil.SetBit(ref b, 6);
                }

                if (OamScanInterruptEnabled)
                {
                    MathUtil.SetBit(ref b, 5);
                }

                if (VBlankInterruptEnabled)
                {
                    MathUtil.SetBit(ref b, 4);
                }

                if (HBlankInterruptEnabled)
                {
                    MathUtil.SetBit(ref b, 3);
                }

                if (CurrentScanline == ScanlineCompare)
                {
                    MathUtil.SetBit(ref b, 2);
                }

                int modeInt = (int)CurrentMode;

                if (MathUtil.IsBitSet((byte)modeInt, 1))
                {
                    MathUtil.SetBit(ref b, 1);
                }

                if (MathUtil.IsBitSet((byte)modeInt, 0))
                {
                    MathUtil.SetBit(ref b, 0);
                }

                return b;
            }, (x) =>
            {
                CoincidenceInterruptEnabled = MathUtil.IsBitSet(x, 6);
                OamScanInterruptEnabled = MathUtil.IsBitSet(x, 5);
                VBlankInterruptEnabled = MathUtil.IsBitSet(x, 4);
                HBlankInterruptEnabled = MathUtil.IsBitSet(x, 3);

                // Writes to bits 7, 2, 1, and 0 are ignored
            });

            MemoryMap.RegisterMmio(0xFF42, () => BgScrollY, (x) => BgScrollY = x);
            MemoryMap.RegisterMmio(0xFF43, () => BgScrollX, (x) => BgScrollX = x);
            MemoryMap.RegisterMmio(0xFF44, () => CurrentScanline, (x) => { }); // writes ignored
            MemoryMap.RegisterMmio(0xFF45, () => ScanlineCompare, (x) => ScanlineCompare = x);
            MemoryMap.RegisterMmio(0xFF4A, () => WindowY, (x) => WindowY = x);
            MemoryMap.RegisterMmio(0xFF4B, () => WindowX, (x) => WindowX = x);

            MemoryMap.RegisterMmio(0xFF47, () => BgPalette.ToRegister(), x => BgPalette.SetFromRegister(x));
            MemoryMap.RegisterMmio(0xFF48, () => ObjectZeroPalette.ToRegister(), x => ObjectZeroPalette.SetFromRegister(x));
            MemoryMap.RegisterMmio(0xFF49, () => ObjectOnePalette.ToRegister(), x => ObjectOnePalette.SetFromRegister(x));

            MemoryMap.RegisterMmio(0xFF46, () => OamDmaStart, x => OamDmaStart = x);
        }

        /// <summary>
        /// Advances the PPU by one cycle.
        /// </summary>
        public void Update()
        {
            if (!EnableLcd)
            {
                return;
            }

            CurrentScanlineCyclePosition++;

            switch (CurrentMode)
            {
                case PpuMode.OamScan:
                    if (CurrentScanlineCyclePosition == 20)
                    {
                        ChangePpuMode(PpuMode.PictureGeneration);
                    }

                    break;
                case PpuMode.PictureGeneration:
                    // TODO: Can be variable
                    if (CurrentScanlineCyclePosition == 63)
                    {
                        ChangePpuMode(PpuMode.HBlank);
                    }

                    break;
                case PpuMode.HBlank:
                    VideoRamRegion.Unlock();
                    OamRegion.Unlock();

                    if (CurrentScanlineCyclePosition == 114)
                    {
                        CurrentScanline++;
                        CurrentScanlineCyclePosition = 0;

                        if (CurrentScanline == 143)
                        {
                            ChangePpuMode(PpuMode.VBlank);
                        }
                        else
                        {
                            ChangePpuMode(PpuMode.OamScan);
                    }
                    }

                    break;
                case PpuMode.VBlank:
                    if (CurrentScanlineCyclePosition == 114)
                    {
                        CurrentScanlineCyclePosition = 0;

                        if (CurrentScanline == 153)
                        {
                            ChangePpuMode(PpuMode.OamScan);
                            CurrentScanline = 0;
                        }
                        else
                        {
                            CurrentScanline++;
                        }
                    }

                    break;
            }

            if (CurrentScanline == ScanlineCompare)
            {
                Cpu.RaiseInterrupt(1);
            }
        }

        private void ChangePpuMode(PpuMode targetMode)
        {
            CurrentMode = targetMode;

            switch (targetMode)
            {
                case PpuMode.OamScan:
                    OamRegion.Lock();

                    break;
                case PpuMode.PictureGeneration:
                    VideoRamRegion.Lock();

                    break;
                case PpuMode.HBlank:
                    OamRegion.Unlock();
                    VideoRamRegion.Unlock();

                    break;
                case PpuMode.VBlank:
                    Cpu.RaiseInterrupt(0);

                    break;
            }
        }

        private void DrawScanline()
        {
            // TODO
        }

    }
}

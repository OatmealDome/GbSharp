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

                if (!EnableLcd)
                {
                    CurrentScanline = 0;
                    CurrentScanlineCyclePosition = 0;

                    ChangePpuMode(PpuMode.OamScan);

                    // Clear the screen
                    for (int i = 0; i < RawOutput.Length; i++)
                    {
                        RawOutput[i] = 255;
                    }

                    // Clear interrupts
                    Cpu.ClearInterrupt(0);
                    Cpu.ClearInterrupt(1);
                }
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

                if (EnableLcd)
                {
                int modeInt = (int)CurrentMode;

                if (MathUtil.IsBitSet((byte)modeInt, 1))
                {
                    MathUtil.SetBit(ref b, 1);
                }

                if (MathUtil.IsBitSet((byte)modeInt, 0))
                {
                    MathUtil.SetBit(ref b, 0);
                }
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
                        ChangeScanline(false);

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
                        if (CurrentScanline == 153)
                        {
                            ChangeScanline(true);
                            ChangePpuMode(PpuMode.OamScan);
                        }
                        else
                        {
                            ChangeScanline(false);
                        }
                    }

                    break;
            }
            }

        private void ChangePpuMode(PpuMode targetMode)
        {
            CurrentMode = targetMode;

            switch (targetMode)
            {
                case PpuMode.OamScan:
                    OamRegion.Lock();

                    if (OamScanInterruptEnabled)
                    {
                        Cpu.RaiseInterrupt(1);
                    }

                    break;
                case PpuMode.PictureGeneration:
                    VideoRamRegion.Lock();

                    break;
                case PpuMode.HBlank:
                    OamRegion.Unlock();
                    VideoRamRegion.Unlock();

                    DrawScanline();

                    if (HBlankInterruptEnabled)
                    {
                        Cpu.RaiseInterrupt(1);
                    }

                    break;
                case PpuMode.VBlank:
                    // TODO: does the PPU even listen to this flag? games just write zero to the bit
                    //if (VBlankInterruptEnabled)
                    //{
                    Cpu.RaiseInterrupt(0);
                    //}

                    break;
            }
        }

        private void ChangeScanline(bool toTop)
        {
            if (CoincidenceInterruptEnabled && CurrentScanline == ScanlineCompare)
            {
                Cpu.RaiseInterrupt(1);
            }

            CurrentScanline = (byte)(toTop ? 0 : CurrentScanline + 1);
            CurrentScanlineCyclePosition = 0;
            SkipDrawingObjectsForScanline = false;
        }

        private void DrawTilePixel(int screenX, int screenY, int tileIdx, int tilePixelX, int tilePixelY)
        {
            ushort dataOfs = (ushort)(UseAlternateTileData ? 0x0 : 0x880);

            ushort dataStartOfs = (ushort)(dataOfs + (tileIdx * 16) + (tilePixelY * 2));

            byte tileLow = VideoRamRegion.ReadDirect(dataStartOfs);
            byte tileHigh = VideoRamRegion.ReadDirect((ushort)(dataStartOfs + 1));

            int colourIdx = (MathUtil.GetBit(tileHigh, 7 - tilePixelX) << 1) | MathUtil.GetBit(tileLow, 7 - tilePixelX);

            int outputOfs = ((screenY * 160) + screenX) * 4;

            switch (BgPalette.GetColourFromIdx(colourIdx))
            {
                case MonochromeColour.Black:
                    RawOutput[outputOfs] = 0;
                    RawOutput[outputOfs + 1] = 0;
                    RawOutput[outputOfs + 2] = 0;

                    break;
                case MonochromeColour.LightGrey:
                    RawOutput[outputOfs] = 169;
                    RawOutput[outputOfs + 1] = 169;
                    RawOutput[outputOfs + 2] = 169;

                    break;
                case MonochromeColour.DarkGrey:
                    RawOutput[outputOfs] = 84;
                    RawOutput[outputOfs + 1] = 84;
                    RawOutput[outputOfs + 2] = 84;

                    break;
                case MonochromeColour.White:
                    RawOutput[outputOfs] = 255;
                    RawOutput[outputOfs + 1] = 255;
                    RawOutput[outputOfs + 2] = 255;

                    break;
            }

            // Alpha should always be 255 (1.0f)
            RawOutput[outputOfs + 3] = 255;
        }

        private void DrawScanline()
        {
            if (PrioritizeBgAndWindow)
            {
                ushort bgTileMapOfs = (ushort)(UseAlternateBgTileMap ? 0x1c00 : 0x1800);

                byte bgPixelY = (byte)(CurrentScanline + BgScrollY);
                int bgTileY = bgPixelY / 8;
                int bgTilePixelY = bgPixelY % 8;

                for (byte x = 0; x < 160; x++)
                {
                    byte bgPixelX = (byte)(x + BgScrollX);
                    int bgTileX = bgPixelX / 8;
                    int bgTilePixelX = bgPixelX % 8;

                    byte tileIdx = VideoRamRegion.ReadDirect((ushort)(bgTileMapOfs + ((bgTileY * 32) + bgTileX)));

                    DrawTilePixel(x, CurrentScanline, tileIdx, bgTilePixelX, bgTilePixelY);
                }

                if (EnableWindow)
                {
                    ushort windowTileMapOfs = (ushort)(UseAlternateWindowTileMap ? 0x1c00 : 0x1800);

                    // TODO: Window rendering
                }
            }
        }

        public byte[] GetPixelOutput()
        {
            return RawOutput;
        }

    }
}

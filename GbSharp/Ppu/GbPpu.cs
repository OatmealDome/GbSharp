using GbSharp.Cpu;
using GbSharp.Memory;
using GbSharp.Ppu.Memory;
using GbSharp.Ppu.Palette;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GbSharp.Ppu
{
    class GbPpu
    {
        private int CurrentScanlineCyclePosition;
        private PpuMode CurrentMode;
        private int CyclesLeftForOamDma;
        private bool SkipDrawingObjectsForScanline;
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

        // LCD Palettes
        private List<ColourPalette> BgPalettes;
        private List<ColourPalette> ObjectPalettes;

        // LCD OAM DMA
        private byte OamDmaStart;

        public GbPpu(GbCpu cpu, GbMemory memory)
        {
            CurrentScanlineCyclePosition = 0;
            CurrentMode = PpuMode.OamScan;
            CyclesLeftForOamDma = 0;
            SkipDrawingObjectsForScanline = false;
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

            BgPalettes = new List<ColourPalette>();
            ObjectPalettes = new List<ColourPalette>();

            Cpu = cpu;
            MemoryMap = memory;

            // CGB can have up to 8 BG and object palettes.
            // On DMG, we only need BG0, OBJ0, and OBJ1, so the rest go unused.
            for (int i = 0; i < 8; i++)
            {
                BgPalettes.Add(new ColourPalette());
                ObjectPalettes.Add(new ColourPalette());
            }

            MemoryMap.RegisterRegion(VideoRamRegion);
            MemoryMap.RegisterRegion(OamRegion);

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

                    // TODO: confirm if DMA continues or not
                    CyclesLeftForOamDma = 0;
                    SkipDrawingObjectsForScanline = false;

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

            // DMG Palette registers
            MemoryMap.RegisterMmio(0xFF47, () => BgPalettes[0].GetDmgRegister(), x => BgPalettes[0].SetFromDmgRegister(x));
            MemoryMap.RegisterMmio(0xFF48, () => ObjectPalettes[0].GetDmgRegister(), x => ObjectPalettes[0].SetFromDmgRegister(x));
            MemoryMap.RegisterMmio(0xFF49, () => ObjectPalettes[1].GetDmgRegister(), x => ObjectPalettes[1].SetFromDmgRegister(x));

            // TODO: Initiating an OAM DMA transfer will lock out all memory except HRAM
            MemoryMap.RegisterMmio(0xFF46, () => OamDmaStart, x =>
            {
                OamDmaStart = x;
                CyclesLeftForOamDma = 160;
            });
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

            if (CyclesLeftForOamDma > 0)
            {
                CyclesLeftForOamDma--;
                SkipDrawingObjectsForScanline = true;

                // Perform the transfer if the cycles left is zero - not really cycle-accurate but should work
                if (CyclesLeftForOamDma == 0)
                {
                    for (int i = 0; i < 0x100; i++)
                    {
                        byte value = MemoryMap.Read((OamDmaStart * 0x100) + i);
                        OamRegion.WriteDirect(i, value);
                    }
                }
            }

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
                    //OamRegion.Lock();

                    if (OamScanInterruptEnabled)
                    {
                        Cpu.RaiseInterrupt(1);
                    }

                    break;
                case PpuMode.PictureGeneration:
                    //VideoRamRegion.Lock();

                    break;
                case PpuMode.HBlank:
                    //OamRegion.Unlock();
                    //VideoRamRegion.Unlock();

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

        private void DrawPixelLineFromTile(int screenX, int screenY, int tileIdx, int tilePixelStartX, int tilePixelStartY, int lineLength, bool isObject = false, int objectPalette = 0)
        {
            int dataOfs = 0;
            if (!isObject && !UseAlternateTileData)
            {
                // The primary tile data mode has some weird quirks - the tileIdx here
                // is actually a signed byte relative to 0x9000
                dataOfs = 0x1000;
                tileIdx = (sbyte)tileIdx;
            }

            int dataStartOfs = dataOfs + (tileIdx * 16) + (tilePixelStartY * 2);

            byte tileLow = VideoRamRegion.ReadDirect(dataStartOfs);
            byte tileHigh = VideoRamRegion.ReadDirect(dataStartOfs + 1);

            for (int tilePixelX = tilePixelStartX; tilePixelX < lineLength; tilePixelX++)
            {
                int colourIdx = (MathUtil.GetBit(tileHigh, 7 - tilePixelX) << 1) | MathUtil.GetBit(tileLow, 7 - tilePixelX);

                if (isObject && colourIdx == 0)
                {
                    continue;
                }

                int outputOfs = ((screenY * 160) + (screenX + tilePixelX)) * 4;

                ColourPalette palette;
                if (isObject)
                {
                    palette = ObjectPalettes[objectPalette];
                }
                else
                {
                    palette = BgPalettes[0];
                }

                LcdColour colour = palette.GetColour(colourIdx);
                RawOutput[outputOfs] = colour.R;
                RawOutput[outputOfs + 1] = colour.G;
                RawOutput[outputOfs + 2] = colour.B;

                // Alpha should always be 255 (1.0f)
                RawOutput[outputOfs + 3] = 255;
            }
        }

        private void DrawScanline()
        {
            if (PrioritizeBgAndWindow)
            {
                int bgTileMapOfs = UseAlternateBgTileMap ? 0x1c00 : 0x1800;

                byte bgPixelY = (byte)(CurrentScanline + BgScrollY);
                int bgTileY = bgPixelY / 8;
                int bgTilePixelY = bgPixelY % 8;

                byte bgPixelX = BgScrollX;
                int bgTileX = bgPixelX / 8;
                int bgTilePixelX = bgPixelX % 8;

                int tileIdx;

                int x = 0;

                while (x != 160)
                {
                    int lineLength = 8 - bgTilePixelX;
                    tileIdx = tileIdx = VideoRamRegion.ReadDirect(bgTileMapOfs + (bgTileY * 32) + bgTileX);

                    DrawPixelLineFromTile(x, CurrentScanline, tileIdx, bgTilePixelX, bgTilePixelY, lineLength);

                    bgTilePixelX += lineLength;
                    x += lineLength;

                    if (bgTilePixelX == 8)
                    {
                        bgTileX++;
                        bgTilePixelX = 0;
                    }
                }

                if (EnableWindow)
                {
                    int windowTileMapOfs = UseAlternateWindowTileMap ? 0x1c00 : 0x1800;

                    // TODO: Window rendering
                }
            }

            if (EnableObjects && !SkipDrawingObjectsForScanline)
            {
                List<GbObject> objectsToRender = new List<GbObject>();

                for (int i = 0; i < 40; i++)
                {
                    int objectAddress = i * 0x4;

                    byte objPixelY = OamRegion.ReadDirect(objectAddress);

                    if (objPixelY == 0 || objPixelY >= 160)
                    {
                        continue;
                    }

                    if (MathUtil.InRange(CurrentScanline + 16, objPixelY, UseDoubleHeightObjectSize ? 16 : 8))
                    {
                        objectsToRender.Add(new GbObject()
                        {
                            XCoord = OamRegion.ReadDirect(objectAddress + 1),
                            YCoord = objPixelY,
                            TileIdx = OamRegion.ReadDirect(objectAddress + 2),
                            Attributes = OamRegion.ReadDirect(objectAddress + 3)
                        });
                    }
                }
                
                // TODO: CGB orders by first appearance in OAM
                // TODO: reverse order of Take enumerable - otherwise sprites with higher X will take priority
                foreach (GbObject obj in objectsToRender.OrderBy(obj => obj.XCoord).Take(10))
                {
                    if (obj.XCoord == 0 || obj.XCoord >= 168)
                    {
                        continue;
                    }

                    int objTargetPixelY = (CurrentScanline + 16) - obj.YCoord;
                    
                    int startScreenX = Math.Max(0, obj.XCoord - 8);
                    int startObjPixelX = 0;

                    int lineLength;
                    if (obj.XCoord < 8) // intersecting with left screen edge
                    {
                        lineLength = obj.XCoord;
                        startObjPixelX = 8 - obj.XCoord;
                    }
                    else if (obj.XCoord + 8 > 168) // intersecting with right edge
                    {
                        lineLength = 168 - obj.XCoord;
                    }
                    else // middle of screen, no intersection
                    {
                        lineLength = 8;
                    }

                    DrawPixelLineFromTile(startScreenX, CurrentScanline, obj.TileIdx, startObjPixelX, objTargetPixelY, lineLength, true, MathUtil.GetBit(obj.Attributes, 4));
                }

            }
        }

        public byte[] GetPixelOutput()
        {
            return RawOutput;
        }

    }
}

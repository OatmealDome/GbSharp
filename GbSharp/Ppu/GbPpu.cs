using GbSharp.Cpu;
using GbSharp.Memory;
using GbSharp.Ppu.Memory;
using GbSharp.Ppu.Palette;
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
        private bool HBlankDmaActive;
        private int BytesLeftInHBlankDma;
        private int HBlankDmaSourcePointer;
        private int HBlankDmaDestinationPointer;
        private VideoRamRegion VideoRamRegion;
        private OamRegion OamRegion;

        private byte[] RawOutput; // RGBA8_UNorm
        private PixelType[] PixelPriority; // Used for calculating if a pixel should be written

        private GbCpu Cpu;
        private GbMemory MemoryMap;

        private HardwareType HardwareType;

        // LCD Control (0xFF40)
        private bool EnableLcd;
        private bool UseAlternateWindowTileMap;
        private bool EnableWindow;
        private bool UseAlternateTileData;
        private bool UseAlternateBgTileMap;
        private bool UseDoubleHeightObjectSize;
        private bool EnableObjects;
        private bool BgAndWindowEnabledOrPriority;

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
        private int BgPaletteWriteSpecification;
        private bool BgPaletteTransferAutoIncrement;
        private int ObjectPaletteWriteSpecification;
        private bool ObjectPaletteTransferAutoIncrement;

        // LCD OAM DMA
        private byte OamDmaStart;

        // LCD VRAM DMA
        private int VramDmaSource;
        private int VramDmaDestination;

        public GbPpu(GbCpu cpu, GbMemory memory)
        {
            CurrentScanlineCyclePosition = 0;
            CurrentMode = PpuMode.OamScan;
            CyclesLeftForOamDma = 0;
            SkipDrawingObjectsForScanline = false;
            HBlankDmaActive = false;
            BytesLeftInHBlankDma = 0;
            HBlankDmaSourcePointer = 0;
            HBlankDmaDestinationPointer = 0;
            VideoRamRegion = new VideoRamRegion(memory);
            OamRegion = new OamRegion();
            RawOutput = new byte[160 * 144 * 4];
            PixelPriority = new PixelType[160];

            EnableLcd = false;
            UseAlternateWindowTileMap = false;
            EnableWindow = false;
            UseAlternateTileData = false;
            UseAlternateBgTileMap = false;
            UseDoubleHeightObjectSize = false;
            EnableObjects = false;
            BgAndWindowEnabledOrPriority = false;

            BgScrollX = 0;
            BgScrollY = 0;
            CurrentScanline = 0;
            ScanlineCompare = 0;
            WindowX = 0;
            WindowY = 0;

            BgPalettes = new List<ColourPalette>();
            ObjectPalettes = new List<ColourPalette>();
            BgPaletteWriteSpecification = 0;
            BgPaletteTransferAutoIncrement = false;
            ObjectPaletteWriteSpecification = 0;
            ObjectPaletteTransferAutoIncrement = false;

            OamDmaStart = 0;
            VramDmaSource = 0;
            VramDmaDestination = 0;

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

                if (BgAndWindowEnabledOrPriority)
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
                BgAndWindowEnabledOrPriority = MathUtil.IsBitSet(x, 0);

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
            MemoryMap.RegisterMmio(0xFF47, () =>
            {
                return BgPalettes[0].GetDmgRegister();
            }, x =>
            {
                BgPalettes[0].SetFromDmgRegister(x, HardwareType == HardwareType.Cgb);
            });

            MemoryMap.RegisterMmio(0xFF48, () =>
            {
                return ObjectPalettes[0].GetDmgRegister();
            }, x =>
            {
                ObjectPalettes[0].SetFromDmgRegister(x, HardwareType == HardwareType.Cgb);
            });

            MemoryMap.RegisterMmio(0xFF49, () =>
            {
                return ObjectPalettes[1].GetDmgRegister();
            }, x =>
            {
                ObjectPalettes[1].SetFromDmgRegister(x, HardwareType == HardwareType.Cgb);
            });

            // CGB Palette Transfer
            MemoryMap.RegisterMmio(0xFF68, () =>
            {
                if (HardwareType == HardwareType.Cgb)
                {
                    byte b = 0x40; // unused 6th bit always set
                    
                    b |= (byte)BgPaletteWriteSpecification;

                    if (BgPaletteTransferAutoIncrement)
                    {
                        MathUtil.SetBit(ref b, 7);
                    }

                    return b;
                }
                else
                {
                    return 0xFF;
                }
            }, x =>
            {
                // Writes ignored for DMG hardware
                if (HardwareType == HardwareType.Cgb)
                {
                    BgPaletteWriteSpecification = x & 0x3F;
                    BgPaletteTransferAutoIncrement = MathUtil.IsBitSet(x, 7);
                }
            });

            MemoryMap.RegisterMmio(0xFF69, () =>
            {
                if (HardwareType == HardwareType.Cgb)
                {
                    return GetPaletteData(BgPalettes, BgPaletteWriteSpecification);
                }
                else
                {
                    return 0xFF;
                }
            }, (x) =>
            {
                // Writes ignored for DMG hardware
                if (HardwareType == HardwareType.Cgb)
                {
                    UpdatePalette(BgPalettes, BgPaletteWriteSpecification, x);

                    if (BgPaletteTransferAutoIncrement)
                    {
                        BgPaletteWriteSpecification++;
                    }
                }
            });

            MemoryMap.RegisterMmio(0xFF6A, () =>
            {
                if (HardwareType == HardwareType.Cgb)
                {
                    byte b = 0x40; // unused 6th bit always set

                    b |= (byte)ObjectPaletteWriteSpecification;

                    if (ObjectPaletteTransferAutoIncrement)
                    {
                        MathUtil.SetBit(ref b, 7);
                    }

                    return b;
                }
                else
                {
                    return 0xFF;
                }
            }, (x) =>
            {
                // Writes ignored for DMG hardware
                if (HardwareType == HardwareType.Cgb)
                {
                    ObjectPaletteWriteSpecification = x & 0x3F;
                    ObjectPaletteTransferAutoIncrement = MathUtil.IsBitSet(x, 7);
                }
            });

            MemoryMap.RegisterMmio(0xFF6B, () =>
            {
                if (HardwareType == HardwareType.Cgb)
                {
                    return GetPaletteData(ObjectPalettes, ObjectPaletteWriteSpecification);
                }
                else
                {
                    return 0xFF;
                }
            }, (x) =>
            {
                // Writes ignored for DMG hardware
                if (HardwareType == HardwareType.Cgb)
                {
                    UpdatePalette(ObjectPalettes, ObjectPaletteWriteSpecification, x);

                    if (ObjectPaletteTransferAutoIncrement)
                    {
                        ObjectPaletteWriteSpecification++;
                    }
                }
            });

            // TODO: Initiating an OAM DMA transfer will lock out all memory except HRAM
            MemoryMap.RegisterMmio(0xFF46, () => OamDmaStart, x =>
            {
                OamDmaStart = x;
                CyclesLeftForOamDma = 160;
            });

            MemoryMap.RegisterMmio(0xFF51, () =>
            {
                return (byte)(VramDmaSource & 0xFF);
            }, (x) =>
            {
                if (HardwareType == HardwareType.Cgb)
                {
                    VramDmaSource = (VramDmaSource & 0xFF) | (x << 8); 
                }
            });

            MemoryMap.RegisterMmio(0xFF52, () =>
            {
                return (byte)(VramDmaDestination & 0xFF);
            }, (x) =>
            {
                if (HardwareType == HardwareType.Cgb)
                {
                    VramDmaDestination = (VramDmaDestination & 0xFF00) | (x & 0xF0); // lower 4 bytes ignored
                }
            });

            MemoryMap.RegisterMmio(0xFF53, () =>
            {
                return (byte)(VramDmaDestination & 0xFF00);
            }, (x) =>
            {
                if (HardwareType == HardwareType.Cgb)
                {
                    VramDmaDestination = (VramDmaDestination & 0xFF) | ((x & 0x1F) << 8); // upper 3 bytes ignored
                }
            });

            MemoryMap.RegisterMmio(0xFF54, () =>
            {
                return (byte)(VramDmaDestination & 0xFF);
            }, (x) =>
            {
                if (HardwareType == HardwareType.Cgb)
                {
                    VramDmaDestination = (VramDmaDestination & 0xFF00) | (x & 0xF0); // lower 4 bytes ignored
                }
            });

            MemoryMap.RegisterMmio(0xFF55, () =>
            {
                if (HBlankDmaActive || BytesLeftInHBlankDma != 0)
                {
                    byte b = 0x80; // bit 7 always set

                    b |= (byte)((BytesLeftInHBlankDma / 0x10) - 0x1);

                    return b;
                }
                else
                {
                    return 0xFF;
                }
            }, (x) =>
            {
                if (HardwareType == HardwareType.Cgb)
                {
                    int length = ((x & 0x7F) + 0x1) * 0x10; // lower 7 bits

                    if (MathUtil.IsBitSet(x, 7)) // H-Blank DMA
                    {
                        HBlankDmaActive = true;
                        BytesLeftInHBlankDma = length;
                        HBlankDmaSourcePointer = VramDmaSource;
                        HBlankDmaDestinationPointer = VramDmaDestination;
                    }
                    else
                    {
                        if (HBlankDmaActive)
                        {
                            HBlankDmaActive = false;
                        }
                        else
                        {
                            HBlankDmaActive = false;
                            BytesLeftInHBlankDma = 0;

                            // Perform the DMA now
                            for (int i = 0; i < length; i++)
                            {
                                byte val = MemoryMap.Read(VramDmaSource + i);
                                VideoRamRegion.WriteDirect(VramDmaDestination + i, val);
                            }
                        }
                    }
                }
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
                    if (CurrentScanlineCyclePosition == 80)
                    {
                        ChangePpuMode(PpuMode.PictureGeneration);
                    }

                    break;
                case PpuMode.PictureGeneration:
                    if (CurrentScanlineCyclePosition == 252)
                    {
                        ChangePpuMode(PpuMode.HBlank);
                    }

                    break;
                case PpuMode.HBlank:
                    VideoRamRegion.Unlock();
                    OamRegion.Unlock();

                    if (CurrentScanlineCyclePosition == 456)
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
                    if (CurrentScanlineCyclePosition == 456)
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

                    if (HBlankDmaActive)
                    {
                        // Perform any VRAM DMA now
                        int correctedLength = BytesLeftInHBlankDma > 0x10 ? 0x10 : BytesLeftInHBlankDma;

                        for (int i = 0; i < correctedLength; i++)
                        {
                            byte val = MemoryMap.Read(HBlankDmaSourcePointer + i);
                            VideoRamRegion.WriteDirect(HBlankDmaDestinationPointer + i, val);
                        }

                        HBlankDmaSourcePointer += correctedLength;
                        HBlankDmaDestinationPointer += correctedLength;
                        BytesLeftInHBlankDma -= correctedLength;

                        if (BytesLeftInHBlankDma == 0)
                        {
                            HBlankDmaActive = false;
                        }
                    }

                    DrawScanline();

                    if (HBlankInterruptEnabled)
                    {
                        Cpu.RaiseInterrupt(1);
                    }

                    break;
                case PpuMode.VBlank:
                    // The VBlank STAT interrupt is *not the same* as the VBlank interrupt.
                    if (VBlankInterruptEnabled)
                    {
                        Cpu.RaiseInterrupt(1);
                    }

                    Cpu.RaiseInterrupt(0);

                    break;
            }
        }

        private void ChangeScanline(bool toTop)
        {
            CurrentScanline = (byte)(toTop ? 0 : CurrentScanline + 1);

            if (CoincidenceInterruptEnabled && CurrentScanline == ScanlineCompare)
            {
                Cpu.RaiseInterrupt(1);
            }

            CurrentScanlineCyclePosition = 0;
            SkipDrawingObjectsForScanline = false;
        }

        private void DrawTilePixel(int screenX, int screenY, int tileIdx, byte attributes, int tilePixelX, int tilePixelY, bool isObject = false)
        {
            int dataOfs = 0;
            if (!isObject && !UseAlternateTileData)
            {
                // The primary tile data mode has some weird quirks - the tileIdx here
                // is actually a signed byte relative to 0x9000
                dataOfs = 0x1000;
                tileIdx = (sbyte)tileIdx;
            }

            int dataStartOfs = dataOfs + (tileIdx * 16) + (tilePixelY * 2);

            int bank = 0;
            if (HardwareType == HardwareType.Cgb)
            {
                bank = MathUtil.GetBit(attributes, 3);
            }

            byte tileLow = VideoRamRegion.ReadDirect(dataStartOfs, bank);
            byte tileHigh = VideoRamRegion.ReadDirect(dataStartOfs + 1, bank);

            int colourIdx = (MathUtil.GetBit(tileHigh, 7 - tilePixelX) << 1) | MathUtil.GetBit(tileLow, 7 - tilePixelX);

            ColourPalette palette;
            if (isObject)
            {
                // Colour 0 is used as transparancy for objects.
                if (colourIdx == 0)
                {
                    return;
                }

                // If this priority bit is set, this pixel should only be drawn if
                // the colour behind it is BG colour 0. BG colour 1-3 are always
                // shown in front of the object.
                // TODO: exception case when two sprites overlap
                if (MathUtil.IsBitSet(attributes, 7))
                {
                    if (PixelPriority[screenX] != PixelType.BgColourZero)
                    {
                        return;
                    }
                }

                int paletteIdx;
                if (HardwareType == HardwareType.Cgb)
                {
                    paletteIdx = attributes & 0x7;
                }
                else
                {
                    paletteIdx = MathUtil.GetBit(attributes, 4);
                }

                palette = ObjectPalettes[paletteIdx];
            }
            else
            {
                if (HardwareType == HardwareType.Cgb)
                {
                    palette = BgPalettes[attributes & 0x7];
                }
                else
                {
                    palette = BgPalettes[0];
                }

                // Write the BG colour as our priority
                PixelPriority[screenX] = colourIdx == 0 ? PixelType.BgColourZero : PixelType.BgColourOther;
            }

            int outputOfs = ((screenY * 160) + screenX) * 4;

            byte ToScreenColour(int colour)
            {
                if (HardwareType == HardwareType.Cgb)
                {
                    return (byte)((colour / 31.0f) * 255.0f);
                }
                else
                {
                    return (byte)colour;
                }
            }

            LcdColour colour = palette.GetColour(colourIdx);
            RawOutput[outputOfs] = ToScreenColour(colour.R);
            RawOutput[outputOfs + 1] = ToScreenColour(colour.G);
            RawOutput[outputOfs + 2] = ToScreenColour(colour.B);

            // Alpha should always be 255 (1.0f)
            RawOutput[outputOfs + 3] = 255;
        }

        private void DrawScanline()
        {
            // The BG/Window flag is used for other purposes on CGB, so it's OK if it isn't set
            if (HardwareType == HardwareType.Cgb || BgAndWindowEnabledOrPriority)
            {
                int bgTileMapOfs = UseAlternateBgTileMap ? 0x1c00 : 0x1800;
                int windowTileMapOfs = UseAlternateWindowTileMap ? 0x1c00 : 0x1800;

                byte bgPixelY = (byte)(CurrentScanline + BgScrollY);
                int bgTileY = bgPixelY / 8;
                int bgTilePixelY = bgPixelY % 8;

                int realWindowX = WindowX - 7;
                bool windowChecking = EnableWindow && realWindowX >= 0 && CurrentScanline >= WindowY;

                for (byte x = 0; x < 160; x++)
                {
                    int tilePixelX;
                    int tilePixelY;
                    int tileAddress;

                    if (windowChecking && x >= realWindowX)
                    {
                        int windowPixelY = CurrentScanline - WindowY;
                        int windowTileY = windowPixelY / 8;
                        tilePixelY = windowPixelY % 8;

                        int windowPixelX = x - realWindowX;
                        int windowTileX = windowPixelX / 8;
                        tilePixelX = windowPixelX % 8;

                        tileAddress = windowTileMapOfs + (windowTileY * 32) + windowTileX;
                    }
                    else
                    {
                        tilePixelY = bgTilePixelY;

                        byte bgPixelX = (byte)(x + BgScrollX);
                        int bgTileX = bgPixelX / 8;
                        tilePixelX = bgPixelX % 8;

                        tileAddress = bgTileMapOfs + (bgTileY * 32) + bgTileX;
                    }

                    // Tile map is always in bank 0
                    byte tileIdx = VideoRamRegion.ReadDirect(tileAddress, 0);

                    byte attributes = 0;
                    if (HardwareType == HardwareType.Cgb)
                    {
                        // Attributes are always stored in bank 1
                        attributes = VideoRamRegion.ReadDirect(tileAddress, 1);
                    }

                    DrawTilePixel(x, CurrentScanline, tileIdx, attributes, tilePixelX, tilePixelY);
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
                            Attributes = OamRegion.ReadDirect(objectAddress + 3),
                            OamIdx = i
                        });
                    }
                }

                // Sort objects based on X-coordinate, using OAM index as a tiebreaker.
                // Since only 10 objects are allowed at a time, we take the first 10 objects.
                // The drawing order is reversed so objects with a lower X coordinate are drawn first
                // and overlapped by objects with higher X coordinates.
                // TODO: CGB uses OAM index only for sorting
                IEnumerable<GbObject> sortedObjs = objectsToRender.GroupBy(o => o.XCoord).Select(g => g.OrderBy(o => o.OamIdx))
                                                                  .Select(oe => oe.First())
                                                                  .Take(10)
                                                                  .Reverse();

                foreach (GbObject obj in sortedObjs)
                {
                    if (obj.XCoord == 0 || obj.XCoord >= 168)
                    {
                        continue;
                    }

                    int objTargetPixelY = (CurrentScanline + 16) - obj.YCoord;

                    // Vertical flip
                    if (MathUtil.IsBitSet(obj.Attributes, 6))
                    {
                        objTargetPixelY = 7 - objTargetPixelY;
                    }

                    for (int x = 0; x < 160; x++)
                    {
                        if (!MathUtil.InRange(x + 8, obj.XCoord, 8))
                        {
                            continue;
                        }

                        int objTargetPixelX = (x + 8) - obj.XCoord;

                        // Horizontal flip
                        if (MathUtil.IsBitSet(obj.Attributes, 5))
                        {
                            objTargetPixelX = 7 - objTargetPixelX;
                        }

                        DrawTilePixel(x, CurrentScanline, obj.TileIdx, obj.Attributes, objTargetPixelX, objTargetPixelY, true);
                    }
                }

            }
        }

        private byte GetPaletteData(List<ColourPalette> palettes, int writeSpecification)
        {
            byte b = 0;

            ColourPalette palette = palettes[writeSpecification >> 3];
            LcdColour colour = palette.GetColour((writeSpecification >> 1) & 0x3);

            if (MathUtil.IsBitSet((byte)writeSpecification, 0))
            {
                b |= 0x80; // high bit unused
                b |= (byte)(colour.B << 2);
                b |= (byte)(colour.G >> 3);
            }
            else
            {
                b |= (byte)((colour.G & 0x7) << 5);
                b |= (byte)colour.R;
            }

            return b;
        }

        private void UpdatePalette(List<ColourPalette> palettes, int writeSpecification, byte value)
        {
            ColourPalette palette = palettes[writeSpecification >> 3];
            LcdColour colour = palette.GetColour((writeSpecification >> 1) & 0x3);

            if (MathUtil.IsBitSet((byte)writeSpecification, 0))
            {
                colour.B = (value >> 2) & 0x1F;
                colour.G = (colour.G & 0x7) | ((value & 0x3) << 3);
            }
            else
            {
                colour.G = (colour.G & 0x18) | (value >> 5);
                colour.R = value & 0x1F;
            }
        }

        public void SetHardwareType(HardwareType type)
        {
            HardwareType = type;
        }

        public byte[] GetPixelOutput()
        {
            return RawOutput;
        }

    }
}

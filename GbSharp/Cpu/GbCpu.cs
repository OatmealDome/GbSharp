using GbSharp.Memory;
using System;

namespace GbSharp.Cpu
{
    class GbCpu
    {
        private ushort PC;
        private ushort SP;
        private byte A;
        private byte F;
        private readonly RegisterPair BC;
        private readonly RegisterPair DE;
        private readonly RegisterPair HL;
        private readonly IeRegisterRegion IeRegion;

        private readonly GbMemory MemoryMap;

        public GbCpu(GbMemory memory)
        {
            PC = 0;
            SP = 0;
            A = 0;
            F = 0;
            BC = new RegisterPair();
            DE = new RegisterPair();
            HL = new RegisterPair();

            IeRegion = new IeRegisterRegion();
            memory.RegisterRegion(0xFFFF, 0x1, IeRegion);

            MemoryMap = memory;
        }

        private byte AdvancePC()
        {
            byte val = MemoryMap.Read(PC);
            PC++;

            return val;
        }

        private bool CheckFlag(CpuFlag flag)
        {
            // for example, checking Carry:
            // F      1001 0000
            // mask   0001 0000
            // AND    0001 0000
            int mask = (1 << (int)flag);
            return (F & mask) == mask;
        }

        private void ClearFlag(CpuFlag flag)
        {
            // for example, clearing Carry:
            // F      1001 0000
            // mask   0001 0000
            // invert 1110 0000
            // AND    1000 0000
            F &= (byte)~(1 << (int)flag);
        }

        private void SetFlag(CpuFlag flag)
        {
            // for example, setting Carry:
            // F    1000 0000
            // mask 0001 0000
            // XOR  1001 0000
            F |= (byte)(1 << (int)flag);
        }

        private void SetFlag(CpuFlag flag, bool val)
        {
            if (val)
            {
                SetFlag(flag);
            }
            else
            {
                ClearFlag(flag);
            }
        }

        private bool CheckOverflowOnBit(int baseVal, int operand, int bit)
        {
            // The carry flags are set if there is a carry from one group
            // of bits to another.
            //
            // For example, take the following:
            // 
            // LD A, 0x8 ; 0b1000
            // LD B, 0x8 ; 0b1000
            // ADD A, B  ; A = A + B
            //
            //      1
            //   0000 1000
            // + 0000 1000
            // -----------
            //   0001 0000
            //
            // In this addition operation, the half carry flag will be set.
            // 
            // An easy way to check to see if the (half) carry flag should
            // be set is to see if the sum of the relevant bits creates a
            // number which can't fit in the same number of bits.
            //
            // Taking the example above:
            //
            // Using the bit we are checking for a carry on, we calculate
            // that the mask is 0000 1111 ((1 << (3 + 1)) - 1).
            //
            //     0000 1000
            // AND 0000 1111
            // -------------
            //     0000 1000
            //
            //      1
            //        1000
            // +      1000
            // -----------
            //   0001 0000
            //
            // Since the sum is greater than the mask, there will be a carry
            // from bit 3 to 4.
            //

            int mask = (1 << (bit + 1)) - 1;

            return (baseVal & mask) + (operand & mask) > mask;
        }

        private bool CheckBorrowFromBit(int baseVal, int operand, int bit)
        {
            // The carry flags are set if there is a borrow from one group
            // of bits to another.
            //
            // For example, take the following:
            //
            // LD A, 0x10 ; 0b00010000
            // LD B, 0x8  ; 0b0001000
            // SUB A, B   ; A = A - B
            //
            //      - 1
            //   0001 0000
            // - 0000 1000
            // -----------
            //   0000 1000
            //
            // In this subtraction operation, the half carry flag will be
            // set because there is a borrow from bit 4 to 3.
            // 
            // An easy way to check this is take the difference of the 
            // relevant bits and check to see if it is negative.
            //
            // Taking the example above:
            //
            // Using the bit we are checking for a borrow from, we calculate
            // that the mask is 0000 1111 ((1 << 4) - 1).
            //
            //     0000 1000
            // AND 0000 1111
            // -------------
            //     0000 1000
            //
            //     0001 0000
            // AND 0000 1111
            // -------------
            //     0000 0000
            //
            // Then, we perform a subtraction with these values as signed:
            //
            //      - 
            //        0000
            // -      1000
            // -----------
            //   1111 1000
            //
            // Since the difference is negative, there will be a borrow from
            // bit 4.
            //

            int mask = (1 << bit) - 1;

            return (baseVal & mask) - (operand & mask) < 0;
        }

        private void PushStack(ushort value)
        {
            SP--;
            MemoryMap.Write(SP, (byte)(value >> 8));

            SP--;
            MemoryMap.Write(SP, (byte)(value & 0xFF));
        }

        private ushort PopStack()
        {
            ushort value = MemoryMap.Read(SP);
            SP++;

            value |= (ushort)(MemoryMap.Read(SP) << 8);
            SP++;

            return value;
        }

        /// <summary>
        /// Advances the CPU by one instruction.
        /// </summary>
        /// <returns>The number of CPU cycles taken to execute this instruction.</returns>
        public int Step()
        {
            byte opcode = AdvancePC();
            switch (opcode)
            {
                // NOP
                case 0x00: return 1;

                // STOP
                // case 0x10: ...;

                // JR Nf, s8
                case 0x20: return Jr(CpuFlag.Zero, false);
                case 0x30: return Jr(CpuFlag.Carry, false);

                // LD pair, u16
                case 0x01: return Ld(BC);
                case 0x11: return Ld(BC);
                case 0x21: return Ld(BC);
                case 0x31: return Ld(BC);

                // LD (pair), A
                case 0x02: return Ld(A, BC);
                case 0x12: return Ld(A, DE);
                case 0x22: return Ld(A, HL, PostLdOperation.Increment);
                case 0x32: return Ld(A, HL, PostLdOperation.Decrement);

                // INC pair
                case 0x03: return IncPair(BC);
                case 0x13: return IncPair(DE);
                case 0x23: return IncPair(HL);
                case 0x33: return IncSp();

                // INC x
                case 0x04: return Inc(ref BC.High);
                case 0x14: return Inc(ref DE.High);
                case 0x24: return Inc(ref HL.High);

                // INC (HL)
                case 0x34: return IncPtr(HL);

                // DEC x
                case 0x05: return Dec(ref BC.High);
                case 0x15: return Dec(ref DE.High);
                case 0x25: return Dec(ref HL.High);
                case 0x35: return DecPtr(HL);

                // LD x, u8
                case 0x06: return Ld(ref BC.High);
                case 0x16: return Ld(ref DE.High);
                case 0x26: return Ld(ref HL.High);

                // LD (HL), u8
                case 0x36: return Ld(AdvancePC(), HL) + 1;

                // RLCA
                case 0x07: return Rlca();
                
                // RLA
                case 0x17: return Rla();

                // DAA
                // case 0x27: ...;

                // SCF
                case 0x37: return Scf();

                // LD (u16), SP
                case 0x08: return LdSpToAddress();

                // JR s8
                case 0x18: return Jr(CpuFlag.None);

                // JR f, s8
                case 0x28: return Jr(CpuFlag.Zero, true);
                case 0x38: return Jr(CpuFlag.Carry, true);

                // ADD HL, pair
                case 0x09: return Add(BC.Value);
                case 0x19: return Add(DE.Value);
                case 0x29: return Add(HL.Value);
                case 0x39: return Add(SP);

                // LD A, (pair)
                case 0x0A: return Ld(BC, ref A);
                case 0x1A: return Ld(DE, ref A);
                case 0x2A: return Ld(HL, ref A, PostLdOperation.Increment);
                case 0x3A: return Ld(HL, ref A, PostLdOperation.Decrement);

                // DEC pair
                case 0x0B: return DecPair(BC);
                case 0x1B: return DecPair(DE);
                case 0x2B: return DecPair(HL);
                case 0x3B: return DecSp();

                // INC x
                case 0x0C: return Inc(ref BC.Low);
                case 0x1C: return Inc(ref DE.Low);
                case 0x2C: return Inc(ref HL.Low);
                case 0x3C: return Inc(ref A);

                // DEC x
                case 0x0D: return Dec(ref BC.Low);
                case 0x1D: return Dec(ref DE.Low);
                case 0x2D: return Dec(ref HL.Low);
                case 0x3D: return Dec(ref A);

                // LD x, u8
                case 0x0E: return Ld(ref BC.Low);
                case 0x1E: return Ld(ref DE.Low);
                case 0x2E: return Ld(ref HL.Low);
                case 0x3E: return Ld(ref A);

                // RRCA
                case 0x0F: return Rrca();

                // RRA
                case 0x1F: return Rra();

                // CPL
                case 0x2F: return Cpl();

                // CCF
                case 0x3F: return Ccf();

                // LD B, x
                case 0x40: return Ld(BC.High, ref BC.High);
                case 0x41: return Ld(BC.Low, ref BC.High);
                case 0x42: return Ld(DE.High, ref BC.High);
                case 0x43: return Ld(DE.Low, ref BC.High);
                case 0x44: return Ld(HL.High, ref BC.High);
                case 0x45: return Ld(HL.Low, ref BC.High);
                case 0x46: return Ld(HL, ref BC.High);
                case 0x47: return Ld(A, ref BC.High);

                // LD C, x
                case 0x48: return Ld(BC.High, ref BC.Low);
                case 0x49: return Ld(BC.Low, ref BC.Low);
                case 0x4A: return Ld(DE.High, ref BC.Low);
                case 0x4B: return Ld(DE.Low, ref BC.Low);
                case 0x4C: return Ld(HL.High, ref BC.Low);
                case 0x4D: return Ld(HL.Low, ref BC.Low);
                case 0x4E: return Ld(HL, ref BC.Low);
                case 0x4F: return Ld(A, ref BC.Low);

                // LD D, x
                case 0x50: return Ld(BC.High, ref DE.High);
                case 0x51: return Ld(BC.Low, ref DE.High);
                case 0x52: return Ld(DE.High, ref DE.High);
                case 0x53: return Ld(DE.Low, ref DE.High);
                case 0x54: return Ld(HL.High, ref DE.High);
                case 0x55: return Ld(HL.Low, ref DE.High);
                case 0x56: return Ld(HL, ref DE.High);
                case 0x57: return Ld(A, ref DE.High);

                // LD E, x
                case 0x58: return Ld(BC.High, ref DE.Low);
                case 0x59: return Ld(BC.Low, ref DE.Low);
                case 0x5A: return Ld(DE.High, ref DE.Low);
                case 0x5B: return Ld(DE.Low, ref DE.Low);
                case 0x5C: return Ld(HL.High, ref DE.Low);
                case 0x5D: return Ld(HL.Low, ref DE.Low);
                case 0x5E: return Ld(HL, ref DE.Low);
                case 0x5F: return Ld(A, ref DE.Low);

                // LD H, x
                case 0x60: return Ld(BC.High, ref HL.High);
                case 0x61: return Ld(BC.Low, ref HL.High);
                case 0x62: return Ld(DE.High, ref HL.High);
                case 0x63: return Ld(DE.Low, ref HL.High);
                case 0x64: return Ld(HL.High, ref HL.High);
                case 0x65: return Ld(HL.Low, ref HL.High);
                case 0x66: return Ld(HL, ref HL.High);
                case 0x67: return Ld(A, ref HL.High);

                // LD L, x
                case 0x68: return Ld(BC.High, ref HL.Low);
                case 0x69: return Ld(BC.Low, ref HL.Low);
                case 0x6A: return Ld(DE.High, ref HL.Low);
                case 0x6B: return Ld(DE.Low, ref HL.Low);
                case 0x6C: return Ld(HL.High, ref HL.Low);
                case 0x6D: return Ld(HL.Low, ref HL.Low);
                case 0x6E: return Ld(HL, ref HL.Low);
                case 0x6F: return Ld(A, ref HL.Low);

                // LD (HL), x
                case 0x70: return Ld(BC.High, HL);
                case 0x71: return Ld(BC.Low, HL);
                case 0x72: return Ld(DE.High, HL);
                case 0x73: return Ld(DE.Low, HL);
                case 0x74: return Ld(HL.High, HL);
                case 0x75: return Ld(HL.Low, HL);
                case 0x77: return Ld(A, HL);

                // HALT
                // case 0x76: ...;

                // LD A, x
                case 0x78: return Ld(BC.High, ref A);
                case 0x79: return Ld(BC.Low, ref A);
                case 0x7A: return Ld(DE.High, ref A);
                case 0x7B: return Ld(DE.Low, ref A);
                case 0x7C: return Ld(HL.High, ref A);
                case 0x7D: return Ld(HL.Low, ref A);
                case 0x7E: return Ld(HL, ref A);
                case 0x7F: return Ld(A, ref A);

                // ADD A, x
                case 0x80: return Add(BC.High, false);
                case 0x81: return Add(BC.Low, false);
                case 0x82: return Add(DE.High, false);
                case 0x83: return Add(DE.Low, false);
                case 0x84: return Add(HL.High, false);
                case 0x85: return Add(HL.Low, false);
                case 0x86: return AddPtr(false);
                case 0x87: return Add(A, false);

                // ADC A, x
                case 0x88: return Add(BC.High, true);
                case 0x89: return Add(BC.Low, true);
                case 0x8A: return Add(DE.High, true);
                case 0x8B: return Add(DE.Low, true);
                case 0x8C: return Add(HL.High, true);
                case 0x8D: return Add(HL.Low, true);
                case 0x8E: return AddPtr(true);
                case 0x8F: return Add(A, true);

                // SUB A, x
                case 0x90: return Sub(BC.High, false);
                case 0x91: return Sub(BC.Low, false);
                case 0x92: return Sub(DE.High, false);
                case 0x93: return Sub(DE.Low, false);
                case 0x94: return Sub(HL.High, false);
                case 0x95: return Sub(HL.Low, false);
                case 0x96: return SubPtr(false);
                case 0x97: return Sub(A, false);

                // SBC A, x
                case 0x98: return Sub(BC.High, true);
                case 0x99: return Sub(BC.Low, true);
                case 0x9A: return Sub(DE.High, true);
                case 0x9B: return Sub(DE.Low, true);
                case 0x9C: return Sub(HL.High, true);
                case 0x9D: return Sub(HL.Low, true);
                case 0x9E: return SubPtr(true);
                case 0x9F: return Sub(A, true);

                // AND A, x
                case 0xA0: return And(BC.High);
                case 0xA1: return And(BC.Low);
                case 0xA2: return And(DE.High);
                case 0xA3: return And(DE.Low);
                case 0xA4: return And(HL.High);
                case 0xA5: return And(HL.Low);
                case 0xA6: return AndPtr();
                case 0xA7: return And(A);

                // XOR A, x
                case 0xA8: return Xor(BC.High);
                case 0xA9: return Xor(BC.Low);
                case 0xAA: return Xor(DE.High);
                case 0xAB: return Xor(DE.Low);
                case 0xAC: return Xor(HL.High);
                case 0xAD: return Xor(HL.Low);
                case 0xAE: return XorPtr();
                case 0xAF: return Xor(A);

                // OR A, x
                case 0xB0: return Or(BC.High);
                case 0xB1: return Or(BC.Low);
                case 0xB2: return Or(DE.High);
                case 0xB3: return Or(DE.Low);
                case 0xB4: return Or(HL.High);
                case 0xB5: return Or(HL.Low);
                case 0xB6: return OrPtr();
                case 0xB7: return Or(A);

                // CP A, x
                case 0xB8: return Sub(BC.High, false, false);
                case 0xB9: return Sub(BC.Low, false, false);
                case 0xBA: return Sub(DE.High, false, false);
                case 0xBB: return Sub(DE.Low, false, false);
                case 0xBC: return Sub(HL.High, false, false);
                case 0xBD: return Sub(HL.Low, false, false);
                case 0xBE: return SubPtr(false, false);
                case 0xBF: return Sub(A, false, false);

                // RET Cf
                case 0xC0: return Ret(CpuFlag.Zero, false);
                case 0xD0: return Ret(CpuFlag.Carry, false);

                // LD (FF00 + u8), A
                case 0xE0: return LdUpperMemory(true);

                // LD A, (FF00 + u8)
                case 0xF0: return LdUpperMemory(false);

                // POP pair
                case 0xC1: return PopInst(BC);
                case 0xD1: return PopInst(DE);
                case 0xE1: return PopInst(HL);
                case 0xF1: return PopInstAf();

                // JP Cf, u16
                case 0xC2: return Jp(CpuFlag.Zero, false);
                case 0xD2: return Jp(CpuFlag.Carry, false);

                // LD (FF00 + C), A
                case 0xE2: return LdUpperMemory(BC.Low, true);

                // LD A, (FF00 + C)
                case 0xF2: return LdUpperMemory(BC.Low, false);

                // JP u16
                case 0xC3: return Jp(CpuFlag.None);

                // DI
                // case 0xF3: ...;

                // CALL Nf, u16
                case 0xC4: return Call(CpuFlag.Zero, false);
                case 0xD4: return Call(CpuFlag.Carry, false);

                // PUSH pair
                case 0xC5: return PushInst(BC);
                case 0xD5: return PushInst(DE);
                case 0xE5: return PushInst(HL);
                case 0xF5: return PushInstAf();

                // ADD A, u8
                case 0xC6: return Add();

                // SUB A, u8
                case 0xD6: return Sub();

                // AND A, u8
                case 0xE6: return And();

                // OR A, u8
                case 0xF6: return Or();

                // RST vec
                case 0xC7: return Rst(0x00);
                case 0xD7: return Rst(0x10);
                case 0xE7: return Rst(0x10);
                case 0xF7: return Rst(0x30);
                case 0xCF: return Rst(0x08);
                case 0xDF: return Rst(0x18);
                case 0xEF: return Rst(0x28);
                case 0xFF: return Rst(0x38);

                // RET f
                case 0xC8: return Ret(CpuFlag.Zero, true);
                case 0xD8: return Ret(CpuFlag.Carry, true);

                // ADD SP, s8
                case 0xE8: return AddSp(false);
                case 0xF8: return AddSp(true);

                // RET
                case 0xC9: return Ret(CpuFlag.None);

                // RETI
                // case 0xD9: return Reti(CpuFlag.None);

                // JP HL
                case 0xE9: return JpHl();

                // LD SP, HL
                case 0xF9: return LdSpHl();
                
                default:
                    throw new Exception($"Invalid opcode {opcode} at PC = {PC - 1}");
            }

        }

        /// <summary>
        /// NOP
        /// </summary>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Nop()
        {
            return 1;
        }

        /// <summary>
        /// JR s8
        /// JR Cf, s8
        /// </summary>
        /// <param name="flag">The CpuFlag to check.</param>
        /// <param name="setTo">The value of the CpuFlag needed to execute the jump</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Jr(CpuFlag flag, bool setTo = false)
        {
            sbyte offset = (sbyte)AdvancePC();

            if (flag == CpuFlag.None || CheckFlag(flag) == setTo)
            {
                PC = (ushort)(PC + offset);

                return 3;
            }

            return 2;
        }

        /// <summary>
        /// JP u16
        /// JP Cf, u16
        /// </summary>
        /// <param name="flag">The CpuFlag to check.</param>
        /// <param name="setTo">The value of the CpuFlag needed to execute the jump</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Jp(CpuFlag flag, bool setTo = false)
        {
            ushort address = (ushort)(AdvancePC() | (AdvancePC() << 8));

            if (flag == CpuFlag.None || CheckFlag(flag) == setTo)
            {
                PC = address;

                return 4;
            }

            return 3;
        }

        /// <summary>
        /// JP HL
        /// </summary>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int JpHl()
        {
            PC = HL.Value;

            return 1;
        }

        /// <summary>
        /// CALL u16
        /// CALL Cf, u16
        /// </summary>
        /// <param name="flag">The CpuFlag to check.</param>
        /// <param name="setTo">The value of the CpuFlag needed to execute the jump</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Call(CpuFlag flag, bool setTo = false)
        {
            ushort address = (ushort)(AdvancePC() | (AdvancePC() << 8));

            if (flag == CpuFlag.None || CheckFlag(flag) == setTo)
            {
                PushStack(PC);

                PC = address;

                return 6;
            }

            return 3;
        }

        /// <summary>
        /// RET
        /// RET Cf
        /// </summary>
        /// <param name="flag">The CpuFlag to check.</param>
        /// <param name="setTo">The value of the CpuFlag needed to execute the jump</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Ret(CpuFlag flag, bool setTo = false)
        {
            if (flag == CpuFlag.None || CheckFlag(flag) == setTo)
            {
                PC = PopStack();

                return flag == CpuFlag.None ? 4 : 5;
            }

            return 2;
        }

        /// <summary>
        /// RST vec
        /// </summary>
        /// <param name="vec">The RST vector to jump to.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Rst(byte vec)
        {
            PushStack(PC);

            PC = vec;

            return 4;
        }

        /// <summary>
        /// PUSH pair
        /// </summary>
        /// <param name="pair">The RegisterPair to push to the stack.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int PushInst(RegisterPair pair)
        {
            PushStack(pair.Value);

            return 4;
        }

        /// <summary>
        /// PUSH AF
        /// </summary>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int PushInstAf()
        {
            PushStack((ushort)(A << 8 | F));

            return 4;
        }

        /// <summary>
        /// POP pair
        /// </summary>
        /// <param name="pair">The RegisterPair to pop from the stack.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int PopInst(RegisterPair pair)
        {
            pair.Value = PopStack();

            return 3;
        }

        /// <summary>
        /// POP AF
        /// </summary>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int PopInstAf()
        {
            ushort value = PopStack();

            A = (byte)(value >> 8);
            F = (byte)(value & 0xFF);

            return 3;
        }

        /// <summary>
        /// LD dest, source
        /// </summary>
        /// <param name="source">The value to load.</param>
        /// <param name="dest">The destination register.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Ld(byte source, ref byte dest)
        {
            dest = source;
            return 1;
        }

        /// <summary>
        /// LD dest, (memoryPtr)
        /// </summary>
        /// <param name="memoryPtr">The RegisterPair containing the source memory pointer.</param>
        /// <param name="dest">The destination register.</param>
        /// <param name="postOperation">The operation to perform on the RegisterPair after writing the value.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Ld(RegisterPair memoryPtr, ref byte dest, PostLdOperation postOperation = PostLdOperation.None)
        {
            dest = MemoryMap.Read(memoryPtr.Value);

            if (postOperation == PostLdOperation.Increment)
            {
                memoryPtr.Value++;
            }
            else if (postOperation == PostLdOperation.Decrement)
            {
                memoryPtr.Value--;
            }

            return 2;
        }

        /// <summary>
        /// LD (memoryPtr), source
        /// </summary>
        /// <param name="source">The value to load.</param>
        /// <param name="memoryPtr">The RegisterPair containing the destination memory pointer.</param>
        /// <param name="postOperation">The operation to perform on the RegisterPair after writing the value.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Ld(byte source, RegisterPair memoryPtr, PostLdOperation postOperation = PostLdOperation.None)
        {
            MemoryMap.Write(memoryPtr.Value, source);

            if (postOperation == PostLdOperation.Increment)
            {
                memoryPtr.Value++;
            }
            else if (postOperation == PostLdOperation.Decrement)
            {
                memoryPtr.Value--;
            }

            return 2;
        }

        /// <summary>
        /// LD pair, u16
        /// </summary>
        /// <param name="dest">The destination RegisterPair.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Ld(RegisterPair dest)
        {
            dest.Low = AdvancePC();
            dest.High = AdvancePC();

            return 3;
        }

        /// <summary>
        /// LD x, u8
        /// </summary>
        /// <param name="register">The destination register.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Ld(ref byte register)
        {
            register = AdvancePC();

            return 2;
        }

        /// <summary>
        /// LD (u16), SP
        /// </summary>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int LdSpToAddress()
        {
            MemoryMap.Write(AdvancePC(), (byte)(SP & 0xFF));
            MemoryMap.Write(AdvancePC(), (byte)(SP >> 8));

            return 5;
        }

        /// <summary>
        /// LD (FF00 + offset), A
        /// LD A, (FF00 + offset)
        /// </summary>
        /// <param name="offset">The offset from 0xFF00.</param>
        /// <param name="store">If the accumulator should be stored to the address. If unset, the accumulator will be set to the value at the address.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int LdUpperMemory(byte offset, bool store)
        {
            ushort address = (ushort)(0xFF00 + offset);

            if (store)
            {
                MemoryMap.Write(address, A);
            }
            else
            {
                A = MemoryMap.Read(address);
            }

            return 2;
        }

        /// <summary>
        /// LD (FF00 + u8), A
        /// LD A, (FF00 + u8)
        /// </summary>
        /// <param name="store">If the accumulator should be stored to the address. If unset, the accumulator will be set to the value at the address.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int LdUpperMemory(bool store)
        {
            return LdUpperMemory(AdvancePC(), store) + 1;
        }

        /// <summary>
        /// LD SP, HL
        /// </summary>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int LdSpHl()
        {
            SP = HL.Value;

            return 2;
        }

        /// <summary>
        /// INC pair
        /// </summary>
        /// <param name="pair">The RegisterPair to increment.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int IncPair(RegisterPair pair)
        {
            pair.Value++;

            return 2;
        }

        /// <summary>
        /// INC (pair)
        /// </summary>
        /// <param name="pair">The RegisterPair containing the memory pointer to increment.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int IncPtr(RegisterPair pair)
        {
            ClearFlag(CpuFlag.Negative);

            byte baseVal = MemoryMap.Read(pair.Value);
            byte sum = (byte)(baseVal + 1);

            SetFlag(CpuFlag.HalfCarry, CheckOverflowOnBit(baseVal, 1, 3));

            SetFlag(CpuFlag.Zero, sum == 0);

            MemoryMap.Write(pair.Value, sum);

            return 3;
        }

        /// <summary>
        /// INC SP
        /// </summary>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int IncSp()
        {
            SP++;

            return 2;
        }

        /// <summary>
        /// INC x
        /// </summary>
        /// <param name="register">The register to increment.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Inc(ref byte register)
        {
            ClearFlag(CpuFlag.Negative);

            byte baseVal = register++;

            SetFlag(CpuFlag.HalfCarry, CheckOverflowOnBit(baseVal, 1, 3));

            SetFlag(CpuFlag.Zero, register == 0);

            return 1;
        }

        /// <summary>
        /// DEC pair
        /// </summary>
        /// <param name="pair">The RegisterPair to decrement.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int DecPair(RegisterPair pair)
        {
            pair.Value--;

            return 2;
        }

        /// <summary>
        /// DEC (pair)
        /// </summary>
        /// <param name="pair">The RegisterPair containing the memory pointer to decrement.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int DecPtr(RegisterPair pair)
        {
            SetFlag(CpuFlag.Negative);

            byte baseVal = MemoryMap.Read(pair.Value);
            byte difference = (byte)(baseVal - 1);

            SetFlag(CpuFlag.HalfCarry, CheckBorrowFromBit(baseVal, 1, 4));

            SetFlag(CpuFlag.Zero, difference == 0);

            MemoryMap.Write(pair.Value, difference);

            return 3;
        }

        /// <summary>
        /// DEC SP
        /// </summary>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int DecSp()
        {
            SP++;

            return 2;
        }

        /// <summary>
        /// DEC x
        /// </summary>
        /// <param name="register">The register to decrement.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Dec(ref byte register)
        {
            SetFlag(CpuFlag.Negative);

            byte baseVal = register--;

            SetFlag(CpuFlag.HalfCarry, CheckBorrowFromBit(baseVal, 1, 4));

            SetFlag(CpuFlag.Zero, register == 0);

            return 1;
        }

        /// <summary>
        /// RLCA
        /// 
        /// Rotates A left and stores the seventh bit into the carry and the zeroth bit.
        /// </summary>
        /// <returns></returns>
        private int Rlca()
        {
            ClearFlag(CpuFlag.Zero);
            ClearFlag(CpuFlag.Negative);
            ClearFlag(CpuFlag.HalfCarry);

            int seventhBit = A >> 7;
            SetFlag(CpuFlag.Carry, seventhBit == 1);

            A = (byte)((A << 1) | seventhBit);

            return 1;
        }

        /// <summary>
        /// RLA
        /// 
        /// Rotates A left through the carry.
        /// </summary>
        /// <returns></returns>
        private int Rla()
        {
            ClearFlag(CpuFlag.Zero);
            ClearFlag(CpuFlag.Negative);
            ClearFlag(CpuFlag.HalfCarry);

            int carry = CheckFlag(CpuFlag.Carry) ? 1 : 0;

            int seventhBit = A >> 7;
            SetFlag(CpuFlag.Carry, seventhBit == 1);

            A = (byte)((A << 1) | carry);

            return 1;
        }

        /// RRCA
        /// 
        /// Rotates A right and stores the zeroth bit into the carry and the seventh bit.
        /// </summary>
        /// <returns></returns>
        private int Rrca()
        {
            ClearFlag(CpuFlag.Zero);
            ClearFlag(CpuFlag.Negative);
            ClearFlag(CpuFlag.HalfCarry);

            int zerothBit = A & 1;
            SetFlag(CpuFlag.Carry, zerothBit == 1);

            A = (byte)((A >> 1) | zerothBit << 7);

            return 1;
        }

        /// <summary>
        /// RRA
        /// 
        /// Rotates A right through the carry.
        /// </summary>
        /// <returns></returns>
        private int Rra()
        {
            ClearFlag(CpuFlag.Zero);
            ClearFlag(CpuFlag.Negative);
            ClearFlag(CpuFlag.HalfCarry);

            int carry = CheckFlag(CpuFlag.Carry) ? 1 : 0;

            int zerothBit = A & 1;
            SetFlag(CpuFlag.Carry, zerothBit == 1);

            A = (byte)((A >> 1) | carry << 7);

            return 1;
        }

        /// <summary>
        /// SCF
        /// </summary>
        /// <returns></returns>
        private int Scf()
        {
            SetFlag(CpuFlag.Carry);

            return 1;
        }

        /// <summary>
        /// ADD A, source
        /// ADC A, source
        /// </summary>
        /// <param name="source">The summand.</param>
        /// <param name="addCarry">Whether the carry should be added.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Add(byte source, bool addCarry = false)
        {
            ClearFlag(CpuFlag.Negative);

            bool halfCarry = false;
            bool carry = false;

            if (addCarry)
            {
                byte carryVal = (byte)(CheckFlag(CpuFlag.Carry) ? 1 : 0);

                halfCarry = CheckOverflowOnBit(A, 1, 3);
                carry = CheckOverflowOnBit(A, 1, 7);

                A += carryVal;
            }

            if (!halfCarry)
            {
                halfCarry = CheckOverflowOnBit(A, source, 3);
            }

            if (!carry)
            {
                carry = CheckOverflowOnBit(A, source, 7);
            }

            A += source;

            SetFlag(CpuFlag.HalfCarry, halfCarry);
            SetFlag(CpuFlag.Carry, carry);

            SetFlag(CpuFlag.Zero, A == 0);

            return 1;
        }

        /// <summary>
        /// ADD A, (HL)
        /// ADC A, (HL)
        /// </summary>
        /// <param name="addCarry">Whether the carry should be added.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>

        private int AddPtr(bool addCarry = false)
        {
            byte value = MemoryMap.Read(HL.Value);

            return Add(value, addCarry) + 1;
        }

        /// <summary>
        /// ADD A, u8
        /// ADC A, u8
        /// </summary>
        /// <param name="addCarry">Whether the carry should be added.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Add(bool addCarry = false)
        {
            byte value = AdvancePC();

            return Add(value, addCarry) + 1;
        }

        /// <summary>
        /// ADD SP, s8
        /// LD HL, SP + s8
        /// </summary>
        /// <param name="storeHl">If the result should be stored into HL instead of SP.</param>
        /// <returns></returns>
        private int AddSp(bool storeHl)
        {
            ClearFlag(CpuFlag.Zero);
            ClearFlag(CpuFlag.Negative);

            sbyte offset = (sbyte)AdvancePC();

            CheckOverflowOnBit(SP, offset, 3);
            CheckOverflowOnBit(SP, offset, 7);

            ushort newAddress = (ushort)(SP + offset);
            if (storeHl)
            {
                HL.Value = newAddress;

                return 3;
            }
            else
            {
                SP = newAddress;

                return 4;
            }
        }

        /// <summary>
        /// SUB A, source
        /// SBC A, source
        /// CP A, source
        /// </summary>
        /// <param name="source">The subtrahend.</param>
        /// <param name="subtractCarry">Whether the carry should be subtracted.</param>
        /// <param name="storeResult">Whether the result should be stored in the accumulator.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Sub(byte source, bool subtractCarry = false, bool storeResult = true)
        {
            SetFlag(CpuFlag.Negative);

            bool halfCarry = false;
            bool carry = false;

            byte val = A;

            if (subtractCarry)
            {
                byte carryVal = (byte)(CheckFlag(CpuFlag.Carry) ? 1 : 0);

                halfCarry = CheckBorrowFromBit(A, 1, 4);
                carry = carryVal > A;

                val -= carryVal;
            }

            if (!halfCarry)
            {
                halfCarry = CheckBorrowFromBit(A, source, 4);
            }

            if (!carry)
            {
                carry = source > A;
            }

            val -= source;

            SetFlag(CpuFlag.HalfCarry, halfCarry);
            SetFlag(CpuFlag.Carry, carry);

            SetFlag(CpuFlag.Zero, val == 0);

            if (storeResult)
            {
                A = val;
            }

            return 1;
        }

        /// <summary>
        /// SUB A, (HL)
        /// SBC A, (HL)
        /// CP A, (HL)
        /// </summary>
        /// <param name="addCarry">Whether the carry should be subtracted.</param>
        /// <param name="storeResult">Whether the result should be stored into to the accumulator.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>

        private int SubPtr(bool addCarry = false, bool storeResult = true)
        {
            byte value = MemoryMap.Read(HL.Value);

            return Sub(value, addCarry, storeResult) + 1;
        }

        /// <summary>
        /// SUB A, u8
        /// SBC A, u8
        /// CP A, u8
        /// </summary>
        /// <param name="addCarry">Whether the carry should be subtracted.</param>
        /// <param name="storeResult">Whether the result should be stored into to the accumulator.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Sub(bool addCarry = false, bool storeResult = true)
        {
            byte value = AdvancePC();

            return Sub(value, addCarry) + 1;
        }

        /// <summary>
        /// ADD HL, u16
        /// 
        /// Used for both RegisterPairs and SP.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Add(ushort value)
        {
            ClearFlag(CpuFlag.Negative);

            SetFlag(CpuFlag.HalfCarry, CheckOverflowOnBit(HL.Value, value, 11));
            SetFlag(CpuFlag.Carry, CheckOverflowOnBit(HL.Value, value, 15));

            HL.Value += value;

            SetFlag(CpuFlag.Zero, HL.Value == 0);

            return 2;
        }

        /// <summary>
        /// CPL
        /// </summary>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Cpl()
        {
            A = (byte)~A;

            return 1;
        }

        /// <summary>
        /// CCF
        /// </summary>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Ccf()
        {
            SetFlag(CpuFlag.Carry, !CheckFlag(CpuFlag.Carry));

            return 1;
        }

        /// <summary>
        /// AND A, source
        /// </summary>
        /// <param name="source">The value to AND the accumulator with.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int And(byte source)
        {
            ClearFlag(CpuFlag.Negative);
            SetFlag(CpuFlag.HalfCarry);
            ClearFlag(CpuFlag.Carry);

            A &= source;

            SetFlag(CpuFlag.Zero, A == 0);

            return 1;
        }

        /// <summary>
        /// AND A, (HL)
        /// </summary>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int AndPtr()
        {
            byte value = MemoryMap.Read(HL.Value);

            return And(value) + 1;
        }

        /// <summary>
        /// AND A, u8
        /// </summary>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int And()
        {
            byte value = AdvancePC();

            return And(value) + 1;
        }

        /// <summary>
        /// XOR A, source
        /// </summary>
        /// <param name="source">The value to XOR the accumulator with.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Xor(byte source)
        {
            ClearFlag(CpuFlag.Negative);
            ClearFlag(CpuFlag.HalfCarry);
            ClearFlag(CpuFlag.Carry);

            A ^= source;

            SetFlag(CpuFlag.Zero, A == 0);

            return 1;
        }

        /// <summary>
        /// XOR A, (HL)
        /// </summary>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int XorPtr()
        {
            byte value = MemoryMap.Read(HL.Value);

            return Xor(value) + 1;
        }

        /// <summary>
        /// XOR A, u8
        /// </summary>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Xor()
        {
            byte value = AdvancePC();

            return Xor(value) + 1;
        }

        /// <summary>
        /// OR A, source
        /// </summary>
        /// <param name="source">The value to OR the accumulator with.</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Or(byte source)
        {
            ClearFlag(CpuFlag.Negative);
            ClearFlag(CpuFlag.HalfCarry);
            ClearFlag(CpuFlag.Carry);

            A |= source;

            SetFlag(CpuFlag.Zero, A == 0);

            return 1;
        }

        /// <summary>
        /// OR A, (HL)
        /// </summary>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int OrPtr()
        {
            byte value = MemoryMap.Read(HL.Value);

            return Or(value) + 1;
        }

        /// <summary>
        /// OR A, u8
        /// </summary>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Or()
        {
            byte value = AdvancePC();

            return Or(value) + 1;
        }

    }
}

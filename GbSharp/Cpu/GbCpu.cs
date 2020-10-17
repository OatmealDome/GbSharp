﻿using GbSharp.Memory;
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
            F ^= (byte)(1 << (int)flag);
        }

        private void SetFlag(CpuFlag flag, bool val)
        {
            bool currentFlag = CheckFlag(flag);
            if (currentFlag && !val)
            {
                ClearFlag(flag);
            }
            else if (!currentFlag && val)
            {
                SetFlag(flag);
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
                case 0x18: return Jr();

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
        /// </summary>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Jr()
        {
            sbyte offset = (sbyte)AdvancePC();
            PC = (ushort)(PC + offset);

            return 3;
        }

        /// <summary>
        /// JR Cf, s8
        /// </summary>
        /// <param name="flag">The CpuFlag to check.</param>
        /// <param name="setTo">The value of the CpuFlag needed to execute the jump</param>
        /// <returns>The number of CPU cycles to execute this instruction.</returns>
        private int Jr(CpuFlag flag, bool setTo)
        {
            sbyte offset = (sbyte)AdvancePC();
            bool flagValue = CheckFlag(flag);

            if (setTo == flagValue)
            {
                PC = (ushort)(PC + offset);

                return 3;
            }

            return 2;
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

            if (sum == 0)
            {
                SetFlag(CpuFlag.Zero);
            }
            else
            {
                ClearFlag(CpuFlag.Zero);
            }

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

            if (register == 0)
            {
                SetFlag(CpuFlag.Zero);
            }
            else
            {
                ClearFlag(CpuFlag.Zero);
            }

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

            if (difference == 0)
            {
                SetFlag(CpuFlag.Zero);
            }
            else
            {
                ClearFlag(CpuFlag.Zero);
            }

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

            if (register == 0)
            {
                SetFlag(CpuFlag.Zero);
            }
            else
            {
                ClearFlag(CpuFlag.Zero);
            }

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

    }
}

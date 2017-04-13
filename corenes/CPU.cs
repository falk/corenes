using System;
using System.Reflection;
using System.Security.Cryptography;

namespace corenes
{
    public struct StepParameters
    {
        public ushort address;
        public ushort pc;
        public byte mode;
    }

    class Cpu
    {
        public Memory Memory { get; }

        enum Interrupts
        {
            None,
            NMI,
            IRQ
        };

        // Accumulator (A)
        private byte _a;

        // Index register (X)
        private byte _x;

        // Index register (Y)
        private byte _y;

        // Stack Pointer (SP)
        private byte _sp;

        // Program Counter (PC)
        private ushort _pc;

        private int _cycles;

        private StepParameters _stepParameters;

        // Flags
        private byte _c;

        private byte _z;
        private byte _i;
        private byte _d;
        private byte _b;
        private byte _u;
        private byte _v;
        private byte _n;

        public Cpu(Emulator memory)
        {
            Memory = memory;
            this._stepParameters = new StepParameters();
        }

        public void Reset()
        {
            _x = _y = _a = 0;
            _pc = Read16(0xFFFC);
            _sp = 0xFD;
            SetFlags(0x24);
        }

        private void SetFlags(byte flags)
        {
            _c = (byte) ((flags >> 0) & 1);
            _z = (byte) ((flags >> 1) & 1);
            _i = (byte) ((flags >> 2) & 1);
            _d = (byte) ((flags >> 3) & 1);
            _b = (byte) ((flags >> 4) & 1);
            _u = (byte) ((flags >> 5) & 1);
            _v = (byte) ((flags >> 6) & 1);
            _n = (byte) ((flags >> 7) & 1);
        }

        private byte GetFlags()
        {
            byte flags = (byte) (_c << 0);
            flags |= (byte) (_z << 1);
            flags |= (byte) (_i << 2);
            flags |= (byte) (_d << 3);
            flags |= (byte) (_b << 4);
            flags |= (byte) (_u << 5);
            flags |= (byte) (_v << 6);
            flags |= (byte) (_n << 7);

            return flags;
        }

        public int Step()
        {
            byte opCode = Memory.read(_pc);
            string instructionName = _instructionNames[opCode];


            int cycles = _cycles;

            switch (_interrupt)
            {
                case Interrupts.NMI:
                    NMI();
                    break;
                case Interrupts.IRQ:
                    IRQ();
                    break;
            }
            _interrupt = Interrupts.None;

            var mode = _adressingMode[opCode];
            bool samePage = false;
            ushort address = 0;

            switch (mode)
            {
                // Absolute
                case 1:
                    address = Memory.read((ushort) (_pc + 1));
                    break;
                // Absolute X
                case 2:
                    address = (ushort) (Memory.read((ushort) (_pc + 1)) + _x);
                    samePage = SamePage((ushort) (address - _x), address);
                    break;
                // Absolute Y
                case 3:
                    address = (ushort) (Memory.read((ushort) (_pc + 1)) + _y);
                    samePage = SamePage((ushort) (address - _y), address);
                    break;
                // Immediate
                case 5:
                    address = (ushort) (_pc + 1);
                    break;
                // Accumulator
                // Implied
                case 4:
                case 6:
                    address = 0;
                    break;
                // Indexed indirect
                case 7:
                    address = Read16Bug((ushort) (Memory.read((ushort) (_pc + 1)) + _x));
                    break;
                // Indirect
                case 8:
                    address = Read16Bug(Read16((ushort) (_pc + 1)));
                    break;
                // Indirect indexed
                case 9:
                    address = (ushort) (Read16Bug(Memory.read( (ushort) (_pc + 1))) + _y);
                    samePage = SamePage((ushort) (address - _y), address);
                    break;
                // Relative
                case 10:
                    var offset = Memory.read((ushort) (_pc + 1));
                    if (offset < 0x80)
                    {
                        address = (ushort)(_pc + 2 + offset);
                    }
                    else
                    {
                        address = (ushort) (_pc + 2 + offset - 0x100);
                    }
                    break;
                // Zero page
                case 11:
                    address = Memory.read((ushort) (_pc + 1));
                    break;
                // Zero page X
                case 12:
                    address = (ushort) (Memory.read((ushort) (_pc + 1)) + _x);
                    break;
                // Zero page Y
                case 13:
                    address = (ushort) (Memory.read((ushort) (_pc + 1)) + _y);
                    break;
            }


            _cycles += _instructionCycles[opCode];

            if (samePage)
            {
                _cycles += _instructionPageCycles[opCode];
            }

            _pc += _instructionSizes[opCode];

            _stepParameters.address = address;
            _stepParameters.mode = mode;
            _stepParameters.pc = _pc;

            Type type = typeof(Cpu);
            MethodInfo op = type.GetMethod(instructionName, BindingFlags.NonPublic | BindingFlags.Instance);
            try
            {
                op.Invoke(this, new object[] { _stepParameters });
            }
            catch (Exception e)
            {
                Console.WriteLine("Missing op: " + instructionName);
                throw;
            }
            Console.WriteLine(instructionName);

            return _cycles - cycles;
        }

        // Non-Maskable Interrupt
        private void NMI()
        {
            push16(_pc);
            PHP(new StepParameters());
            _pc = Read16(0xFFFA);
            _i = 1;
            _cycles += 7;
        }

        // IRQ - IRQ Interrupt
        private void IRQ()
        {
            push16(_pc);
            PHP(new StepParameters());
            _pc = Read16(0xFFFE);
            _i = 1;
            _cycles += 7;
        }

        // BCC - Branch if Carry Clear
        private void BCC(StepParameters parms)
        {
            if (_c == 0)
            {
                _pc = parms.address;
                addBranchCycles(parms);
            }
        }

        // BCS - Branch if Carry Set
        private void BCS(StepParameters parms)
        {
            if (_c != 0)
            {
                _pc = parms.address;
                addBranchCycles(parms);
            }
        }

        // BEQ - Branch if Equal
        private void BEQ(StepParameters parms)
        {
            if (_z != 0)
            {
                _pc = parms.address;
                addBranchCycles(parms);
            }
        }

        // BMI - Branch if negative
        private void BMI(StepParameters parms)
        {
            if (_n != 0)
            {
                _pc = parms.address;
                addBranchCycles(parms);
            }
        }

        // BNE - Branch if Not Equal
        private void BNE(StepParameters parms)
        {
            if (_z == 0)
            {
                _pc = parms.address;
                addBranchCycles(parms);
            }
        }

        // Branch if positive
        private void BPL(StepParameters parms)
        {
            if (_n == 0)
            {
                _pc = parms.address;
                addBranchCycles(parms);
            }
        }

        // addBranchCycles adds a cycle for taking a branch and adds another cycle
        // if the branch jumps to a new page
        private void addBranchCycles(StepParameters parms)
        {
            _cycles++;
            if (!SamePage(parms.pc, parms.address))
            {
                _cycles++;
            }
        }


        // Store accumulator
        private void STA(StepParameters parms)
        {
            Memory.write(parms.address, _a);           
        }

        private ushort Read16(ushort address)
        {
            var low = Memory.read(address);
            var high = Memory.read((ushort) (address + 1));
            return (ushort) (high << 8 | low);
        }

        private ushort Read16Bug(ushort address)
        {
            ushort b = (ushort)((address & 0xFF00) | (address + 1));
            var low = Memory.read(address);
            var high = Memory.read(b);
            return (ushort)(high << 8 | low);
        }

        // Check if addresses are on the same page
        private bool SamePage(ushort addressA, ushort addressB)
        {
            return (addressA & 0xFF00) == (addressB & 0xFF00);
        }

        // LDA - Load Accumulator
        private void LDA(StepParameters parms)
        {
            _a = Memory.read(parms.address);
            setZN(_a);
        }

        // BRK - Force Interrupt
        private void BRK(StepParameters parms)
        {
            push16(_pc);
            PHP(parms);
            SEI(parms);
            _pc = Read16(0xFFFE);
        }

        // LDX - Load X Register
        private void LDX(StepParameters parms)
        {
            _x = Memory.read(parms.address);
            setZN(_x);
        }

        // TXS - Transfer X to Stack Pointer
        private void TXS(StepParameters parms)
        {
            _sp = _x;
        }

        // setZ sets the zero flag if the argument is zero
        private void setZ(byte value)
        {
            if (value == 0)
            {
                _z = 1;
            }
            else
            {
                _z = 0;
            }
        }

        // Sets the zero flag and the negative flag
        private void setZN(byte value)
        {
            setZ(value);
            setN(value);
        }

        // Sets the negative flag if the argument is negative (high bit is set)
        private void setN(byte value)
        {
            if ((value & 0x80) != 0)
            {
                _n = 1;
            }
            else
            {
                _n = 0;
            }
        }

        // Push 2 bytes to stack
        private void push16(ushort value)
        {
            byte high = (byte) (value >> 8);
            byte low = (byte) (value & 0xFF);
            push(high);
            push(low);
        }

        private void push(byte value)
        {
            Memory.write((ushort) (0x100 | _sp), value);
            _sp--;
        }

        // CLD - Clear Decimal Mode
        private void CLD(StepParameters parms)
        {
            _d = 0;
        }


        private void PHP(StepParameters parms)
        {
            push((byte) (GetFlags() | 0x10));
        }

        // SEI - Set Interrupt Disable
        private void SEI(StepParameters parms)
        {
            _i = 1;
        }

        private void NOP(StepParameters parms)
        {
            
        }

        private readonly byte[] _adressingMode =
        {
            6, 7, 6, 7, 11, 11, 11, 11, 6, 5, 4, 5, 1, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 12, 12, 6, 3, 6, 3, 2, 2, 2, 2,
            1, 7, 6, 7, 11, 11, 11, 11, 6, 5, 4, 5, 1, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 12, 12, 6, 3, 6, 3, 2, 2, 2, 2,
            6, 7, 6, 7, 11, 11, 11, 11, 6, 5, 4, 5, 1, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 12, 12, 6, 3, 6, 3, 2, 2, 2, 2,
            6, 7, 6, 7, 11, 11, 11, 11, 6, 5, 4, 5, 8, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 12, 12, 6, 3, 6, 3, 2, 2, 2, 2,
            5, 7, 5, 7, 11, 11, 11, 11, 6, 5, 6, 5, 1, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 13, 13, 6, 3, 6, 3, 2, 2, 3, 3,
            5, 7, 5, 7, 11, 11, 11, 11, 6, 5, 6, 5, 1, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 13, 13, 6, 3, 6, 3, 2, 2, 3, 3,
            5, 7, 5, 7, 11, 11, 11, 11, 6, 5, 6, 5, 1, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 12, 12, 6, 3, 6, 3, 2, 2, 2, 2,
            5, 7, 5, 7, 11, 11, 11, 11, 6, 5, 6, 5, 1, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 12, 12, 6, 3, 6, 3, 2, 2, 2, 2
        };

        // instructionSizes indicates the size of each instruction in bytes
        private byte[] _instructionSizes =
        {
            1, 2, 0, 0, 2, 2, 2, 0, 1, 2, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 3, 3, 3, 0,
            3, 2, 0, 0, 2, 2, 2, 0, 1, 2, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 3, 3, 3, 0,
            1, 2, 0, 0, 2, 2, 2, 0, 1, 2, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 3, 3, 3, 0,
            1, 2, 0, 0, 2, 2, 2, 0, 1, 2, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 0, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 0, 3, 0, 0,
            2, 2, 2, 0, 2, 2, 2, 0, 1, 2, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 2, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 2, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 3, 3, 3, 0,
        };

        // instructionCycles indicates the number of cycles used by each instruction,
        // not including conditional cycles
        private byte[] _instructionCycles =
        {
            7, 6, 2, 8, 3, 3, 5, 5, 3, 2, 2, 2, 4, 4, 6, 6,
            2, 5, 2, 8, 4, 4, 6, 6, 2, 4, 2, 7, 4, 4, 7, 7,
            6, 6, 2, 8, 3, 3, 5, 5, 4, 2, 2, 2, 4, 4, 6, 6,
            2, 5, 2, 8, 4, 4, 6, 6, 2, 4, 2, 7, 4, 4, 7, 7,
            6, 6, 2, 8, 3, 3, 5, 5, 3, 2, 2, 2, 3, 4, 6, 6,
            2, 5, 2, 8, 4, 4, 6, 6, 2, 4, 2, 7, 4, 4, 7, 7,
            6, 6, 2, 8, 3, 3, 5, 5, 4, 2, 2, 2, 5, 4, 6, 6,
            2, 5, 2, 8, 4, 4, 6, 6, 2, 4, 2, 7, 4, 4, 7, 7,
            2, 6, 2, 6, 3, 3, 3, 3, 2, 2, 2, 2, 4, 4, 4, 4,
            2, 6, 2, 6, 4, 4, 4, 4, 2, 5, 2, 5, 5, 5, 5, 5,
            2, 6, 2, 6, 3, 3, 3, 3, 2, 2, 2, 2, 4, 4, 4, 4,
            2, 5, 2, 5, 4, 4, 4, 4, 2, 4, 2, 4, 4, 4, 4, 4,
            2, 6, 2, 8, 3, 3, 5, 5, 2, 2, 2, 2, 4, 4, 6, 6,
            2, 5, 2, 8, 4, 4, 6, 6, 2, 4, 2, 7, 4, 4, 7, 7,
            2, 6, 2, 8, 3, 3, 5, 5, 2, 2, 2, 2, 4, 4, 6, 6,
            2, 5, 2, 8, 4, 4, 6, 6, 2, 4, 2, 7, 4, 4, 7, 7,
        };

// instructionPageCycles indicates the number of cycles used by each
// instruction when a page is crossed
    private byte[] _instructionPageCycles =
        {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 0, 1, 0, 0, 0, 0, 0, 1, 0, 1, 1, 1, 1, 1,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0,
        };

        private string[] _instructionNames =
        {
            "BRK", "ORA", "KIL", "SLO", "NOP", "ORA", "ASL", "SLO",
            "PHP", "ORA", "ASL", "ANC", "NOP", "ORA", "ASL", "SLO",
            "BPL", "ORA", "KIL", "SLO", "NOP", "ORA", "ASL", "SLO",
            "CLC", "ORA", "NOP", "SLO", "NOP", "ORA", "ASL", "SLO",
            "JSR", "AND", "KIL", "RLA", "BIT", "AND", "ROL", "RLA",
            "PLP", "AND", "ROL", "ANC", "BIT", "AND", "ROL", "RLA",
            "BMI", "AND", "KIL", "RLA", "NOP", "AND", "ROL", "RLA",
            "SEC", "AND", "NOP", "RLA", "NOP", "AND", "ROL", "RLA",
            "RTI", "EOR", "KIL", "SRE", "NOP", "EOR", "LSR", "SRE",
            "PHA", "EOR", "LSR", "ALR", "JMP", "EOR", "LSR", "SRE",
            "BVC", "EOR", "KIL", "SRE", "NOP", "EOR", "LSR", "SRE",
            "CLI", "EOR", "NOP", "SRE", "NOP", "EOR", "LSR", "SRE",
            "RTS", "ADC", "KIL", "RRA", "NOP", "ADC", "ROR", "RRA",
            "PLA", "ADC", "ROR", "ARR", "JMP", "ADC", "ROR", "RRA",
            "BVS", "ADC", "KIL", "RRA", "NOP", "ADC", "ROR", "RRA",
            "SEI", "ADC", "NOP", "RRA", "NOP", "ADC", "ROR", "RRA",
            "NOP", "STA", "NOP", "SAX", "STY", "STA", "STX", "SAX",
            "DEY", "NOP", "TXA", "XAA", "STY", "STA", "STX", "SAX",
            "BCC", "STA", "KIL", "AHX", "STY", "STA", "STX", "SAX",
            "TYA", "STA", "TXS", "TAS", "SHY", "STA", "SHX", "AHX",
            "LDY", "LDA", "LDX", "LAX", "LDY", "LDA", "LDX", "LAX",
            "TAY", "LDA", "TAX", "LAX", "LDY", "LDA", "LDX", "LAX",
            "BCS", "LDA", "KIL", "LAX", "LDY", "LDA", "LDX", "LAX",
            "CLV", "LDA", "TSX", "LAS", "LDY", "LDA", "LDX", "LAX",
            "CPY", "CMP", "NOP", "DCP", "CPY", "CMP", "DEC", "DCP",
            "INY", "CMP", "DEX", "AXS", "CPY", "CMP", "DEC", "DCP",
            "BNE", "CMP", "KIL", "DCP", "NOP", "CMP", "DEC", "DCP",
            "CLD", "CMP", "NOP", "DCP", "NOP", "CMP", "DEC", "DCP",
            "CPX", "SBC", "NOP", "ISC", "CPX", "SBC", "INC", "ISC",
            "INX", "SBC", "NOP", "SBC", "CPX", "SBC", "INC", "ISC",
            "BEQ", "SBC", "KIL", "ISC", "NOP", "SBC", "INC", "ISC",
            "SED", "SBC", "NOP", "ISC", "NOP", "SBC", "INC", "ISC",
        };

        private Interrupts _interrupt;

        public void TriggerNMI()
        {
            _interrupt = Interrupts.NMI;
        }

        public void TriggerIRQ()
        {
            if (_i == 0)
            {
                _interrupt = Interrupts.IRQ;
            }
        }
    }
}
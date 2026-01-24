namespace tawny;

/// <summary>
/// Implements the TMS9900 instruction set architecture.
/// Handles instruction decoding and execution for all TMS9900 opcodes.
/// </summary>
public class Tms9900Isa
{
    private readonly Tms9900Cpu _cpu;
    private readonly Tms9900Memory _memory;

    public Tms9900Isa(Tms9900Cpu cpu, Tms9900Memory memory)
    {
        _cpu = cpu;
        _memory = memory;
    }

    /// <summary>
    /// Execute a single instruction.
    /// </summary>
    public void Execute(ushort instruction)
    {
        // Decode and execute based on opcode
        byte opcode = (byte)(instruction >> 12);

        switch (opcode)
        {
            case 0x0:
                ExecuteFormat1(instruction);
                break;
            case 0x1:
                ExecuteJumps(instruction);
                break;
            case 0x2:
            case 0x3:
                ExecuteDualOperand(instruction);
                break;
            default:
                ExecuteSingleOperand(instruction);
                break;
        }
    }

    /// <summary>
    /// Execute Format I instructions (arithmetic/logical operations).
    /// </summary>
    private void ExecuteFormat1(ushort instruction)
    {
        int opcode = (instruction >> 8) & 0xFF;

        switch (opcode)
        {
            case 0x02: // LI - Load Immediate
                ExecuteLI(instruction);
                break;
            case 0x03: // Check for RTWP and other 0x03xx instructions
                if ((instruction & 0xFFC0) == 0x0380)
                    _cpu.ReturnFromContext();
                else if ((instruction & 0xFFC0) == 0x03C0)
                    ExecuteBLWP(instruction);
                else
                    DecodeExtendedOps(instruction);
                break;
            case 0x04: // AI - Add Immediate or CLR/NEG/INV/INC
                if ((instruction & 0xFF00) == 0x0400)
                {
                    // Check if this is AI or single-operand instruction
                    if ((instruction & 0x00F0) == 0)
                        ExecuteAI(instruction);
                    else
                        DecodeExtendedOps(instruction);
                }
                else
                {
                    DecodeExtendedOps(instruction);
                }
                break;
            case 0x05: // INCT, DEC, DECT, BL
                DecodeExtendedOps(instruction);
                break;
            case 0x06: // ANDI - AND Immediate or SWPB/SETO/ABS
                if ((instruction & 0xFF00) == 0x0600)
                    ExecuteANDI(instruction);
                else
                    DecodeExtendedOps(instruction);
                break;
            case 0x08: // ORI - OR Immediate
                ExecuteORI(instruction);
                break;
            case 0x0A: // CI - Compare Immediate
                ExecuteCI(instruction);
                break;
            case 0x0C: // STWP - Store Workspace Pointer
                ExecuteSTWP(instruction);
                break;
            case 0x0E: // STST - Store Status Register
                ExecuteSTST(instruction);
                break;
            default:
                // Unknown instruction - could log or halt
                _cpu.Stop();
                break;
        }
    }

    /// <summary>
    /// Execute jump instructions.
    /// </summary>
    private void ExecuteJumps(ushort instruction)
    {
        int opcode = (instruction >> 8) & 0xFF;
        sbyte displacement = (sbyte)(instruction & 0xFF);
        ushort jumpTarget = (ushort)(_cpu.ProgramCounter + (displacement * 2));

        switch (opcode)
        {
            case 0x10: // JMP - Unconditional jump
                _cpu.SetProgramCounter(jumpTarget);
                break;
            case 0x11: // JLT - Jump if Less Than
                if (!_cpu.IsLogicalGreaterThan() && !_cpu.IsEqual())
                    _cpu.SetProgramCounter(jumpTarget);
                break;
            case 0x12: // JLE - Jump if Less or Equal
                if (!_cpu.IsLogicalGreaterThan() || _cpu.IsEqual())
                    _cpu.SetProgramCounter(jumpTarget);
                break;
            case 0x13: // JEQ - Jump if Equal
                if (_cpu.IsEqual())
                    _cpu.SetProgramCounter(jumpTarget);
                break;
            case 0x14: // JHE - Jump if High or Equal
                if (_cpu.IsLogicalGreaterThan() || _cpu.IsEqual())
                    _cpu.SetProgramCounter(jumpTarget);
                break;
            case 0x15: // JGT - Jump if Greater Than
                if (_cpu.IsArithmeticGreaterThan())
                    _cpu.SetProgramCounter(jumpTarget);
                break;
            case 0x16: // JNE - Jump if Not Equal
                if (!_cpu.IsEqual())
                    _cpu.SetProgramCounter(jumpTarget);
                break;
            case 0x17: // JNC - Jump if No Carry
                if (!_cpu.GetCarry())
                    _cpu.SetProgramCounter(jumpTarget);
                break;
            case 0x18: // JOC - Jump if Carry
                if (_cpu.GetCarry())
                    _cpu.SetProgramCounter(jumpTarget);
                break;
            case 0x19: // JNO - Jump if No Overflow
                if (!_cpu.GetOverflow())
                    _cpu.SetProgramCounter(jumpTarget);
                break;
            case 0x1A: // JL - Jump if Less (arithmetic)
                if (!_cpu.IsArithmeticGreaterThan() && !_cpu.IsEqual())
                    _cpu.SetProgramCounter(jumpTarget);
                break;
            case 0x1B: // JH - Jump if High (logical)
                if (_cpu.IsLogicalGreaterThan())
                    _cpu.SetProgramCounter(jumpTarget);
                break;
            case 0x1C: // JOP - Jump if Odd Parity
                // Not commonly implemented in simple simulators
                break;
        }
    }

    /// <summary>
    /// Execute dual operand instructions (register-to-register).
    /// </summary>
    private void ExecuteDualOperand(ushort instruction)
    {
        int opcode = (instruction >> 12) & 0xF;
        int dest = (instruction >> 6) & 0xF;
        int src = instruction & 0xF;

        switch (opcode)
        {
            case 0x2: // COC - Compare Ones Corresponding
                ExecuteCOC(src, dest);
                break;
            case 0x3: // CZC - Compare Zeros Corresponding
                ExecuteCZC(src, dest);
                break;
        }
    }

    /// <summary>
    /// Execute single operand instructions.
    /// </summary>
    private void ExecuteSingleOperand(ushort instruction)
    {
        int opcode = (instruction >> 10) & 0x3F;
        int ts = (instruction >> 4) & 0x3; // Addressing mode
        int reg = instruction & 0xF;

        switch (opcode)
        {
            case 0x04: // SZC - Set Zeros Corresponding
            case 0x05: // SZCB - Set Zeros Corresponding Byte
                ExecuteSZC(instruction, (opcode & 1) == 1);
                break;
            case 0x06: // S - Subtract
            case 0x07: // SB - Subtract Byte
                ExecuteS(instruction, (opcode & 1) == 1);
                break;
            case 0x08: // C - Compare
            case 0x09: // CB - Compare Byte
                ExecuteC(instruction, (opcode & 1) == 1);
                break;
            case 0x0A: // A - Add
            case 0x0B: // AB - Add Byte
                ExecuteA(instruction, (opcode & 1) == 1);
                break;
            case 0x0C: // MOV - Move
            case 0x0D: // MOVB - Move Byte
                ExecuteMOV(instruction, (opcode & 1) == 1);
                break;
            case 0x0E: // SOC - Set Ones Corresponding
            case 0x0F: // SOCB - Set Ones Corresponding Byte
                ExecuteSOC(instruction, (opcode & 1) == 1);
                break;
            case 0x10: // SLA - Shift Left Arithmetic
                ExecuteSLA(instruction);
                break;
            case 0x11: // SRA - Shift Right Arithmetic
                ExecuteSRA(instruction);
                break;
            case 0x12: // SRC - Shift Right Circular
                ExecuteSRC(instruction);
                break;
            case 0x13: // SRL - Shift Right Logical
                ExecuteSRL(instruction);
                break;
            default:
                DecodeExtendedOps(instruction);
                break;
        }
    }

    /// <summary>
    /// Decode extended operations and special instructions.
    /// </summary>
    private void DecodeExtendedOps(ushort instruction)
    {
        int opcode = (instruction >> 6) & 0x3FF;

        if ((instruction & 0xFC00) == 0x2C00) // XOP
        {
            ExecuteXOP(instruction);
        }
        else if ((instruction & 0xFFC0) == 0x0380) // RTWP
        {
            _cpu.ReturnFromContext();
        }
        else if ((instruction & 0xFFC0) == 0x03C0) // BLWP - Branch and Link with Workspace Pointer
        {
            ExecuteBLWP(instruction);
        }
        else if ((instruction & 0xFFC0) == 0x0400) // CLR - Clear
        {
            ExecuteCLR(instruction);
        }
        else if ((instruction & 0xFFC0) == 0x0440) // NEG - Negate
        {
            ExecuteNEG(instruction);
        }
        else if ((instruction & 0xFFC0) == 0x0480) // INV - Invert
        {
            ExecuteINV(instruction);
        }
        else if ((instruction & 0xFFC0) == 0x04C0) // INC - Increment
        {
            ExecuteINC(instruction);
        }
        else if ((instruction & 0xFFC0) == 0x0500) // INCT - Increment by Two
        {
            ExecuteINCT(instruction);
        }
        else if ((instruction & 0xFFC0) == 0x0540) // DEC - Decrement
        {
            ExecuteDEC(instruction);
        }
        else if ((instruction & 0xFFC0) == 0x0580) // DECT - Decrement by Two
        {
            ExecuteDECT(instruction);
        }
        else if ((instruction & 0xFFC0) == 0x05C0) // BL - Branch and Link
        {
            ExecuteBL(instruction);
        }
        else if ((instruction & 0xFFC0) == 0x0600) // SWPB - Swap Bytes
        {
            ExecuteSWPB(instruction);
        }
        else if ((instruction & 0xFFC0) == 0x0640) // SETO - Set to Ones
        {
            ExecuteSETO(instruction);
        }
        else if ((instruction & 0xFFC0) == 0x0680) // ABS - Absolute Value
        {
            ExecuteABS(instruction);
        }
    }

    // ==================== Instruction Implementations ====================

    private void ExecuteLI(ushort instruction)
    {
        int reg = instruction & 0xF;
        ushort immediate = _memory.ReadWord(_cpu.ProgramCounter);
        _cpu.SetProgramCounter((ushort)(_cpu.ProgramCounter + 2));
        _cpu.WriteRegister(reg, immediate);
        _cpu.UpdateStatusFlags(immediate);
    }

    private void ExecuteAI(ushort instruction)
    {
        int reg = instruction & 0xF;
        ushort immediate = _memory.ReadWord(_cpu.ProgramCounter);
        _cpu.SetProgramCounter((ushort)(_cpu.ProgramCounter + 2));
        ushort value = _cpu.ReadRegister(reg);
        int result = value + immediate;
        _cpu.WriteRegister(reg, (ushort)result);
        _cpu.UpdateStatusFlags(result);
        _cpu.SetCarry(result > 0xFFFF);
        _cpu.SetOverflow(((value ^ result) & (immediate ^ result) & 0x8000) != 0);
    }

    private void ExecuteANDI(ushort instruction)
    {
        int reg = instruction & 0xF;
        ushort immediate = _memory.ReadWord(_cpu.ProgramCounter);
        _cpu.SetProgramCounter((ushort)(_cpu.ProgramCounter + 2));
        ushort value = _cpu.ReadRegister(reg);
        ushort result = (ushort)(value & immediate);
        _cpu.WriteRegister(reg, result);
        _cpu.UpdateStatusFlags(result);
    }

    private void ExecuteORI(ushort instruction)
    {
        int reg = instruction & 0xF;
        ushort immediate = _memory.ReadWord(_cpu.ProgramCounter);
        _cpu.SetProgramCounter((ushort)(_cpu.ProgramCounter + 2));
        ushort value = _cpu.ReadRegister(reg);
        ushort result = (ushort)(value | immediate);
        _cpu.WriteRegister(reg, result);
        _cpu.UpdateStatusFlags(result);
    }

    private void ExecuteCI(ushort instruction)
    {
        int reg = instruction & 0xF;
        ushort immediate = _memory.ReadWord(_cpu.ProgramCounter);
        _cpu.SetProgramCounter((ushort)(_cpu.ProgramCounter + 2));
        ushort value = _cpu.ReadRegister(reg);
        int result = value - immediate;
        _cpu.UpdateStatusFlags(result);
    }

    private void ExecuteSTWP(ushort instruction)
    {
        int reg = instruction & 0xF;
        _cpu.WriteRegister(reg, _cpu.WorkspacePointer);
    }

    private void ExecuteSTST(ushort instruction)
    {
        int reg = instruction & 0xF;
        _cpu.WriteRegister(reg, _cpu.StatusRegister);
    }

    private void ExecuteCOC(int src, int dest)
    {
        ushort srcVal = _cpu.ReadRegister(src);
        ushort destVal = _cpu.ReadRegister(dest);
        bool equal = (srcVal & destVal) == srcVal;
        if (equal)
            _cpu.UpdateStatusFlags(0); // Sets EQ flag
        else
            _cpu.UpdateStatusFlags(1); // Clears EQ flag
    }

    private void ExecuteCZC(int src, int dest)
    {
        ushort srcVal = _cpu.ReadRegister(src);
        ushort destVal = _cpu.ReadRegister(dest);
        bool equal = ((~srcVal) & destVal) == (~srcVal);
        if (equal)
            _cpu.UpdateStatusFlags(0);
        else
            _cpu.UpdateStatusFlags(1);
    }

    private void ExecuteSZC(ushort instruction, bool isByte)
    {
        // Simplified implementation - would need full addressing mode support
        int src = (instruction >> 6) & 0xF;
        int dest = instruction & 0xF;
        ushort srcVal = _cpu.ReadRegister(src);
        ushort destVal = _cpu.ReadRegister(dest);
        ushort result = (ushort)(destVal & ~srcVal);
        _cpu.WriteRegister(dest, result);
        _cpu.UpdateStatusFlags(result);
    }

    private void ExecuteS(ushort instruction, bool isByte)
    {
        int src = (instruction >> 6) & 0xF;
        int dest = instruction & 0xF;
        ushort srcVal = _cpu.ReadRegister(src);
        ushort destVal = _cpu.ReadRegister(dest);
        int result = destVal - srcVal;
        _cpu.WriteRegister(dest, (ushort)result);
        _cpu.UpdateStatusFlags(result);
        _cpu.SetCarry(result >= 0);
        _cpu.SetOverflow(((destVal ^ srcVal) & (destVal ^ result) & 0x8000) != 0);
    }

    private void ExecuteC(ushort instruction, bool isByte)
    {
        int src = (instruction >> 6) & 0xF;
        int dest = instruction & 0xF;
        ushort srcVal = _cpu.ReadRegister(src);
        ushort destVal = _cpu.ReadRegister(dest);
        int result = destVal - srcVal;
        _cpu.UpdateStatusFlags(result);
    }

    private void ExecuteA(ushort instruction, bool isByte)
    {
        int src = (instruction >> 6) & 0xF;
        int dest = instruction & 0xF;
        ushort srcVal = _cpu.ReadRegister(src);
        ushort destVal = _cpu.ReadRegister(dest);
        int result = destVal + srcVal;
        _cpu.WriteRegister(dest, (ushort)result);
        _cpu.UpdateStatusFlags(result);
        _cpu.SetCarry(result > 0xFFFF);
        _cpu.SetOverflow(((destVal ^ result) & (srcVal ^ result) & 0x8000) != 0);
    }

    private void ExecuteMOV(ushort instruction, bool isByte)
    {
        int src = (instruction >> 6) & 0xF;
        int dest = instruction & 0xF;
        ushort value = _cpu.ReadRegister(src);
        _cpu.WriteRegister(dest, value);
        _cpu.UpdateStatusFlags(value);
    }

    private void ExecuteSOC(ushort instruction, bool isByte)
    {
        int src = (instruction >> 6) & 0xF;
        int dest = instruction & 0xF;
        ushort srcVal = _cpu.ReadRegister(src);
        ushort destVal = _cpu.ReadRegister(dest);
        ushort result = (ushort)(destVal | srcVal);
        _cpu.WriteRegister(dest, result);
        _cpu.UpdateStatusFlags(result);
    }

    private void ExecuteSLA(ushort instruction)
    {
        int reg = (instruction >> 6) & 0xF;
        int count = instruction & 0xF;
        if (count == 0) count = _cpu.ReadRegister(0) & 0xF;
        if (count == 0) count = 16;

        ushort value = _cpu.ReadRegister(reg);
        for (int i = 0; i < count; i++)
        {
            _cpu.SetCarry((value & 0x8000) != 0);
            value <<= 1;
        }
        _cpu.WriteRegister(reg, value);
        _cpu.UpdateStatusFlags(value);
    }

    private void ExecuteSRA(ushort instruction)
    {
        int reg = (instruction >> 6) & 0xF;
        int count = instruction & 0xF;
        if (count == 0) count = _cpu.ReadRegister(0) & 0xF;
        if (count == 0) count = 16;

        ushort value = _cpu.ReadRegister(reg);
        bool signBit = (value & 0x8000) != 0;
        for (int i = 0; i < count; i++)
        {
            _cpu.SetCarry((value & 0x0001) != 0);
            value >>= 1;
            if (signBit) value |= 0x8000;
        }
        _cpu.WriteRegister(reg, value);
        _cpu.UpdateStatusFlags(value);
    }

    private void ExecuteSRC(ushort instruction)
    {
        int reg = (instruction >> 6) & 0xF;
        int count = instruction & 0xF;
        if (count == 0) count = _cpu.ReadRegister(0) & 0xF;
        if (count == 0) count = 16;

        ushort value = _cpu.ReadRegister(reg);
        for (int i = 0; i < count; i++)
        {
            bool lsb = (value & 0x0001) != 0;
            _cpu.SetCarry(lsb);
            value >>= 1;
            if (lsb) value |= 0x8000;
        }
        _cpu.WriteRegister(reg, value);
        _cpu.UpdateStatusFlags(value);
    }

    private void ExecuteSRL(ushort instruction)
    {
        int reg = (instruction >> 6) & 0xF;
        int count = instruction & 0xF;
        if (count == 0) count = _cpu.ReadRegister(0) & 0xF;
        if (count == 0) count = 16;

        ushort value = _cpu.ReadRegister(reg);
        for (int i = 0; i < count; i++)
        {
            _cpu.SetCarry((value & 0x0001) != 0);
            value >>= 1;
        }
        _cpu.WriteRegister(reg, value);
        _cpu.UpdateStatusFlags(value);
    }

    private void ExecuteXOP(ushort instruction)
    {
        int xopNumber = (instruction >> 6) & 0xF;
        int reg = instruction & 0xF;
        ushort sourceAddress = _cpu.ReadRegister(reg);
        _cpu.ExecuteXOP(xopNumber, sourceAddress);
    }

    private void ExecuteBLWP(ushort instruction)
    {
        int reg = instruction & 0xF;
        ushort address = _cpu.ReadRegister(reg);
        ushort newWP = _memory.ReadWord(address);
        ushort newPC = _memory.ReadWord((ushort)(address + 2));
        _cpu.ContextSwitch(newWP, newPC);
    }

    private void ExecuteCLR(ushort instruction)
    {
        int reg = instruction & 0xF;
        _cpu.WriteRegister(reg, 0);
    }

    private void ExecuteNEG(ushort instruction)
    {
        int reg = instruction & 0xF;
        ushort value = _cpu.ReadRegister(reg);
        int result = -(short)value;
        _cpu.WriteRegister(reg, (ushort)result);
        _cpu.UpdateStatusFlags(result);
        _cpu.SetOverflow(value == 0x8000);
    }

    private void ExecuteINV(ushort instruction)
    {
        int reg = instruction & 0xF;
        ushort value = _cpu.ReadRegister(reg);
        ushort result = (ushort)~value;
        _cpu.WriteRegister(reg, result);
        _cpu.UpdateStatusFlags(result);
    }

    private void ExecuteINC(ushort instruction)
    {
        int reg = instruction & 0xF;
        ushort value = _cpu.ReadRegister(reg);
        int result = value + 1;
        _cpu.WriteRegister(reg, (ushort)result);
        _cpu.UpdateStatusFlags(result);
        _cpu.SetCarry(result > 0xFFFF);
        _cpu.SetOverflow(value == 0x7FFF);
    }

    private void ExecuteINCT(ushort instruction)
    {
        int reg = instruction & 0xF;
        ushort value = _cpu.ReadRegister(reg);
        int result = value + 2;
        _cpu.WriteRegister(reg, (ushort)result);
        _cpu.UpdateStatusFlags(result);
        _cpu.SetCarry(result > 0xFFFF);
    }

    private void ExecuteDEC(ushort instruction)
    {
        int reg = instruction & 0xF;
        ushort value = _cpu.ReadRegister(reg);
        int result = value - 1;
        _cpu.WriteRegister(reg, (ushort)result);
        _cpu.UpdateStatusFlags(result);
        _cpu.SetCarry(result >= 0);
        _cpu.SetOverflow(value == 0x8000);
    }

    private void ExecuteDECT(ushort instruction)
    {
        int reg = instruction & 0xF;
        ushort value = _cpu.ReadRegister(reg);
        int result = value - 2;
        _cpu.WriteRegister(reg, (ushort)result);
        _cpu.UpdateStatusFlags(result);
        _cpu.SetCarry(result >= 0);
    }

    private void ExecuteBL(ushort instruction)
    {
        int reg = instruction & 0xF;
        ushort address = _cpu.ReadRegister(reg);
        _cpu.WriteRegister(11, _cpu.ProgramCounter); // R11 = return address
        _cpu.SetProgramCounter(address);
    }

    private void ExecuteSWPB(ushort instruction)
    {
        int reg = instruction & 0xF;
        ushort value = _cpu.ReadRegister(reg);
        ushort result = (ushort)((value << 8) | (value >> 8));
        _cpu.WriteRegister(reg, result);
    }

    private void ExecuteSETO(ushort instruction)
    {
        int reg = instruction & 0xF;
        _cpu.WriteRegister(reg, 0xFFFF);
    }

    private void ExecuteABS(ushort instruction)
    {
        int reg = instruction & 0xF;
        short value = (short)_cpu.ReadRegister(reg);
        if (value < 0)
        {
            value = (short)-value;
            _cpu.SetOverflow(value == -32768);
        }
        _cpu.WriteRegister(reg, (ushort)value);
        _cpu.UpdateStatusFlags(value);
    }
}

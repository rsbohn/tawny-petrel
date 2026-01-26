namespace tawny;

/// <summary>
/// Represents the TMS9900 CPU with workspace pointer architecture.
/// The TMS9900 uniquely stores all registers in RAM pointed to by the workspace pointer.
/// </summary>
public class Tms9900Cpu
{
    private readonly Tms9900Memory _memory;
    private readonly Tms9900Isa _isa;

    // CPU state
    private ushort _workspacePointer; // WP - Points to 16-word register workspace in memory
    private ushort _programCounter;   // PC - Current instruction address
    private ushort _statusRegister;   // ST - Status flags and interrupt mask

    // Status register bit positions
    private const int LGT_BIT = 0x8000; // Logical Greater Than
    private const int AGT_BIT = 0x4000; // Arithmetic Greater Than
    private const int EQ_BIT = 0x2000;  // Equal
    private const int C_BIT = 0x1000;   // Carry
    private const int OV_BIT = 0x0800;  // Overflow
    private const int OP_BIT = 0x0400;  // Odd Parity
    private const int X_BIT = 0x0200;   // Extended operation

    public bool IsRunning { get; private set; }

    public ushort WorkspacePointer => _workspacePointer;
    public ushort ProgramCounter => _programCounter;
    public ushort StatusRegister => _statusRegister;

    public Tms9900Cpu(Tms9900Memory memory)
    {
        _memory = memory;
        _isa = new Tms9900Isa(this, memory);
        Reset();
    }

    /// <summary>
    /// Reset the CPU to initial state.
    /// </summary>
    public void Reset()
    {
        // On reset, read initial WP and PC from memory locations 0x0000 and 0x0002
        _workspacePointer = _memory.ReadWord(0x0000);
        _programCounter = _memory.ReadWord(0x0002);
        _statusRegister = 0;
        IsRunning = false;
    }

    /// <summary>
    /// Start executing from the current PC.
    /// </summary>
    public void Start()
    {
        IsRunning = true;
    }

    /// <summary>
    /// Stop execution.
    /// </summary>
    public void Stop()
    {
        IsRunning = false;
    }

    /// <summary>
    /// Execute a single instruction.
    /// </summary>
    public void Step()
    {
        if (!IsRunning) return;

        ushort instruction = _memory.ReadWord(_programCounter);
        _programCounter += 2;
        _isa.Execute(instruction);
    }

    /// <summary>
    /// Execute instructions for a specified number of cycles.
    /// </summary>
    public void Run(int cycles = -1)
    {
        IsRunning = true;
        int count = 0;
        while (IsRunning && (cycles < 0 || count < cycles))
        {
            Step();
            count++;
        }
    }

    /// <summary>
    /// Read a workspace register (R0-R15).
    /// Registers are stored in memory at WP + (register_number * 2).
    /// </summary>
    public ushort ReadRegister(int register)
    {
        if (register < 0 || register > 15)
            throw new ArgumentException($"Invalid register number: {register}");
        
        ushort address = (ushort)(_workspacePointer + (register * 2));
        return _memory.ReadWord(address);
    }

    /// <summary>
    /// Write a workspace register (R0-R15).
    /// </summary>
    public void WriteRegister(int register, ushort value)
    {
        if (register < 0 || register > 15)
            throw new ArgumentException($"Invalid register number: {register}");
        
        ushort address = (ushort)(_workspacePointer + (register * 2));
        _memory.WriteWord(address, value);
    }

    /// <summary>
    /// Set the program counter.
    /// </summary>
    public void SetProgramCounter(ushort address)
    {
        _programCounter = address;
    }

    /// <summary>
    /// Set the workspace pointer.
    /// </summary>
    public void SetWorkspacePointer(ushort address)
    {
        _workspacePointer = address;
    }

    /// <summary>
    /// Update status flags based on a result.
    /// </summary>
    public void UpdateStatusFlags(int result, bool isArithmetic = true)
    {
        // Clear condition flags
        _statusRegister &= 0x1FFF;

        // Set Equal flag
        if ((result & 0xFFFF) == 0)
            _statusRegister |= EQ_BIT;

        // Set Logical Greater Than (result > 0 when treated as unsigned)
        if (result > 0)
            _statusRegister |= LGT_BIT;

        // Set Arithmetic Greater Than (result > 0 when treated as signed)
        if (isArithmetic)
        {
            short signedResult = (short)(result & 0xFFFF);
            if (signedResult > 0)
                _statusRegister |= AGT_BIT;
        }
    }

    /// <summary>
    /// Update status flags based on an 8-bit result.
    /// </summary>
    public void UpdateStatusFlagsByte(byte result)
    {
        _statusRegister &= 0x1FFF;

        if (result == 0)
            _statusRegister |= EQ_BIT;

        if (result > 0)
            _statusRegister |= LGT_BIT;

        sbyte signedResult = unchecked((sbyte)result);
        if (signedResult > 0)
            _statusRegister |= AGT_BIT;
    }

    /// <summary>
    /// Set the carry flag.
    /// </summary>
    public void SetCarry(bool value)
    {
        if (value)
            _statusRegister |= C_BIT;
        else
            _statusRegister &= unchecked((ushort)~C_BIT);
    }

    /// <summary>
    /// Set the overflow flag.
    /// </summary>
    public void SetOverflow(bool value)
    {
        if (value)
            _statusRegister |= OV_BIT;
        else
            _statusRegister &= unchecked((ushort)~OV_BIT);
    }

    /// <summary>
    /// Get the carry flag.
    /// </summary>
    public bool GetCarry()
    {
        return (_statusRegister & C_BIT) != 0;
    }

    /// <summary>
    /// Get the overflow flag.
    /// </summary>
    public bool GetOverflow()
    {
        return (_statusRegister & OV_BIT) != 0;
    }

    /// <summary>
    /// Check if Equal flag is set.
    /// </summary>
    public bool IsEqual()
    {
        return (_statusRegister & EQ_BIT) != 0;
    }

    /// <summary>
    /// Check if Logical Greater Than flag is set.
    /// </summary>
    public bool IsLogicalGreaterThan()
    {
        return (_statusRegister & LGT_BIT) != 0;
    }

    /// <summary>
    /// Check if Arithmetic Greater Than flag is set.
    /// </summary>
    public bool IsArithmeticGreaterThan()
    {
        return (_statusRegister & AGT_BIT) != 0;
    }

    /// <summary>
    /// Context switch - saves current context and loads new one (used by XOP/BLWP).
    /// </summary>
    public void ContextSwitch(ushort newWP, ushort newPC)
    {
        // Save old context to new workspace
        ushort oldWP = _workspacePointer;
        ushort oldPC = _programCounter;
        ushort oldST = _statusRegister;

        // Switch to new workspace
        _workspacePointer = newWP;

        // Save old context in new workspace registers
        WriteRegister(13, oldWP);  // R13 = old WP
        WriteRegister(14, oldPC);  // R14 = old PC
        WriteRegister(15, oldST);  // R15 = old ST

        // Set new PC
        _programCounter = newPC;
    }

    /// <summary>
    /// Return from context switch (RTWP instruction).
    /// </summary>
    public void ReturnFromContext()
    {
        // Restore context from current workspace
        ushort oldST = ReadRegister(15);
        ushort oldPC = ReadRegister(14);
        ushort oldWP = ReadRegister(13);

        _statusRegister = oldST;
        _programCounter = oldPC;
        _workspacePointer = oldWP;
    }

    /// <summary>
    /// Execute an XOP (Extended Operation).
    /// </summary>
    public void ExecuteXOP(int xopNumber, ushort sourceAddress)
    {
        // XOP vector table starts at 0x0040, each entry is 4 bytes (WP, PC)
        ushort vectorAddress = (ushort)(0x0040 + (xopNumber * 4));
        
        ushort newWP = _memory.ReadWord(vectorAddress);
        ushort newPC = _memory.ReadWord((ushort)(vectorAddress + 2));

        // Perform context switch
        ContextSwitch(newWP, newPC);

        // Store source address in R11 of new workspace
        WriteRegister(11, sourceAddress);
    }

    /// <summary>
    /// Get CPU state for debugging.
    /// </summary>
    public string GetState()
    {
        return $"PC={_programCounter:X4} WP={_workspacePointer:X4} ST={_statusRegister:X4} " +
               $"R0={ReadRegister(0):X4} R1={ReadRegister(1):X4} R2={ReadRegister(2):X4}";
    }
}

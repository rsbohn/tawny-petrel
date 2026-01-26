namespace tawny;

/// <summary>
/// Represents the memory system for the TMS9900 processor.
/// Implements a 16-bit address space with memory-mapped registers via workspace pointer.
/// </summary>
public class Tms9900Memory
{
    private readonly byte[] _memory;
    private const int MemorySize = 0x10000; // 64KB address space
    private const ushort UartStatusAddress = 0xF000;
    private const ushort UartDataAddress = 0xF002;

    public Tms9900Memory()
    {
        _memory = new byte[MemorySize];
    }

    /// <summary>
    /// Read a 16-bit word from memory (big-endian).
    /// </summary>
    public ushort ReadWord(ushort address)
    {
        if (address >= MemorySize - 1)
            throw new ArgumentOutOfRangeException(nameof(address), 
                $"Address 0x{address:X4} is too close to end of memory for word access");

        if (address == UartStatusAddress)
        {
            return 0x0000;
        }
        
        // TMS9900 is big-endian
        return (ushort)((_memory[address] << 8) | _memory[address + 1]);
    }

    /// <summary>
    /// Write a 16-bit word to memory (big-endian).
    /// </summary>
    public void WriteWord(ushort address, ushort value)
    {
        if (address >= MemorySize - 1)
            throw new ArgumentOutOfRangeException(nameof(address), 
                $"Address 0x{address:X4} is too close to end of memory for word access");
        
        if (address == UartDataAddress)
        {
            Console.Write((char)(value & 0x00FF));
            Console.Out.Flush();
        }

        _memory[address] = (byte)(value >> 8);
        _memory[address + 1] = (byte)(value & 0xFF);
    }

    /// <summary>
    /// Read a byte from memory.
    /// Note: ushort address (0-65535) is naturally within bounds of 64KB memory.
    /// </summary>
    public byte ReadByte(ushort address)
    {
        // No bounds check needed: ushort max (0xFFFF = 65535) is always < MemorySize (0x10000 = 65536)
        if (address == UartStatusAddress)
        {
            return 0x00;
        }
        return _memory[address];
    }

    /// <summary>
    /// Write a byte to memory.
    /// Note: ushort address (0-65535) is naturally within bounds of 64KB memory.
    /// </summary>
    public void WriteByte(ushort address, byte value)
    {
        // No bounds check needed: ushort max (0xFFFF = 65535) is always < MemorySize (0x10000 = 65536)
        if (address == UartDataAddress)
        {
            Console.Write((char)value);
            Console.Out.Flush();
        }
        _memory[address] = value;
    }

    /// <summary>
    /// Load a program into memory starting at the specified address.
    /// </summary>
    public void LoadProgram(ushort startAddress, byte[] program)
    {
        if (program == null)
            throw new ArgumentNullException(nameof(program));
        
        if (startAddress + program.Length > MemorySize)
            throw new ArgumentOutOfRangeException(nameof(startAddress), 
                $"Program of size {program.Length} at address 0x{startAddress:X4} exceeds memory bounds");
        
        Array.Copy(program, 0, _memory, startAddress, program.Length);
    }

    /// <summary>
    /// Clear all memory.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_memory, 0, _memory.Length);
    }

    /// <summary>
    /// Get a memory dump for debugging.
    /// </summary>
    public byte[] GetMemoryDump(ushort startAddress, int length)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative");
        
        if (startAddress + length > MemorySize)
            throw new ArgumentOutOfRangeException(nameof(length), 
                $"Dump of size {length} at address 0x{startAddress:X4} exceeds memory bounds");
        
        var dump = new byte[length];
        Array.Copy(_memory, startAddress, dump, 0, length);
        return dump;
    }
}

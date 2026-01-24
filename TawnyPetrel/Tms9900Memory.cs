namespace TawnyPetrel;

/// <summary>
/// Represents the memory system for the TMS9900 processor.
/// Implements a 16-bit address space with memory-mapped registers via workspace pointer.
/// </summary>
public class Tms9900Memory
{
    private readonly byte[] _memory;
    private const int MemorySize = 0x10000; // 64KB address space

    public Tms9900Memory()
    {
        _memory = new byte[MemorySize];
    }

    /// <summary>
    /// Read a 16-bit word from memory (big-endian).
    /// </summary>
    public ushort ReadWord(ushort address)
    {
        // TMS9900 is big-endian
        return (ushort)((_memory[address] << 8) | _memory[address + 1]);
    }

    /// <summary>
    /// Write a 16-bit word to memory (big-endian).
    /// </summary>
    public void WriteWord(ushort address, ushort value)
    {
        _memory[address] = (byte)(value >> 8);
        _memory[address + 1] = (byte)(value & 0xFF);
    }

    /// <summary>
    /// Read a byte from memory.
    /// </summary>
    public byte ReadByte(ushort address)
    {
        return _memory[address];
    }

    /// <summary>
    /// Write a byte to memory.
    /// </summary>
    public void WriteByte(ushort address, byte value)
    {
        _memory[address] = value;
    }

    /// <summary>
    /// Load a program into memory starting at the specified address.
    /// </summary>
    public void LoadProgram(ushort startAddress, byte[] program)
    {
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
        var dump = new byte[length];
        Array.Copy(_memory, startAddress, dump, 0, length);
        return dump;
    }
}

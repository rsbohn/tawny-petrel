# Tawny Petrel

A TMS9900 simulator designed to provide source-level compatibility with the Usagi Electric minicomputer project while offering an immediate, interactive development environment.

## Features

The simulator faithfully implements the TMS9900 instruction set including:

- **Workspace Pointer Architecture**: All 16 registers (R0-R15) are stored in RAM, pointed to by the Workspace Pointer (WP)
- **Extended Operations (XOP)**: Software interrupt mechanism for system calls and privileged operations
- **Memory Mapper**: Full 64KB addressable memory with big-endian byte ordering
- **Complete Instruction Set**: Arithmetic, logical, shift, jump, and control instructions
- **Context Switching**: Fast task switching via BLWP and RTWP instructions
- **Interactive REPL**: Command-line interface for step-by-step execution and debugging

## Architecture

The simulator consists of three core components:

### Tms9900Memory.cs
Memory management system with:
- 64KB addressable space (16-bit addressing)
- Big-endian byte ordering (TMS9900 standard)
- Word and byte-level read/write operations
- Program loading capabilities

### Tms9900Cpu.cs
CPU implementation featuring:
- Workspace pointer-based register architecture
- Status register with condition flags (LGT, AGT, EQ, C, OV)
- Context switching support
- XOP (Extended Operation) handling
- Single-step and continuous execution modes

### Tms9900Isa.cs
Instruction Set Architecture with:
- 69 TMS9900 instructions
- Immediate, register, and addressing mode support
- Jump instructions with condition flag evaluation
- Shift and rotate operations
- Extended operations (XOP, BLWP, RTWP)

## Building

```bash
cd tawny
dotnet build
```

## Running

```bash
cd tawny
dotnet run
```

The simulator will run a demo program and then enter interactive mode with the following commands:

- `s` or `step` - Execute single instruction
- `r` or `reset` - Reset CPU to initial state
- `d <addr>` - Display memory at address (hex format)
- `reg` or `registers` - Show all register values
- `q` or `quit` - Exit the simulator

## Testing

```bash
cd tawny.Tests
dotnet test
```

The test suite includes 26 comprehensive tests covering:
- Memory operations (read/write, byte ordering)
- CPU operations (registers, status flags, context switching)
- Instruction execution (immediate, arithmetic, logical, jumps)
- XOP and RTWP functionality

## Example Usage

```csharp
// Create simulator
var memory = new Tms9900Memory();
var cpu = new Tms9900Cpu(memory);

// Set up workspace and program counter
memory.WriteWord(0x0000, 0x2000); // WP = 0x2000
memory.WriteWord(0x0002, 0x0100); // PC = 0x0100

// Load a program
ushort[] program = {
    0x0200, 0x0005,  // LI R0, 0x0005  (Load immediate 5 into R0)
    0x0201, 0x0003,  // LI R1, 0x0003  (Load immediate 3 into R1)
};

ushort address = 0x0100;
foreach (var instruction in program)
{
    memory.WriteWord(address, instruction);
    address += 2;
}

// Execute
cpu.Reset();
cpu.Start();
cpu.Step();  // Execute LI R0, 0x0005
cpu.Step();  // Execute LI R1, 0x0003

// Check results
Console.WriteLine($"R0 = {cpu.ReadRegister(0):X4}");  // R0 = 0005
Console.WriteLine($"R1 = {cpu.ReadRegister(1):X4}");  // R1 = 0003
```

## TMS9900 Architecture Highlights

The TMS9900 is unique among 16-bit processors for its memory-based register architecture:

1. **No Hardware Registers**: All 16 registers exist in RAM at the location pointed to by WP
2. **Fast Context Switching**: Changing contexts only requires updating WP (single instruction)
3. **Flexible Workspaces**: Multiple register sets can exist simultaneously in different memory regions
4. **Status Register**: Maintains condition codes (LGT, AGT, EQ) and interrupt masks

This architecture was designed for real-time embedded systems in the 1970s-80s, enabling rapid task switching without the overhead of saving/restoring register banks.

## License

See LICENSE file for details.

## Contributing

Contributions are welcome! Please ensure all tests pass before submitting pull requests:

```bash
dotnet test
```

## References

- TMS9900 Microprocessor Data Manual (Texas Instruments)
- [Usagi Electric](https://usagi-electric.com/) - Minicomputer project

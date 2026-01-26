# Tawny Petrel Memory Guide

## Overview

Tawny Petrel models the TMS9900's 64KB address space with big-endian word access. Memory is byte-addressed, but the CPU treats words as the primary unit. The workspace pointer (WP) makes memory the register file, so understanding layout and endianness is essential.

## Memory Model

- **Address space**: 0x0000 to 0xFFFF (64KB)
- **Byte-addressable**: every address refers to a byte
- **Word operations**: read/write 16-bit words using big-endian order
- **No alignment enforcement**: word access does not require even addresses, but odd addresses can cause confusing results

## Endianness

The TMS9900 uses big-endian words. A word stored at address `0x2000` looks like:

```
Address  Value
0x2000   high byte
0x2001   low byte
```

In code, `WriteWord(0x2000, 0x1234)` stores `0x12` at `0x2000` and `0x34` at `0x2001`.

## Word and Byte Access

### Word Access

- `ReadWord` and `WriteWord` require a valid two-byte range.
- Addresses at or above `0xFFFF` are rejected because `address + 1` would exceed memory.

### Byte Access

- `ReadByte` and `WriteByte` accept the full 0x0000-0xFFFF range.
- Bounds checks are implicit because the address type is `ushort`.

## Workspace Pointer Registers

The CPU registers live in memory. WP points to a 16-word register block:

- R0 = `WP + 0`
- R1 = `WP + 2`
- ...
- R15 = `WP + 30`

This enables fast context switching by changing WP rather than copying register values.

## Reset Vectors

On reset, the CPU reads initial values from low memory:

- `0x0000` -> initial WP
- `0x0002` -> initial PC

A typical setup before `Reset()` is:

```
memory.WriteWord(0x0000, 0x2000); // WP
memory.WriteWord(0x0002, 0x0100); // PC
```

## Loading Programs

You can load a byte array into memory directly using `LoadProgram`:

```csharp
var memory = new Tms9900Memory();
byte[] program = {
    0x02, 0x00, 0x00, 0x05, // LI R0, 0x0005
    0x0C, 0x03              // STWP R3
};

memory.LoadProgram(0x0100, program);
```

`LoadProgram` rejects null buffers and any program that would exceed `0xFFFF`.

## Monitor Memory Inspection

The interactive monitor supports a simple dump command:

- `x <addr>`: shows 16 words starting at `addr` (hexadecimal input)

Example:
```
> x 0100
Memory at 0x0100:
  0x0100: 000200 170001 170002 170003 170004 170005 170006 170007
  0x0110: ...
```

## Safety Checks

The memory subsystem enforces:

- Word access bounds (`ReadWord`, `WriteWord`)
- Program load bounds (`LoadProgram`)
- Dump range bounds (`GetMemoryDump`)

These checks prevent accidental overflows and out-of-range reads.

## Tips and Best Practices

- Use even addresses for word access to match TMS9900 conventions.
- Always set reset vectors before calling `cpu.Reset()`.
- Use `x <addr> [size]` to verify program bytes after loading.

## Tawny Petrel Memory Map

Tawny Petrel Memory Map (64K words, addresses in hexadecimal):
0x0000-0x0003: Reset vectors (WP, PC)
0x0004-0x003F: Interrupt vectors (levels 0-14, 4 bytes each)
0x0040-0x007F: XOP vectors (XOP 0-15, 4 bytes each)
0x0080-0x009F: Monitor variables (16 words)
0x00A0-0x00AF: Monitor data stack (8 words)
0x00B0-0x00BF: Monitor workspace (16 words, R0-R15)
0x00C0-0xEFFF: General RAM (user code and data)
0xF000-0xFFFE: Memory-mapped I/O (reserved for peripherals)
MMIO devices:

0xF000 (>F000): UART status register (THRE bit 0)
0xF002 (>F002): UART data register

User code by convention starts at 0x0100.

## Additional Resources

- `tawny/Tms9900Memory.cs`
- `tawny/Tms9900Cpu.cs`
- `IMPLEMENTATION_SUMMARY.md`

---

**Version**: 1.0  
**Last Updated**: 2026-01-24  
**Project**: Tawny Petrel TMS9900 Emulator

# Tawny Petrel ISA Guide

## Overview

Tawny Petrel implements a working subset of the TMS9900 instruction set. The core features are in place: workspace pointer registers, status flags, context switching, and a wide range of arithmetic/logical operations. The current ISA focus is register-to-register behavior with simplified addressing.

## Instruction Formats (Current Implementation)

The decoder uses the classic TMS9900 format groups, but with reduced addressing support:

- **Format I (immediates and special ops)**: LI, AI, ANDI, ORI, CI, STWP, STST, RTWP, BLWP, CLR, etc.
- **Jumps**: 8-bit signed displacement, multiplied by 2
- **Single-operand**: arithmetic/logical/shift operations (register-only in practice)
- **Dual-operand**: COC and CZC (register-only)

Addressing mode bits are parsed but not fully implemented yet. Most operations assume register operands only.

## Status Register Flags

Supported flags and behavior:

- **LGT**: Logical greater-than (unsigned result > 0)
- **AGT**: Arithmetic greater-than (signed result > 0)
- **EQ**: Equal (result == 0)
- **C**: Carry
- **OV**: Overflow

The **OP** and **X** bits are defined but not fully updated in the current implementation.

## Implemented Instructions

### Immediate Operations

- `LI` - Load Immediate
- `AI` - Add Immediate
- `ANDI` - AND Immediate
- `ORI` - OR Immediate
- `CI` - Compare Immediate

### Data Movement

- `MOV` / `MOVB`
- `STWP` - Store Workspace Pointer
- `STST` - Store Status Register
- `SWPB` - Swap Bytes

### Arithmetic

- `A` / `AB`
- `S` / `SB`
- `DIV` - Unsigned divide (see below)
- `MPY` - Unsigned multiply (see below)
- `INC`
- `INCT`
- `DEC`
- `DECT`
- `NEG`
- `ABS`

### Logical

- `INV`
- `SZC` / `SZCB`
- `SOC` / `SOCB`
- `COC`
- `CZC`

### Shift / Rotate

- `SLA`
- `SRA`
- `SRC`
- `SRL`

### Jumps

- `JMP`
- `JEQ`
- `JNE`
- `JLT`
- `JLE`
- `JGT`
- `JHE`
- `JH`
- `JL`
- `JNC`
- `JOC`
- `JNO`
- `JOP` (decoded but not executed)

### Control

- `BL` - Branch and Link (stores return PC in R11)
- `B` - Branch to address
- `BLWP` - Branch and Link with Workspace Pointer
- `RTWP` - Return from Workspace Pointer
- `RT` - Return (alias for `B *R11`)
- `XOP` - Extended Operation

## Context Switching and XOP

The TMS9900 supports fast context switching through the workspace pointer:

- `BLWP` saves the old WP/PC/ST into R13-R15 of the new workspace.
- `RTWP` restores WP/PC/ST from R13-R15.
- `XOP` uses a vector table starting at `0x0040` with entries `(WP, PC)`.

Each XOP vector is 4 bytes:

```
0x0040 + (xop * 4) -> new WP
0x0042 + (xop * 4) -> new PC
```

## Byte Operations (Current Behavior)

Byte forms (`MOVB`, `AB`, `SB`, `SZCB`, `SOCB`, `CB`) are decoded, but the current implementation treats them like word operations. If you rely on byte semantics, expect incorrect results until byte masking is implemented.

## DIV (Unsigned Divide)

Encoding: `[001111][DDDD][TT][SSSS]`

- `DIV S, D` divides the unsigned 32-bit value `(WRD:WRD+1)` by unsigned `S`.
- If unsigned `S` is less than or equal to unsigned `WRD`, no operation occurs and `ST4` is set.
- On success, `WRD` receives the quotient and `WRD+1` receives the remainder.
- If `D == 15`, the remainder is written to the word in memory immediately after `WR15`.

## MPY (Unsigned Multiply)

Encoding: `[001110][DDDD][TT][SSSS]`

- `MPY S, D` multiplies unsigned `S` by unsigned `WRD`.
- The 32-bit product is stored in `WRD` (high word) and `WRD+1` (low word).
- If `D == 15`, the low word is written to the word in memory immediately after `WR15`.
- No status flags are modified.

## Example: Minimal Program

The demo program loaded by the monitor is a good reference:

```
0x0200 0005  ; LI R0, 0x0005
0x0201 0003  ; LI R1, 0x0003
0x0202 FFFF  ; LI R2, 0xFFFF
0x0C03       ; STWP R3
```

Load these words starting at `0x0100`, set WP/PC vectors, then step through execution to see register updates.

## Known Gaps

- Full addressing mode support is not implemented yet.
- Byte operations behave like word operations.
- `JOP` is decoded but does not change control flow.
- Parity flag (OP) is not set by the current ALU operations.

## Additional Resources

- `tawny/Tms9900Isa.cs`
- `tawny/Tms9900Cpu.cs`
- `IMPLEMENTATION_SUMMARY.md`

---

**Version**: 1.0  
**Last Updated**: 2026-01-24  
**Project**: Tawny Petrel TMS9900 Emulator

# Tawny Petrel Assembler Guide

## Overview

Tawny Petrel includes a simple assembler that targets the TMS9900 ISA and follows the Petrel monitor conventions. The assembler defaults to **hexadecimal** input with familiar radix prefixes and emits Motorola S-records for byte-accurate program loading.

## Running the Assembler

Use the `asm` subcommand when launching the `tawny` project:

```bash
dotnet run --project tawny -- asm <source> [-o <dest-folder>]
```

Examples:

```bash
dotnet run --project tawny -- asm sd/hellorld.asm

dotnet run --project tawny -- asm sd/hellorld.asm -o build
```

## Output Files

The assembler writes three files using the input basename:

- Listing: `<basename>.lst`
- Symbols: `<basename>.sym`
- S-record: `<basename>.srec`

By default, outputs are written next to the source file. Use `-o` to redirect them to a destination folder while keeping the same basenames.

Example:

```
Source:  sd/hellorld.asm
Output:  sd/hellorld.lst
         sd/hellorld.sym
         sd/hellorld.srec
```

With `-o build`:

```
Output:  build/hellorld.lst
         build/hellorld.sym
         build/hellorld.srec
```

## Number Syntax

The assembler follows the Petrel numeric conventions:

- **Hexadecimal (default)**: `C0`, `40`, `0`
- **Decimal**: `#64`
- **Octal**: `@300`, `@100`, `@0`

This matches the monitor behavior and keeps source code consistent across tools.

## S-record Format

The assembler emits Motorola S-records (`.srec`) containing **byte** data encoded in **hexadecimal**. This allows precise byte-level program loading while preserving the TMS9900's big-endian word representation in memory.

## Listing File

The listing (`.lst`) includes:

- Address
- Encoded words/bytes
- Source line
- Errors or warnings

Use the listing to verify instruction encoding and address placement.

## Symbol File

The symbol table (`.sym`) provides label-to-address mappings for debugging and cross-references.

## Supported Directives

- TXT /your message here/ ; produces one word per character
- ORG addr
- RORG addr ; relocatable?
- DW, DD, DQ ; Data word, double, quad -- big endian

## Tips

- Use explicit radix prefixes to avoid ambiguity.
- Prefer even addresses for word-aligned instructions.
- Keep S-records as the interchange format for loaders and tools.

---

**Version**: 1.0  
**Last Updated**: 2026-01-25  
**Project**: Tawny Petrel TMS9900 Emulator

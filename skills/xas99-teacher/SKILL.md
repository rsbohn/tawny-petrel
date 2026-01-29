---
name: xas99-teacher
description: Learn to assemble TMS9900 opcodes by running xas99 and observing the results.
---

# Xas99 Teacher

## Quick Start

- Prepare a minimal source file with the desired operation.
- Assemble with xas99 to generate a `.lst` file.
- Review the listing to extract opcode bits and operands.
- (Optional) Assemble with Tawny Petrel and compare encodings.

## Common Tasks

### Create a minimal source file

Use a consistent scaffolding so addresses are predictable.

```asm
    AORG >0000
    DATA WPA    ; WP
    DATA START  ; PC

    AORG >1000  ; workspaces
WPA BSS >20

    AORG >1800
START
    ; put instruction under test here
    IDLE
```

### Create a listing

- Listing file:

```bash
~/.local/bin/xas99 -L scratch/<name>.lst -o scratch/ <source>.asm
```

### Extract opcode fields from a listing

- Identify the instruction word (left of the mnemonic).
- Convert the word to binary and mark field boundaries.
- Compare the fields against the ISA encoding.

Example workflow (manual):
1) `DIV 5,3` encodes to `3CC5`
2) Binary: `0011 1100 1100 0101`
3) Fields: `001111` `DDDD` `TT` `SSSS`

### Compare with Tawny Petrel

Use the built-in assembler to compare encodings.

```bash
dotnet run --project tawny -- asm scratch/<name>.asm -o scratch
```

Then compare `scratch/<name>.lst` to the xas99 listing.

### Common pitfalls

- Missing/unknown mnemonics: the assembler may treat the mnemonic as a label.
- Operand order: TMS9900 is `OP src, dest`.
- Addressing modes: use register-only first, then add `@`, `*`, or indexed forms.
- Byte vs word forms: `SOC` vs `SOCB`, `A` vs `AB`.

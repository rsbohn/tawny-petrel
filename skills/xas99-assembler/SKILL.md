---
name: xas99-assembler
description: Assemble TMS9900/TI-99 assembly with ~/.local/bin/xas99. Use when you need to assemble .asm sources into object code, program images, binaries, or text exports, and when you need listings/symbol tables.
---

# Xas99 Assembler

## Quick Start (object code)

- Assemble a source to Editor/Assembler object code in `./scratch/`:

```bash
~/.local/bin/xas99 -o scratch/ <source>.asm
```

- Example using the repo sample:

```bash
~/.local/bin/xas99 -o scratch/ sd/hellorld.asm
```

## Common Tasks

### Create a listing or symbol table

- Listing file:

```bash
~/.local/bin/xas99 -L scratch/<name>.lst -o scratch/ <source>.asm
```

- Listing with symbol table appended:

```bash
~/.local/bin/xas99 -L scratch/<name>.lst -S -o scratch/ <source>.asm
```

### Set output name or directory

- `-o` accepts a file path or a target directory.
- Keep outputs under `./scratch/` unless the user says otherwise.

### Create program images or raw binaries

```bash
~/.local/bin/xas99 -i -o scratch/ <source>.asm    # E/A option 5 image
~/.local/bin/xas99 -b -o scratch/ <source>.asm    # raw binary
```

### Export text representations

```bash
~/.local/bin/xas99 -t a2 -o scratch/ <source>.asm # assembly DATA/BYTE text
~/.local/bin/xas99 -t c2 -o scratch/ <source>.asm # C-style byte list
```

## Notes

- Run `~/.local/bin/xas99 --help` for supported targets and syntax modes.
- Use `-R` when sources use `R`-prefixed registers (e.g., `R0`).
- `xas99` does not emit Motorola SREC; use the Tawny assembler when SREC is required.

## Workaround: xas99 to SREC (two-step)

If you must start with xas99, generate a raw binary and convert it with GNU `objcopy`:

```bash
~/.local/bin/xas99 -b -R -o scratch/ awesome.asm
objcopy -I binary -O srec scratch/awesome.bin scratch/awesome.srec
```

## Procedure: Compare Tawny vs xas99 output (ignore comments)

Use this when aligning Tawny’s assembler to xas99.

1) Build xas99 listing:

```bash
~/.local/bin/xas99 -R -L scratch/<name>.xas99.lst -o scratch/ <source>.asm
```

2) Build Tawny listing:

```bash
dotnet run --project tawny -- asm <source>.asm -o scratch
```

3) Compare emitted words (ignore comments/listing formatting). Use this script in the repo root:

```bash
python3 - <<'PY'
import re
from pathlib import Path

root = Path('.')
tawny_path = root / 'scratch' / '<name>.lst'
xas_path = root / 'scratch' / '<name>.xas99.lst'

def parse_tawny(path):
    addr_to_word = {}
    for line in path.read_text().splitlines():
        m = re.match(r'^([0-9A-F]{4})\\s+(.*)$', line)
        if not m:
            continue
        addr = int(m.group(1), 16)
        rest = m.group(2)
        tokens = rest.split()
        words = []
        for tok in tokens:
            if re.fullmatch(r'[0-9A-F]{4}', tok):
                words.append(int(tok, 16))
            else:
                break
        if not words:
            continue
        for w in words:
            addr_to_word[addr] = w
            addr += 2
    return addr_to_word

def parse_xas(path):
    addr_to_word = {}
    for line in path.read_text().splitlines():
        tokens = line.split()
        if not tokens:
            continue
        starts_with_digit = line[0].isdigit()
        if starts_with_digit:
            if len(tokens) >= 3 and re.fullmatch(r'[0-9A-F]{4}', tokens[1]) and re.fullmatch(r'[0-9A-F]{4}r?', tokens[2]):
                addr = int(tokens[1], 16)
                word = int(tokens[2][:4], 16)
                addr_to_word[addr] = word
        else:
            if len(tokens) >= 2 and re.fullmatch(r'[0-9A-F]{4}', tokens[0]) and re.fullmatch(r'[0-9A-F]{4}r?', tokens[1]):
                addr = int(tokens[0], 16)
                word = int(tokens[1][:4], 16)
                addr_to_word[addr] = word
    return addr_to_word

tawny = parse_tawny(tawny_path)
xas = parse_xas(xas_path)

all_addrs = sorted(set(tawny) | set(xas))
print("Addr  Tawny  xas99")
print("----  -----  -----")
for addr in all_addrs:
    t = tawny.get(addr)
    x = xas.get(addr)
    if t != x:
        t_str = f"{t:04X}" if t is not None else "----"
        x_str = f"{x:04X}" if x is not None else "----"
        print(f"{addr:04X}  {t_str}  {x_str}")
PY
```

#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$root_dir"

nix shell nixpkgs#dotnet-sdk_9 -c dotnet run --project tawny -- asm sd/dullboy.asm -o build

printf "load build/dullboy.srec\nboot\nc 100\nq\n" | \
  nix shell nixpkgs#dotnet-sdk_9 -c dotnet run --project tawny

.PHONY: clean

clean:
	rm -f scratch/*
	rm -f build/*

build/adder.srec: sd/adder.asm
	mkdir -p build
	dotnet run --project tawny -- asm sd/adder.asm -o build

scratch/adder.srec: sd/adder.asm
	mkdir -p scratch
	~/.local/bin/xas99 -b -R -o scratch/ -L scratch/adder.lst sd/adder.asm
	objcopy -I binary -O srec scratch/adder.bin scratch/adder.srec

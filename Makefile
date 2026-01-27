.PHONY: clean

clean:
	rm -f scratch/*

scratch/adder.srec: sd/adder.asm
	mkdir -p scratch
	~/.local/bin/xas99 -b -R -o scratch/ sd/adder.asm
	objcopy -I binary -O srec scratch/adder.bin scratch/adder.srec

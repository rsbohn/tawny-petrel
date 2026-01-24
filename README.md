# tawny-petrel
TMS9900 minicomputer emulator

**Tawny Petrel** is a TMS9900 simulator designed to provide source-level compatibility with the Usagi Electric minicomputer project while offering an immediate, interactive development environment. The simulator faithfully implements the TMS9900 instruction set including the workspace pointer architecture, extended operations (XOP), and memory mapper, allowing developers to write and test assembly code that will run on actual TMS9900 hardware. Rather than replicating the complete TI 990 minicomputer environment, Tawny Petrel focuses on accurate instruction-level simulation of the processor itself.

Following the design philosophy of the Petrel family (Ashen Petrel for HP 3000, Dusky Petrel for DG Nova, and Olive Petrel for PDP-8), Tawny Petrel features a Forth-inspired monitor system that emphasizes direct interaction with the machine. The monitor provides immediate access to memory examination and modification, register inspection, single-stepping, and program execution control. This approach prioritizes programmer productivity and learning over strict historical accuracy, making the TMS9900 architecture accessible and enjoyable to explore.

The simulator is built as a development and experimentation platform for the TMS9900 architecture, supporting the full instruction set including the distinctive workspace pointer mechanism that uses memory locations as registers, the XOP extended operation system for software-implemented instructions and system calls, and memory mapping capabilities. Tawny Petrel aims to be both a practical tool for Usagi Electric community members to develop and test code, and an educational resource for understanding this historically significant 16-bit processor architecture.


[Usagi on Github](https://github.com/Nakazoto/TMS9900-Homebrew)
using System.Threading;

namespace tawny;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "asm", StringComparison.OrdinalIgnoreCase))
        {
            RunAssembler(args);
            return;
        }

        Console.WriteLine("===========================================");
        Console.WriteLine("Tawny Petrel - TMS9900 Simulator");
        Console.WriteLine("===========================================");
        Console.WriteLine();

        // Initialize the simulator
        var memory = new Tms9900Memory();
        var cpu = new Tms9900Cpu(memory);
        InitializeMonitorStack(memory);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cpu.Stop();
            Console.WriteLine();
            Console.WriteLine("Execution stopped.");
        };

        // Interactive mode
        Console.WriteLine("===========================================");
        Console.WriteLine("Interactive Mode");
        Console.WriteLine("===========================================");
        Console.WriteLine("Commands:");
        Console.WriteLine("  s          - Execute single step");
        Console.WriteLine("  r          - Reset CPU");
        Console.WriteLine("  x <addr> [n]   - Examine memory at address (hex)");
        Console.WriteLine("  exam <addr> - Alias for x");
        Console.WriteLine("  d <addr> <val...> - Deposit words into memory (hex)");
        Console.WriteLine("  dep <addr> <val...> - Alias for d");
        Console.WriteLine("  demo       - Run the demo program");
        Console.WriteLine("  regs       - Show all registers");
        Console.WriteLine("  . dup drop swap over + - and or xor invert @ !");
        Console.WriteLine("  load <file> - Load SREC file into memory");
        Console.WriteLine("  boot [file] - Load SREC (optional) and set WP/PC from 0000-0003");
        Console.WriteLine("  dis <addr> [count] - Disassemble from address (hex)");
        Console.WriteLine("  trace [n]  - Trace execution for n steps (hex)");
        Console.WriteLine("  c [n]      - Continue execution (optional step count, hex)");
        Console.WriteLine("  help       - Show this help");
        Console.WriteLine("  q          - Quit");
        Console.WriteLine("  numeric literals: hex default, % for octal, # for decimal");
        Console.WriteLine();

        bool running = true;
        while (running)
        {
            Console.Write("> ");
            string? input = Console.ReadLine();
            if (input == null)
            {
                Console.WriteLine();
                break;
            }
            if (string.IsNullOrWhiteSpace(input)) continue;

            string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string command = parts[0].ToLower();

            switch (command)
            {
                case "s":
                case "step":
                    if (!cpu.IsRunning)
                    {
                        cpu.Start();
                    }
                    cpu.Step();
                    Console.WriteLine(FormatState(cpu));
                    break;

                case "r":
                case "reset":
                    cpu.Reset();
                    cpu.Start();
                    Console.WriteLine("CPU reset.");
                    Console.WriteLine(cpu.GetState());
                    break;

                case "x":
                case "exam":
                case "dump":
                    if (parts.Length > 1)
                    {
                        if (TryParseLiteral(parts[1], out ushort addr))
                        {
                            addr = (ushort)(addr & 0xFFFE);
                            int countWords = 0x10;
                            if (parts.Length > 2)
                            {
                                if (!TryParseLiteral(parts[2], out ushort parsedCount))
                                {
                                    Console.WriteLine($"Invalid count: {parts[2]}");
                                    break;
                                }
                                countWords = parsedCount;
                            }

                            Console.WriteLine($"Memory at {FormatHex(addr)}:");
                            for (int offset = 0; offset < countWords; offset += 8)
                            {
                                ushort lineAddr = (ushort)(addr + (offset * 2));
                                var lineText = new System.Text.StringBuilder();
                                lineText.Append($"  {FormatHex(lineAddr)}:");
                                int wordsInLine = Math.Min(8, countWords - offset);
                                for (int i = 0; i < wordsInLine; i++)
                                {
                                    ushort word = memory.ReadWord((ushort)(lineAddr + i * 2));
                                    lineText.Append($" {FormatHex(word)}");
                                }
                                Console.WriteLine(lineText.ToString());
                            }
                        }
                        else
                        {
                            Console.WriteLine("Invalid address. Use hex (e.g., C000), % for octal, # for decimal.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Usage: x <address>");
                    }
                    break;

                case "d":
                case "dep":
                case "deposit":
                    if (parts.Length > 2)
                    {
                        if (TryParseLiteral(parts[1], out ushort addr))
                        {
                            addr = (ushort)(addr & 0xFFFE);
                            ushort writeAddr = addr;
                            int written = 0;
                            for (int i = 2; i < parts.Length; i++)
                            {
                                if (!TryParseLiteral(parts[i], out ushort value))
                                {
                                    Console.WriteLine($"Invalid value: {parts[i]}");
                                    written = 0;
                                    break;
                                }
                                memory.WriteWord(writeAddr, value);
                                writeAddr += 2;
                                written++;
                            }

                            if (written > 0)
                            {
                                Console.WriteLine($"Wrote {written} word(s) starting at {FormatHex(addr)}.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Invalid address. Use hex (e.g., C000), % for octal, # for decimal.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Usage: d <address> <value...>");
                    }
                    break;

                case ".":
                case "dup":
                case "drop":
                case "swap":
                case "over":
                case "+":
                case "-":
                case "and":
                case "or":
                case "xor":
                case "invert":
                case "@":
                case "!":
                    if (!ExecuteStackExpression(parts, memory))
                    {
                        Console.WriteLine("Stack error.");
                    }
                    break;

                case "demo":
                    RunDemo(memory, cpu);
                    break;

                case "load":
                    if (parts.Length < 2)
                    {
                        Console.WriteLine("Usage: load <filename>");
                        break;
                    }
                    TryLoadSrec(parts[1], memory);
                    break;

                case "c":
                    using (var listener = new UartKeyboardListener(memory, cpu, 8))
                    {
                        if (parts.Length > 1)
                        {
                            if (!TryParseLiteral(parts[1], out ushort count))
                            {
                                Console.WriteLine($"Invalid count: {parts[1]}");
                                break;
                            }
                            cpu.Start();
                            for (int i = 0; i < count; i++)
                            {
                                if (!cpu.IsRunning) break;
                                cpu.Step();
                            }
                            Console.WriteLine(FormatState(cpu));
                        }
                        else
                        {
                            cpu.Start();
                            cpu.Run();
                            Console.WriteLine(FormatState(cpu));
                        }
                    }
                    break;

                case "boot":
                    if (parts.Length > 1)
                    {
                        if (!TryLoadSrec(parts[1], memory))
                        {
                            break;
                        }
                    }
                    BootFromVectors(cpu, memory);
                    break;

                case "dis":
                    if (parts.Length < 2)
                    {
                        Console.WriteLine("Usage: dis <address> [count]");
                        break;
                    }
                    if (!TryParseLiteral(parts[1], out ushort disAddr))
                    {
                        Console.WriteLine($"Invalid address: {parts[1]}");
                        break;
                    }
                    int disCount = 1;
                    if (parts.Length > 2)
                    {
                        if (!TryParseLiteral(parts[2], out ushort parsedCount))
                        {
                            Console.WriteLine($"Invalid count: {parts[2]}");
                            break;
                        }
                        disCount = parsedCount;
                    }
                    Disassemble(memory, (ushort)(disAddr & 0xFFFE), disCount);
                    break;

                case "trace":
                case "t":
                    int traceCount = 1;
                    if (parts.Length > 1)
                    {
                        if (!TryParseLiteral(parts[1], out ushort parsedCount))
                        {
                            Console.WriteLine($"Invalid count: {parts[1]}");
                            break;
                        }
                        traceCount = parsedCount;
                    }
                    if (!cpu.IsRunning)
                    {
                        cpu.Start();
                    }
                    for (int i = 0; i < traceCount; i++)
                    {
                        if (!cpu.IsRunning) break;
                        ushort pc = cpu.ProgramCounter;
                        ushort instruction = memory.ReadWord(pc);
                        var dis = DisassembleInstruction(memory, pc, instruction);
                        cpu.Step();
                        Console.WriteLine($"{FormatHex(pc)}: {FormatHex(instruction)} {dis.Text} -> {FormatState(cpu)}");
                    }
                    break;

                case "help":
                    PrintHelp();
                    break;

                case "regs":
                case "registers":
                    Console.WriteLine($"PC: {FormatHex(cpu.ProgramCounter)}");
                    Console.WriteLine($"WP: {FormatHex(cpu.WorkspacePointer)}");
                    Console.WriteLine($"ST: {FormatHex(cpu.StatusRegister)}");
                    Console.WriteLine(FormatRegisterLine(cpu, 0, 8));
                    Console.WriteLine(FormatRegisterLine(cpu, 8, 16));
                    break;

                case "q":
                case "quit":
                case "exit":
                    running = false;
                    break;

                default:
                    if (!ExecuteStackExpression(parts, memory))
                    {
                        Console.WriteLine("Unknown command. Type 'q' to quit.");
                    }
                    break;
            }
        }

        Console.WriteLine();
        Console.WriteLine("Goodbye!");
    }

    static void PrintHelp()
    {
        Console.WriteLine("Commands:");
        Console.WriteLine("  s          - Execute single step");
        Console.WriteLine("  r          - Reset CPU");
        Console.WriteLine("  x <addr> [n]   - Examine memory at address (hex)");
        Console.WriteLine("  exam <addr> - Alias for x");
        Console.WriteLine("  d <addr> <val...> - Deposit words into memory (hex)");
        Console.WriteLine("  dep <addr> <val...> - Alias for d");
        Console.WriteLine("  demo       - Run the demo program");
        Console.WriteLine("  regs       - Show all registers");
        Console.WriteLine("  . dup drop swap over + - and or xor invert @ !");
        Console.WriteLine("  load <file> - Load SREC file into memory");
        Console.WriteLine("  boot [file] - Load SREC (optional) and set WP/PC from 0000-0003");
        Console.WriteLine("  dis <addr> [count] - Disassemble from address (hex)");
        Console.WriteLine("  trace [n]  - Trace execution for n steps (hex)");
        Console.WriteLine("  c [n]      - Continue execution (optional step count, hex)");
        Console.WriteLine("  help       - Show this help");
        Console.WriteLine("  q          - Quit");
        Console.WriteLine("  numeric literals: hex default, % for octal, # for decimal");
    }

    static bool TryLoadSrec(string path, Tms9900Memory memory)
    {
        try
        {
            IReadOnlyDictionary<ushort, byte> bytes = SRecordReader.ReadFile(path);
            foreach (var pair in bytes)
            {
                memory.WriteByte(pair.Key, pair.Value);
            }
            Console.WriteLine($"Loaded {bytes.Count} byte(s) from {path}.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
    }

    static void BootFromVectors(Tms9900Cpu cpu, Tms9900Memory memory)
    {
        cpu.SetWorkspacePointer(memory.ReadWord(0x0000));
        cpu.SetProgramCounter(memory.ReadWord(0x0002));
        cpu.Stop();
        Console.WriteLine("Boot vectors loaded.");
        Console.WriteLine(FormatState(cpu));
    }

    static void Disassemble(Tms9900Memory memory, ushort address, int count)
    {
        ushort pc = address;
        for (int i = 0; i < count; i++)
        {
            ushort instruction = memory.ReadWord(pc);
            var result = DisassembleInstruction(memory, pc, instruction);
            Console.WriteLine($"{FormatHex(pc)}: {FormatHex(instruction)} {result.Text}");
            pc = (ushort)(pc + (result.Words * 2));
        }
    }

    static DisasmResult DisassembleInstruction(Tms9900Memory memory, ushort pc, ushort instruction)
    {
        int opcodeNibble = instruction >> 12;
        if (opcodeNibble == 0xC)
        {
            return DisassembleFormat2(memory, pc, instruction, "MOV", false);
        }
        if (opcodeNibble == 0xD)
        {
            return DisassembleFormat2(memory, pc, instruction, "MOVB", true);
        }
        if (opcodeNibble == 0xA)
        {
            return DisassembleFormat2(memory, pc, instruction, "A", false);
        }
        if (opcodeNibble == 0xB)
        {
            return DisassembleFormat2(memory, pc, instruction, "AB", true);
        }
        if (opcodeNibble == 0x1)
        {
            return DisassembleJump(pc, instruction);
        }
        if (opcodeNibble == 0x0)
        {
            return DisassembleFormat1(memory, pc, instruction);
        }

        int op6 = (instruction >> 10) & 0x3F;
        if (TryGetRegDestMnemonic(op6, out string mnemonic))
        {
            return DisassembleRegDest(memory, pc, instruction, mnemonic);
        }
        if (TryGetShiftMnemonic(op6, out string shiftMnemonic))
        {
            int reg = (instruction >> 6) & 0xF;
            int count = instruction & 0xF;
            return new DisasmResult($"{shiftMnemonic} {FormatRegister(reg)}, {FormatHex((ushort)count)}", 1);
        }

        return new DisasmResult("DATA", 1);
    }

    static DisasmResult DisassembleFormat1(Tms9900Memory memory, ushort pc, ushort instruction)
    {
        if ((instruction & 0xFFF0) == 0x0200)
        {
            return DisassembleImmediate(memory, pc, instruction, "LI");
        }
        if ((instruction & 0xFFF0) == 0x0220)
        {
            return DisassembleImmediate(memory, pc, instruction, "AI");
        }
        if ((instruction & 0xFFF0) == 0x0240)
        {
            return DisassembleImmediate(memory, pc, instruction, "ANDI");
        }
        if ((instruction & 0xFFF0) == 0x0260)
        {
            return DisassembleImmediate(memory, pc, instruction, "ORI");
        }
        if ((instruction & 0xFFF0) == 0x0280)
        {
            return DisassembleImmediate(memory, pc, instruction, "CI");
        }
        if ((instruction & 0xFFF0) == 0x0300)
        {
            ushort immediate = memory.ReadWord((ushort)(pc + 2));
            return new DisasmResult($"LIMI {FormatHex(immediate)}", 2);
        }
        if ((instruction & 0xFFC0) == 0x0340)
        {
            return new DisasmResult("IDLE", 1);
        }
        if ((instruction & 0xFFC0) == 0x0380)
        {
            return new DisasmResult("RTWP", 1);
        }
        if ((instruction & 0xFFC0) == 0x0400)
        {
            return DisassembleSingleOperand(memory, pc, instruction, "BLWP");
        }
        if ((instruction & 0xFFC0) == 0x0440)
        {
            if (instruction == 0x045B)
            {
                return new DisasmResult("RT", 1);
            }
            return DisassembleSingleOperand(memory, pc, instruction, "B");
        }
        if ((instruction & 0xFFC0) == 0x04C0)
        {
            return DisassembleSingleOperand(memory, pc, instruction, "CLR");
        }
        if ((instruction & 0xFFC0) == 0x0680)
        {
            return DisassembleSingleOperand(memory, pc, instruction, "BL");
        }
        if ((instruction & 0xFFC0) == 0x0500)
        {
            return DisassembleSingleOperand(memory, pc, instruction, "NEG");
        }
        if ((instruction & 0xFFC0) == 0x0540)
        {
            return DisassembleSingleOperand(memory, pc, instruction, "INV");
        }
        if ((instruction & 0xFFC0) == 0x0580)
        {
            return DisassembleSingleOperand(memory, pc, instruction, "INC");
        }
        if ((instruction & 0xFFC0) == 0x05C0)
        {
            return DisassembleSingleOperand(memory, pc, instruction, "INCT");
        }
        if ((instruction & 0xFFC0) == 0x0600)
        {
            return DisassembleSingleOperand(memory, pc, instruction, "DEC");
        }
        if ((instruction & 0xFFC0) == 0x0640)
        {
            return DisassembleSingleOperand(memory, pc, instruction, "DECT");
        }
        if ((instruction & 0xFFC0) == 0x06C0)
        {
            return DisassembleSingleOperand(memory, pc, instruction, "SWPB");
        }
        if ((instruction & 0xFFC0) == 0x0700)
        {
            return DisassembleSingleOperand(memory, pc, instruction, "SETO");
        }
        if ((instruction & 0xFFC0) == 0x0740)
        {
            return DisassembleSingleOperand(memory, pc, instruction, "ABS");
        }
        if ((instruction & 0xFFF0) == 0x0C00)
        {
            return new DisasmResult($"STWP {FormatRegister(instruction & 0xF)}", 1);
        }
        if ((instruction & 0xFFF0) == 0x0E00)
        {
            return new DisasmResult($"STST {FormatRegister(instruction & 0xF)}", 1);
        }
        if ((instruction & 0xFC00) == 0x2C00)
        {
            int xop = (instruction >> 6) & 0xF;
            int reg = instruction & 0xF;
            return new DisasmResult($"XOP {FormatRegister(reg)}, {FormatHex((ushort)xop)}", 1);
        }

        return new DisasmResult("DATA", 1);
    }

    static DisasmResult DisassembleImmediate(Tms9900Memory memory, ushort pc, ushort instruction, string mnemonic)
    {
        int reg = instruction & 0xF;
        ushort immediate = memory.ReadWord((ushort)(pc + 2));
        return new DisasmResult($"{mnemonic} {FormatRegister(reg)}, {FormatHex(immediate)}", 2);
    }

    static DisasmResult DisassembleJump(ushort pc, ushort instruction)
    {
        int opcode = (instruction >> 8) & 0xFF;
        if (!JumpMnemonic(opcode, out string mnemonic))
        {
            return new DisasmResult("DATA", 1);
        }
        sbyte displacement = unchecked((sbyte)(instruction & 0xFF));
        ushort target = (ushort)(pc + 2 + (displacement * 2));
        return new DisasmResult($"{mnemonic} {FormatHex(target)}", 1);
    }

    static bool JumpMnemonic(int opcode, out string mnemonic)
    {
        switch (opcode)
        {
            case 0x10: mnemonic = "JMP"; return true;
            case 0x11: mnemonic = "JLT"; return true;
            case 0x12: mnemonic = "JLE"; return true;
            case 0x13: mnemonic = "JEQ"; return true;
            case 0x14: mnemonic = "JHE"; return true;
            case 0x15: mnemonic = "JGT"; return true;
            case 0x16: mnemonic = "JNE"; return true;
            case 0x17: mnemonic = "JNC"; return true;
            case 0x18: mnemonic = "JOC"; return true;
            case 0x19: mnemonic = "JNO"; return true;
            case 0x1A: mnemonic = "JL"; return true;
            case 0x1B: mnemonic = "JH"; return true;
            case 0x1C: mnemonic = "JOP"; return true;
            default:
                mnemonic = string.Empty;
                return false;
        }
    }

    static bool TryGetRegDestMnemonic(int op6, out string mnemonic)
    {
        switch (op6)
        {
            case 0x04: mnemonic = "SZC"; return true;
            case 0x05: mnemonic = "SZCB"; return true;
            case 0x06: mnemonic = "S"; return true;
            case 0x07: mnemonic = "SB"; return true;
            case 0x08: mnemonic = "C"; return true;
            case 0x09: mnemonic = "CB"; return true;
            case 0x0A: mnemonic = "A"; return true;
            case 0x0B: mnemonic = "AB"; return true;
            case 0x0E: mnemonic = "SOC"; return true;
            case 0x0F: mnemonic = "SOCB"; return true;
            default:
                mnemonic = string.Empty;
                return false;
        }
    }

    static bool TryGetShiftMnemonic(int op6, out string mnemonic)
    {
        switch (op6)
        {
            case 0x10: mnemonic = "SLA"; return true;
            case 0x11: mnemonic = "SRA"; return true;
            case 0x12: mnemonic = "SRC"; return true;
            case 0x13: mnemonic = "SRL"; return true;
            default:
                mnemonic = string.Empty;
                return false;
        }
    }

    static DisasmResult DisassembleFormat2(Tms9900Memory memory, ushort pc, ushort instruction, string mnemonic, bool isByte)
    {
        int td = (instruction >> 10) & 0x3;
        int d = (instruction >> 6) & 0xF;
        int ts = (instruction >> 4) & 0x3;
        int s = instruction & 0xF;

        int wordsUsed = 1;
        ushort nextWord = (ushort)(pc + 2);
        string src = FormatOperand(memory, ref nextWord, ts, s, ref wordsUsed);
        string dest = FormatOperand(memory, ref nextWord, td, d, ref wordsUsed);

        return new DisasmResult($"{mnemonic} {src}, {dest}", wordsUsed);
    }

    static DisasmResult DisassembleRegDest(Tms9900Memory memory, ushort pc, ushort instruction, string mnemonic)
    {
        int dest = (instruction >> 6) & 0xF;
        int ts = (instruction >> 4) & 0x3;
        int s = instruction & 0xF;

        int wordsUsed = 1;
        ushort nextWord = (ushort)(pc + 2);
        string src = FormatOperand(memory, ref nextWord, ts, s, ref wordsUsed);

        return new DisasmResult($"{mnemonic} {src}, {FormatRegister(dest)}", wordsUsed);
    }

    static DisasmResult DisassembleSingleOperand(Tms9900Memory memory, ushort pc, ushort instruction, string mnemonic)
    {
        int mode = (instruction >> 4) & 0x3;
        int reg = instruction & 0xF;
        int wordsUsed = 1;
        ushort nextWord = (ushort)(pc + 2);
        string operand = FormatOperand(memory, ref nextWord, mode, reg, ref wordsUsed);
        return new DisasmResult($"{mnemonic} {operand}", wordsUsed);
    }

    static string FormatOperand(Tms9900Memory memory, ref ushort nextWordAddr, int mode, int reg, ref int wordsUsed)
    {
        switch (mode)
        {
            case 0:
                return FormatRegister(reg);
            case 1:
                return $"*{FormatRegister(reg)}";
            case 2:
            {
                ushort displacement = memory.ReadWord(nextWordAddr);
                nextWordAddr += 2;
                wordsUsed++;
                if (reg == 0)
                {
                    return $"@{FormatHex(displacement)}";
                }
                return $"@{FormatHex(displacement)}({FormatRegister(reg)})";
            }
            case 3:
                return $"*{FormatRegister(reg)}+";
            default:
                return "??";
        }
    }

    static string FormatRegister(int register)
    {
        return register.ToString();
    }

    private readonly record struct DisasmResult(string Text, int Words);

    static void RunAssembler(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet run --project tawny -- asm <source> [-o <dest-folder>]");
            Environment.Exit(1);
        }

        string sourcePath = args[1];
        string? outputDir = null;

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "-o" && i + 1 < args.Length)
            {
                outputDir = args[i + 1];
                i++;
            }
        }

        try
        {
            var assembler = new Assembler();
            AssemblerResult result = assembler.Assemble(sourcePath, outputDir);
            Console.WriteLine($"Wrote {result.Bytes.Count} byte(s).");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Environment.Exit(1);
        }
    }

    static void RunDemo(Tms9900Memory memory, Tms9900Cpu cpu)
    {
        // Set up initial workspace pointer and program counter
        memory.WriteWord(0x0000, 0x2000); // WP = 0x2000
        memory.WriteWord(0x0002, 0x0100); // PC = 0x0100

        Console.WriteLine("Demo: Simple TMS9900 program");
        Console.WriteLine("-----------------------------");

        // Create a simple test program focusing on instructions that work
        // LI R0, 0x0005    ; Load immediate 5 into R0
        // LI R1, 0x0003    ; Load immediate 3 into R1
        // LI R2, 0xFFFF    ; Load immediate 0xFFFF into R2
        // STWP R3          ; Store workspace pointer to R3

        ushort[] program = new ushort[]
        {
            0x0200, 0x0005,  // LI R0, 0x0005
            0x0201, 0x0003,  // LI R1, 0x0003
            0x0202, 0xFFFF,  // LI R2, 0xFFFF
            0x0C03,          // STWP R3
        };

        // Load program into memory
        ushort address = 0x0100;
        foreach (var instruction in program)
        {
            memory.WriteWord(address, instruction);
            address += 2;
        }

        // Reset CPU to load WP and PC
        cpu.Reset();

        Console.WriteLine($"Initial state: {FormatState(cpu)}");
        Console.WriteLine();

        // Execute the program
        cpu.Start();

        Console.WriteLine("Executing instructions:");
        for (int i = 0; i < 4; i++)
        {
            Console.WriteLine($"Step {i + 1}: {FormatState(cpu)}");
            cpu.Step();
        }

        Console.WriteLine();
        Console.WriteLine($"Final state: {FormatState(cpu)}");
        Console.WriteLine();
        Console.WriteLine("Expected results:");
        Console.WriteLine("  R0 = 0005");
        Console.WriteLine("  R1 = 0003");
        Console.WriteLine("  R2 = FFFF");
        Console.WriteLine("  R3 = 2000 (workspace pointer)");
        Console.WriteLine();

        // Verify results
        ushort r0 = cpu.ReadRegister(0);
        ushort r1 = cpu.ReadRegister(1);
        ushort r2 = cpu.ReadRegister(2);
        ushort r3 = cpu.ReadRegister(3);

        Console.WriteLine("Actual results:");
        Console.WriteLine($"  R0 = {FormatHex(r0)}");
        Console.WriteLine($"  R1 = {FormatHex(r1)}");
        Console.WriteLine($"  R2 = {FormatHex(r2)}");
        Console.WriteLine($"  R3 = {FormatHex(r3)}");
        Console.WriteLine();

        bool success = (r0 == 0x0005 && r1 == 0x0003 && r2 == 0xFFFF && r3 == 0x2000);
        Console.WriteLine(success ? "✓ Test passed!" : "✗ Test failed!");
        Console.WriteLine();
    }

    static void InitializeMonitorStack(Tms9900Memory memory)
    {
        memory.WriteWord(0x0200, 0x0240); // data stack base
        memory.WriteWord(0x0202, 0x0000); // data stack offset (0-7)
    }

    static bool ExecuteStackExpression(string[] parts, Tms9900Memory memory)
    {
        foreach (string token in parts)
        {
            if (TryParseLiteral(token, out ushort literal))
            {
                if (!PushStack(memory, literal)) return false;
                continue;
            }

            switch (token.ToLower())
            {
                case ".":
                    if (!PopStack(memory, out ushort value)) return false;
                    Console.WriteLine(FormatHex(value));
                    break;
                case "dup":
                    if (!PeekStack(memory, out ushort top)) return false;
                    if (!PushStack(memory, top)) return false;
                    break;
                case "drop":
                    if (!PopStack(memory, out _)) return false;
                    break;
                case "swap":
                    if (!PopStack(memory, out ushort a)) return false;
                    if (!PopStack(memory, out ushort b)) return false;
                    if (!PushStack(memory, a)) return false;
                    if (!PushStack(memory, b)) return false;
                    break;
                case "over":
                    if (!PopStack(memory, out ushort first)) return false;
                    if (!PopStack(memory, out ushort second)) return false;
                    if (!PushStack(memory, second)) return false;
                    if (!PushStack(memory, first)) return false;
                    if (!PushStack(memory, second)) return false;
                    break;
                case "+":
                    if (!PopStack(memory, out ushort addB)) return false;
                    if (!PopStack(memory, out ushort addA)) return false;
                    if (!PushStack(memory, (ushort)(addA + addB))) return false;
                    break;
                case "-":
                    if (!PopStack(memory, out ushort subB)) return false;
                    if (!PopStack(memory, out ushort subA)) return false;
                    if (!PushStack(memory, (ushort)(subA - subB))) return false;
                    break;
                case "and":
                    if (!PopStack(memory, out ushort andB)) return false;
                    if (!PopStack(memory, out ushort andA)) return false;
                    if (!PushStack(memory, (ushort)(andA & andB))) return false;
                    break;
                case "or":
                    if (!PopStack(memory, out ushort orB)) return false;
                    if (!PopStack(memory, out ushort orA)) return false;
                    if (!PushStack(memory, (ushort)(orA | orB))) return false;
                    break;
                case "xor":
                    if (!PopStack(memory, out ushort xorB)) return false;
                    if (!PopStack(memory, out ushort xorA)) return false;
                    if (!PushStack(memory, (ushort)(xorA ^ xorB))) return false;
                    break;
                case "invert":
                    if (!PopStack(memory, out ushort invA)) return false;
                    if (!PushStack(memory, (ushort)~invA)) return false;
                    break;
                case "@":
                    if (!PopStack(memory, out ushort addr)) return false;
                    if (!PushStack(memory, memory.ReadWord((ushort)(addr & 0xFFFE)))) return false;
                    break;
                case "!":
                    if (!PopStack(memory, out ushort storeAddr)) return false;
                    if (!PopStack(memory, out ushort storeValue)) return false;
                    memory.WriteWord((ushort)(storeAddr & 0xFFFE), storeValue);
                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    static bool TryParseLiteral(string token, out ushort value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(token)) return false;

        char prefix = token[0];
        string digits = token;
        if (prefix == '#' || prefix == '$' || prefix == '>' || prefix == '%')
        {
            digits = token.Substring(1);
        }
        else
        {
            prefix = '\0';
        }

        if (string.IsNullOrWhiteSpace(digits)) return false;

        if (prefix == '#')
        {
            return ushort.TryParse(digits, out value);
        }

        if (prefix == '%')
        {
            return TryParseOctalUShort(digits, out value);
        }

        if (prefix == '$' || prefix == '>' || prefix == '\0')
        {
            return TryParseHexUShort(digits, out value);
        }

        return false;
    }

    static bool PushStack(Tms9900Memory memory, ushort value)
    {
        ushort baseAddr = memory.ReadWord(0x0200);
        ushort offset = memory.ReadWord(0x0202);
        if (offset > 7) return false;

        ushort addr = (ushort)(baseAddr + (offset * 2));
        memory.WriteWord(addr, value);
        memory.WriteWord(0x0202, (ushort)(offset + 1));
        return true;
    }

    static bool PopStack(Tms9900Memory memory, out ushort value)
    {
        value = 0;
        ushort offset = memory.ReadWord(0x0202);
        if (offset == 0) return false;

        offset--;
        ushort baseAddr = memory.ReadWord(0x0200);
        ushort addr = (ushort)(baseAddr + (offset * 2));
        value = memory.ReadWord(addr);
        memory.WriteWord(0x0202, offset);
        return true;
    }

    static bool PeekStack(Tms9900Memory memory, out ushort value)
    {
        value = 0;
        ushort offset = memory.ReadWord(0x0202);
        if (offset == 0) return false;

        ushort baseAddr = memory.ReadWord(0x0200);
        ushort addr = (ushort)(baseAddr + ((offset - 1) * 2));
        value = memory.ReadWord(addr);
        return true;
    }

    static bool TryParseOctalUShort(string text, out ushort value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        foreach (char ch in text.Trim())
        {
            if (ch < '0' || ch > '7') return false;
            int digit = ch - '0';
            int next = (value * 8) + digit;
            if (next > ushort.MaxValue) return false;
            value = (ushort)next;
        }

        return true;
    }

    static bool TryParseHexUShort(string text, out ushort value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        return ushort.TryParse(text.Trim(), System.Globalization.NumberStyles.HexNumber, null, out value);
    }

    static string FormatHex(ushort value)
    {
        return Convert.ToString(value, 16).PadLeft(4, '0').ToUpperInvariant();
    }

    static string FormatRegisterLine(Tms9900Cpu cpu, int start, int end)
    {
        var lineText = new System.Text.StringBuilder();
        for (int i = start; i < end; i++)
        {
            string regLabel = i.ToString();
            if (i > start)
            {
                lineText.Append(' ');
            }
            lineText.Append($"R{regLabel}={FormatHex(cpu.ReadRegister(i))}");
        }

        return lineText.ToString();
    }

    static string FormatState(Tms9900Cpu cpu)
    {
        return $"PC={FormatHex(cpu.ProgramCounter)} WP={FormatHex(cpu.WorkspacePointer)} " +
               $"ST={FormatHex(cpu.StatusRegister)} R0={FormatHex(cpu.ReadRegister(0))} " +
               $"R1={FormatHex(cpu.ReadRegister(1))} R2={FormatHex(cpu.ReadRegister(2))}";
    }

    private sealed class UartKeyboardListener : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly Thread _thread;

        public UartKeyboardListener(Tms9900Memory memory, Tms9900Cpu cpu, int interruptLevel)
        {
            _thread = new Thread(() => ListenLoop(memory, cpu, interruptLevel, _cts.Token))
            {
                IsBackground = true
            };
            _thread.Start();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _thread.Join();
            _cts.Dispose();
        }

        private static void ListenLoop(Tms9900Memory memory, Tms9900Cpu cpu, int interruptLevel, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    ConsoleKeyInfo key = Console.ReadKey(true);
                    if (key.KeyChar != '\0')
                    {
                        memory.ReceiveUartByte((byte)key.KeyChar);
                        cpu.TriggerInterrupt(interruptLevel);
                    }
                }
                else
                {
                    Thread.Sleep(5);
                }
            }
        }
    }
}

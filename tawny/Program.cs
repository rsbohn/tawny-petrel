namespace tawny;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("===========================================");
        Console.WriteLine("Tawny Petrel - TMS9900 Simulator");
        Console.WriteLine("===========================================");
        Console.WriteLine();

        // Initialize the simulator
        var memory = new Tms9900Memory();
        var cpu = new Tms9900Cpu(memory);
        InitializeMonitorStack(memory);

        // Interactive mode
        Console.WriteLine("===========================================");
        Console.WriteLine("Interactive Mode");
        Console.WriteLine("===========================================");
        Console.WriteLine("Commands:");
        Console.WriteLine("  s          - Execute single step");
        Console.WriteLine("  r          - Reset CPU");
        Console.WriteLine("  x <addr> [n]   - Examine memory at address (octal)");
        Console.WriteLine("  exam <addr> - Alias for x");
        Console.WriteLine("  d <addr> <val...> - Deposit words into memory (octal)");
        Console.WriteLine("  dep <addr> <val...> - Alias for d");
        Console.WriteLine("  demo       - Run the demo program");
        Console.WriteLine("  regs       - Show all registers");
        Console.WriteLine("  . dup drop swap over + - and or xor invert @ !");
        Console.WriteLine("  help       - Show this help");
        Console.WriteLine("  q          - Quit");
        Console.WriteLine();

        bool running = true;
        while (running)
        {
            Console.Write("> ");
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;

            string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string command = parts[0].ToLower();

            switch (command)
            {
                case "s":
                case "step":
                    if (cpu.IsRunning)
                    {
                        cpu.Step();
                        Console.WriteLine(cpu.GetState());
                    }
                    else
                    {
                        Console.WriteLine("CPU is not running. Use 'r' to reset.");
                    }
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
                        if (TryParseOctalUShort(parts[1], out ushort addr))
                        {
                            addr = (ushort)(addr & 0xFFFE);
                            int countWords = 0x10;
                            if (parts.Length > 2)
                            {
                                if (!TryParseOctalUShort(parts[2], out ushort parsedCount))
                                {
                                    Console.WriteLine($"Invalid count: {parts[2]}");
                                    break;
                                }
                                countWords = parsedCount;
                            }

                            Console.WriteLine($"Memory at 0o{Convert.ToString(addr, 8).PadLeft(6, '0')}:");
                            for (int offset = 0; offset < countWords; offset += 8)
                            {
                                ushort lineAddr = (ushort)(addr + (offset * 2));
                                var lineText = new System.Text.StringBuilder();
                                lineText.Append($"  {Convert.ToString(lineAddr, 8).PadLeft(6, '0')}:");
                                int wordsInLine = Math.Min(8, countWords - offset);
                                for (int i = 0; i < wordsInLine; i++)
                                {
                                    ushort word = memory.ReadWord((ushort)(lineAddr + i * 2));
                                    lineText.Append($" {Convert.ToString(word, 8).PadLeft(6, '0')}");
                                }
                                Console.WriteLine(lineText.ToString());
                            }
                        }
                        else
                        {
                            Console.WriteLine("Invalid address. Use octal format (e.g., 2000)");
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
                        if (TryParseOctalUShort(parts[1], out ushort addr))
                        {
                            addr = (ushort)(addr & 0xFFFE);
                            ushort writeAddr = addr;
                            int written = 0;
                            for (int i = 2; i < parts.Length; i++)
                            {
                                if (!TryParseOctalUShort(parts[i], out ushort value))
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
                                Console.WriteLine($"Wrote {written} word(s) starting at 0o{Convert.ToString(addr, 8).PadLeft(6, '0')}.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Invalid address. Use octal format (e.g., 2000)");
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

                case "help":
                    PrintHelp();
                    break;

                case "regs":
                case "registers":
                    Console.WriteLine($"PC: {FormatOctal(cpu.ProgramCounter)}");
                    Console.WriteLine($"WP: {FormatOctal(cpu.WorkspacePointer)}");
                    Console.WriteLine($"ST: {FormatOctal(cpu.StatusRegister)}");
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
        Console.WriteLine("  x <addr> [n]   - Examine memory at address (octal)");
        Console.WriteLine("  exam <addr> - Alias for x");
        Console.WriteLine("  d <addr> <val...> - Deposit words into memory (octal)");
        Console.WriteLine("  dep <addr> <val...> - Alias for d");
        Console.WriteLine("  demo       - Run the demo program");
        Console.WriteLine("  regs       - Show all registers");
        Console.WriteLine("  . dup drop swap over + - and or xor invert @ !");
        Console.WriteLine("  help       - Show this help");
        Console.WriteLine("  q          - Quit");
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
        Console.WriteLine("  R0 = 000005");
        Console.WriteLine("  R1 = 000003");
        Console.WriteLine("  R2 = 177777");
        Console.WriteLine("  R3 = 002000 (workspace pointer)");
        Console.WriteLine();

        // Verify results
        ushort r0 = cpu.ReadRegister(0);
        ushort r1 = cpu.ReadRegister(1);
        ushort r2 = cpu.ReadRegister(2);
        ushort r3 = cpu.ReadRegister(3);

        Console.WriteLine("Actual results:");
        Console.WriteLine($"  R0 = {FormatOctal(r0)}");
        Console.WriteLine($"  R1 = {FormatOctal(r1)}");
        Console.WriteLine($"  R2 = {FormatOctal(r2)}");
        Console.WriteLine($"  R3 = {FormatOctal(r3)}");
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
                    Console.WriteLine(FormatOctal(value));
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
        if (prefix == '#' || prefix == '$' || prefix == '>')
        {
            digits = token.Substring(1);
        }

        if (string.IsNullOrWhiteSpace(digits)) return false;

        if (prefix == '#')
        {
            return ushort.TryParse(digits, out value);
        }

        if (prefix == '$' || prefix == '>')
        {
            return ushort.TryParse(digits, System.Globalization.NumberStyles.HexNumber, null, out value);
        }

        return TryParseOctalUShort(token, out value);
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

    static string FormatOctal(ushort value)
    {
        return Convert.ToString(value, 8).PadLeft(6, '0');
    }

    static string FormatRegisterLine(Tms9900Cpu cpu, int start, int end)
    {
        var lineText = new System.Text.StringBuilder();
        for (int i = start; i < end; i++)
        {
            string regLabel = Convert.ToString(i, 8).PadLeft(2, '0');
            if (i > start)
            {
                lineText.Append(' ');
            }
            lineText.Append($"R{regLabel}={FormatOctal(cpu.ReadRegister(i))}");
        }

        return lineText.ToString();
    }

    static string FormatState(Tms9900Cpu cpu)
    {
        return $"PC={FormatOctal(cpu.ProgramCounter)} WP={FormatOctal(cpu.WorkspacePointer)} " +
               $"ST={FormatOctal(cpu.StatusRegister)} R0={FormatOctal(cpu.ReadRegister(0))} " +
               $"R1={FormatOctal(cpu.ReadRegister(1))} R2={FormatOctal(cpu.ReadRegister(2))}";
    }
}

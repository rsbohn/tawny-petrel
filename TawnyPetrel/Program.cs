namespace TawnyPetrel;

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
        
        Console.WriteLine($"Initial state: {cpu.GetState()}");
        Console.WriteLine();

        // Execute the program
        cpu.Start();
        
        Console.WriteLine("Executing instructions:");
        for (int i = 0; i < 4; i++)
        {
            Console.WriteLine($"Step {i + 1}: {cpu.GetState()}");
            cpu.Step();
        }

        Console.WriteLine();
        Console.WriteLine($"Final state: {cpu.GetState()}");
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
        Console.WriteLine($"  R0 = {r0:X4}");
        Console.WriteLine($"  R1 = {r1:X4}");
        Console.WriteLine($"  R2 = {r2:X4}");
        Console.WriteLine($"  R3 = {r3:X4}");
        Console.WriteLine();

        bool success = (r0 == 0x0005 && r1 == 0x0003 && r2 == 0xFFFF && r3 == 0x2000);
        Console.WriteLine(success ? "✓ Test passed!" : "✗ Test failed!");
        Console.WriteLine();

        // Interactive mode
        Console.WriteLine("===========================================");
        Console.WriteLine("Interactive Mode");
        Console.WriteLine("===========================================");
        Console.WriteLine("Commands:");
        Console.WriteLine("  s          - Execute single step");
        Console.WriteLine("  r          - Reset CPU");
        Console.WriteLine("  d <addr>   - Display memory at address (hex)");
        Console.WriteLine("  reg        - Show all registers");
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

                case "d":
                case "dump":
                    if (parts.Length > 1)
                    {
                        if (ushort.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out ushort addr))
                        {
                            Console.WriteLine($"Memory at 0x{addr:X4}:");
                            for (int i = 0; i < 16; i++)
                            {
                                ushort word = memory.ReadWord((ushort)(addr + i * 2));
                                Console.WriteLine($"  {addr + i * 2:X4}: {word:X4}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Invalid address. Use hex format (e.g., 2000)");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Usage: d <address>");
                    }
                    break;

                case "reg":
                case "registers":
                    Console.WriteLine($"PC: {cpu.ProgramCounter:X4}");
                    Console.WriteLine($"WP: {cpu.WorkspacePointer:X4}");
                    Console.WriteLine($"ST: {cpu.StatusRegister:X4}");
                    for (int i = 0; i < 16; i++)
                    {
                        Console.WriteLine($"R{i:D2}: {cpu.ReadRegister(i):X4}");
                    }
                    break;

                case "q":
                case "quit":
                case "exit":
                    running = false;
                    break;

                default:
                    Console.WriteLine("Unknown command. Type 'q' to quit.");
                    break;
            }
        }

        Console.WriteLine();
        Console.WriteLine("Goodbye!");
    }
}

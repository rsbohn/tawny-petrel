using Xunit;
using TawnyPetrel;

namespace TawnyPetrel.Tests;

public class Tms9900CpuTests
{
    [Fact]
    public void Reset_ShouldLoadWPandPCFromMemory()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // WP
        memory.WriteWord(0x0002, 0x0100); // PC
        
        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        
        Assert.Equal((ushort)0x2000, cpu.WorkspacePointer);
        Assert.Equal((ushort)0x0100, cpu.ProgramCounter);
    }

    [Fact]
    public void ReadRegister_ShouldReadFromWorkspace()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // WP at 0x2000
        memory.WriteWord(0x0002, 0x0100); // PC
        
        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        
        // Write to R0 (at WP + 0 = 0x2000)
        memory.WriteWord(0x2000, 0x1234);
        
        Assert.Equal((ushort)0x1234, cpu.ReadRegister(0));
    }

    [Fact]
    public void WriteRegister_ShouldWriteToWorkspace()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // WP
        memory.WriteWord(0x0002, 0x0100); // PC
        
        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        
        cpu.WriteRegister(1, 0xABCD);
        
        // R1 should be at WP + 2 = 0x2002
        Assert.Equal((ushort)0xABCD, memory.ReadWord(0x2002));
    }

    [Fact]
    public void UpdateStatusFlags_EqualFlag_ShouldBeSetForZero()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000);
        memory.WriteWord(0x0002, 0x0100);
        
        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        
        cpu.UpdateStatusFlags(0);
        
        Assert.True(cpu.IsEqual());
    }

    [Fact]
    public void UpdateStatusFlags_LGTFlag_ShouldBeSetForPositive()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000);
        memory.WriteWord(0x0002, 0x0100);
        
        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        
        cpu.UpdateStatusFlags(5);
        
        Assert.True(cpu.IsLogicalGreaterThan());
    }

    [Fact]
    public void SetCarry_ShouldSetCarryFlag()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000);
        memory.WriteWord(0x0002, 0x0100);
        
        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        
        cpu.SetCarry(true);
        
        Assert.True(cpu.GetCarry());
    }

    [Fact]
    public void SetOverflow_ShouldSetOverflowFlag()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000);
        memory.WriteWord(0x0002, 0x0100);
        
        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        
        cpu.SetOverflow(true);
        
        Assert.True(cpu.GetOverflow());
    }

    [Fact]
    public void ContextSwitch_ShouldSaveOldContext()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // Initial WP
        memory.WriteWord(0x0002, 0x0100); // Initial PC
        
        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        cpu.Start();
        
        // Perform context switch to new workspace at 0x3000
        cpu.ContextSwitch(0x3000, 0x0200);
        
        // Check that new WP and PC are set
        Assert.Equal((ushort)0x3000, cpu.WorkspacePointer);
        Assert.Equal((ushort)0x0200, cpu.ProgramCounter);
        
        // Check that old context is saved in new workspace
        // R13 should have old WP, R14 old PC, R15 old ST
        Assert.Equal((ushort)0x2000, cpu.ReadRegister(13));
        Assert.Equal((ushort)0x0100, cpu.ReadRegister(14));
    }

    [Fact]
    public void ReturnFromContext_ShouldRestoreContext()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // Initial WP
        memory.WriteWord(0x0002, 0x0100); // Initial PC
        
        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        cpu.Start();
        
        // Perform context switch
        cpu.ContextSwitch(0x3000, 0x0200);
        
        // Return from context
        cpu.ReturnFromContext();
        
        // Should restore original context
        Assert.Equal((ushort)0x2000, cpu.WorkspacePointer);
        Assert.Equal((ushort)0x0100, cpu.ProgramCounter);
    }

    [Fact]
    public void ExecuteXOP_ShouldPerformContextSwitch()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // Initial WP
        memory.WriteWord(0x0002, 0x0100); // Initial PC
        
        // Set up XOP vector 1 at 0x0044
        memory.WriteWord(0x0044, 0x3000); // New WP
        memory.WriteWord(0x0046, 0x0500); // New PC
        
        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        cpu.Start();
        
        // Execute XOP 1
        cpu.ExecuteXOP(1, 0x1234);
        
        // Check context switch occurred
        Assert.Equal((ushort)0x3000, cpu.WorkspacePointer);
        Assert.Equal((ushort)0x0500, cpu.ProgramCounter);
        
        // Check that source address is saved in R11
        Assert.Equal((ushort)0x1234, cpu.ReadRegister(11));
    }
}

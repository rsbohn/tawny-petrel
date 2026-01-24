using Xunit;
using TawnyPetrel;

namespace TawnyPetrel.Tests;

public class Tms9900IsaTests
{
    [Fact]
    public void LI_LoadImmediate_ShouldLoadValueIntoRegister()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // WP
        memory.WriteWord(0x0002, 0x0100); // PC
        
        // Program: LI R0, 0x1234
        memory.WriteWord(0x0100, 0x0200); // LI R0
        memory.WriteWord(0x0102, 0x1234); // Immediate value
        
        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        cpu.Start();
        cpu.Step();
        
        Assert.Equal((ushort)0x1234, cpu.ReadRegister(0));
    }

    [Fact]
    public void AI_AddImmediate_ShouldAddToRegister()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // WP
        memory.WriteWord(0x0002, 0x0100); // PC
        
        // Set R0 to 5
        memory.WriteWord(0x2000, 0x0005);
        
        // Program: AI R0, 0x0003
        memory.WriteWord(0x0100, 0x0400); // AI R0
        memory.WriteWord(0x0102, 0x0003); // Immediate value
        
        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        cpu.Start();
        cpu.Step();
        
        Assert.Equal((ushort)0x0008, cpu.ReadRegister(0));
    }

    [Fact]
    public void ANDI_AndImmediate_ShouldAndWithRegister()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // WP
        memory.WriteWord(0x0002, 0x0100); // PC
        
        // Set R0 to 0xFF0F
        memory.WriteWord(0x2000, 0xFF0F);
        
        // Program: ANDI R0, 0x0F0F
        memory.WriteWord(0x0100, 0x0600); // ANDI R0
        memory.WriteWord(0x0102, 0x0F0F); // Immediate value
        
        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        cpu.Start();
        cpu.Step();
        
        Assert.Equal((ushort)0x0F0F, cpu.ReadRegister(0));
    }

    [Fact]
    public void ORI_OrImmediate_ShouldOrWithRegister()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // WP
        memory.WriteWord(0x0002, 0x0100); // PC
        
        // Set R0 to 0x0F00
        memory.WriteWord(0x2000, 0x0F00);
        
        // Program: ORI R0, 0x00F0
        memory.WriteWord(0x0100, 0x0800); // ORI R0
        memory.WriteWord(0x0102, 0x00F0); // Immediate value
        
        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        cpu.Start();
        cpu.Step();
        
        Assert.Equal((ushort)0x0FF0, cpu.ReadRegister(0));
    }

    [Fact]
    public void CI_CompareImmediate_ShouldSetEqualFlag()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // WP
        memory.WriteWord(0x0002, 0x0100); // PC
        
        // Set R0 to 0x1234
        memory.WriteWord(0x2000, 0x1234);
        
        // Program: CI R0, 0x1234
        memory.WriteWord(0x0100, 0x0A00); // CI R0
        memory.WriteWord(0x0102, 0x1234); // Immediate value
        
        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        cpu.Start();
        cpu.Step();
        
        Assert.True(cpu.IsEqual());
    }

    [Fact]
    public void STWP_StoreWorkspacePointer_ShouldStoreWP()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // WP
        memory.WriteWord(0x0002, 0x0100); // PC
        
        // Program: STWP R1
        memory.WriteWord(0x0100, 0x0C01); // STWP R1
        
        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        cpu.Start();
        cpu.Step();
        
        Assert.Equal((ushort)0x2000, cpu.ReadRegister(1));
    }

    [Fact]
    public void STST_StoreStatus_ShouldStoreStatus()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // WP
        memory.WriteWord(0x0002, 0x0100); // PC
        
        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        cpu.Start();
        
        // Set some status flags
        cpu.SetCarry(true);
        cpu.SetOverflow(true);
        
        // Program: STST R1
        memory.WriteWord(0x0100, 0x0E01); // STST R1
        cpu.Step();
        
        Assert.Equal(cpu.StatusRegister, cpu.ReadRegister(1));
    }

    [Fact]
    public void JMP_UnconditionalJump_ShouldJumpCorrectly()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // WP
        memory.WriteWord(0x0002, 0x0100); // PC
        
        // Program: JMP +4 (displacement = 2, so jump to PC + 4)
        memory.WriteWord(0x0100, 0x1002); // JMP with displacement 2
        
        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        cpu.Start();
        cpu.Step();
        
        // PC should be at 0x0100 + 2 (after fetch) + 4 (displacement * 2) = 0x0106
        Assert.Equal((ushort)0x0106, cpu.ProgramCounter);
    }

    [Fact]
    public void JEQ_JumpIfEqual_ShouldJumpWhenEqual()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // WP
        memory.WriteWord(0x0002, 0x0100); // PC
        
        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        cpu.Start();
        
        // Set equal flag
        cpu.UpdateStatusFlags(0);
        
        // Program: JEQ +4
        memory.WriteWord(0x0100, 0x1302); // JEQ with displacement 2
        cpu.Step();
        
        Assert.Equal((ushort)0x0106, cpu.ProgramCounter);
    }

    [Fact]
    public void JNE_JumpIfNotEqual_ShouldNotJumpWhenEqual()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // WP
        memory.WriteWord(0x0002, 0x0100); // PC
        
        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        cpu.Start();
        
        // Set equal flag
        cpu.UpdateStatusFlags(0);
        
        // Program: JNE +4
        memory.WriteWord(0x0100, 0x1602); // JNE with displacement 2
        cpu.Step();
        
        // Should not jump, PC should be at 0x0102
        Assert.Equal((ushort)0x0102, cpu.ProgramCounter);
    }

    [Fact]
    public void RTWP_ReturnFromContext_ShouldRestoreContext()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // Initial WP
        memory.WriteWord(0x0002, 0x0100); // Initial PC
        
        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        cpu.Start();
        
        // Perform context switch
        cpu.ContextSwitch(0x3000, 0x0200);
        
        // Program at 0x0200: RTWP
        memory.WriteWord(0x0200, 0x0380); // RTWP instruction
        cpu.Step();
        
        // Should restore original context
        Assert.Equal((ushort)0x2000, cpu.WorkspacePointer);
        Assert.Equal((ushort)0x0100, cpu.ProgramCounter);
    }
}

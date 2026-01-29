using Xunit;
using tawny;

namespace tawny.Tests;

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
        memory.WriteWord(0x0100, 0x0220); // AI R0
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
        memory.WriteWord(0x0100, 0x0240); // ANDI R0
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
        memory.WriteWord(0x0100, 0x0260); // ORI R0
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
        memory.WriteWord(0x0100, 0x0280); // CI R0
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
    public void MOV_Indirect_ShouldMoveWordFromMemory()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // WP
        memory.WriteWord(0x0002, 0x0100); // PC

        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        cpu.Start();

        cpu.WriteRegister(1, 0x3000);
        memory.WriteWord(0x3000, 0xBEEF);

        // MOV *R1, R2
        memory.WriteWord(0x0100, 0xC091);
        cpu.Step();

        Assert.Equal((ushort)0xBEEF, cpu.ReadRegister(2));
    }

    [Fact]
    public void MOV_AutoIncrement_ShouldAdvanceSourceRegister()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // WP
        memory.WriteWord(0x0002, 0x0100); // PC

        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        cpu.Start();

        cpu.WriteRegister(1, 0x3000);
        memory.WriteWord(0x3000, 0x1234);

        // MOV *R1+, R2
        memory.WriteWord(0x0100, 0xC0B1);
        cpu.Step();

        Assert.Equal((ushort)0x1234, cpu.ReadRegister(2));
        Assert.Equal((ushort)0x3002, cpu.ReadRegister(1));
    }

    [Fact]
    public void MOV_Indexed_ShouldUseDisplacement()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // WP
        memory.WriteWord(0x0002, 0x0100); // PC

        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        cpu.Start();

        memory.WriteWord(0x0200, 0xCAFE);

        // MOV @>0200, R1
        memory.WriteWord(0x0100, 0xC060);
        memory.WriteWord(0x0102, 0x0200);
        cpu.Step();

        Assert.Equal((ushort)0xCAFE, cpu.ReadRegister(1));
    }

    [Fact]
    public void MOVB_RegisterToRegister_ShouldUpdateLowByte()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // WP
        memory.WriteWord(0x0002, 0x0100); // PC

        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        cpu.Start();

        cpu.WriteRegister(1, 0xABCD);
        cpu.WriteRegister(2, 0x1100);

        // MOVB R1, R2
        memory.WriteWord(0x0100, 0xD081);
        cpu.Step();

        Assert.Equal((ushort)0x11CD, cpu.ReadRegister(2));
    }

    [Fact]
    public void MPY_RegisterMultiply_ShouldStoreProductInDestPair()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // WP
        memory.WriteWord(0x0002, 0x0100); // PC

        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        cpu.Start();

        cpu.WriteRegister(3, 0x0010);
        cpu.WriteRegister(4, 0x0000);
        cpu.WriteRegister(5, 0x0003);

        // MPY R5, R3
        memory.WriteWord(0x0100, 0x38C5);
        cpu.Step();

        Assert.Equal((ushort)0x0000, cpu.ReadRegister(3));
        Assert.Equal((ushort)0x0030, cpu.ReadRegister(4));
    }

    [Fact]
    public void MPY_DestIsR15_ShouldWriteLowWordAfterWorkspace()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // WP
        memory.WriteWord(0x0002, 0x0100); // PC

        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        cpu.Start();

        cpu.WriteRegister(15, 0x0002);
        cpu.WriteRegister(1, 0x0003);
        memory.WriteWord(0x2020, 0xFFFF);

        // MPY R1, R15
        memory.WriteWord(0x0100, 0x3BC1);
        cpu.Step();

        Assert.Equal((ushort)0x0000, cpu.ReadRegister(15));
        Assert.Equal((ushort)0x0006, memory.ReadWord(0x2020));
    }

    [Fact]
    public void DIV_ValidDivision_ShouldStoreQuotientAndRemainder()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // WP
        memory.WriteWord(0x0002, 0x0100); // PC

        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        cpu.Start();

        cpu.WriteRegister(3, 0x0002);
        cpu.WriteRegister(4, 0x0000);
        cpu.WriteRegister(5, 0x0003);

        // DIV R5, R3
        memory.WriteWord(0x0100, 0x3CC5);
        cpu.Step();

        Assert.Equal((ushort)0xAAAA, cpu.ReadRegister(3));
        Assert.Equal((ushort)0x0002, cpu.ReadRegister(4));
        Assert.False(cpu.GetStatusBit4());
    }

    [Fact]
    public void DIV_DivisorLessOrEqualHighWord_ShouldSetStatusBit4AndNoOp()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x0000, 0x2000); // WP
        memory.WriteWord(0x0002, 0x0100); // PC

        var cpu = new Tms9900Cpu(memory);
        cpu.Reset();
        cpu.Start();

        cpu.WriteRegister(3, 0x0003);
        cpu.WriteRegister(4, 0x1111);
        cpu.WriteRegister(5, 0x0003);

        // DIV R5, R3
        memory.WriteWord(0x0100, 0x3CC5);
        cpu.Step();

        Assert.Equal((ushort)0x0003, cpu.ReadRegister(3));
        Assert.Equal((ushort)0x1111, cpu.ReadRegister(4));
        Assert.True(cpu.GetStatusBit4());
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

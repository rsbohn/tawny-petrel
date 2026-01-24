using Xunit;
using TawnyPetrel;

namespace TawnyPetrel.Tests;

public class Tms9900MemoryTests
{
    [Fact]
    public void ReadWord_ShouldReturnCorrectValue()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x1000, 0x1234);
        
        var result = memory.ReadWord(0x1000);
        
        Assert.Equal((ushort)0x1234, result);
    }

    [Fact]
    public void WriteWord_ShouldStoreBigEndian()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x1000, 0x1234);
        
        // TMS9900 is big-endian, so high byte first
        var highByte = memory.ReadByte(0x1000);
        var lowByte = memory.ReadByte(0x1001);
        
        Assert.Equal((byte)0x12, highByte);
        Assert.Equal((byte)0x34, lowByte);
    }

    [Fact]
    public void ReadByte_ShouldReturnCorrectValue()
    {
        var memory = new Tms9900Memory();
        memory.WriteByte(0x1000, 0xAB);
        
        var result = memory.ReadByte(0x1000);
        
        Assert.Equal((byte)0xAB, result);
    }

    [Fact]
    public void LoadProgram_ShouldLoadDataCorrectly()
    {
        var memory = new Tms9900Memory();
        byte[] program = { 0x01, 0x02, 0x03, 0x04 };
        
        memory.LoadProgram(0x1000, program);
        
        Assert.Equal((byte)0x01, memory.ReadByte(0x1000));
        Assert.Equal((byte)0x02, memory.ReadByte(0x1001));
        Assert.Equal((byte)0x03, memory.ReadByte(0x1002));
        Assert.Equal((byte)0x04, memory.ReadByte(0x1003));
    }

    [Fact]
    public void Clear_ShouldZeroAllMemory()
    {
        var memory = new Tms9900Memory();
        memory.WriteWord(0x1000, 0xFFFF);
        
        memory.Clear();
        
        Assert.Equal((ushort)0x0000, memory.ReadWord(0x1000));
    }
}

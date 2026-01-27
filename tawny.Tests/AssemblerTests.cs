using tawny;
using Xunit;

namespace tawny.Tests;

public class AssemblerTests
{
    [Fact]
    public void Assemble_DataDirectives_ShouldEmitBigEndianWords()
    {
        string[] lines =
        {
            "ORG 10",
            "DW 1, 2",
            "DD $01020304",
            "DQ $0102030405060708",
            "TXT /HI/",
            "END"
        };

        var assembler = new Assembler();
        AssemblerResult result = assembler.AssembleLines(lines, "test.asm");

        AssertWord(result, 0x0010, 0x0001);
        AssertWord(result, 0x0012, 0x0002);
        AssertWord(result, 0x0014, 0x0102);
        AssertWord(result, 0x0016, 0x0304);
        AssertWord(result, 0x0018, 0x0102);
        AssertWord(result, 0x001A, 0x0304);
        AssertWord(result, 0x001C, 0x0506);
        AssertWord(result, 0x001E, 0x0708);
        AssertWord(result, 0x0020, 0x0048);
        AssertWord(result, 0x0022, 0x0049);
    }

    [Fact]
    public void Assemble_HelloProgram_ShouldMatchKnownEncodings()
    {
        string[] lines =
        {
            "RORG >0000",
            "DATA >1000",
            "DATA >0080",
            "RORG >0080",
            "TOP LI 2,>0200",
            "LI 3,>F000",
            "LI 4,>F002",
            "PR1 MOV *3, 1",
            "ANDI 1, >0001",
            "JNE PR1",
            "MOV *2, 1",
            "MOV 1, *4",
            "TST INCT 2",
            "MOV *2, 1",
            "JEQ TOP",
            "JMP PR1",
            "RORG >0200",
            "DATA >0048",
            "DATA >0000",
            "END"
        };

        var assembler = new Assembler();
        AssemblerResult result = assembler.AssembleLines(lines, "hello.asm");

        AssertWord(result, 0x0000, 0x1000);
        AssertWord(result, 0x0002, 0x0080);
        AssertWord(result, 0x0080, 0x0202);
        AssertWord(result, 0x0082, 0x0200);
        AssertWord(result, 0x008C, 0xC053);
        AssertWord(result, 0x008E, 0x0241);
        AssertWord(result, 0x0092, 0x16FC);
        AssertWord(result, 0x0098, 0x05C2);
        AssertWord(result, 0x009C, 0x13F1);
        AssertWord(result, 0x009E, 0x10F6);
        AssertWord(result, 0x0200, 0x0048);

        Assert.Contains("TOP 0080", result.SymbolText);
        Assert.Contains("PR1 008C", result.SymbolText);
        Assert.Contains("TST 0098", result.SymbolText);
    }

    private static void AssertWord(AssemblerResult result, ushort address, ushort expected)
    {
        Assert.True(result.Bytes.TryGetValue(address, out byte high));
        Assert.True(result.Bytes.TryGetValue((ushort)(address + 1), out byte low));
        ushort actual = (ushort)((high << 8) | low);
        Assert.Equal(expected, actual);
    }
}

using tawny;
using Xunit;

namespace tawny.Tests;

public class SRecordReaderTests
{
    [Fact]
    public void ReadLines_ShouldRoundTripWriterOutput()
    {
        var bytes = new SortedDictionary<ushort, byte>
        {
            { 0x0100, 0x01 },
            { 0x0101, 0x02 },
            { 0x0104, 0xFF }
        };

        string srec = SRecordWriter.Write(bytes);
        var read = SRecordReader.ReadLines(srec.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));

        Assert.Equal(bytes.Count, read.Count);
        foreach (var pair in bytes)
        {
            Assert.True(read.TryGetValue(pair.Key, out byte value));
            Assert.Equal(pair.Value, value);
        }
    }
}

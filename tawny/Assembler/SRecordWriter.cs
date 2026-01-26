using System.Text;

namespace tawny;

public static class SRecordWriter
{
    public static string Write(IReadOnlyDictionary<ushort, byte> bytes)
    {
        if (bytes.Count == 0)
        {
            return "S9030000FC";
        }

        var ordered = bytes.OrderBy(pair => pair.Key).ToList();
        var lines = new List<string>();
        int index = 0;

        while (index < ordered.Count)
        {
            ushort startAddress = ordered[index].Key;
            var recordBytes = new List<byte>();
            ushort currentAddress = startAddress;

            while (index < ordered.Count && ordered[index].Key == currentAddress && recordBytes.Count < 16)
            {
                recordBytes.Add(ordered[index].Value);
                currentAddress++;
                index++;
            }

            lines.Add(BuildS1Record(startAddress, recordBytes));
        }

        lines.Add("S9030000FC");
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildS1Record(ushort address, List<byte> data)
    {
        int count = data.Count + 3;
        int sum = count + ((address >> 8) & 0xFF) + (address & 0xFF);
        foreach (byte value in data)
        {
            sum += value;
        }
        byte checksum = (byte)(~sum & 0xFF);

        var sb = new StringBuilder();
        sb.Append("S1");
        sb.Append(count.ToString("X2"));
        sb.Append(address.ToString("X4"));
        foreach (byte value in data)
        {
            sb.Append(value.ToString("X2"));
        }
        sb.Append(checksum.ToString("X2"));
        return sb.ToString();
    }
}

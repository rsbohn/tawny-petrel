using System.Globalization;

namespace tawny;

public static class SRecordReader
{
    public static IReadOnlyDictionary<ushort, byte> ReadFile(string path)
    {
        var lines = File.ReadAllLines(path);
        return ReadLines(lines);
    }

    public static IReadOnlyDictionary<ushort, byte> ReadLines(IEnumerable<string> lines)
    {
        var bytes = new SortedDictionary<ushort, byte>();
        int lineNumber = 0;

        foreach (string raw in lines)
        {
            lineNumber++;
            string line = raw.Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if (!line.StartsWith('S'))
            {
                throw new InvalidOperationException($"Line {lineNumber}: Invalid S-record prefix.");
            }

            char type = line.Length > 1 ? line[1] : '\0';
            switch (type)
            {
                case '0':
                case '9':
                    continue;
                case '1':
                    ReadS1(line, lineNumber, bytes);
                    break;
                default:
                    throw new InvalidOperationException($"Line {lineNumber}: Unsupported record type S{type}.");
            }
        }

        return bytes;
    }

    private static void ReadS1(string line, int lineNumber, SortedDictionary<ushort, byte> bytes)
    {
        if (line.Length < 10)
        {
            throw new InvalidOperationException($"Line {lineNumber}: S1 record too short.");
        }

        int count = ParseHexByte(line, 2, lineNumber);
        int expectedChars = 4 + (count * 2);
        if (line.Length < expectedChars)
        {
            throw new InvalidOperationException($"Line {lineNumber}: S1 record length mismatch.");
        }

        ushort address = ParseHexWord(line, 4, lineNumber);
        int dataBytes = count - 3;
        int dataStart = 8;
        int sum = count + ((address >> 8) & 0xFF) + (address & 0xFF);

        for (int i = 0; i < dataBytes; i++)
        {
            byte value = (byte)ParseHexByte(line, dataStart + (i * 2), lineNumber);
            bytes[(ushort)(address + i)] = value;
            sum += value;
        }

        byte checksum = (byte)ParseHexByte(line, dataStart + (dataBytes * 2), lineNumber);
        byte computed = (byte)(~sum & 0xFF);
        if (checksum != computed)
        {
            throw new InvalidOperationException($"Line {lineNumber}: S1 checksum mismatch.");
        }
    }

    private static int ParseHexByte(string line, int start, int lineNumber)
    {
        if (start + 2 > line.Length)
        {
            throw new InvalidOperationException($"Line {lineNumber}: Unexpected end of line.");
        }

        string slice = line.Substring(start, 2);
        if (!int.TryParse(slice, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value))
        {
            throw new InvalidOperationException($"Line {lineNumber}: Invalid hex byte '{slice}'.");
        }

        return value;
    }

    private static ushort ParseHexWord(string line, int start, int lineNumber)
    {
        if (start + 4 > line.Length)
        {
            throw new InvalidOperationException($"Line {lineNumber}: Unexpected end of line.");
        }

        string slice = line.Substring(start, 4);
        if (!ushort.TryParse(slice, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort value))
        {
            throw new InvalidOperationException($"Line {lineNumber}: Invalid hex word '{slice}'.");
        }

        return value;
    }
}

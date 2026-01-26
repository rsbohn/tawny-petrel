using System.Text;

namespace tawny;

public sealed class Assembler
{
    private static readonly HashSet<string> Directives = new(StringComparer.OrdinalIgnoreCase)
    {
        "ORG",
        "RORG",
        "DW",
        "DD",
        "DQ",
        "TXT",
        "DATA",
        "END"
    };

    private static readonly Dictionary<string, int> JumpOpcodes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "JMP", 0x10 },
        { "JLT", 0x11 },
        { "JLE", 0x12 },
        { "JEQ", 0x13 },
        { "JHE", 0x14 },
        { "JGT", 0x15 },
        { "JNE", 0x16 },
        { "JNC", 0x17 },
        { "JOC", 0x18 },
        { "JNO", 0x19 },
        { "JL", 0x1A },
        { "JH", 0x1B },
        { "JOP", 0x1C }
    };

    private static readonly Dictionary<string, ushort> ImmediateOpcodes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "LI", 0x0200 },
        { "AI", 0x0220 },
        { "ANDI", 0x0240 },
        { "ORI", 0x0260 },
        { "CI", 0x0280 }
    };

    private static readonly Dictionary<string, ushort> Format2Opcodes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "MOV", 0xC000 },
        { "MOVB", 0xD000 },
        { "A", 0xA000 },
        { "AB", 0xB000 },
        { "S", 0x6000 },
        { "SB", 0x7000 },
        { "C", 0x8000 },
        { "CB", 0x9000 }
    };

    private static readonly Dictionary<string, ushort> SingleOperandOpcodes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "INCT", 0x05C0 }
    };

    public AssemblerResult Assemble(string sourcePath, string? outputDir)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException("Source path is required.", nameof(sourcePath));
        }

        var lines = File.ReadAllLines(sourcePath);
        var result = AssembleLines(lines, sourcePath);

        string baseName = Path.GetFileNameWithoutExtension(sourcePath);
        string targetDir = outputDir ?? Path.GetDirectoryName(sourcePath) ?? ".";
        Directory.CreateDirectory(targetDir);

        string listingPath = Path.Combine(targetDir, $"{baseName}.lst");
        string symbolPath = Path.Combine(targetDir, $"{baseName}.sym");
        string srecPath = Path.Combine(targetDir, $"{baseName}.srec");

        File.WriteAllText(listingPath, result.ListingText, Encoding.ASCII);
        File.WriteAllText(symbolPath, result.SymbolText, Encoding.ASCII);
        File.WriteAllText(srecPath, result.SrecText, Encoding.ASCII);

        return result;
    }

    public AssemblerResult AssembleLines(IReadOnlyList<string> lines, string sourceName)
    {
        var parsedLines = new List<ParsedLine>(lines.Count);
        for (int i = 0; i < lines.Count; i++)
        {
            parsedLines.Add(ParseLine(lines[i], i + 1));
        }

        var symbols = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        ushort locationCounter = 0;

        foreach (var line in parsedLines)
        {
            if (line.IsEmpty) continue;

            if (line.Label != null)
            {
                if (symbols.ContainsKey(line.Label))
                {
                    errors.Add($"Line {line.LineNumber}: Duplicate label '{line.Label}'.");
                }
                else
                {
                    symbols[line.Label] = locationCounter;
                }
            }

            if (line.Mnemonic == null) continue;

            if (string.Equals(line.Mnemonic, "END", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (IsOriginDirective(line.Mnemonic))
            {
                if (!TryEvaluate(line.OperandText, symbols, out ushort originValue))
                {
                    errors.Add($"Line {line.LineNumber}: Invalid origin value '{line.OperandText}'.");
                }
                else
                {
                    locationCounter = originValue;
                }
                continue;
            }

            if (IsDataDirective(line.Mnemonic, out int wordSize))
            {
                if (string.Equals(line.Mnemonic, "TXT", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryExtractTxt(line.OperandText, out string text))
                    {
                        errors.Add($"Line {line.LineNumber}: TXT expects /text/.");
                    }
                    else
                    {
                        locationCounter = (ushort)(locationCounter + (text.Length * 2));
                    }
                }
                else
                {
                    var operands = SplitOperands(line.OperandText);
                    locationCounter = (ushort)(locationCounter + (operands.Count * wordSize * 2));
                }
                continue;
            }

            if (ImmediateOpcodes.ContainsKey(line.Mnemonic))
            {
                locationCounter = (ushort)(locationCounter + 4);
                continue;
            }

            if (SingleOperandOpcodes.ContainsKey(line.Mnemonic) || Format2Opcodes.ContainsKey(line.Mnemonic) || JumpOpcodes.ContainsKey(line.Mnemonic))
            {
                locationCounter = (ushort)(locationCounter + 2);
                continue;
            }

            errors.Add($"Line {line.LineNumber}: Unknown mnemonic '{line.Mnemonic}'.");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        var outputBytes = new SortedDictionary<ushort, byte>();
        var listingLines = new List<string>();
        locationCounter = 0;

        foreach (var line in parsedLines)
        {
            if (line.IsEmpty)
            {
                listingLines.Add(string.Empty);
                continue;
            }

            if (line.Mnemonic == null)
            {
                listingLines.Add(FormatListingLine(null, Array.Empty<ushort>(), line.Original));
                continue;
            }

            if (string.Equals(line.Mnemonic, "END", StringComparison.OrdinalIgnoreCase))
            {
                listingLines.Add(FormatListingLine(null, Array.Empty<ushort>(), line.Original));
                break;
            }

            if (IsOriginDirective(line.Mnemonic))
            {
                if (!TryEvaluate(line.OperandText, symbols, out ushort originValue))
                {
                    throw new InvalidOperationException($"Line {line.LineNumber}: Invalid origin value '{line.OperandText}'.");
                }
                locationCounter = originValue;
                listingLines.Add(FormatListingLine(locationCounter, Array.Empty<ushort>(), line.Original));
                continue;
            }

            if (IsDataDirective(line.Mnemonic, out int wordSize))
            {
                if (string.Equals(line.Mnemonic, "TXT", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryExtractTxt(line.OperandText, out string text))
                    {
                        throw new InvalidOperationException($"Line {line.LineNumber}: TXT expects /text/.");
                    }
                    var words = new List<ushort>();
                    ushort startAddress = locationCounter;
                    foreach (char ch in text)
                    {
                        ushort word = (ushort)ch;
                        words.Add(word);
                        EmitWord(outputBytes, locationCounter, word);
                        locationCounter += 2;
                    }
                    listingLines.Add(FormatListingLine(startAddress, words.ToArray(), line.Original));
                }
                else
                {
                    var operands = SplitOperands(line.OperandText);
                    var words = new List<ushort>();
                    ushort startAddress = locationCounter;
                    foreach (string operand in operands)
                    {
                        if (!TryEvaluateWide(operand, symbols, wordSize, out ulong value))
                        {
                            throw new InvalidOperationException($"Line {line.LineNumber}: Invalid data value '{operand}'.");
                        }
                        foreach (ushort word in ExpandWideValue(value, wordSize))
                        {
                            words.Add(word);
                            EmitWord(outputBytes, locationCounter, word);
                            locationCounter += 2;
                        }
                    }
                    listingLines.Add(FormatListingLine(startAddress, words.ToArray(), line.Original));
                }
                continue;
            }

            if (ImmediateOpcodes.TryGetValue(line.Mnemonic, out ushort immediateOpcode))
            {
                var operands = SplitOperands(line.OperandText);
                if (operands.Count != 2)
                {
                    throw new InvalidOperationException($"Line {line.LineNumber}: Expected reg, immediate for {line.Mnemonic}.");
                }

                int reg = ParseRegister(operands[0], line.LineNumber);
                if (!TryEvaluate(operands[1], symbols, out ushort immediateValue))
                {
                    throw new InvalidOperationException($"Line {line.LineNumber}: Invalid immediate '{operands[1]}'.");
                }

                ushort instruction = (ushort)(immediateOpcode | (ushort)reg);
                EmitWord(outputBytes, locationCounter, instruction);
                EmitWord(outputBytes, (ushort)(locationCounter + 2), immediateValue);
                listingLines.Add(FormatListingLine(locationCounter, new[] { instruction, immediateValue }, line.Original));
                locationCounter += 4;
                continue;
            }

            if (Format2Opcodes.TryGetValue(line.Mnemonic, out ushort format2Opcode))
            {
                var operands = SplitOperands(line.OperandText);
                if (operands.Count != 2)
                {
                    throw new InvalidOperationException($"Line {line.LineNumber}: Expected src, dest for {line.Mnemonic}.");
                }

                var dest = ParseOperand(operands[1], line.LineNumber);
                var src = ParseOperand(operands[0], line.LineNumber);

                ushort instruction = (ushort)(format2Opcode |
                                             ((ushort)dest.Mode << 10) |
                                             (dest.Register << 6) |
                                             ((ushort)src.Mode << 4) |
                                             src.Register);

                EmitWord(outputBytes, locationCounter, instruction);
                listingLines.Add(FormatListingLine(locationCounter, new[] { instruction }, line.Original));
                locationCounter += 2;
                continue;
            }

            if (SingleOperandOpcodes.TryGetValue(line.Mnemonic, out ushort singleOpcode))
            {
                var operands = SplitOperands(line.OperandText);
                if (operands.Count != 1)
                {
                    throw new InvalidOperationException($"Line {line.LineNumber}: Expected single operand for {line.Mnemonic}.");
                }

                int reg = ParseRegister(operands[0], line.LineNumber);
                ushort instruction = (ushort)(singleOpcode | (ushort)reg);
                EmitWord(outputBytes, locationCounter, instruction);
                listingLines.Add(FormatListingLine(locationCounter, new[] { instruction }, line.Original));
                locationCounter += 2;
                continue;
            }

            if (JumpOpcodes.TryGetValue(line.Mnemonic, out int jumpOpcode))
            {
                var operands = SplitOperands(line.OperandText);
                if (operands.Count != 1)
                {
                    throw new InvalidOperationException($"Line {line.LineNumber}: Expected target for {line.Mnemonic}.");
                }

                if (!TryEvaluate(operands[0], symbols, out ushort target))
                {
                    throw new InvalidOperationException($"Line {line.LineNumber}: Invalid jump target '{operands[0]}'.");
                }

                int delta = target - (locationCounter + 2);
                if ((delta & 0x1) != 0)
                {
                    throw new InvalidOperationException($"Line {line.LineNumber}: Jump target not word-aligned.");
                }

                int displacement = delta / 2;
                if (displacement < sbyte.MinValue || displacement > sbyte.MaxValue)
                {
                    throw new InvalidOperationException($"Line {line.LineNumber}: Jump displacement out of range.");
                }

                byte dispByte = unchecked((byte)(sbyte)displacement);
                ushort instruction = (ushort)((jumpOpcode << 8) | dispByte);
                EmitWord(outputBytes, locationCounter, instruction);
                listingLines.Add(FormatListingLine(locationCounter, new[] { instruction }, line.Original));
                locationCounter += 2;
                continue;
            }

            throw new InvalidOperationException($"Line {line.LineNumber}: Unsupported mnemonic '{line.Mnemonic}'.");
        }

        string listingText = string.Join(Environment.NewLine, listingLines);
        string symbolText = FormatSymbols(symbols);
        string srecText = SRecordWriter.Write(outputBytes);

        return new AssemblerResult(listingText, symbolText, srecText, outputBytes);
    }

    private static bool IsOriginDirective(string mnemonic)
    {
        return string.Equals(mnemonic, "ORG", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(mnemonic, "RORG", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDataDirective(string mnemonic, out int wordSize)
    {
        wordSize = 0;
        if (string.Equals(mnemonic, "TXT", StringComparison.OrdinalIgnoreCase))
        {
            wordSize = 1;
            return true;
        }
        if (string.Equals(mnemonic, "DW", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mnemonic, "DATA", StringComparison.OrdinalIgnoreCase))
        {
            wordSize = 1;
            return true;
        }
        if (string.Equals(mnemonic, "DD", StringComparison.OrdinalIgnoreCase))
        {
            wordSize = 2;
            return true;
        }
        if (string.Equals(mnemonic, "DQ", StringComparison.OrdinalIgnoreCase))
        {
            wordSize = 4;
            return true;
        }
        return false;
    }

    private static bool TryExtractTxt(string operandText, out string text)
    {
        text = string.Empty;
        int first = operandText.IndexOf('/');
        int last = operandText.LastIndexOf('/');
        if (first < 0 || last <= first)
        {
            return false;
        }
        text = operandText.Substring(first + 1, last - first - 1);
        return true;
    }

    private static List<string> SplitOperands(string operandText)
    {
        if (string.IsNullOrWhiteSpace(operandText))
        {
            return new List<string>();
        }

        string normalized = operandText.Replace(",", " ");
        return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static int ParseRegister(string token, int lineNumber)
    {
        string trimmed = token.Trim();
        if (trimmed.StartsWith("R", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(1);
        }

        if (!TryParseLiteral(trimmed, out ushort value) || value > 15)
        {
            throw new InvalidOperationException($"Line {lineNumber}: Invalid register '{token}'.");
        }

        return value;
    }

    private static Operand ParseOperand(string token, int lineNumber)
    {
        string trimmed = token.Trim();
        AddressMode mode = AddressMode.Register;

        if (trimmed.StartsWith("*", StringComparison.Ordinal))
        {
            mode = AddressMode.Indirect;
            trimmed = trimmed.Substring(1);
        }

        if (trimmed.Contains('(') || trimmed.Contains(')') || trimmed.Contains('+') || trimmed.StartsWith("@", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Line {lineNumber}: Addressing mode not supported: '{token}'.");
        }

        int reg = ParseRegister(trimmed, lineNumber);
        return new Operand(mode, (ushort)reg);
    }

    private static bool TryEvaluate(string token, Dictionary<string, ushort> symbols, out ushort value)
    {
        if (TryParseLiteral(token, out value))
        {
            return true;
        }

        if (symbols.TryGetValue(token, out value))
        {
            return true;
        }

        if (token.StartsWith("-", StringComparison.Ordinal))
        {
            if (TryParseLiteral(token.Substring(1), out ushort positive))
            {
                value = unchecked((ushort)(-(short)positive));
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static bool TryEvaluateWide(string token, Dictionary<string, ushort> symbols, int wordSize, out ulong value)
    {
        if (TryParseWideLiteral(token, out value))
        {
            return true;
        }

        if (symbols.TryGetValue(token, out ushort symValue))
        {
            value = symValue;
            return true;
        }

        if (token.StartsWith("-", StringComparison.Ordinal))
        {
            if (TryParseWideLiteral(token.Substring(1), out ulong positive))
            {
                long signed = -(long)positive;
                value = unchecked((ulong)signed);
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static IEnumerable<ushort> ExpandWideValue(ulong value, int wordSize)
    {
        int totalBytes = wordSize * 2;
        for (int offset = totalBytes - 2; offset >= 0; offset -= 2)
        {
            ushort word = (ushort)((value >> (offset * 8)) & 0xFFFF);
            yield return word;
        }
    }

    private static void EmitWord(SortedDictionary<ushort, byte> output, ushort address, ushort word)
    {
        output[address] = (byte)(word >> 8);
        output[(ushort)(address + 1)] = (byte)(word & 0xFF);
    }

    private static string FormatListingLine(ushort? address, IReadOnlyList<ushort> words, string source)
    {
        string trimmedSource = source.TrimEnd();
        if (address == null)
        {
            return trimmedSource;
        }

        string addrText = FormatOctal(address.Value);
        string dataText = string.Join(" ", words.Select(FormatOctal));
        if (string.IsNullOrEmpty(dataText))
        {
            return $"{addrText}  {trimmedSource}";
        }
        return $"{addrText} {dataText}  {trimmedSource}";
    }

    private static string FormatSymbols(Dictionary<string, ushort> symbols)
    {
        var lines = symbols.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => $"{pair.Key} {FormatOctal(pair.Value)}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatOctal(ushort value)
    {
        return Convert.ToString(value, 8).PadLeft(6, '0');
    }

    private static bool TryParseLiteral(string token, out ushort value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(token)) return false;

        string digits = token;
        char prefix = token[0];
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

        return TryParseOctal(digits, out value);
    }

    private static bool TryParseWideLiteral(string token, out ulong value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(token)) return false;

        string digits = token;
        char prefix = token[0];
        if (prefix == '#' || prefix == '$' || prefix == '>')
        {
            digits = token.Substring(1);
        }

        if (string.IsNullOrWhiteSpace(digits)) return false;

        if (prefix == '#')
        {
            return ulong.TryParse(digits, out value);
        }

        if (prefix == '$' || prefix == '>')
        {
            return ulong.TryParse(digits, System.Globalization.NumberStyles.HexNumber, null, out value);
        }

        return TryParseOctalWide(digits, out value);
    }

    private static bool TryParseOctal(string text, out ushort value)
    {
        value = 0;
        foreach (char ch in text)
        {
            if (ch < '0' || ch > '7') return false;
            int digit = ch - '0';
            int next = (value * 8) + digit;
            if (next > ushort.MaxValue) return false;
            value = (ushort)next;
        }
        return true;
    }

    private static bool TryParseOctalWide(string text, out ulong value)
    {
        value = 0;
        foreach (char ch in text)
        {
            if (ch < '0' || ch > '7') return false;
            int digit = ch - '0';
            ulong next = (value * 8) + (ulong)digit;
            value = next;
        }
        return true;
    }

    private static ParsedLine ParseLine(string line, int lineNumber)
    {
        string stripped = StripComment(line);
        if (string.IsNullOrWhiteSpace(stripped))
        {
            return ParsedLine.Empty(lineNumber, line);
        }

        string trimmed = stripped.Trim();
        string[] tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return ParsedLine.Empty(lineNumber, line);
        }

        int index = 0;
        string? label = null;
        string token = tokens[index];
        if (token.EndsWith(":", StringComparison.Ordinal))
        {
            label = token.TrimEnd(':');
            index++;
        }
        else if (!IsDirectiveOrMnemonic(token))
        {
            label = token;
            index++;
        }

        if (index >= tokens.Length)
        {
            return new ParsedLine(lineNumber, line, label, null, string.Empty);
        }

        string mnemonic = tokens[index];
        index++;
        string operandText = index < tokens.Length ? string.Join(" ", tokens.Skip(index)) : string.Empty;

        return new ParsedLine(lineNumber, line, label, mnemonic, operandText);
    }

    private static string StripComment(string line)
    {
        int commentIndex = line.IndexOf(';');
        if (commentIndex < 0)
        {
            return line;
        }

        string upper = line.ToUpperInvariant();
        int txtIndex = upper.IndexOf("TXT", StringComparison.Ordinal);
        if (txtIndex >= 0)
        {
            int firstSlash = line.IndexOf('/', txtIndex);
            if (firstSlash >= 0)
            {
                int secondSlash = line.IndexOf('/', firstSlash + 1);
                if (secondSlash >= 0 && commentIndex > firstSlash && commentIndex < secondSlash)
                {
                    commentIndex = line.IndexOf(';', secondSlash + 1);
                }
            }
        }

        return commentIndex >= 0 ? line.Substring(0, commentIndex) : line;
    }

    private static bool IsDirectiveOrMnemonic(string token)
    {
        return Directives.Contains(token) ||
               ImmediateOpcodes.ContainsKey(token) ||
               Format2Opcodes.ContainsKey(token) ||
               SingleOperandOpcodes.ContainsKey(token) ||
               JumpOpcodes.ContainsKey(token);
    }

    private readonly record struct Operand(AddressMode Mode, ushort Register);

    private readonly record struct ParsedLine(int LineNumber, string Original, string? Label, string? Mnemonic, string OperandText)
    {
        public bool IsEmpty => Label == null && Mnemonic == null && string.IsNullOrWhiteSpace(OperandText);

        public static ParsedLine Empty(int lineNumber, string original)
        {
            return new ParsedLine(lineNumber, original, null, null, string.Empty);
        }

    }

    private enum AddressMode : ushort
    {
        Register = 0,
        Indirect = 1
    }
}

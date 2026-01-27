using System.Text;

namespace tawny;

public sealed class Assembler
{
    private static readonly HashSet<string> Directives = new(StringComparer.OrdinalIgnoreCase)
    {
        "BSS",
        "EQU",
        "ORG",
        "RORG",
        "TITL",
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
        { "MOVB", 0xD000 }
    };

    private static readonly Dictionary<string, int> RegDestOpcodes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "SZC", 0x04 },
        { "SZCB", 0x05 },
        { "S", 0x06 },
        { "SB", 0x07 },
        { "C", 0x08 },
        { "CB", 0x09 },
        { "A", 0x0A },
        { "AB", 0x0B },
        { "SOC", 0x0E },
        { "SOCB", 0x0F }
    };

    private static readonly Dictionary<string, ushort> SingleOperandOpcodes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "BLWP", 0x0400 },
        { "CLR", 0x04C0 },
        { "NEG", 0x0500 },
        { "INV", 0x0540 },
        { "INC", 0x0580 },
        { "INCT", 0x05C0 },
        { "DEC", 0x0600 },
        { "DECT", 0x0640 },
        { "SWPB", 0x06C0 },
        { "SETO", 0x0700 },
        { "ABS", 0x0740 }
    };

    private static readonly Dictionary<string, ushort> ImpliedOpcodes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "IDLE", 0x0340 },
        { "RTWP", 0x0380 }
    };

    private static readonly Dictionary<string, ushort> Immediate4Opcodes = new(StringComparer.OrdinalIgnoreCase)
    {
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

            if (line.Mnemonic != null && string.Equals(line.Mnemonic, "TITL", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (line.Mnemonic != null && string.Equals(line.Mnemonic, "EQU", StringComparison.OrdinalIgnoreCase))
            {
                if (line.Label == null)
                {
                    errors.Add($"Line {line.LineNumber}: EQU requires a label.");
                    continue;
                }

                if (symbols.ContainsKey(line.Label))
                {
                    errors.Add($"Line {line.LineNumber}: Duplicate label '{line.Label}'.");
                    continue;
                }

                if (!TryEvaluate(line.OperandText, symbols, out ushort equValue))
                {
                    errors.Add($"Line {line.LineNumber}: Invalid EQU value '{line.OperandText}'.");
                    continue;
                }

                symbols[line.Label] = equValue;
                continue;
            }

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

            if (string.Equals(line.Mnemonic, "BSS", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryEvaluate(line.OperandText, symbols, out ushort bssBytes))
                {
                    errors.Add($"Line {line.LineNumber}: Invalid BSS size '{line.OperandText}'.");
                }
                else
                {
                    locationCounter = (ushort)(locationCounter + bssBytes);
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

            if (string.Equals(line.Mnemonic, "LIMI", StringComparison.OrdinalIgnoreCase))
            {
                locationCounter = (ushort)(locationCounter + 4);
                continue;
            }

            if (Immediate4Opcodes.ContainsKey(line.Mnemonic))
            {
                locationCounter = (ushort)(locationCounter + 2);
                continue;
            }

            if (ImpliedOpcodes.ContainsKey(line.Mnemonic))
            {
                locationCounter = (ushort)(locationCounter + 2);
                continue;
            }

            if (Format2Opcodes.ContainsKey(line.Mnemonic))
            {
                var operands = SplitOperands(line.OperandText);
                if (operands.Count == 2)
                {
                    Operand src = ParseOperand(operands[0], line.LineNumber, symbols, true);
                    Operand dest = ParseOperand(operands[1], line.LineNumber, symbols, true);
                    int words = 1 + (src.HasExtraWord ? 1 : 0) + (dest.HasExtraWord ? 1 : 0);
                    locationCounter = (ushort)(locationCounter + (words * 2));
                }
                else
                {
                    errors.Add($"Line {line.LineNumber}: Expected src, dest for {line.Mnemonic}.");
                }
                continue;
            }

            if (RegDestOpcodes.ContainsKey(line.Mnemonic))
            {
                var operands = SplitOperands(line.OperandText);
                if (operands.Count == 2)
                {
                    Operand src = ParseOperand(operands[0], line.LineNumber, symbols, true);
                    int words = 1 + (src.HasExtraWord ? 1 : 0);
                    locationCounter = (ushort)(locationCounter + (words * 2));
                }
                else
                {
                    errors.Add($"Line {line.LineNumber}: Expected src, dest for {line.Mnemonic}.");
                }
                continue;
            }

            if (SingleOperandOpcodes.ContainsKey(line.Mnemonic))
            {
                var operands = SplitOperands(line.OperandText);
                if (operands.Count == 1)
                {
                    Operand operand = ParseOperand(operands[0], line.LineNumber, symbols, true);
                    int words = 1 + (operand.HasExtraWord ? 1 : 0);
                    locationCounter = (ushort)(locationCounter + (words * 2));
                }
                else
                {
                    errors.Add($"Line {line.LineNumber}: Expected single operand for {line.Mnemonic}.");
                }
                continue;
            }

            if (JumpOpcodes.ContainsKey(line.Mnemonic))
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
                if (!IsCommentOnlyLine(line.Original))
                {
                    listingLines.Add(string.Empty);
                }
                continue;
            }

            if (line.Mnemonic == null)
            {
                listingLines.Add(FormatListingLine(null, Array.Empty<ushort>(), line.Original));
                continue;
            }

            if (string.Equals(line.Mnemonic, "TITL", StringComparison.OrdinalIgnoreCase))
            {
                listingLines.Add(FormatListingLine(null, Array.Empty<ushort>(), line.Original));
                continue;
            }

            if (string.Equals(line.Mnemonic, "EQU", StringComparison.OrdinalIgnoreCase))
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

            if (string.Equals(line.Mnemonic, "BSS", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryEvaluate(line.OperandText, symbols, out ushort bssBytes))
                {
                    throw new InvalidOperationException($"Line {line.LineNumber}: Invalid BSS size '{line.OperandText}'.");
                }
                ushort startAddress = locationCounter;
                locationCounter = (ushort)(locationCounter + bssBytes);
                listingLines.Add(FormatListingLine(startAddress, Array.Empty<ushort>(), line.Original));
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

            if (string.Equals(line.Mnemonic, "LIMI", StringComparison.OrdinalIgnoreCase))
            {
                var operands = SplitOperands(line.OperandText);
                if (operands.Count != 1)
                {
                    throw new InvalidOperationException($"Line {line.LineNumber}: Expected immediate for LIMI.");
                }

                if (!TryEvaluate(operands[0], symbols, out ushort immediateValue))
                {
                    throw new InvalidOperationException($"Line {line.LineNumber}: Invalid immediate '{operands[0]}'.");
                }

                ushort instruction = 0x0300;
                EmitWord(outputBytes, locationCounter, instruction);
                EmitWord(outputBytes, (ushort)(locationCounter + 2), immediateValue);
                listingLines.Add(FormatListingLine(locationCounter, new[] { instruction, immediateValue }, line.Original));
                locationCounter += 4;
                continue;
            }

            if (Immediate4Opcodes.TryGetValue(line.Mnemonic, out ushort immediate4Opcode))
            {
                var operands = SplitOperands(line.OperandText);
                if (operands.Count != 1)
                {
                    throw new InvalidOperationException($"Line {line.LineNumber}: Expected immediate for {line.Mnemonic}.");
                }

                if (!TryEvaluate(operands[0], symbols, out ushort imm4Value) || imm4Value > 0xF)
                {
                    throw new InvalidOperationException($"Line {line.LineNumber}: Invalid immediate '{operands[0]}'.");
                }

                ushort instruction = (ushort)(immediate4Opcode | imm4Value);
                EmitWord(outputBytes, locationCounter, instruction);
                listingLines.Add(FormatListingLine(locationCounter, new[] { instruction }, line.Original));
                locationCounter += 2;
                continue;
            }

            if (ImpliedOpcodes.TryGetValue(line.Mnemonic, out ushort impliedOpcode))
            {
                if (!string.IsNullOrWhiteSpace(line.OperandText))
                {
                    throw new InvalidOperationException($"Line {line.LineNumber}: {line.Mnemonic} does not take operands.");
                }

                EmitWord(outputBytes, locationCounter, impliedOpcode);
                listingLines.Add(FormatListingLine(locationCounter, new[] { impliedOpcode }, line.Original));
                locationCounter += 2;
                continue;
            }

            if (Format2Opcodes.TryGetValue(line.Mnemonic, out ushort format2Opcode))
            {
                var operands = SplitOperands(line.OperandText);
                if (operands.Count != 2)
                {
                    throw new InvalidOperationException($"Line {line.LineNumber}: Expected src, dest for {line.Mnemonic}.");
                }

                var src = ParseOperand(operands[0], line.LineNumber, symbols, false);
                var dest = ParseOperand(operands[1], line.LineNumber, symbols, false);

                ushort instruction = (ushort)(format2Opcode |
                                             ((ushort)dest.Mode << 10) |
                                             (dest.Register << 6) |
                                             ((ushort)src.Mode << 4) |
                                             src.Register);

                EmitWord(outputBytes, locationCounter, instruction);
                var words = new List<ushort> { instruction };
                locationCounter += 2;
                if (src.HasExtraWord)
                {
                    EmitWord(outputBytes, locationCounter, src.ExtraWord);
                    words.Add(src.ExtraWord);
                    locationCounter += 2;
                }
                if (dest.HasExtraWord)
                {
                    EmitWord(outputBytes, locationCounter, dest.ExtraWord);
                    words.Add(dest.ExtraWord);
                    locationCounter += 2;
                }
                listingLines.Add(FormatListingLine((ushort)(locationCounter - (words.Count * 2)), words.ToArray(), line.Original));
                continue;
            }

            if (RegDestOpcodes.TryGetValue(line.Mnemonic, out int regDestOpcode))
            {
                var operands = SplitOperands(line.OperandText);
                if (operands.Count != 2)
                {
                    throw new InvalidOperationException($"Line {line.LineNumber}: Expected src, dest for {line.Mnemonic}.");
                }

                Operand src = ParseOperand(operands[0], line.LineNumber, symbols, false);
                int destReg = ParseRegister(operands[1], line.LineNumber);
                ushort instruction = (ushort)((regDestOpcode << 10) |
                                              (destReg << 6) |
                                              ((ushort)src.Mode << 4) |
                                              src.Register);

                EmitWord(outputBytes, locationCounter, instruction);
                var words = new List<ushort> { instruction };
                locationCounter += 2;
                if (src.HasExtraWord)
                {
                    EmitWord(outputBytes, locationCounter, src.ExtraWord);
                    words.Add(src.ExtraWord);
                    locationCounter += 2;
                }
                listingLines.Add(FormatListingLine((ushort)(locationCounter - (words.Count * 2)), words.ToArray(), line.Original));
                continue;
            }

            if (SingleOperandOpcodes.TryGetValue(line.Mnemonic, out ushort singleOpcode))
            {
                var operands = SplitOperands(line.OperandText);
                if (operands.Count != 1)
                {
                    throw new InvalidOperationException($"Line {line.LineNumber}: Expected single operand for {line.Mnemonic}.");
                }

                Operand operand = ParseOperand(operands[0], line.LineNumber, symbols, false);
                ushort instruction = (ushort)(singleOpcode | ((ushort)operand.Mode << 4) | operand.Register);
                EmitWord(outputBytes, locationCounter, instruction);
                var words = new List<ushort> { instruction };
                locationCounter += 2;
                if (operand.HasExtraWord)
                {
                    EmitWord(outputBytes, locationCounter, operand.ExtraWord);
                    words.Add(operand.ExtraWord);
                    locationCounter += 2;
                }
                listingLines.Add(FormatListingLine((ushort)(locationCounter - (words.Count * 2)), words.ToArray(), line.Original));
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

    private static Operand ParseOperand(string token, int lineNumber, Dictionary<string, ushort> symbols, bool allowUnresolved)
    {
        string trimmed = token.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException($"Line {lineNumber}: Missing operand.");
        }

        if (trimmed.StartsWith("*", StringComparison.Ordinal))
        {
            string inner = trimmed.Substring(1).Trim();
            bool autoIncrement = inner.EndsWith("+", StringComparison.Ordinal);
            if (autoIncrement)
            {
                inner = inner.Substring(0, inner.Length - 1).Trim();
            }

            int reg = ParseRegister(inner, lineNumber);
            return new Operand(autoIncrement ? AddressMode.AutoIncrement : AddressMode.Indirect, (ushort)reg, false, 0);
        }

        bool explicitAt = trimmed.StartsWith("@", StringComparison.Ordinal);
        string core = explicitAt ? trimmed.Substring(1).Trim() : trimmed;

        int parenIndex = core.IndexOf('(');
        if (parenIndex >= 0)
        {
            if (!core.EndsWith(")", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Line {lineNumber}: Invalid indexed operand '{token}'.");
            }

            string dispText = core.Substring(0, parenIndex).Trim();
            string regText = core.Substring(parenIndex + 1, core.Length - parenIndex - 2).Trim();
            if (string.IsNullOrWhiteSpace(regText))
            {
                throw new InvalidOperationException($"Line {lineNumber}: Missing index register in '{token}'.");
            }

            ushort displacement = 0;
            if (!string.IsNullOrWhiteSpace(dispText) && !TryEvaluate(dispText, symbols, out displacement))
            {
                if (!allowUnresolved)
                {
                    throw new InvalidOperationException($"Line {lineNumber}: Invalid displacement '{dispText}'.");
                }
            }

            int reg = ParseRegister(regText, lineNumber);
            return new Operand(AddressMode.Indexed, (ushort)reg, true, displacement);
        }

        if (explicitAt)
        {
            if (!TryEvaluate(core, symbols, out ushort displacement))
            {
                if (!allowUnresolved)
                {
                    throw new InvalidOperationException($"Line {lineNumber}: Invalid symbol '{core}'.");
                }
                displacement = 0;
            }
            return new Operand(AddressMode.Indexed, 0, true, displacement);
        }

        int register = ParseRegister(core, lineNumber);
        return new Operand(AddressMode.Register, (ushort)register, false, 0);
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

        string addrText = FormatHex(address.Value);
        string dataText = string.Join(" ", words.Select(FormatHex));
        if (string.IsNullOrEmpty(dataText))
        {
            return $"{addrText}  {trimmedSource}";
        }
        return $"{addrText} {dataText}  {trimmedSource}";
    }

    private static string FormatSymbols(Dictionary<string, ushort> symbols)
    {
        var lines = symbols.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => $"{pair.Key} {FormatHex(pair.Value)}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatHex(ushort value)
    {
        return Convert.ToString(value, 16).PadLeft(4, '0').ToUpperInvariant();
    }

    private static bool TryParseLiteral(string token, out ushort value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(token)) return false;

        string digits = token;
        char prefix = token[0];
        if (prefix == '#' || prefix == '$' || prefix == '>' || prefix == '%')
        {
            digits = token.Substring(1);
        }
        else
        {
            prefix = '\0';
        }

        if (string.IsNullOrWhiteSpace(digits)) return false;

        if (prefix == '#')
        {
            return ushort.TryParse(digits, out value);
        }

        if (prefix == '%')
        {
            return TryParseOctal(digits, out value);
        }

        if (prefix == '$' || prefix == '>' || prefix == '\0')
        {
            return ushort.TryParse(digits, System.Globalization.NumberStyles.HexNumber, null, out value);
        }

        return false;
    }

    private static bool TryParseWideLiteral(string token, out ulong value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(token)) return false;

        string digits = token;
        char prefix = token[0];
        if (prefix == '#' || prefix == '$' || prefix == '>' || prefix == '%')
        {
            digits = token.Substring(1);
        }
        else
        {
            prefix = '\0';
        }

        if (string.IsNullOrWhiteSpace(digits)) return false;

        if (prefix == '#')
        {
            return ulong.TryParse(digits, out value);
        }

        if (prefix == '%')
        {
            return TryParseOctalWide(digits, out value);
        }

        if (prefix == '$' || prefix == '>' || prefix == '\0')
        {
            return ulong.TryParse(digits, System.Globalization.NumberStyles.HexNumber, null, out value);
        }

        return false;
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
        if (!string.IsNullOrEmpty(line) && line[0] == '*')
        {
            return ParsedLine.Empty(lineNumber, line);
        }

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

    private static bool IsCommentOnlyLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return false;

        if (line[0] == '*') return true;

        if (line.IndexOf(';') < 0) return false;

        string stripped = StripComment(line);
        return string.IsNullOrWhiteSpace(stripped);
    }

    private static bool IsDirectiveOrMnemonic(string token)
    {
        return Directives.Contains(token) ||
               ImmediateOpcodes.ContainsKey(token) ||
               Immediate4Opcodes.ContainsKey(token) ||
               string.Equals(token, "LIMI", StringComparison.OrdinalIgnoreCase) ||
               Format2Opcodes.ContainsKey(token) ||
               RegDestOpcodes.ContainsKey(token) ||
               SingleOperandOpcodes.ContainsKey(token) ||
               JumpOpcodes.ContainsKey(token) ||
               ImpliedOpcodes.ContainsKey(token);
    }

    private readonly record struct Operand(AddressMode Mode, ushort Register, bool HasExtraWord, ushort ExtraWord);

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
        Indirect = 1,
        Indexed = 2,
        AutoIncrement = 3
    }
}

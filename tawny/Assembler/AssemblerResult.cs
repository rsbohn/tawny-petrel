namespace tawny;

public sealed class AssemblerResult
{
    public AssemblerResult(string listingText, string symbolText, string srecText, IReadOnlyDictionary<ushort, byte> bytes)
    {
        ListingText = listingText;
        SymbolText = symbolText;
        SrecText = srecText;
        Bytes = bytes;
    }

    public string ListingText { get; }
    public string SymbolText { get; }
    public string SrecText { get; }
    public IReadOnlyDictionary<ushort, byte> Bytes { get; }
}

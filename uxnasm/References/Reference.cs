namespace UxnAsm.References;

internal readonly struct Reference
{
    public string Name { get; init; }
    public char Rune { get; init; }
    public ushort Address { get; init; }
}
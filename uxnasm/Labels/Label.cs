namespace UxnAsm.Labels;

internal readonly struct Label
{
    public string Name { get; init; }
    public ushort Address { get; init; }
    public int ReferenceCount { get; init; }
}

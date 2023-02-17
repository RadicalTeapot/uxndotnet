namespace UxnAsm.Marcos;

internal readonly struct Macro
{
    public string Name { get; init; }
    public string[] Items { get; init; }
    public byte Length { get; init; }
}

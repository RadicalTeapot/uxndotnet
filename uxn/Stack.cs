namespace uxn;

public class Stack
{
    public byte[] Data { get; }
    public uint DataPointer
    {
        get { return _dataPointer + _pointerOffset; }
        set { _dataPointer = value; }
    }

    private uint _dataPointer;
    private uint _pointerOffset;

    public Stack(byte[] memory, uint offset)
    {
        Data = memory;
        _pointerOffset = offset;
        DataPointer = 0;
    }
}
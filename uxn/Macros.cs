namespace uxn;

internal class UxnMacros
{
    public Stack SourceStack;
    public Stack DestinationStack;
    public ushort ProgramCounter;
        
    private byte _instruction;
    private bool _shortMode;
    private uint _sourcePointer;     // Stack pointer
    private uint _originalSourcePointer;
    
    private readonly BaseUxn _baseUxn;

    public UxnMacros(ushort programCounter, BaseUxn baseUxn)
    {
        ProgramCounter = programCounter;
        _baseUxn = baseUxn;
        SetInstruction(0);
    }

    public void SetInstruction(byte instruction)
    {
        _instruction = instruction;
        SetShortMode();
        SetReturnMode();
        SetKeepMode();
    }

    private void SetShortMode()
    {
        _shortMode = (_instruction & 0x20) == 0x20;
    }

    /* Return Mode
     * Return and Working stack are swapped if in return mode
     */
    private void SetReturnMode()
    {
        if ((_instruction & 0x40) == 0x40)
        {
            SourceStack = _baseUxn.ReturnStack; 
            DestinationStack = _baseUxn.WorkingStack;
        }
        else
        {
            SourceStack = _baseUxn.WorkingStack; 
            DestinationStack = _baseUxn.ReturnStack;
        }
    }

    /* Keep Mode
     * A copy the of src pointer is used when in keep mode, so the address pointed at is not modified
     */
    private void SetKeepMode()
    {
        if ((_instruction & 0x80) == 0x80)
        {
            _originalSourcePointer = SourceStack.DataPointer; 	// TODO this should be a ref to DataPointer
            _sourcePointer = _originalSourcePointer;		// This is a copy of the value
        }
        else
        {
            _sourcePointer = SourceStack.DataPointer;	// TODO this should be a ref to DataPointer so it can be modified when sp is set
        }
    }

    public void Halt(byte errorCode)
    {
        _baseUxn.Halt(_instruction, errorCode, (ushort)(ProgramCounter - 1));
    }

    public void Jump(ushort address)
    {
        if (_shortMode) 
            ProgramCounter = address;
        else 
            ProgramCounter = (ushort)(ProgramCounter + (sbyte)address);
    }

    public void Push8(Stack stack, byte value)
    {
        if (stack.DataPointer == 0xff)
            Halt(2);       // overflow
        stack.Data[stack.DataPointer++] = value;
    }

    public void Push16(Stack stack, ushort value)
    {
        var address = stack.DataPointer;
        if (stack.DataPointer >= 0xfe) 
            Halt(2);   // overflow
        stack.Data[address] = (byte)((value >> 8) & 0xFF);
        stack.Data[address+1] = (byte)(value & 0xFF);
        stack.DataPointer = address + 2;
    }

    public void Push(Stack stack, ushort value)
    {
        if (_shortMode)
            Push16(stack, value);
        else
            Push8(stack, (byte)(value & 0xFF));
    }

    public byte Pop8()
    {
        var address = _sourcePointer;
        if (address == 0)
            Halt(1);   // underflow
        
        _sourcePointer = --address;
        return SourceStack.Data[address];
    }

    public ushort Pop16()
    {
        var address = _sourcePointer;
        if (address <= 1)
            Halt(1);   // underflow
        
        _sourcePointer = address - 2;
        return (ushort)((SourceStack.Data[address - 2] << 8) + SourceStack.Data[address - 1]);
    }

    public ushort Pop()
    {
        return _shortMode ? Pop16() : Pop8();
    }

    public void Poke(uint address, ushort value)
    {
        if (_shortMode)
        {
            _baseUxn.Ram[address] = (byte)((value >> 8) & 0xFF);
            _baseUxn.Ram[address + 1] = (byte)(value & 0xFF);
        }
        else
        {
            _baseUxn.Ram[address] = (byte)(value & 0xFF);
        }
    }

    public ushort Peek16(uint address)
    {
        return (ushort)((_baseUxn.Ram[address] << 8) + _baseUxn.Ram[address + 1]);
    }

    public ushort Peek(uint address)
    {
        return _shortMode ? Peek16(address) : _baseUxn.Ram[address];
    }

    public ushort DeviceRead(byte address)
    {
        ushort value = _baseUxn.DeviceIn(address);
        if (_shortMode)
            value = (ushort)((value << 8) + _baseUxn.DeviceIn((byte)(address + 1)));
        return value;
    }

    public void DeviceWrite(byte address, ushort value)
    {
        if (_shortMode)
        {
            _baseUxn.DeviceOut(address, (byte)(value >> 8 & 0xFF));
            _baseUxn.DeviceOut((byte)(address+1), (byte)(value & 0xFF));
        }
        else
        {
            _baseUxn.DeviceOut(address, (byte)(value & 0xFF));
        }
    }
}
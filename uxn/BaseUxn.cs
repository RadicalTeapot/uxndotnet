namespace uxn;

public interface IUxn
{
    public byte DeviceIn(byte address);
    public void DeviceOut(byte address, byte value);
    public void Halt(byte instruction, byte errorCode, ushort address);
    public void Eval(ushort programCounter);
}

public abstract class BaseUxn: IUxn
{
    private const uint PageProgram = 0x100; // 256
    private const uint RamPageSize = 0x10000; // 65536
    private const uint RamPages = 0x10; // 16
    private const uint DevPtrOffset = 0xF0200;
    public byte[] Ram { get; }
    public byte[] Dev;
    public Stack WorkingStack;
    public Stack ReturnStack;

    public BaseUxn()
    {
        var ram = InitializeRam();
        WorkingStack = new Stack(ram, 0xF0000); // Last ram page
        ReturnStack = new Stack(ram, 0xF0100);
        Dev = ram;
        Ram = ram;
    }

    private static byte[] InitializeRam()
    {
        var ram = new byte[RamPageSize * RamPages];
        for (var i = 0; i < ram.Length; i++)
            ram[i] = 0;
        return ram;
    }

    public abstract byte DeviceIn(byte address);
    public abstract void DeviceOut(byte address, byte value);
    public abstract void Halt(byte instruction, byte errorCode, ushort address);

    public void Eval(ushort programCounter)
    {
        var macros = new UxnMacros(programCounter, this);
	    
        if (macros.ProgramCounter == 0 || Dev[DevPtrOffset + 0x0f] == 0)
            return;

        while (true)
        {
            var instruction = Ram[macros.ProgramCounter++];
            macros.SetInstruction(instruction);
            
            var opcode = (byte)(instruction & 0x1f);
            switch(opcode - ((opcode == 0 ? 1 : 0) * (instruction >> 5))) {
			    
                /* Literals/Calls */
                case -0x0: /* BRK */ 
                    return;
                case -0x1: /* JCI */
                {
                    var popped = macros.Pop8();
                    if (popped == 0)
                    {
                        macros.ProgramCounter += 2; 
                        break;
                    }
                    var peeked = macros.Peek16(macros.ProgramCounter);
                    macros.ProgramCounter = (ushort)(macros.ProgramCounter + peeked + 2);
                    break;
                }
                case -0x2: /* JMI */
                {
                    var peeked = macros.Peek16(macros.ProgramCounter); 
                    macros.ProgramCounter = (ushort)(macros.ProgramCounter + peeked + 2);
                    break;
                }
                case -0x3: /* JSI */
                {
                    macros.Push16(ReturnStack, (ushort)(macros.ProgramCounter + 2));
                    var peeked = macros.Peek16(macros.ProgramCounter); 
                    macros.ProgramCounter = (ushort)(macros.ProgramCounter + peeked + 2);
                    break;
                }
                case -0x4: /* LIT */
                case -0x6: /* LITr */
                {
                    macros.Push8(macros.SourceStack, Ram[macros.ProgramCounter++]);
                    break;
                }
                case -0x5: /* LIT2 */
                case -0x7: /* LIT2r */
                {
                    var peeked = macros.Peek16(macros.ProgramCounter);
                    macros.Push16(macros.SourceStack, peeked);
                    macros.ProgramCounter += 2; 
                    break;
                }
			    
                /* ALU */
                case 0x01: /* INC */
                {
                    var popped = macros.Pop();
                    macros.Push(macros.SourceStack, (ushort)(popped + 1));
                    break;
                }
                case 0x02: /* POP */
                {
                    macros.Pop(); // Discard
                    break;
                }
                case 0x03: /* NIP */
                {
                    var first = macros.Pop();
                    macros.Pop();	// Discard
                    macros.Push(macros.SourceStack, first);
                    break;
                }
                case 0x04: /* SWP */
                {
                    var first = macros.Pop();
                    var second= macros.Pop();
                    macros.Push(macros.SourceStack, first);
                    macros.Push(macros.SourceStack, second); 
                    break;
                }
                case 0x05: /* ROT */
                {
                    var first = macros.Pop();
                    var second = macros.Pop();
                    var third =macros.Pop();
                    macros.Push(macros.SourceStack, second);
                    macros.Push(macros.SourceStack, first);
                    macros.Push(macros.SourceStack, third);
                    break;
                }
                case 0x06: /* DUP */
                {
                    var value = macros.Pop();
                    macros.Push(macros.SourceStack, value);
                    macros.Push(macros.SourceStack, value);
                    break;
                }
                case 0x07: /* OVR */
                {
                    var first = macros.Pop();
                    var second = macros.Pop();
                    macros.Push(macros.SourceStack, second);
                    macros.Push(macros.SourceStack, first);
                    macros.Push(macros.SourceStack, second); 
                    break;
                }
                case 0x08: /* EQU */
                {
                    var first = macros.Pop();
                    var second = macros.Pop();
                    macros.Push8(macros.SourceStack, (byte)(first == second ? 1 : 0));
                    break;
                }
                case 0x09: /* NEQ */
                {
                    var first = macros.Pop();
                    var second = macros.Pop();
                    macros.Push8(macros.SourceStack, (byte)(first != second ? 1 : 0));
                    break;
                }
                case 0x0a: /* GTH */
                {
                    var first = macros.Pop();
                    var second = macros.Pop();
                    macros.Push8(macros.SourceStack, (byte)(first > second ? 1 : 0));
                    break;
                }
                case 0x0b: /* LTH */
                {
                    var first = macros.Pop();
                    var second = macros.Pop();
                    macros.Push8(macros.SourceStack, (byte)(first < second ? 1 : 0));
                    break;
                }
                case 0x0c: /* JMP */
                {
                    var address = macros.Pop(); 
                    macros.Jump(address); 
                    break;
                }
                case 0x0d: /* JCN */
                {
                    var address = macros.Pop();
                    var value = macros.Pop8();
                    if (value > 0) 
                        macros.Jump(address);
                    break;
                }
                case 0x0e: /* JSR */
                {
                    var address = macros.Pop();
                    macros.Push16(macros.DestinationStack, macros.ProgramCounter);
                    macros.Jump(address);
                    break;
                }
                case 0x0f: /* STH */
                {
                    var address = macros.Pop();
                    macros.Push(macros.DestinationStack, address);
                    break;
                }
                case 0x10: /* LDZ */
                {
                    var address = macros.Pop8();
                    var value = macros.Peek(address);
                    macros.Push(macros.SourceStack, value);
                    break;
                }
                case 0x11: /* STZ */
                {
                    var address = macros.Pop8();
                    var value = macros.Pop();
                    macros.Poke(address, value);
                    break;
                }
                case 0x12: /* LDR */
                {
                    var offset = (sbyte)macros.Pop8();
                    var address = (uint)(macros.ProgramCounter + offset);
                    var value = macros.Peek(address);
                    macros.Push(macros.SourceStack, value);
                    break;
                }
                case 0x13: /* STR */
                {
                    var offset = (sbyte)macros.Pop8();
                    var value = macros.Pop();
                    var address = (uint)(macros.ProgramCounter + offset);
                    macros.Poke(address, value);
                    break;
                }
                case 0x14: /* LDA */
                {
                    var address = macros.Pop16();
                    var value = macros.Peek(address);
                    macros.Push(macros.SourceStack, value);
                    break;
                }
                case 0x15: /* STA */
                {
                    var address = macros.Pop16();
                    var value = macros.Pop();
                    macros.Poke(address, value);
                    break;
                }
                case 0x16: /* DEI */
                {
                    var address = macros.Pop8();
                    var value = macros.DeviceRead(address);
                    macros.Push(macros.SourceStack, value);
                    break;
                }
                case 0x17: /* DEO */
                {
                    var address = macros.Pop8();
                    var value = macros.Pop();
                    macros.DeviceWrite(address, value);
                    break;
                }
                case 0x18: /* ADD */
                {
                    var first = macros.Pop();
                    var second = macros.Pop();
                    macros.Push(macros.SourceStack, (ushort)(second + first));
                    break;
                }
                case 0x19: /* SUB */
                {
                    var first = macros.Pop();
                    var second = macros.Pop();
                    macros.Push(macros.SourceStack, (ushort)(second - first));
                    break;
                }
                case 0x1a: /* MUL */
                {
                    var first = macros.Pop();
                    var second = macros.Pop();
                    macros.Push(macros.SourceStack, (ushort)((second * first) & 0xFFFF));
                    break;
                }
                case 0x1b: /* DIV */
                {
                    var first = macros.Pop();
                    var second = macros.Pop();
                    if (first == 0) 
                        macros.Halt(3); // Divide by zero
                    macros.Push(macros.SourceStack, (ushort)(second / first));
                    break;
                }
                case 0x1c: /* AND */
                {
                    var first = macros.Pop();
                    var second = macros.Pop();
                    macros.Push(macros.SourceStack, (ushort)(second & first));
                    break;
                }
                case 0x1d: /* ORA */
                {
                    var first = macros.Pop();
                    var second = macros.Pop();
                    macros.Push(macros.SourceStack, (ushort)(second | first));
                    break;
                }
                case 0x1e: /* EOR */
                {
                    var first = macros.Pop();
                    var second = macros.Pop();
                    macros.Push(macros.SourceStack, (ushort)(second ^ first));
                    break;
                }
                case 0x1f: /* SFT */
                {
                    var first = macros.Pop8();
                    var second = macros.Pop();
                    macros.Push(macros.SourceStack, (ushort)((second >> (first & 0x0f)) << ((first & 0xf0) >> 4))); // Left shift using low nibble or high shift using high nibble
                    break;
                }
            }
        }
    }
}
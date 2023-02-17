using System.Text;
using UxnAsm.Exceptions;
using UxnAsm.Labels;
using UxnAsm.Marcos;
using UxnAsm.Parser;
using UxnAsm.References;
using UxnAsm.Utils;

namespace UxnAsm;

internal class UxnProgram
{
    private const int MaxReferencesCount = 0x1000;  // 4096
    private const int MaxMacroCount = 0x100;        // 256
    
    private const int Trim = 0x0100;                // 256
    private const int MaxProgramLength = 0xFFFF;    // 65535

    private const string DefaultScope = "on-reset";
    
    private ushort _macrosIndex;
    private ushort _referenceIndex;
    
    private readonly byte[] _data;
    private ushort _pointer;
    private uint _length;
    
    private string _scope;

    // TODO Make a macros container and references container and extract the relevant methods
    private readonly Macro[] _macros;
    private readonly Reference[] _references;

    private readonly LabelsContainer _labelsContainer;
    
    public UxnProgram()
    {
        _scope = "";
        
        _data = new byte[MaxProgramLength];
        _pointer = 0;
        
        _macrosIndex = 0;
        _macros = new Macro[MaxMacroCount];

        _labelsContainer = new LabelsContainer();
        
        _referenceIndex = 0;
        _references = new Reference[MaxReferencesCount];
    }

    // TODO Struct are value types, it we should return the ref to it instead
    private Macro? FindMacro(string name)
    {
        for (int i = 0; i < _macrosIndex; i++)
        {
            if (_macros[i].Name == name) return _macros[i];
        }

        return null;
    }

    private void MakeMacro(string name, FileReader reader)
    {
        AssertMacroNameIsValid(name);
        if (_macrosIndex == MaxMacroCount)
            throw new ProgramException("Macros limit exceeded", name);
        
        var items = new string[0x40];
        byte itemIndex = 0;
        while (true)
        {
            if (reader.ReadWord(out var word) == FileReader.Status.EndOfFileReached)
                throw new ParserException("Could not read next word", name);
            
            if (word[0] == '{') continue;
            if (word[0] == '}') break;
            if (word[0] == '%')
                throw new ParserException("Macro error", name);
            if (itemIndex >= items.Length)
                throw new ParserException("Macro size exceeded", name);
            items[itemIndex++] = word;
        }

        _macros[_macrosIndex++] = new Macro
        {
            Length = itemIndex,
            Name = name,
            Items = items
        };
    }
    
    private void AssertMacroNameIsValid(string name)
    {
        if (FindMacro(name) != null)
            throw new ParserException("Macro duplicate", name);
        
        if (StringUtils.StringIsHex(name) && name.Length % 2 == 0)
            throw new ParserException("Macro name is hex number", name);

        if (OpCodes.Find(name) != 0 || name == "BRK" || name.Length == 0)
            throw new ParserException("Macro name is invalid", name);
    }

    private void MakeReference(string scope, string label, ushort address)
    {
        if (_referenceIndex == MaxReferencesCount)
            throw new ProgramException("References limit exceeded", label);
        string name;
        if (label[1] == '&')
        {
            name = LabelsContainer.GetSubLabel(scope, label[2..]);
        }
        else
        {
            name = label[1..];
            var index = name.IndexOf('/');
            if (index > 0)
                _labelsContainer.IncrementLabelReferenceCount(name[..index]);
        }

        _references[_referenceIndex++] = new Reference
        {
            Address = address,
            Name = name,
            Rune = label[0]
        };
    }

    private void WriteByte(byte b)
    {
        if (_pointer < Trim)
            throw new ParserException("Writing in zero page", "");
        
        // Not needed as pointer is u16 so max value == MaxProgramLength
        // TODO Think about how to let user write till last byte but still prevent overflow (right now it's caught since after overflow user would write in zero page)
        // if (_pointer > MaxProgramLength)
        //     throw new ParserException("Writing after end of RAM", "");

        if (_pointer < _length)
            throw new ProgramException("Memory overwrite", "");

        _data[_pointer++] = b;
        _length = _pointer;
    }

    private void WriteOpCode(string opcode)
    {
        WriteByte(OpCodes.Find(opcode));
    }

    private void WriteShort(ushort s, bool lit)
    {
        if (lit)
            WriteByte(OpCodes.Find("LIT2"));
        WriteByte((byte)(s >> 8));
        WriteByte((byte)(s & 0xFF));
    }

    private void WriteLitByte(byte b)
    {
        WriteByte(OpCodes.Find("LIT"));
        WriteByte(b);
    }

    private void DoInclude(string filePath)
    {
        FileReader fileReader;
        try
        {
            fileReader = new FileReader(filePath);
        }
        catch (FileNotFoundException)
        {
            throw new ProgramException("Include missing", filePath);
        }

        while (fileReader.ReadWord(out var word) != FileReader.Status.EndOfFileReached)
            Parse(word, fileReader);
    }

    private void Parse(string word, FileReader fileReader)
    {
        if (word.Length >= 63)
            throw new ParserException("Invalid token", word);

        var wordWithoutFirstChar = word[1..];

        switch (word[0])
        {
            case '(':       // Comment
                if (word.Length != 1)
                    throw new ParserException("Malformed comment", word);
                var depth = 1;
                while (fileReader.ReadWord(out var nextWord) != FileReader.Status.EndOfFileReached)
                {
                    if (nextWord.Length != 1) continue;
                    
                    if (nextWord[0] == '(') depth++;
                    else if (nextWord[0] == ')' && --depth < 1) break;
                }
                break;
            case '~':       // Include
                DoInclude(wordWithoutFirstChar);
                break;
            case '%':       // Macro
                MakeMacro(wordWithoutFirstChar, fileReader);
                break;
            case '|':       // Pad absolute
                if (!StringUtils.StringIsHex(wordWithoutFirstChar))
                    throw new ParserException("Invalid padding", word);
                _pointer = StringUtils.HexStringAsShort(wordWithoutFirstChar);
                break;
            case '$':       // Pad relative
                if (!StringUtils.StringIsHex(wordWithoutFirstChar))
                    throw new ParserException("Invalid padding", word);
                _pointer += StringUtils.HexStringAsShort(wordWithoutFirstChar);
                break;
            case '@':       // Label
                _labelsContainer.MakeLabel(wordWithoutFirstChar, _pointer);
                _scope = wordWithoutFirstChar;
                break;
            case '&':       // Sub label
                var subLabel = LabelsContainer.GetSubLabel(_scope, wordWithoutFirstChar);
                _labelsContainer.MakeLabel(subLabel, _pointer);
                break;
            case '#':       // LIT hex
                var isHex = StringUtils.StringIsHex(wordWithoutFirstChar);
                switch (isHex)
                {
                    case true when word.Length == 3:
                        WriteLitByte(StringUtils.HexStringAsByte(wordWithoutFirstChar));
                        break;
                    case true when word.Length == 5:
                        WriteShort(StringUtils.HexStringAsShort(wordWithoutFirstChar), true);
                        break;
                    default:
                        throw new ParserException("Invalid hex literal", word);
                }
                break;
            case '_':       // Raw byte relative
                MakeReference(_scope, word, _pointer);
                WriteLitByte(0xFF);
                break;
            case ',':       // Literal byte relative
                MakeReference(_scope, word, (ushort)(_pointer+1));
                WriteLitByte(0xFF);
                break;
            case '-':       // Raw byte absolute
                MakeReference(_scope, word, _pointer);
                WriteLitByte(0xFF);
                break;
            case '.':       // Literal byte zero-page
                MakeReference(_scope, word, (ushort)(_pointer+1));
                WriteLitByte(0xFF);
                break;
            case ':':       // Raw short absolute
            case '=':
                MakeReference(_scope, word, _pointer);
                WriteShort(0xFFFF, false);
                break;
            case ';':       // Literal short absolute
                MakeReference(_scope, word, (ushort)(_pointer+1));
                WriteShort(0xFFFF, true);
                break;
            case '?':       // JCI
                MakeReference(_scope, word, (ushort)(_pointer+1));
                WriteByte(0x20);
                WriteShort(0xFFFF, false);
                break;
            case '!':       // JMI
                MakeReference(_scope, word, (ushort)(_pointer+1));
                WriteByte(0x40);
                WriteShort(0xFFFF, false);
                break;
            case '"':       // Raw string
                foreach (var c in word[1..])
                    WriteByte((byte)(c & 0xFF));
                break;
            default:
                if (word[0] is '[' or ']' && word.Length == 1) 
                    break;
                
                // Op code
                if (OpCodes.Find(word) != 0 || word[..Math.Min(word.Length, 3)] == "BRK")
                    WriteOpCode(word);
                // Raw byte
                else if (StringUtils.StringIsHex(word) && word.Length == 2)
                    WriteByte(StringUtils.HexStringAsByte(word));
                // Raw short
                else if (StringUtils.StringIsHex(word) && word.Length == 4)
                    WriteShort(StringUtils.HexStringAsShort(word), false);
                else
                {
                    // Macro
                    var macro = FindMacro(word);
                    if (macro != null)
                    {
                        for (var i = 0; i < macro.Value.Length; i++)
                            Parse(macro.Value.Items[i], fileReader);
                        break;
                    }
                    
                    // JSI
                    MakeReference(_scope, word[1..], (ushort)(_pointer+1));
                    WriteByte(0x60);
                    WriteShort(0xFFFF, false);
                }
                break;
        }
    }

    private void Resolve()
    {
        for (int i = 0; i < _referenceIndex; i++)
        {
            var reference = _references[i];
            int address;
            switch (reference.Rune)
            {
                case '_':       // Relative
                case ',':
                    try
                    {
                        address = _labelsContainer.GetLabel(reference.Name).Address;
                    }
                    catch (LabelException)
                    {
                        throw new ParserException("Unknown relative reference", reference.Name);
                    }
                    
                    address -= reference.Address + 2;
                    if ((sbyte)address != (sbyte)(byte)address)
                        throw new ParserException("Relative reference is too far", reference.Name);
                    
                    _data[reference.Address] = (byte)address;
                    _labelsContainer.IncrementLabelReferenceCount(reference.Name);
                    break;
                case '-':       // Zero page
                case '.':
                    try
                    {
                        address = _labelsContainer.GetLabel(reference.Name).Address;
                    }
                    catch (LabelException)
                    {
                        throw new ParserException("Unknown zero-page reference", reference.Name);
                    }
                    _data[reference.Address] = (byte)(address & 0xFF);
                    _labelsContainer.IncrementLabelReferenceCount(reference.Name);
                    break;
                case ':':       // Absolute
                case '=':
                case ';':
                    try
                    {
                        address = _labelsContainer.GetLabel(reference.Name).Address;
                    }
                    catch (LabelException)
                    {
                        throw new ParserException("Unknown absolute reference", reference.Name);
                    }
                    _data[reference.Address] = (byte)(address >> 8);
                    _data[reference.Address + 1] = (byte)(address & 0xFF);
                    _labelsContainer.IncrementLabelReferenceCount(reference.Name);
                    break;
                case '?':       // Absolute
                case '!':
                default:
                    try
                    {
                        address = _labelsContainer.GetLabel(reference.Name).Address;
                    }
                    catch (LabelException)
                    {
                        throw new ParserException("Unknown absolute reference", reference.Name);
                    }
                    address -= reference.Address + 2;
                    _data[reference.Address] = (byte)(address >> 8);
                    _data[reference.Address + 1] = (byte)(address & 0xFF);
                    _labelsContainer.IncrementLabelReferenceCount(reference.Name);
                    break;
            }
        }
    }

    private void Assemble(FileReader fileReader)
    {
        _pointer = Trim;
        _scope = DefaultScope;
        while (fileReader.ReadWord(out var word) != FileReader.Status.EndOfFileReached)
        {
            if (word.Length > 0x3D)
                throw new ParserException("Invalid token", word);
            Parse(word, fileReader);
        }

        Resolve();
    }

    private void Review(string filePath)
    {
        var unusedLabels = _labelsContainer.GetUnusedLabels();
        foreach (var label in unusedLabels)
            Console.WriteLine($"-- Unused label: {label.Name}");
            
        Console.WriteLine($"Assembled {filePath} in {_length - Trim} bytes " +
                          $"({Math.Round((_length - Trim) / 652.80, 2)}% used), {_labelsContainer.LabelCount} labels " +
                          $"and {_macrosIndex} macros");
    }

    private void WriteSym(string filePath)
    {
        if (filePath.Length > 0x60 - 5) return;
        filePath += ".sym";
        
        using var writer = new StreamWriter(filePath);
        for (int i = 0; i < _labelsContainer.LabelCount; i++)
        {
            var label = _labelsContainer.GetLabel(i);
            var highByte = (byte)(label.Address >> 8);
            var lowByte = (byte)(label.Address & 0xFF);
            writer.Write(highByte);
            writer.Write(lowByte);
            writer.Write(label.Name);
        }
    }

    public void Compile(string inputFilePath, string outputFilePath)
    {
        FileReader fileReader;
        try
        {
            fileReader = new FileReader(inputFilePath);
        }
        catch
        {
            throw new ProgramException("Could not read input file", inputFilePath);
        }
        Assemble(fileReader);
        if (_length <= Trim)
            throw new ProgramException("Assembly", "Output rom is empty");

        using var stream = File.Open(outputFilePath, FileMode.Create);
        using var writer = new BinaryWriter(stream, Encoding.ASCII);
        writer.Write(_data, Trim, (int)_length - Trim);
        
        Review(outputFilePath);
        WriteSym(outputFilePath);
    }
}
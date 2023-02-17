using UxnAsm.Exceptions;
using UxnAsm.Parser;
using UxnAsm.Utils;

namespace UxnAsm.Labels;

internal class LabelsContainer
{
    
    private const int MaxLabelLength = 0x40;        // 64
    private const int MaxLabelCount = 0x400;        // 1024

    public ushort LabelCount => _count;
    private ushort _count;
    private readonly Label[] _labels;

    public LabelsContainer()
    {
        _count = 0;
        _labels = new Label[MaxLabelCount];
    }
    
    private bool HasLabel(string name)
    {
        return _labels.Any(label => label.Name == name);
    }

    private int GetLabelIndex(string name)
    {
        for (int i = 0; i < _count; i++)
        {
            if (_labels[i].Name == name) return i;
        }

        throw new LabelException("Could not find label", name);
    }

    public void MakeLabel(string name, ushort address)
    {
        AssertLabelNameIsValid(name);
        if (_count == MaxLabelCount)
            throw new LabelException("Labels limit exceeded", name);
        
        _labels[_count++] = new Label
        {
            Address = address,
            ReferenceCount = 0,
            Name = name
        };
    }
    
    private void AssertLabelNameIsValid(string name)
    {
        if (HasLabel(name))
            throw new ParserException("Label duplicate", name);
        
        if (StringUtils.StringIsHex(name) && name.Length is 2 or 4)
            throw new ParserException("Label name is hex number", name);

        if (OpCodes.Find(name) != 0 || name == "BRK" || name.Length == 0)
            throw new ParserException("Label name is invalid", name);
    }

    public List<Label> GetUnusedLabels()
    {
        var unusedLabels = new List<Label>();
        for (var i = 0; i < _count; i++)
        {
            var label = _labels[i];
            if (label.Name[0] is >= 'A' and <= 'Z') continue;   // Ignore capitalized labels (devices)
            if (label.ReferenceCount == 0) unusedLabels.Add(label);
        }

        return unusedLabels;
    }

    // Safe to return the whole label as it's an immutable struct
    public Label GetLabel(string name)
    {
        var index = GetLabelIndex(name);
        return _labels[index];
    }
    public Label GetLabel(int index)
    {
        return _labels[index];
    }

    public void IncrementLabelReferenceCount(string labelName)
    {
        if (HasLabel(labelName))
        {
            var index = GetLabelIndex(labelName);
            _labels[index] = new Label
            {
                Name = labelName,
                Address = _labels[index].Address,
                ReferenceCount = _labels[index].ReferenceCount + 1,
            };
        }
    }
    
    // Prefix label with scope
    public static string GetSubLabel(string scope, string label)
    {
        if (scope.Length + label.Length >= MaxLabelLength - 1)
            throw new LabelException("Sub-label length too long", label);
        return $"{scope}/{label}";
    }
}
namespace UxnAsm.Exceptions;

internal class BaseException: Exception
{
    public BaseException(string name, string message): base(name)
    {
        var errorWriter = Console.Error;
        errorWriter.WriteLine($"{name}: {message}");
    }
}

internal class ParserException: BaseException
{
    public ParserException(string name, string message): base(name, message) {}
}

internal class LabelException : ParserException
{
    public LabelException(string name, string message) : base(name, message) {}
}
    
internal class ProgramException : ParserException
{
    public ProgramException(string name, string message) : base(name, message) {}
}

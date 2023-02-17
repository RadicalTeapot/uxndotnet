namespace UxnAsm.Utils;

internal class FileReader
{
    public enum Status
    {
        InProgress = 0,
        EndOfFileReached,
    }
    
    private readonly string _contents;
    private static readonly char[] WordDelimiters = { ' ', '\n', '\t', '\r' };
    
    private int _position;
    
    public FileReader(string filePath)
    {
        using var stream = new StreamReader(filePath);
        _contents = stream.ReadToEnd();
        _position = 0;
    }

    public Status ReadWord(out string nextWord)
    {
        nextWord = "";
        if (_position > _contents.Length - 1)
            return Status.EndOfFileReached;
        
        while (nextWord.Length == 0)
        {
            var nextIndex = _contents.IndexOfAny(WordDelimiters, _position);
            if (nextIndex == -1)
            {
                nextWord = _contents[_position..];
                _position = _contents.Length;
            }
            else
            {
                nextWord = _contents.Substring(_position, nextIndex - _position);
                _position = nextIndex+1;
            }
        }
        
        return nextWord != "" ? Status.InProgress : Status.EndOfFileReached;
    }
}
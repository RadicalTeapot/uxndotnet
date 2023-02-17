namespace UxnAsm.Utils;

internal static class StringUtils
{
    public static bool StringIsHex(string s)
    {
        foreach (var c in s)
        {
            if (c is not (>= '0' and <= '9') && c is not (>= 'a' and <= 'f'))
                return false;
        }

        return s.Length >= 1;
    }

    private static int HexStringAsNumber(string word)
    {
        var value = 0;
        foreach (var c in word)
        {
            value = c switch
            {
                >= '0' and <= '9' => value * 16 + (c - '0'),
                >= 'a' and <= 'f' => value * 16 + 10 + (c - 'a'),
                _ => value
            };
        }

        return value;
    }

    public static byte HexStringAsByte(string word)
    {
        var value = HexStringAsNumber(word);
        return (byte)(value & 0xFF);
    }

    public static ushort HexStringAsShort(string word)
    {
        var value = HexStringAsNumber(word);
        return (ushort)(value & 0xFFFF);
    }
}
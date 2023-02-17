namespace UxnAsm.Parser;

internal static class OpCodes
{
    private static readonly string[] Operators = {
        "LIT", "INC", "POP", "NIP", "SWP", "ROT", "DUP", "OVR",
        "EQU", "NEQ", "GTH", "LTH", "JMP", "JCN", "JSR", "STH",
        "LDZ", "STZ", "LDR", "STR", "LDA", "STA", "DEI", "DEO",
        "ADD", "SUB", "MUL", "DIV", "AND", "ORA", "EOR", "SFT"
    };
    
    public static byte Find(string opCode)
    {
        for (byte i = 0; i < 0x20; i++)
        {
            var m = 0;
            if (Operators[i] != opCode[..Math.Min(opCode.Length, 3)]) continue;
            
            if (i == 0) i |= 1 << 7; // Force keep mode for LIT op code
            
            while (3 + m < opCode.Length)
            {
                switch (opCode[3 + m])
                {
                    case '2':
                        i |= 1 << 5;    // Short mode
                        break;
                    case 'r':
                        i |= 1 << 6;    // Return mode
                        break;
                    case 'k':
                        i |= 1 << 7;    // Keep mode
                        break;
                    default:
                        return 0;       // No match
                }

                m++;
            }

            return i;
        }
        return 0;
    }
}
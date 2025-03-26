using System.Text;

namespace Universe;

public static class SafeFileName
{
    public static string Get(string fileName, bool veryVerySafe = true)
    {
        char GetSafeChar(char ch)
        {
            return veryVerySafe ? '_' : ch;
        }

        if (string.IsNullOrEmpty(fileName)) return "";
        var keyPath = fileName;

        StringBuilder ret = new StringBuilder();
        int len = keyPath.Length;
        bool isOpen = true;
        for (int i = 0; i < keyPath.Length; i++)
        {
            char c = keyPath[i];
            if (c == '\\') ret.Append(GetSafeChar((char)0x29F5));
            else if (c == '/') ret.Append(GetSafeChar((char)0x2215));
            else if (c == ':') ret.Append(GetSafeChar((char)0xA789));
            else if (c == '"')
            {
                if (isOpen) ret.Append(GetSafeChar((char)0x201C)); else ret.Append(GetSafeChar((char)0x201D));
                isOpen = !isOpen;
            }
            else
                ret.Append(c);
        }

        return ret.ToString();
    }
}
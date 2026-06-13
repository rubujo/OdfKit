namespace OdfKit.DOM;

internal static class OdfTokenCharacters
{
    public static bool IsAsciiLetterOrDigit(char ch)
    {
        return ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';
    }
}

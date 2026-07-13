using OdfKit.Compliance;

namespace OdfKit.Core;

internal static class OdfOptionGuard
{
    internal static int EnsurePositive(int value, string parameterName)
    {
        if (value < 1)
        {
            throw CreateOutOfRange(parameterName);
        }

        return value;
    }

    internal static long EnsurePositive(long value, string parameterName)
    {
        if (value < 1)
        {
            throw CreateOutOfRange(parameterName);
        }

        return value;
    }

    internal static long EnsureNonNegative(long value, string parameterName)
    {
        if (value < 0)
        {
            throw CreateOutOfRange(parameterName);
        }

        return value;
    }

    private static ArgumentOutOfRangeException CreateOutOfRange(string parameterName) =>
        new(parameterName, OdfLocalizer.GetMessage("Err_ArgumentOutOfRange_Count"));
}

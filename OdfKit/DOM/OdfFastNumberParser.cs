using System;

namespace OdfKit.DOM;

internal static class OdfFastNumberParser
{
    internal static int LastUtf8LookupHitCountForTests;

    internal static int LastCharLookupHitCountForTests;

    internal static int LastSpanParserHitCountForTests;

    public static bool TryParse(ReadOnlySpan<byte> value, out double result)
    {
        if (TryLookupUtf8(value, out result))
        {
            LastUtf8LookupHitCountForTests++;
            return true;
        }

        return TryParseSimpleAscii(value, out result);
    }

    public static bool TryParse(ReadOnlySpan<char> value, out double result)
    {
        if (TryLookupChars(value, out result))
        {
            LastCharLookupHitCountForTests++;
            return true;
        }

        return TryParseSimpleChars(value, out result);
    }

    private static bool TryLookupUtf8(ReadOnlySpan<byte> value, out double result)
    {
        result = 0;
        switch (value.Length)
        {
            case 1 when value[0] is >= (byte)'0' and <= (byte)'9':
                result = value[0] - (byte)'0';
                return true;
            case 2 when value.SequenceEqual("-1"u8):
                result = -1;
                return true;
            case 2 when value.SequenceEqual("10"u8):
                result = 10;
                return true;
            case 3 when value.SequenceEqual("0.5"u8):
                result = 0.5;
                return true;
            case 3 when value.SequenceEqual("1.5"u8):
                result = 1.5;
                return true;
            case 3 when value.SequenceEqual("100"u8):
                result = 100;
                return true;
            default:
                return false;
        }
    }

    private static bool TryLookupChars(ReadOnlySpan<char> value, out double result)
    {
        result = 0;
        switch (value.Length)
        {
            case 1 when value[0] is >= '0' and <= '9':
                result = value[0] - '0';
                return true;
            case 2 when value.SequenceEqual("-1"):
                result = -1;
                return true;
            case 2 when value.SequenceEqual("10"):
                result = 10;
                return true;
            case 3 when value.SequenceEqual("0.5"):
                result = 0.5;
                return true;
            case 3 when value.SequenceEqual("1.5"):
                result = 1.5;
                return true;
            case 3 when value.SequenceEqual("100"):
                result = 100;
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseSimpleAscii(ReadOnlySpan<byte> value, out double result)
    {
        result = 0;
        if (value.IsEmpty || value.Length > 16)
        {
            return false;
        }

        bool negative = false;
        int index = 0;
        if (value[0] == (byte)'-')
        {
            negative = true;
            index = 1;
            if (index == value.Length)
            {
                return false;
            }
        }

        long integer = 0;
        int digits = 0;
        while (index < value.Length && value[index] is >= (byte)'0' and <= (byte)'9')
        {
            integer = (integer * 10) + value[index] - (byte)'0';
            index++;
            digits++;
        }

        double parsed = integer;
        if (index < value.Length && value[index] == (byte)'.')
        {
            index++;
            double scale = 0.1;
            int fractionalDigits = 0;
            while (index < value.Length && value[index] is >= (byte)'0' and <= (byte)'9')
            {
                parsed += (value[index] - (byte)'0') * scale;
                scale *= 0.1;
                index++;
                fractionalDigits++;
            }

            digits += fractionalDigits;
        }

        if (digits == 0 || index != value.Length)
        {
            return false;
        }

        result = negative ? -parsed : parsed;
        LastSpanParserHitCountForTests++;
        return true;
    }

    private static bool TryParseSimpleChars(ReadOnlySpan<char> value, out double result)
    {
        result = 0;
        if (value.IsEmpty || value.Length > 16)
        {
            return false;
        }

        bool negative = false;
        int index = 0;
        if (value[0] == '-')
        {
            negative = true;
            index = 1;
            if (index == value.Length)
            {
                return false;
            }
        }

        long integer = 0;
        int digits = 0;
        while (index < value.Length && value[index] is >= '0' and <= '9')
        {
            integer = (integer * 10) + value[index] - '0';
            index++;
            digits++;
        }

        double parsed = integer;
        if (index < value.Length && value[index] == '.')
        {
            index++;
            double scale = 0.1;
            int fractionalDigits = 0;
            while (index < value.Length && value[index] is >= '0' and <= '9')
            {
                parsed += (value[index] - '0') * scale;
                scale *= 0.1;
                index++;
                fractionalDigits++;
            }

            digits += fractionalDigits;
        }

        if (digits == 0 || index != value.Length)
        {
            return false;
        }

        result = negative ? -parsed : parsed;
        LastSpanParserHitCountForTests++;
        return true;
    }
}

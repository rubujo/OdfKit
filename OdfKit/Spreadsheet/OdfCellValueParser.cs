using System;
using System.Globalization;
using System.Xml;

namespace OdfKit.Spreadsheet;

internal static class OdfCellValueParser
{
    internal static double? ReadNumber(string? value)
    {
        string? lexical = value?.Trim(' ', '\t', '\r', '\n');
        return lexical switch
        {
            "INF" => double.PositiveInfinity,
            "-INF" => double.NegativeInfinity,
            "NaN" => double.NaN,
            _ => double.TryParse(lexical, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ? number : null
        };
    }

    internal static string FormatNumber(double value) => double.IsPositiveInfinity(value) ? "INF"
        : double.IsNegativeInfinity(value) ? "-INF" : value.ToString(CultureInfo.InvariantCulture);

    internal static object? ReadDate(string? value)
    {
        if (value is null)
            return null;
        string lexical = value.Trim(' ', '\t', '\r', '\n');
        int time = lexical.IndexOf('T');
        int point = time < 0 ? -1 : lexical.IndexOf('.', time);
        if (point >= 0)
            for (int index = point + 1; index < lexical.Length && lexical[index] is >= '0' and <= '9'; index++)
                if (index - point > 7 && lexical[index] != '0')
                    return value;
        return DateTime.TryParse(lexical, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed)
            ? (object)parsed : value;
    }

    internal static object? ReadDuration(string? value)
    {
        if (value is null || value.Length == 0)
            return null;
        string lexical = value.Trim(' ', '\t', '\r', '\n');
        int time = lexical.IndexOf('T');
        int calendarEnd = time < 0 ? lexical.Length : time;
        bool nonzero = false;
        for (int index = 0; index < calendarEnd; index++)
        {
            char current = lexical[index];
            if (current is >= '1' and <= '9')
                nonzero = true;
            else if (current is 'Y' or 'M' or 'D')
            {
                // 非零年／月須保留曆法意義，不能以固定天數近似。
                if ((current is 'Y' or 'M') && nonzero)
                    return value;
                nonzero = false;
            }
        }
        int point = lexical.IndexOf('.');
        if (point >= 0)
            for (int index = point + 1; index < lexical.Length && lexical[index] is >= '0' and <= '9'; index++)
                if (index - point > 7 && lexical[index] != '0')
                    return value;
        try
        {
            return XmlConvert.ToTimeSpan(lexical);
        }
        catch (FormatException)
        {
            return value;
        }
        catch (OverflowException)
        {
            return value;
        }
    }
}

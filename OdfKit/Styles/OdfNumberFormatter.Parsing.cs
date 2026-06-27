using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Styles;

public partial class OdfNumberFormatter
{
    #region 內部解析與翻譯邏輯

    private static readonly ConcurrentDictionary<string, FormatInfo> FormatInfoPool = new(StringComparer.Ordinal);

    /// <summary>
    /// 解析標準格式。
    /// </summary>
    /// <param name="format">格式字串</param>
    /// <param name="culture">地區設定資訊</param>
    /// <returns>解析後的標準格式字串</returns>
    public static string ResolveStandardFormat(string format, CultureInfo culture)
    {
        if (string.IsNullOrEmpty(format))
            return "Standard";
        if (format.Length > 2)
            return format;

        char specifier = format[0];
        int precision = -1;
        if (format.Length == 2 && char.IsDigit(format[1]))
        {
            precision = format[1] - '0';
        }

        var numInfo = culture.NumberFormat;
        var dtInfo = culture.DateTimeFormat;

        switch (specifier)
        {
            case 'c':
            case 'C':
                int decC = precision >= 0 ? precision : numInfo.CurrencyDecimalDigits;
                string decimalsC = decC > 0 ? "." + new string('0', decC) : string.Empty;
                return numInfo.CurrencySymbol + "#,##0" + decimalsC;
            case 'n':
            case 'N':
                int decN = precision >= 0 ? precision : numInfo.NumberDecimalDigits;
                string decimalsN = decN > 0 ? "." + new string('0', decN) : string.Empty;
                return "#,##0" + decimalsN;
            case 'f':
            case 'F':
                int decF = precision >= 0 ? precision : numInfo.NumberDecimalDigits;
                string decimalsF = decF > 0 ? "." + new string('0', decF) : string.Empty;
                return "0" + decimalsF;
            case 'p':
            case 'P':
                int decP = precision >= 0 ? precision : numInfo.PercentDecimalDigits;
                string decimalsP = decP > 0 ? "." + new string('0', decP) : string.Empty;
                return "0" + decimalsP + "%";
            case 'd':
                return dtInfo.ShortDatePattern;
            case 'D':
                return dtInfo.LongDatePattern;
            case 't':
                return dtInfo.ShortTimePattern;
            case 'T':
                return dtInfo.LongTimePattern;
            case 'g':
                return dtInfo.ShortDatePattern + " " + dtInfo.ShortTimePattern;
            case 'G':
                return dtInfo.ShortDatePattern + " " + dtInfo.LongTimePattern;
            default:
                return format;
        }
    }

    /// <summary>
    /// 解析格式模式。
    /// </summary>
    /// <param name="pattern">格式模式字串</param>
    /// <returns>解析後的 <see cref="FormatInfo"/> 執行個體</returns>
    public static FormatInfo ParsePattern(string pattern)
    {
        if (pattern is null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }

        return FormatInfoPool.GetOrAdd(pattern, ParsePatternCore);
    }

    private static FormatInfo ParsePatternCore(string pattern)
    {
        FormatType type;
        string currencySymbol = "$";
        IReadOnlyList<DateTimeToken>? dateTimeTokens = null;
        if (pattern.Contains("%"))
        {
            type = FormatType.Percentage;
        }
        else if (ContainsCurrencySymbol(pattern, out string symbol))
        {
            type = FormatType.Currency;
            currencySymbol = symbol;
        }
        else if (ContainsDateTimeChars(pattern))
        {
            type = IsTimeOnlyPattern(pattern) ? FormatType.Time : FormatType.Date;
            dateTimeTokens = ParseDateTimeTokens(pattern);
            return new FormatInfo(type, currencySymbol: currencySymbol, dateTimeTokens: dateTimeTokens);
        }
        else
        {
            type = FormatType.Number;
        }

        string clean = StripLiteralsAndSymbols(pattern);
        bool grouping = clean.Contains(",");

        int minIntegerDigits;
        int decimalPlaces;
        int dotIndex = clean.IndexOf('.');
        if (dotIndex >= 0)
        {
            string integerPart = clean.Substring(0, dotIndex);
            string decimalPart = clean.Substring(dotIndex + 1);

            int zeros = 0;
            foreach (char c in integerPart)
                if (c == '0')
                    zeros++;
            minIntegerDigits = Math.Max(1, zeros);

            int decCount = 0;
            foreach (char c in decimalPart)
                if (c == '0' || c == '#')
                    decCount++;
            decimalPlaces = decCount;
        }
        else
        {
            int zeros = 0;
            foreach (char c in clean)
                if (c == '0')
                    zeros++;
            minIntegerDigits = Math.Max(1, zeros);
            decimalPlaces = 0;
        }

        return new FormatInfo(type, decimalPlaces, minIntegerDigits, grouping, currencySymbol, dateTimeTokens);
    }

    private static bool ContainsCurrencySymbol(string pattern, out string symbol)
    {
        symbol = "$";
        if (pattern.Contains("NT$"))
        { symbol = "NT$"; return true; }
        if (pattern.Contains("$"))
        { symbol = "$"; return true; }
        if (pattern.Contains("€"))
        { symbol = "€"; return true; }
        if (pattern.Contains("£"))
        { symbol = "£"; return true; }
        if (pattern.Contains("¥"))
        { symbol = "¥"; return true; }
        if (pattern.Contains("¤"))
        { symbol = "$"; return true; }
        return false;
    }

    private static bool ContainsDateTimeChars(string pattern)
    {
        foreach (char c in pattern)
        {
            if ("yMdhHmst/:".IndexOf(c) >= 0)
                return true;
        }
        return false;
    }

    private static bool IsTimeOnlyPattern(string pattern)
    {
        bool hasDate = false;
        bool hasTime = false;
        foreach (char c in pattern)
        {
            if ("yMd".IndexOf(c) >= 0)
                hasDate = true;
            if ("hHms".IndexOf(c) >= 0)
                hasTime = true;
        }
        return hasTime && !hasDate;
    }

    private static List<DateTimeToken> ParseDateTimeTokens(string pattern)
    {
        List<DateTimeToken> tokens = [];
        int i = 0;
        while (i < pattern.Length)
        {
            char c = pattern[i];
            if (c == '\'')
            {
                int start = i + 1;
                int end = pattern.IndexOf('\'', start);
                if (end >= 0)
                {
                    tokens.Add(new DateTimeToken(pattern.Substring(start, end - start), true));
                    i = end + 1;
                }
                else
                {
                    tokens.Add(new DateTimeToken(pattern.Substring(start), true));
                    break;
                }
                continue;
            }
            if (c == '\\')
            {
                if (i + 1 < pattern.Length)
                {
                    tokens.Add(new DateTimeToken(pattern[i + 1].ToString(), true));
                    i += 2;
                }
                else
                {
                    i++;
                }
                continue;
            }

            if ("yMdhHmst".IndexOf(c) >= 0)
            {
                int len = 1;
                while (i + len < pattern.Length && pattern[i + len] == c)
                    len++;
                tokens.Add(new DateTimeToken(pattern.Substring(i, len), false));
                i += len;
            }
            else
            {
                tokens.Add(new DateTimeToken(c.ToString(), true));
                i++;
            }
        }
        return tokens;
    }

    private static string StripLiteralsAndSymbols(string pattern)
    {
        StringBuilder sb = new();
        bool inQuote = false;
        for (int i = 0; i < pattern.Length; i++)
        {
            char c = pattern[i];
            if (c == '\'')
            { inQuote = !inQuote; continue; }
            if (inQuote)
                continue;
            if (c == '\\')
            { i++; continue; }
            if ("0#.,".IndexOf(c) >= 0)
                sb.Append(c);
        }
        return sb.ToString();
    }


    #endregion
}

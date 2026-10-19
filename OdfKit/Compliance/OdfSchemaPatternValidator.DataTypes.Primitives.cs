using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;

namespace OdfKit.Compliance;
/// <summary>
/// Provides the OdfSchemaPatternValidator API.
/// 提供 OdfSchemaPatternValidator API。
/// </summary>

public static partial class OdfSchemaPatternValidator
{
    #region Data Types - Primitives

    private static readonly char[] s_xmlWhitespace = [' ', '\t', '\r', '\n'];

    private static string NormalizeDatatypeWhitespace(string dataType, string value)
    {
        // XML Schema whiteSpace：string 保留，normalizedString 替換，其餘內建型別摺疊 XML 四種空白。
        // https://www.w3.org/TR/xmlschema-2/#rf-whiteSpace
        if (dataType is "" or "string" or "xsd:string")
            return value;
        if (dataType is "normalizedString" or "xsd:normalizedString")
            return value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        return NormalizeValue(value);
    }

    private static bool IsNumericDatatype(string type)
    {
        switch (type)
        {
            case "decimal":
            case "xsd:decimal":
            case "integer":
            case "xsd:integer":
            case "nonNegativeInteger":
            case "xsd:nonNegativeInteger":
            case "positiveInteger":
            case "xsd:positiveInteger":
            case "negativeInteger":
            case "xsd:negativeInteger":
            case "nonPositiveInteger":
            case "xsd:nonPositiveInteger":
            case "byte":
            case "xsd:byte":
            case "unsignedByte":
            case "xsd:unsignedByte":
            case "short":
            case "xsd:short":
            case "unsignedShort":
            case "xsd:unsignedShort":
            case "int":
            case "xsd:int":
            case "unsignedInt":
            case "xsd:unsignedInt":
            case "long":
            case "xsd:long":
            case "unsignedLong":
            case "xsd:unsignedLong":
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseXmlBoolean(string value, out bool parsed)
    {
        try
        {
            parsed = XmlConvert.ToBoolean(value);
            return true;
        }
        catch (FormatException)
        {
            parsed = false;
            return false;
        }
    }

    private static bool IsXmlDate(string value, bool requireTime)
    {
        if (value.Length == 0)
            return false;
        int yearStart = value[0] == '-' ? 1 : 0;
        int yearEnd = value.IndexOf('-', yearStart);
        int yearDigits = yearEnd - yearStart;
        if (yearDigits < 4 || yearDigits > 4 && value[yearStart] == '0' || value.Length - yearEnd < 6)
            return false;

        // 以 ASCII 詞法與模 400 曆法檢查日期，保留任意精度年份。
        int yearModulo400 = 0;
        bool nonzeroYear = false;
        for (int index = yearStart; index < yearEnd; index++)
        {
            char digit = value[index];
            if (digit < '0' || digit > '9')
                return false;
            nonzeroYear |= digit != '0';
            yearModulo400 = (yearModulo400 * 10 + digit - '0') % 400;
        }
        if (!nonzeroYear || value[yearEnd + 3] != '-')
            return false;
        for (int index = yearEnd + 1; index < yearEnd + 6; index++)
            if (index != yearEnd + 3 && (value[index] < '0' || value[index] > '9'))
                return false;

        // XML Schema 1.0 的 -0001 對應天文年 0；詞法形式仍禁止 0000。
        if (yearStart == 1)
            yearModulo400 = (yearModulo400 + 399) % 400;
        int month = (value[yearEnd + 1] - '0') * 10 + value[yearEnd + 2] - '0';
        int day = (value[yearEnd + 4] - '0') * 10 + value[yearEnd + 5] - '0';
        if (month < 1 || month > 12 || day < 1)
            return false;
        int maximumDay = month switch
        {
            2 => yearModulo400 % 4 == 0 && (yearModulo400 % 100 != 0 || yearModulo400 == 0) ? 29 : 28,
            4 or 6 or 9 or 11 => 30,
            _ => 31
        };
        if (day > maximumDay)
            return false;

        int dateEnd = yearEnd + 6;
        if (requireTime)
            return dateEnd < value.Length && value[dateEnd] == 'T' && IsXmlTime(value.Substring(dateEnd + 1));
        return IsXmlTimezone(value, dateEnd);
    }

    private static bool IsXmlTimezone(string value, int start)
    {
        int length = value.Length - start;
        if (length == 0)
            return true;
        if (length == 1)
            return value[start] == 'Z';
        if (length != 6 || value[start] is not ('+' or '-') || value[start + 3] != ':')
            return false;
        for (int index = 1; index < length; index++)
            if (index != 3 && (value[start + index] < '0' || value[start + index] > '9'))
                return false;
        int hour = (value[start + 1] - '0') * 10 + value[start + 2] - '0';
        int minute = (value[start + 4] - '0') * 10 + value[start + 5] - '0';
        return minute <= 59 && (hour < 14 || hour == 14 && minute == 0);
    }

    private static bool IsXmlTime(string value)
    {
        Match match = Regex.Match(
            value ?? string.Empty,
            @"^(?<hour>[0-9]{2}):(?<minute>[0-9]{2}):(?<second>[0-9]{2})(?<fraction>\.[0-9]+)?(?<timezone>Z|[+-][0-9]{2}:[0-9]{2})?$");
        if (!match.Success)
        {
            return false;
        }

        int hour = int.Parse(match.Groups["hour"].Value, CultureInfo.InvariantCulture);
        int minute = int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture);
        int second = int.Parse(match.Groups["second"].Value, CultureInfo.InvariantCulture);
        if (hour > 24 || minute > 59 || second > 59 ||
            hour == 24 && (minute != 0 || second != 0 || match.Groups["fraction"].Value.Any(character => character != '.' && character != '0')))
        {
            return false;
        }

        return IsXmlTimezone(match.Groups["timezone"].Value, 0);
    }

    private static bool IsQName(string value)
    {
        string[] parts = (value ?? string.Empty).Split(':');
        if (parts.Length == 1)
        {
            return IsNCName(parts[0]);
        }

        return parts.Length == 2 &&
            IsNCName(parts[0]) &&
            IsNCName(parts[1]);
    }

    private static bool MatchesXmlTokenList(string value, Func<string, bool> predicate)
    {
        string[] tokens = SplitListTokens(value);
        return tokens.Length > 0 && tokens.All(predicate);
    }

    private static bool IsNCName(string value)
    {
        try
        {
            XmlConvert.VerifyNCName(value);
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private static bool IsNmtoken(string value)
    {
        try
        {
            XmlConvert.VerifyNMTOKEN(value);
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private static string NormalizeValue(string value)
    {
        value ??= string.Empty;
        return value.IndexOfAny(s_xmlWhitespace) < 0 ? value :
            string.Join(" ", value.Split(s_xmlWhitespace, StringSplitOptions.RemoveEmptyEntries));
    }

    #endregion
}

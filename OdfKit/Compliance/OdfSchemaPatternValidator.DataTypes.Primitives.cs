using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;

namespace OdfKit.Compliance;

public static partial class OdfSchemaPatternValidator
{
    #region Data Types - Primitives

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

    private static bool IsXmlTime(string value)
    {
        Match match = Regex.Match(
            value ?? string.Empty,
            @"^(?<hour>\d{2}):(?<minute>\d{2}):(?<second>\d{2})(\.\d+)?(?<timezone>Z|[+-]\d{2}:\d{2})?$");
        if (!match.Success)
        {
            return false;
        }

        int hour = int.Parse(match.Groups["hour"].Value, CultureInfo.InvariantCulture);
        int minute = int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture);
        int second = int.Parse(match.Groups["second"].Value, CultureInfo.InvariantCulture);
        if (hour > 23 || minute > 59 || second > 59)
        {
            return false;
        }

        string timezone = match.Groups["timezone"].Value;
        if (timezone.Length == 0 || timezone == "Z")
        {
            return true;
        }

        int timezoneHour = int.Parse(timezone.Substring(1, 2), CultureInfo.InvariantCulture);
        int timezoneMinute = int.Parse(timezone.Substring(4, 2), CultureInfo.InvariantCulture);
        if (timezoneMinute > 59)
        {
            return false;
        }

        return timezoneHour < 14 ||
            (timezoneHour == 14 && timezoneMinute == 0);
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
        string[] tokens = (value ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
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
        return string.Join(" ", (value ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    #endregion
}

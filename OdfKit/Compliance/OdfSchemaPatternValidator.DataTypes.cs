using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace OdfKit.Compliance;

public static partial class OdfSchemaPatternValidator
{
    #region Data Types & Facets

    private static string[] SplitListTokens(string value)
    {
        return (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool MatchesLiteralValue(OdfSchemaPatternNode node, string value)
    {
        if (!MatchesDataType(node, value))
        {
            return false;
        }

        return LiteralValuesEqual(node.DataType, value, node.Value);
    }

    private static bool MatchesDataValue(
        OdfSchemaPatternNode node,
        string value,
        MatchContext context)
    {
        if (!MatchesDataType(node, value))
        {
            return false;
        }

        return !node.Children
            .Where(child => child.Kind == OdfSchemaPatternNodeKind.Except)
            .Any(child => MatchesDataExcept(child, value, context));
    }

    private static bool MatchesDataExcept(
        OdfSchemaPatternNode node,
        string value,
        MatchContext context)
    {
        return node.Children.Any(child => MatchAttributeValueNode(child, value, context));
    }

    private static bool MatchesDataType(OdfSchemaPatternNode node, string value)
    {
        return IsSupportedDatatypeLibrary(node.DataTypeLibrary) &&
            MatchesDataType(node.DataType, value) &&
            MatchesDataParameters(node.DataParameters, value);
    }

    private static bool IsSupportedDatatypeLibrary(string dataTypeLibrary)
    {
        return string.IsNullOrWhiteSpace(dataTypeLibrary) ||
            string.Equals(
                dataTypeLibrary,
                "http://www.w3.org/2001/XMLSchema-datatypes",
                StringComparison.Ordinal);
    }

    private static bool MatchesDataType(string dataType, string value)
    {
        string type = dataType ?? string.Empty;
        if (type.Length == 0 ||
            string.Equals(type, "string", StringComparison.Ordinal) ||
            string.Equals(type, "xsd:string", StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(type, "token", StringComparison.Ordinal) ||
            string.Equals(type, "xsd:token", StringComparison.Ordinal))
        {
            return value != null;
        }

        if (string.Equals(type, "normalizedString", StringComparison.Ordinal) ||
            string.Equals(type, "xsd:normalizedString", StringComparison.Ordinal))
        {
            return IsNormalizedString(value);
        }

        try
        {
            switch (type)
            {
                case "boolean":
                case "xsd:boolean":
                    XmlConvert.ToBoolean(value);
                    return true;
                case "byte":
                case "xsd:byte":
                    XmlConvert.ToSByte(value);
                    return true;
                case "unsignedByte":
                case "xsd:unsignedByte":
                    XmlConvert.ToByte(value);
                    return true;
                case "short":
                case "xsd:short":
                    XmlConvert.ToInt16(value);
                    return true;
                case "unsignedShort":
                case "xsd:unsignedShort":
                    XmlConvert.ToUInt16(value);
                    return true;
                case "int":
                case "xsd:int":
                    XmlConvert.ToInt32(value);
                    return true;
                case "integer":
                case "xsd:integer":
                    return TryParseXmlInteger(value, out _);
                case "unsignedInt":
                case "xsd:unsignedInt":
                    XmlConvert.ToUInt32(value);
                    return true;
                case "nonNegativeInteger":
                case "xsd:nonNegativeInteger":
                    return TryParseXmlInteger(value, out BigInteger nonNegativeInteger) &&
                        nonNegativeInteger >= BigInteger.Zero;
                case "long":
                case "xsd:long":
                    XmlConvert.ToInt64(value);
                    return true;
                case "unsignedLong":
                case "xsd:unsignedLong":
                    XmlConvert.ToUInt64(value);
                    return true;
                case "positiveInteger":
                case "xsd:positiveInteger":
                    return TryParseXmlInteger(value, out BigInteger positiveInteger) &&
                        positiveInteger > BigInteger.Zero;
                case "negativeInteger":
                case "xsd:negativeInteger":
                    return TryParseXmlInteger(value, out BigInteger negativeInteger) &&
                        negativeInteger < BigInteger.Zero;
                case "nonPositiveInteger":
                case "xsd:nonPositiveInteger":
                    return TryParseXmlInteger(value, out BigInteger nonPositiveInteger) &&
                        nonPositiveInteger <= BigInteger.Zero;
                case "decimal":
                case "xsd:decimal":
                    return IsXmlDecimal(value);
                case "float":
                case "xsd:float":
                    XmlConvert.ToSingle(value);
                    return true;
                case "double":
                case "xsd:double":
                    XmlConvert.ToDouble(value);
                    return true;
                case "date":
                case "xsd:date":
                    XmlConvert.ToDateTime(value, XmlDateTimeSerializationMode.RoundtripKind);
                    return value.IndexOf('T') < 0;
                case "dateTime":
                case "xsd:dateTime":
                    XmlConvert.ToDateTime(value, XmlDateTimeSerializationMode.RoundtripKind);
                    return value.IndexOf('T') >= 0;
                case "time":
                case "xsd:time":
                    return IsXmlTime(value);
                case "duration":
                case "xsd:duration":
                    XmlConvert.ToTimeSpan(value);
                    return true;
                case "anyURI":
                case "xsd:anyURI":
                    return IsAnyUri(value);
                case "hexBinary":
                case "xsd:hexBinary":
                    return IsHexBinary(value);
                case "base64Binary":
                case "xsd:base64Binary":
                    return IsBase64Binary(value);
                case "language":
                case "xsd:language":
                    return IsLanguage(value);
                case "Name":
                case "xsd:Name":
                    XmlConvert.VerifyName(value);
                    return true;
                case "NCName":
                case "xsd:NCName":
                case "ID":
                case "xsd:ID":
                case "IDREF":
                case "xsd:IDREF":
                case "ENTITY":
                case "xsd:ENTITY":
                    XmlConvert.VerifyNCName(value);
                    return true;
                case "IDREFS":
                case "xsd:IDREFS":
                case "ENTITIES":
                case "xsd:ENTITIES":
                    return MatchesXmlTokenList(value, IsNCName);
                case "NMTOKEN":
                case "xsd:NMTOKEN":
                    XmlConvert.VerifyNMTOKEN(value);
                    return true;
                case "NMTOKENS":
                case "xsd:NMTOKENS":
                    return MatchesXmlTokenList(value, IsNmtoken);
                case "QName":
                case "xsd:QName":
                    return IsQName(value);
                default:
                    return false;
            }
        }
        catch (FormatException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private static bool IsAnyUri(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        return Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out _);
    }

    private static bool TryParseXmlInteger(string value, out BigInteger integer)
    {
        integer = BigInteger.Zero;
        string text = value ?? string.Empty;
        if (!Regex.IsMatch(text, "^[+-]?[0-9]+$"))
        {
            return false;
        }

        return BigInteger.TryParse(
            text,
            NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out integer);
    }

    private static bool IsXmlDecimal(string value)
    {
        return Regex.IsMatch(
            value ?? string.Empty,
            "^[+-]?(?:[0-9]+(?:\\.[0-9]*)?|\\.[0-9]+)$");
    }

    private static bool IsHexBinary(string value)
    {
        return value.Length % 2 == 0 && value.All(IsHexDigit);
    }

    private static bool IsHexDigit(char value)
    {
        return (value >= '0' && value <= '9') ||
            (value >= 'A' && value <= 'F') ||
            (value >= 'a' && value <= 'f');
    }

    private static bool IsBase64Binary(string value)
    {
        try
        {
            Convert.FromBase64String(NormalizeValue(value));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsLanguage(string value)
    {
        return Regex.IsMatch(value ?? string.Empty, "^[A-Za-z]{1,8}(-[A-Za-z0-9]{1,8})*$");
    }

    private static bool IsNormalizedString(string value)
    {
        return value != null &&
            value.IndexOf('\t') < 0 &&
            value.IndexOf('\n') < 0 &&
            value.IndexOf('\r') < 0;
    }

    private static bool LiteralValuesEqual(string dataType, string value, string literal)
    {
        string type = dataType ?? string.Empty;
        if (type.Length == 0 ||
            string.Equals(type, "token", StringComparison.Ordinal) ||
            string.Equals(type, "xsd:token", StringComparison.Ordinal))
        {
            return string.Equals(NormalizeValue(value), NormalizeValue(literal), StringComparison.Ordinal);
        }

        if (string.Equals(type, "string", StringComparison.Ordinal) ||
            string.Equals(type, "xsd:string", StringComparison.Ordinal) ||
            string.Equals(type, "normalizedString", StringComparison.Ordinal) ||
            string.Equals(type, "xsd:normalizedString", StringComparison.Ordinal))
        {
            return string.Equals(value, literal, StringComparison.Ordinal);
        }

        if (IsNumericDatatype(type) &&
            TryCompareXmlDecimals(value, literal, out int numericComparison))
        {
            return numericComparison == 0;
        }

        if ((string.Equals(type, "boolean", StringComparison.Ordinal) ||
            string.Equals(type, "xsd:boolean", StringComparison.Ordinal)) &&
            TryParseXmlBoolean(value, out bool leftBoolean) &&
            TryParseXmlBoolean(literal, out bool rightBoolean))
        {
            return leftBoolean == rightBoolean;
        }

        return string.Equals(value, literal, StringComparison.Ordinal);
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

    private static bool MatchesDataParameters(IReadOnlyList<OdfSchemaDatatypeParameter> parameters, string value)
    {
        var enumerations = new List<string>();
        foreach (OdfSchemaDatatypeParameter parameter in parameters)
        {
            if (string.Equals(parameter.Name, "enumeration", StringComparison.Ordinal))
            {
                enumerations.Add(parameter.Value);
                continue;
            }

            if (!MatchesDataParameter(parameter.Name, parameter.Value, value))
            {
                return false;
            }
        }

        return enumerations.Count == 0 ||
            enumerations.Any(item => string.Equals(item, value, StringComparison.Ordinal));
    }

    private static bool MatchesDataParameter(string name, string parameterValue, string value)
    {
        switch (name)
        {
            case "minInclusive":
                return CompareFacetValues(value, parameterValue) >= 0;
            case "maxInclusive":
                return CompareFacetValues(value, parameterValue) <= 0;
            case "minExclusive":
                return CompareFacetValues(value, parameterValue) > 0;
            case "maxExclusive":
                return CompareFacetValues(value, parameterValue) < 0;
            case "length":
                return value.Length == ParseNonNegativeInt(parameterValue);
            case "minLength":
                return value.Length >= ParseNonNegativeInt(parameterValue);
            case "maxLength":
                return value.Length <= ParseNonNegativeInt(parameterValue);
            case "pattern":
                try
                {
                    return Regex.IsMatch(value, parameterValue);
                }
                catch (ArgumentException)
                {
                    return false;
                }
            case "enumeration":
                return string.Equals(value, parameterValue, StringComparison.Ordinal);
            default:
                return true;
        }
    }

    private static int CompareFacetValues(string value, string parameterValue)
    {
        if (TryCompareXmlDecimals(value, parameterValue, out int decimalComparison))
        {
            return decimalComparison;
        }

        DateTimeOffset leftDate;
        DateTimeOffset rightDate;
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out leftDate) &&
            DateTimeOffset.TryParse(parameterValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out rightDate))
        {
            return leftDate.CompareTo(rightDate);
        }

        return string.CompareOrdinal(value, parameterValue);
    }

    private static int ParseNonNegativeInt(string value)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) && parsed >= 0
            ? parsed
            : -1;
    }

    private static bool TryCompareXmlDecimals(string value, string parameterValue, out int comparison)
    {
        comparison = 0;
        if (!TryParseXmlDecimal(value, out BigInteger left, out int leftScale) ||
            !TryParseXmlDecimal(parameterValue, out BigInteger right, out int rightScale))
        {
            return false;
        }

        if (leftScale < rightScale)
        {
            left *= BigInteger.Pow(10, rightScale - leftScale);
        }
        else if (rightScale < leftScale)
        {
            right *= BigInteger.Pow(10, leftScale - rightScale);
        }

        comparison = left.CompareTo(right);
        return true;
    }

    private static bool TryParseXmlDecimal(string value, out BigInteger unscaledValue, out int scale)
    {
        unscaledValue = BigInteger.Zero;
        scale = 0;
        if (!IsXmlDecimal(value))
        {
            return false;
        }

        string text = value;
        bool isNegative = text[0] == '-';
        if (isNegative || text[0] == '+')
        {
            text = text.Substring(1);
        }

        int decimalPoint = text.IndexOf('.');
        if (decimalPoint >= 0)
        {
            scale = text.Length - decimalPoint - 1;
            text = text.Remove(decimalPoint, 1);
        }

        unscaledValue = BigInteger.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);
        if (isNegative)
        {
            unscaledValue = -unscaledValue;
        }

        return true;
    }

    internal static bool PatternMatchesElementName(
        OdfSchemaPatternDefinition pattern,
        XElement element,
        OdfSchemaSet schema)
    {
        var context = new MatchContext(schema);
        foreach (OdfSchemaPatternNode root in pattern.Roots)
        {
            if (PatternMatchesElementName(root, element, context))
            {
                return true;
            }
        }
        return false;
    }

    private static bool PatternMatchesElementName(
        OdfSchemaPatternNode node,
        XElement element,
        MatchContext context)
    {
        if (node.Kind == OdfSchemaPatternNodeKind.Ref)
        {
            if (string.IsNullOrWhiteSpace(node.ReferenceName) || !context.EnterReference(node.ReferenceName))
            {
                return false;
            }

            try
            {
                OdfSchemaPatternDefinition? pattern = context.Schema.FindPattern(node.ReferenceName);
                if (pattern == null)
                {
                    return false;
                }

                foreach (OdfSchemaPatternNode root in pattern.Roots)
                {
                    if (PatternMatchesElementName(root, element, context))
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                context.LeaveReference(node.ReferenceName);
            }
        }

        if (node.Kind == OdfSchemaPatternNodeKind.Element)
        {
            if (string.IsNullOrEmpty(node.LocalName) && string.IsNullOrEmpty(node.NamespaceUri))
            {
                return true;
            }

            return string.Equals(node.NamespaceUri, element.Name.NamespaceName, StringComparison.Ordinal) &&
                string.Equals(node.LocalName, element.Name.LocalName, StringComparison.Ordinal);
        }

        if (node.Kind == OdfSchemaPatternNodeKind.Choice ||
            node.Kind == OdfSchemaPatternNodeKind.Group ||
            node.Kind == OdfSchemaPatternNodeKind.Interleave ||
            node.Kind == OdfSchemaPatternNodeKind.Optional ||
            node.Kind == OdfSchemaPatternNodeKind.ZeroOrMore ||
            node.Kind == OdfSchemaPatternNodeKind.OneOrMore)
        {
            foreach (OdfSchemaPatternNode child in node.Children)
            {
                if (PatternMatchesElementName(child, element, context))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private sealed class MatchContext
    {
        private readonly HashSet<string> _activeReferences = new HashSet<string>(StringComparer.Ordinal);

        public MatchContext(OdfSchemaSet schema)
        {
            Schema = schema;
        }

        public OdfSchemaSet Schema { get; }

        public bool EnterReference(string referenceName) => _activeReferences.Add(referenceName);

        public void LeaveReference(string referenceName) => _activeReferences.Remove(referenceName);
    }

    #endregion
}

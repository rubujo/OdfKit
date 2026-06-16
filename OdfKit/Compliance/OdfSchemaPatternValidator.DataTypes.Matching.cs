using System;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Xml;

namespace OdfKit.Compliance;

public static partial class OdfSchemaPatternValidator
{
    #region Data Types - Matching

    private static string[] SplitListTokens(string value)
    {
        return (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }

    internal static bool MatchesLiteralValue(OdfSchemaPatternNode node, string value)
    {
        if (!MatchesDataType(node, value))
        {
            return false;
        }

        return LiteralValuesEqual(node.DataType, value, node.Value);
    }

    internal static bool MatchesDataValue(
        OdfSchemaPatternNode node,
        string value,
        OdfSchemaPatternMatchContext context)
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
        OdfSchemaPatternMatchContext context)
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

    #endregion
}

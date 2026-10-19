using System;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace OdfKit.Compliance;
/// <summary>
/// Provides the OdfSchemaPatternValidator API.
/// 提供 OdfSchemaPatternValidator API。
/// </summary>

public static partial class OdfSchemaPatternValidator
{
    #region Data Types - Matching

    private static string[] SplitListTokens(string value)
    {
        return (value ?? string.Empty).Split(s_xmlWhitespace, StringSplitOptions.RemoveEmptyEntries);
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
        value = NormalizeDatatypeWhitespace(node.DataType, value);
        if (node.DataType is "QName" or "xsd:QName")
        {
            if (!IsQName(value))
            {
                return false;
            }
            int separator = value.IndexOf(':');
            if (separator > 0)
            {
                string prefix = value.Substring(0, separator);
                XElement? namespaceElement = context.NamespaceContext;
                XNamespace? boundNamespace = namespaceElement?.Annotation<OdfSchemaNamespaceContext>() is { } originalContext
                    ? originalContext.ResolvePrefix(prefix)
                    : namespaceElement?.GetNamespaceOfPrefix(prefix);
                if (prefix == "xmlns" || boundNamespace is null)
                {
                    return false;
                }
            }
        }

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
        value = NormalizeDatatypeWhitespace(node.DataType, value);
        return IsSupportedDatatypeLibrary(node.DataTypeLibrary) &&
            MatchesDataType(node.DataType, value) &&
            MatchesDataParameters(node.DataType, node.DataParameters, value);
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
                case "double":
                case "xsd:double":
                    return IsXmlFloatingPoint(value);
                case "date":
                case "xsd:date":
                    return IsXmlDate(value, requireTime: false);
                case "dateTime":
                case "xsd:dateTime":
                    return IsXmlDate(value, requireTime: true);
                case "time":
                case "xsd:time":
                    return IsXmlTime(value);
                case "duration":
                case "xsd:duration":
                    return IsXmlDuration(value);
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
        catch (ArgumentException)
        {
            // XmlConvert.VerifyName／VerifyNCName／VerifyNMTOKEN 對空字串擲出 ArgumentException 而非 XmlException；
            // 空值只是不符合該資料型別，不應讓驗證器中止。
            return false;
        }
    }

    private static bool IsAnyUri(string value)
    {
        XmlConvert.VerifyXmlChars(value ?? string.Empty);
        return true;
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

    internal static bool MatchesConfigurationValue(string type, string value)
    {
        string declared = type.Trim(s_xmlWhitespace);
        if (declared == "boolean")
            return NormalizeDatatypeWhitespace("token", value) is "true" or "false";
        string dataType = declared == "datetime" ? "dateTime" : declared;
        if (dataType is not ("short" or "int" or "long" or "double" or "dateTime" or "base64Binary"))
            return true;
        return MatchesDataType(dataType, NormalizeDatatypeWhitespace(dataType, value));
    }

    private static bool IsXmlDuration(string value)
    {
        // 只檢查詞彙，不將任意精度的分量轉成有容量限制的 TimeSpan。
        int index = value.Length > 0 && value[0] == '-' ? 1 : 0;
        if (index >= value.Length || value[index++] != 'P')
            return false;
        bool time = false;
        int lastDate = -1;
        int lastTime = -1;
        while (index < value.Length)
        {
            if (value[index] == 'T')
            {
                if (time || ++index == value.Length)
                    return false;
                time = true;
                continue;
            }
            int start = index;
            while (index < value.Length && value[index] is >= '0' and <= '9')
                index++;
            if (index == start)
                return false;
            bool fraction = index < value.Length && value[index] == '.';
            if (fraction)
            {
                int fractionalStart = ++index;
                while (index < value.Length && value[index] is >= '0' and <= '9')
                    index++;
                if (index == fractionalStart)
                    return false;
            }
            if (index == value.Length)
                return false;
            char unit = value[index++];
            int order = time
                ? unit switch { 'H' => 0, 'M' => 1, 'S' => 2, _ => -1 }
                : unit switch { 'Y' => 0, 'M' => 1, 'D' => 2, _ => -1 };
            if (order < 0 || (fraction && (!time || unit != 'S')))
                return false;
            if (time)
            {
                if (order <= lastTime)
                    return false;
                lastTime = order;
            }
            else
            {
                if (order <= lastDate)
                    return false;
                lastDate = order;
            }
        }
        return time ? lastTime >= 0 : lastDate >= 0;
    }

    internal static bool IsXmlFloatingPoint(string value)
    {
        // CLR 解析器也接受 Infinity 與大小寫變體；XML Schema 僅允許這三種特殊詞彙。
        if (value is "INF" or "-INF" or "NaN")
            return true;

        int index = 0;
        if (index < value.Length && value[index] is '+' or '-')
            index++;
        int digits = 0;
        while (index < value.Length && value[index] is >= '0' and <= '9')
        {
            index++;
            digits++;
        }
        if (index < value.Length && value[index] == '.')
        {
            index++;
            while (index < value.Length && value[index] is >= '0' and <= '9')
            {
                index++;
                digits++;
            }
        }
        if (digits == 0)
            return false;
        if (index < value.Length && value[index] is 'e' or 'E')
        {
            index++;
            if (index < value.Length && value[index] is '+' or '-')
                index++;
            int exponentStart = index;
            while (index < value.Length && value[index] is >= '0' and <= '9')
                index++;
            if (index == exponentStart)
                return false;
        }
        return index == value.Length;
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
        int length = 0;
        int padding = 0;
        char lastData = '\0';
        foreach (char character in value)
        {
            if (character is ' ' or '\t' or '\r' or '\n')
                continue;
            if (character == '=')
            {
                if (++padding > 2)
                    return false;
            }
            else
            {
                if (padding != 0 || character is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '+' or '/'))
                    return false;
                lastData = character;
            }
            length++;
        }
        // 填補後的未使用位元必須為零；只驗證詞彙，不配置解碼緩衝區。
        return length % 4 == 0 && (padding == 0 ||
            (padding == 1 && lastData is 'A' or 'E' or 'I' or 'M' or 'Q' or 'U' or 'Y' or 'c' or 'g' or 'k' or 'o' or 's' or 'w' or '0' or '4' or '8') ||
            (padding == 2 && lastData is 'A' or 'Q' or 'g' or 'w'));
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
        value = NormalizeDatatypeWhitespace(type, value);
        literal = NormalizeDatatypeWhitespace(type, literal);
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

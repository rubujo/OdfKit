using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace OdfKit.Compliance;

public static partial class OdfSchemaPatternValidator
{
    #region Data Types - Facets

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

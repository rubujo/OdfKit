using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Styles
{
    public enum FormatType
    {
        Number,
        Currency,
        Percentage,
        Date,
        Time
    }

    public class DateTimeToken
    {
        public string Token { get; }
        public bool IsLiteral { get; }
        public DateTimeToken(string token, bool isLiteral)
        {
            Token = token;
            IsLiteral = isLiteral;
        }
    }

    public class FormatInfo
    {
        public FormatType Type { get; set; } = FormatType.Number;
        public int DecimalPlaces { get; set; } = 0;
        public int MinIntegerDigits { get; set; } = 1;
        public bool Grouping { get; set; } = false;
        public string CurrencySymbol { get; set; } = "$";
        public List<DateTimeToken> DateTimeTokens { get; set; } = new();
    }

    public class OdfNumberFormatter
    {
        private readonly OdfNode _contentRoot;
        private readonly OdfNode _stylesRoot;
        
        // Key: Canonical XML key of the style, Value: Generated style name (e.g., N1, D1)
        private readonly Dictionary<string, string> _formatCache = new(StringComparer.Ordinal);
        private int _styleCounter = 0;

        public OdfNumberFormatter(OdfNode contentRoot, OdfNode stylesRoot)
        {
            _contentRoot = contentRoot ?? throw new ArgumentNullException(nameof(contentRoot));
            _stylesRoot = stylesRoot ?? throw new ArgumentNullException(nameof(stylesRoot));
            PopulateCacheFromExistingStyles();
        }

        /// <summary>
        /// Registers a .NET format string, translates it to an ODF style if needed, 
        /// and returns the name of the style to reference.
        /// </summary>
        public string GetOrCreateNumberStyle(string dotNetFormat, CultureInfo? culture = null)
        {
            var cult = culture ?? CultureInfo.InvariantCulture;
            string normalized = ResolveStandardFormat(dotNetFormat, cult);

            // 1. Build Format Info
            FormatInfo info = ParsePattern(normalized);

            // 2. Generate temporary style node for key serialization
            OdfNode tempNode = CreateStyleNode(string.Empty, info);
            string canonicalKey = SerializeOdfStyleStructure(tempNode);

            // 3. Check cache
            if (_formatCache.TryGetValue(canonicalKey, out string? existingStyleName))
            {
                return existingStyleName;
            }

            // 4. Determine prefix and generate unique name
            string prefix = info.Type switch
            {
                FormatType.Number => "N",
                FormatType.Currency => "C",
                FormatType.Percentage => "P",
                FormatType.Date => "D",
                FormatType.Time => "T",
                _ => "N"
            };

            string generatedName;
            do
            {
                generatedName = $"{prefix}{++_styleCounter}";
            } while (StyleExistsInDOM(generatedName));

            // Set name on style node and append to DOM
            tempNode.SetAttribute("name", OdfNamespaces.Style, generatedName, "style");
            var automaticStyles = GetOrCreateAutomaticStylesNode();
            automaticStyles.AppendChild(tempNode);

            _formatCache[canonicalKey] = generatedName;
            return generatedName;
        }

        /// <summary>
        /// Resolves style name to node. Returns a fallback Standard number style 
        /// instead of null to prevent NullReferenceException.
        /// </summary>
        public OdfNode GetNumberStyleNode(string styleName)
        {
            if (string.IsNullOrEmpty(styleName))
            {
                return GetOrCreateStandardFallbackNode("Standard");
            }

            OdfNode? styleNode = FindStyleInDOM(styleName);
            if (styleNode != null)
            {
                return styleNode;
            }

            OdfKitDiagnostics.Warn($"Referenced number style '{styleName}' not found. Falling back to Standard style.");
            return GetOrCreateStandardFallbackNode(styleName);
        }

        #region Internal Parsing & Translation Logic

        public static string ResolveStandardFormat(string format, CultureInfo culture)
        {
            if (string.IsNullOrEmpty(format)) return "Standard";
            if (format.Length > 2) return format;

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
                    string decimalsC = decC > 0 ? "." + new string('0', decC) : "";
                    return numInfo.CurrencySymbol + "#,##0" + decimalsC;
                case 'n':
                case 'N':
                    int decN = precision >= 0 ? precision : numInfo.NumberDecimalDigits;
                    string decimalsN = decN > 0 ? "." + new string('0', decN) : "";
                    return "#,##0" + decimalsN;
                case 'f':
                case 'F':
                    int decF = precision >= 0 ? precision : numInfo.NumberDecimalDigits;
                    string decimalsF = decF > 0 ? "." + new string('0', decF) : "";
                    return "0" + decimalsF;
                case 'p':
                case 'P':
                    int decP = precision >= 0 ? precision : numInfo.PercentDecimalDigits;
                    string decimalsP = decP > 0 ? "." + new string('0', decP) : "";
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

        public static FormatInfo ParsePattern(string pattern)
        {
            var info = new FormatInfo();

            if (pattern.Contains("%"))
            {
                info.Type = FormatType.Percentage;
            }
            else if (ContainsCurrencySymbol(pattern, out string symbol))
            {
                info.Type = FormatType.Currency;
                info.CurrencySymbol = symbol;
            }
            else if (ContainsDateTimeChars(pattern))
            {
                info.Type = IsTimeOnlyPattern(pattern) ? FormatType.Time : FormatType.Date;
                info.DateTimeTokens = ParseDateTimeTokens(pattern);
                return info;
            }
            else
            {
                info.Type = FormatType.Number;
            }

            string clean = StripLiteralsAndSymbols(pattern);
            info.Grouping = clean.Contains(",");

            int dotIndex = clean.IndexOf('.');
            if (dotIndex >= 0)
            {
                string integerPart = clean.Substring(0, dotIndex);
                string decimalPart = clean.Substring(dotIndex + 1);

                int zeros = 0;
                foreach (char c in integerPart) if (c == '0') zeros++;
                info.MinIntegerDigits = Math.Max(1, zeros);

                int decCount = 0;
                foreach (char c in decimalPart) if (c == '0' || c == '#') decCount++;
                info.DecimalPlaces = decCount;
            }
            else
            {
                int zeros = 0;
                foreach (char c in clean) if (c == '0') zeros++;
                info.MinIntegerDigits = Math.Max(1, zeros);
                info.DecimalPlaces = 0;
            }

            return info;
        }

        private static bool ContainsCurrencySymbol(string pattern, out string symbol)
        {
            symbol = "$";
            if (pattern.Contains("NT$")) { symbol = "NT$"; return true; }
            if (pattern.Contains("$")) { symbol = "$"; return true; }
            if (pattern.Contains("€")) { symbol = "€"; return true; }
            if (pattern.Contains("£")) { symbol = "£"; return true; }
            if (pattern.Contains("¥")) { symbol = "¥"; return true; }
            if (pattern.Contains("¤")) { symbol = "$"; return true; }
            return false;
        }

        private static bool ContainsDateTimeChars(string pattern)
        {
            foreach (char c in pattern)
            {
                if ("yMdhHmst/:".Contains(c.ToString())) return true;
            }
            return false;
        }

        private static bool IsTimeOnlyPattern(string pattern)
        {
            bool hasDate = false;
            bool hasTime = false;
            foreach (char c in pattern)
            {
                if ("yMd".Contains(c.ToString())) hasDate = true;
                if ("hHms".Contains(c.ToString())) hasTime = true;
            }
            return hasTime && !hasDate;
        }

        private static List<DateTimeToken> ParseDateTimeTokens(string pattern)
        {
            var tokens = new List<DateTimeToken>();
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

                if ("yMdhHmst".Contains(c.ToString()))
                {
                    int len = 1;
                    while (i + len < pattern.Length && pattern[i + len] == c) len++;
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
            var sb = new StringBuilder();
            bool inQuote = false;
            for (int i = 0; i < pattern.Length; i++)
            {
                char c = pattern[i];
                if (c == '\'') { inQuote = !inQuote; continue; }
                if (inQuote) continue;
                if (c == '\\') { i++; continue; }
                if ("0#.,".Contains(c.ToString())) sb.Append(c);
            }
            return sb.ToString();
        }

        #endregion

        #region DOM Manipulation & Cache Deduplication

        private OdfNode CreateStyleNode(string styleName, FormatInfo info)
        {
            string localName = info.Type switch
            {
                FormatType.Number => "number-style",
                FormatType.Currency => "currency-style",
                FormatType.Percentage => "percentage-style",
                FormatType.Date => "date-style",
                FormatType.Time => "time-style",
                _ => "number-style"
            };

            var styleNode = new OdfNode(OdfNodeType.Element, localName, OdfNamespaces.Number, "number");
            if (!string.IsNullOrEmpty(styleName))
            {
                styleNode.SetAttribute("name", OdfNamespaces.Style, styleName, "style");
            }

            switch (info.Type)
            {
                case FormatType.Number:
                case FormatType.Percentage:
                    {
                        var numNode = new OdfNode(OdfNodeType.Element, "number", OdfNamespaces.Number, "number");
                        numNode.SetAttribute("decimal-places", OdfNamespaces.Number, info.DecimalPlaces.ToString(CultureInfo.InvariantCulture), "number");
                        numNode.SetAttribute("min-integer-digits", OdfNamespaces.Number, info.MinIntegerDigits.ToString(CultureInfo.InvariantCulture), "number");
                        if (info.Grouping) numNode.SetAttribute("grouping", OdfNamespaces.Number, "true", "number");
                        styleNode.AppendChild(numNode);

                        if (info.Type == FormatType.Percentage)
                        {
                            var textNode = new OdfNode(OdfNodeType.Element, "text", OdfNamespaces.Number, "number");
                            textNode.TextContent = "%";
                            styleNode.AppendChild(textNode);
                        }
                    }
                    break;

                case FormatType.Currency:
                    {
                        var symbolNode = new OdfNode(OdfNodeType.Element, "currency-symbol", OdfNamespaces.Number, "number");
                        symbolNode.TextContent = info.CurrencySymbol;
                        styleNode.AppendChild(symbolNode);

                        var numNode = new OdfNode(OdfNodeType.Element, "number", OdfNamespaces.Number, "number");
                        numNode.SetAttribute("decimal-places", OdfNamespaces.Number, info.DecimalPlaces.ToString(CultureInfo.InvariantCulture), "number");
                        numNode.SetAttribute("min-integer-digits", OdfNamespaces.Number, info.MinIntegerDigits.ToString(CultureInfo.InvariantCulture), "number");
                        if (info.Grouping) numNode.SetAttribute("grouping", OdfNamespaces.Number, "true", "number");
                        styleNode.AppendChild(numNode);
                    }
                    break;

                case FormatType.Date:
                case FormatType.Time:
                    {
                        bool is12Hour = false;
                        foreach (var t in info.DateTimeTokens)
                        {
                            if (!t.IsLiteral && (t.Token == "tt" || t.Token == "t")) is12Hour = true;
                        }

                        foreach (var token in info.DateTimeTokens)
                        {
                            if (token.IsLiteral)
                            {
                                var textNode = new OdfNode(OdfNodeType.Element, "text", OdfNamespaces.Number, "number");
                                textNode.TextContent = token.Token;
                                styleNode.AppendChild(textNode);
                            }
                            else
                            {
                                OdfNode? partNode = CreateDateTimePartNode(token.Token, is12Hour);
                                if (partNode != null) styleNode.AppendChild(partNode);
                            }
                        }
                    }
                    break;
            }

            return styleNode;
        }

        private OdfNode? CreateDateTimePartNode(string token, bool is12Hour)
        {
            OdfNode node;
            switch (token)
            {
                case "yyyy":
                    node = new OdfNode(OdfNodeType.Element, "year", OdfNamespaces.Number, "number");
                    node.SetAttribute("style", OdfNamespaces.Number, "long", "number");
                    return node;
                case "yy":
                    node = new OdfNode(OdfNodeType.Element, "year", OdfNamespaces.Number, "number");
                    node.SetAttribute("style", OdfNamespaces.Number, "short", "number");
                    return node;
                case "MMMM":
                    node = new OdfNode(OdfNodeType.Element, "month", OdfNamespaces.Number, "number");
                    node.SetAttribute("style", OdfNamespaces.Number, "long", "number");
                    node.SetAttribute("textual", OdfNamespaces.Number, "true", "number");
                    return node;
                case "MMM":
                    node = new OdfNode(OdfNodeType.Element, "month", OdfNamespaces.Number, "number");
                    node.SetAttribute("style", OdfNamespaces.Number, "short", "number");
                    node.SetAttribute("textual", OdfNamespaces.Number, "true", "number");
                    return node;
                case "MM":
                    node = new OdfNode(OdfNodeType.Element, "month", OdfNamespaces.Number, "number");
                    node.SetAttribute("style", OdfNamespaces.Number, "long", "number");
                    return node;
                case "M":
                    node = new OdfNode(OdfNodeType.Element, "month", OdfNamespaces.Number, "number");
                    node.SetAttribute("style", OdfNamespaces.Number, "short", "number");
                    return node;
                case "dd":
                    node = new OdfNode(OdfNodeType.Element, "day", OdfNamespaces.Number, "number");
                    node.SetAttribute("style", OdfNamespaces.Number, "long", "number");
                    return node;
                case "d":
                    node = new OdfNode(OdfNodeType.Element, "day", OdfNamespaces.Number, "number");
                    node.SetAttribute("style", OdfNamespaces.Number, "short", "number");
                    return node;
                case "HH":
                case "hh":
                    node = new OdfNode(OdfNodeType.Element, "hours", OdfNamespaces.Number, "number");
                    node.SetAttribute("style", OdfNamespaces.Number, "long", "number");
                    return node;
                case "H":
                case "h":
                    node = new OdfNode(OdfNodeType.Element, "hours", OdfNamespaces.Number, "number");
                    node.SetAttribute("style", OdfNamespaces.Number, "short", "number");
                    return node;
                case "mm":
                    node = new OdfNode(OdfNodeType.Element, "minutes", OdfNamespaces.Number, "number");
                    node.SetAttribute("style", OdfNamespaces.Number, "long", "number");
                    return node;
                case "m":
                    node = new OdfNode(OdfNodeType.Element, "minutes", OdfNamespaces.Number, "number");
                    node.SetAttribute("style", OdfNamespaces.Number, "short", "number");
                    return node;
                case "ss":
                    node = new OdfNode(OdfNodeType.Element, "seconds", OdfNamespaces.Number, "number");
                    node.SetAttribute("style", OdfNamespaces.Number, "long", "number");
                    return node;
                case "s":
                    node = new OdfNode(OdfNodeType.Element, "seconds", OdfNamespaces.Number, "number");
                    node.SetAttribute("style", OdfNamespaces.Number, "short", "number");
                    return node;
                case "tt":
                case "t":
                    node = new OdfNode(OdfNodeType.Element, "am-pm", OdfNamespaces.Number, "number");
                    return node;
                default:
                    return null;
            }
        }

        private string SerializeOdfStyleStructure(OdfNode node)
        {
            var sb = new StringBuilder();
            sb.Append(node.LocalName).Append('|');
            
            foreach (var child in node.Children)
            {
                sb.Append('[').Append(child.LocalName).Append(':');
                var attrs = new List<OdfAttributeName>(child.Attributes.Keys);
                attrs.Sort((x, y) => string.Compare(x.LocalName, y.LocalName, StringComparison.Ordinal));
                
                foreach (var attr in attrs)
                {
                    if (attr.LocalName == "name" && attr.NamespaceUri == OdfNamespaces.Style) continue;
                    sb.Append($"{attr.NamespaceUri}:{attr.LocalName}={child.Attributes[attr]};");
                }

                if (child.Children.Count > 0 && child.Children[0].NodeType == OdfNodeType.Text)
                {
                    sb.Append("text=").Append(child.Children[0].TextContent).Append(';');
                }
                sb.Append(']');
            }
            return sb.ToString();
        }

        private void PopulateCacheFromExistingStyles()
        {
            ScanAndCacheStyles(FindChildElement(_contentRoot, "automatic-styles", OdfNamespaces.Office));
            ScanAndCacheStyles(FindChildElement(_stylesRoot, "automatic-styles", OdfNamespaces.Office));
            ScanAndCacheStyles(FindChildElement(_stylesRoot, "styles", OdfNamespaces.Office));
        }

        private void ScanAndCacheStyles(OdfNode? parentNode)
        {
            if (parentNode == null) return;
            foreach (var child in parentNode.Children)
            {
                if (child.NamespaceUri == OdfNamespaces.Number && 
                    (child.LocalName == "number-style" || child.LocalName == "currency-style" || 
                     child.LocalName == "percentage-style" || child.LocalName == "date-style" || 
                     child.LocalName == "time-style"))
                {
                    string? name = child.GetAttribute("name", OdfNamespaces.Style);
                    if (name != null)
                    {
                        string canonicalKey = SerializeOdfStyleStructure(child);
                        if (!string.IsNullOrEmpty(canonicalKey))
                        {
                            _formatCache[canonicalKey] = name;
                        }
                    }
                }
            }
        }

        private bool StyleExistsInDOM(string name)
        {
            return FindStyleInDOM(name) != null;
        }

        private OdfNode? FindStyleInDOM(string name)
        {
            var style = FindStyleInParent(FindChildElement(_contentRoot, "automatic-styles", OdfNamespaces.Office), name);
            if (style != null) return style;

            style = FindStyleInParent(FindChildElement(_stylesRoot, "automatic-styles", OdfNamespaces.Office), name);
            if (style != null) return style;

            return FindStyleInParent(FindChildElement(_stylesRoot, "styles", OdfNamespaces.Office), name);
        }

        private OdfNode? FindStyleInParent(OdfNode? parentNode, string name)
        {
            if (parentNode == null) return null;
            foreach (var child in parentNode.Children)
            {
                if (child.NamespaceUri == OdfNamespaces.Number && child.GetAttribute("name", OdfNamespaces.Style) == name)
                {
                    return child;
                }
            }
            return null;
        }

        private OdfNode GetOrCreateStandardFallbackNode(string styleName)
        {
            var fallback = new OdfNode(OdfNodeType.Element, "number-style", OdfNamespaces.Number, "number");
            fallback.SetAttribute("name", OdfNamespaces.Style, styleName, "style");
            var num = new OdfNode(OdfNodeType.Element, "number", OdfNamespaces.Number, "number");
            num.SetAttribute("decimal-places", OdfNamespaces.Number, "0", "number");
            num.SetAttribute("min-integer-digits", OdfNamespaces.Number, "1", "number");
            fallback.AppendChild(num);
            return fallback;
        }

        private OdfNode GetOrCreateAutomaticStylesNode()
        {
            var contentAuto = FindChildElement(_contentRoot, "automatic-styles", OdfNamespaces.Office);
            if (contentAuto == null)
            {
                contentAuto = new OdfNode(OdfNodeType.Element, "automatic-styles", OdfNamespaces.Office, "office");
                if (_contentRoot.Children.Count > 0)
                {
                    _contentRoot.InsertBefore(contentAuto, _contentRoot.Children[0]);
                }
                else
                {
                    _contentRoot.AppendChild(contentAuto);
                }
            }
            return contentAuto;
        }

        private OdfNode? FindChildElement(OdfNode parent, string localName, string nsUri)
        {
            foreach (var child in parent.Children)
            {
                if (child.NodeType == OdfNodeType.Element && 
                    string.Equals(child.LocalName, localName, StringComparison.Ordinal) && 
                    string.Equals(child.NamespaceUri, nsUri, StringComparison.Ordinal))
                {
                    return child;
                }
            }
            return null;
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Styles;

/// <summary>
/// 表示格式類型的列舉。
/// </summary>
public enum FormatType
{
    /// <summary>
    /// 數值格式。
    /// </summary>
    Number,

    /// <summary>
    /// 貨幣格式。
    /// </summary>
    Currency,

    /// <summary>
    /// 百分比格式。
    /// </summary>
    Percentage,

    /// <summary>
    /// 日期格式。
    /// </summary>
    Date,

    /// <summary>
    /// 時間格式。
    /// </summary>
    Time
}

/// <summary>
/// 表示日期時間格式化語彙基元的類別。
/// </summary>
/// <remarks>
/// 初始化 <see cref="DateTimeToken"/> 類別的新執行個體。
/// </remarks>
/// <param name="token">格式化語彙基元字串</param>
/// <param name="isLiteral">指出該語彙基元是否為字面值</param>
public class DateTimeToken(string token, bool isLiteral)
{
    /// <summary>
    /// 取得格式化語彙基元字串。
    /// </summary>
    public string Token { get; } = token;

    /// <summary>
    /// 取得一個值，指出該語彙基元是否為字面值。
    /// </summary>
    public bool IsLiteral { get; } = isLiteral;
}

/// <summary>
/// 儲存格式化資訊的類別。
/// </summary>
public class FormatInfo
{
    /// <summary>
    /// 取得或設定格式類型。
    /// </summary>
    public FormatType Type { get; set; } = FormatType.Number;

    /// <summary>
    /// 取得或設定小數位數。
    /// </summary>
    public int DecimalPlaces { get; set; }

    /// <summary>
    /// 取得或設定最小整數位數。
    /// </summary>
    public int MinIntegerDigits { get; set; } = 1;

    /// <summary>
    /// 取得或設定一個值，指出是否使用千分位分組。
    /// </summary>
    public bool Grouping { get; set; }

    /// <summary>
    /// 取得或設定貨幣符號。
    /// </summary>
    public string CurrencySymbol { get; set; } = "$";

    /// <summary>
    /// 取得或設定日期時間格式的語彙基元清單。
    /// </summary>
    public List<DateTimeToken> DateTimeTokens { get; set; } = [];
}

/// <summary>
/// 負責處理 .NET 格式字串與 ODF 數字樣式之間轉換的格式化器。
/// </summary>
public class OdfNumberFormatter
{
    private readonly OdfNode _contentRoot;
    private readonly OdfNode _stylesRoot;

    // 金鑰：樣式之規範 XML 金鑰，值：產生的樣式名稱（例如 N1, D1）
    private readonly Dictionary<string, string> _formatCache = new(StringComparer.Ordinal);
    private int _styleCounter;

    /// <summary>
    /// 初始化 <see cref="OdfNumberFormatter"/> 類別的新執行個體。
    /// </summary>
    /// <param name="contentRoot">內容 XML 的根節點</param>
    /// <param name="stylesRoot">樣式 XML 的根節點</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="contentRoot"/> 或 <paramref name="stylesRoot"/> 為 null 時拋出</exception>
    public OdfNumberFormatter(OdfNode contentRoot, OdfNode stylesRoot)
    {
        _contentRoot = contentRoot ?? throw new ArgumentNullException(nameof(contentRoot));
        _stylesRoot = stylesRoot ?? throw new ArgumentNullException(nameof(stylesRoot));
        PopulateCacheFromExistingStyles();
    }

    /// <summary>
    /// 註冊 .NET 格式字串，必要時將其翻譯為 ODF 樣式，並傳回要參考的樣式名稱。
    /// </summary>
    /// <param name="dotNetFormat">.NET 格式字串</param>
    /// <param name="culture">地區設定資訊</param>
    /// <returns>註冊或建立的樣式名稱</returns>
    public string GetOrCreateNumberStyle(string dotNetFormat, CultureInfo? culture = null)
    {
        var cult = culture ?? CultureInfo.InvariantCulture;
        string normalized = ResolveStandardFormat(dotNetFormat, cult);

        // 1. 建立格式資訊
        FormatInfo info = ParsePattern(normalized);

        // 2. 建立用於金鑰序列化的臨時樣式節點
        OdfNode tempNode = CreateStyleNode(string.Empty, info);
        string canonicalKey = SerializeOdfStyleStructure(tempNode);

        // 3. 檢查快取
        if (_formatCache.TryGetValue(canonicalKey, out string? existingStyleName))
        {
            return existingStyleName;
        }

        // 4. 判斷前綴並產生唯一名稱
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

        // 設定樣式節點名稱並附加至 DOM
        tempNode.SetAttribute("name", OdfNamespaces.Style, generatedName, "style");
        var automaticStyles = GetOrCreateAutomaticStylesNode();
        automaticStyles.AppendChild(tempNode);

        _formatCache[canonicalKey] = generatedName;
        return generatedName;
    }

    /// <summary>
    /// 將樣式名稱解析為節點。若找不到，則傳回後備的 Standard 數字樣式以防止 NullReferenceException。
    /// </summary>
    /// <param name="styleName">樣式名稱</param>
    /// <returns>解析後的樣式節點</returns>
    public OdfNode GetNumberStyleNode(string styleName)
    {
        if (string.IsNullOrEmpty(styleName))
        {
            return GetOrCreateStandardFallbackNode("Standard");
        }

        OdfNode? styleNode = FindStyleInDOM(styleName);
        if (styleNode is not null)
        {
            return styleNode;
        }

        OdfKitDiagnostics.Warn($"找不到參考的數字樣式 '{styleName}'。後退至 Standard 樣式。");
        return GetOrCreateStandardFallbackNode(styleName);
    }

    #region 內部解析與翻譯邏輯

    /// <summary>
    /// 解析標準格式。
    /// </summary>
    /// <param name="format">格式字串</param>
    /// <param name="culture">地區設定資訊</param>
    /// <returns>解析後的標準格式字串</returns>
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
        FormatInfo info = new();

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
            if ("yMdhHmst/:".IndexOf(c) >= 0) return true;
        }
        return false;
    }

    private static bool IsTimeOnlyPattern(string pattern)
    {
        bool hasDate = false;
        bool hasTime = false;
        foreach (char c in pattern)
        {
            if ("yMd".IndexOf(c) >= 0) hasDate = true;
            if ("hHms".IndexOf(c) >= 0) hasTime = true;
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
        StringBuilder sb = new();
        bool inQuote = false;
        for (int i = 0; i < pattern.Length; i++)
        {
            char c = pattern[i];
            if (c == '\'') { inQuote = !inQuote; continue; }
            if (inQuote) continue;
            if (c == '\\') { i++; continue; }
            if ("0#.,".IndexOf(c) >= 0) sb.Append(c);
        }
        return sb.ToString();
    }

    #endregion

    #region DOM 操作與快取去重

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

        OdfNode styleNode = new(OdfNodeType.Element, localName, OdfNamespaces.Number, "number");
        if (!string.IsNullOrEmpty(styleName))
        {
            styleNode.SetAttribute("name", OdfNamespaces.Style, styleName, "style");
        }

        switch (info.Type)
        {
            case FormatType.Number:
            case FormatType.Percentage:
                {
                    OdfNode numNode = new(OdfNodeType.Element, "number", OdfNamespaces.Number, "number");
                    numNode.SetAttribute("decimal-places", OdfNamespaces.Number, info.DecimalPlaces.ToString(CultureInfo.InvariantCulture), "number");
                    numNode.SetAttribute("min-integer-digits", OdfNamespaces.Number, info.MinIntegerDigits.ToString(CultureInfo.InvariantCulture), "number");
                    if (info.Grouping) numNode.SetAttribute("grouping", OdfNamespaces.Number, "true", "number");
                    styleNode.AppendChild(numNode);

                    if (info.Type == FormatType.Percentage)
                    {
                        OdfNode textNode = new(OdfNodeType.Element, "text", OdfNamespaces.Number, "number");
                        textNode.TextContent = "%";
                        styleNode.AppendChild(textNode);
                    }
                }
                break;

            case FormatType.Currency:
                {
                    OdfNode symbolNode = new(OdfNodeType.Element, "currency-symbol", OdfNamespaces.Number, "number");
                    symbolNode.TextContent = info.CurrencySymbol;
                    styleNode.AppendChild(symbolNode);

                    OdfNode numNode = new(OdfNodeType.Element, "number", OdfNamespaces.Number, "number");
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
                            OdfNode textNode = new(OdfNodeType.Element, "text", OdfNamespaces.Number, "number");
                            textNode.TextContent = token.Token;
                            styleNode.AppendChild(textNode);
                        }
                        else
                        {
                            OdfNode? partNode = CreateDateTimePartNode(token.Token, is12Hour);
                            if (partNode is not null) styleNode.AppendChild(partNode);
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
        StringBuilder sb = new();
        sb.Append(node.LocalName).Append('|');

        foreach (var child in node.Children)
        {
            sb.Append('[').Append(child.LocalName).Append(':');
            List<OdfAttributeName> attrs = [..child.Attributes.Keys];
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
        if (parentNode is null) return;
        foreach (var child in parentNode.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Number &&
                (child.LocalName == "number-style" || child.LocalName == "currency-style" ||
                 child.LocalName == "percentage-style" || child.LocalName == "date-style" ||
                 child.LocalName == "time-style"))
            {
                string? name = child.GetAttribute("name", OdfNamespaces.Style);
                if (name is not null)
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
        return FindStyleInDOM(name) is not null;
    }

    private OdfNode? FindStyleInDOM(string name)
    {
        var style = FindStyleInParent(FindChildElement(_contentRoot, "automatic-styles", OdfNamespaces.Office), name);
        if (style is not null) return style;

        style = FindStyleInParent(FindChildElement(_stylesRoot, "automatic-styles", OdfNamespaces.Office), name);
        if (style is not null) return style;

        return FindStyleInParent(FindChildElement(_stylesRoot, "styles", OdfNamespaces.Office), name);
    }

    private OdfNode? FindStyleInParent(OdfNode? parentNode, string name)
    {
        if (parentNode is null) return null;
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
        OdfNode fallback = new(OdfNodeType.Element, "number-style", OdfNamespaces.Number, "number");
        fallback.SetAttribute("name", OdfNamespaces.Style, styleName, "style");
        OdfNode num = new(OdfNodeType.Element, "number", OdfNamespaces.Number, "number");
        num.SetAttribute("decimal-places", OdfNamespaces.Number, "0", "number");
        num.SetAttribute("min-integer-digits", OdfNamespaces.Number, "1", "number");
        fallback.AppendChild(num);
        return fallback;
    }

    private OdfNode GetOrCreateAutomaticStylesNode()
    {
        var contentAuto = FindChildElement(_contentRoot, "automatic-styles", OdfNamespaces.Office);
        if (contentAuto is null)
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

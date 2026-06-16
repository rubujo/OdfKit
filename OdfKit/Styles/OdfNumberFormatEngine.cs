using System;
using System.Globalization;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Styles;

/// <summary>
/// ODF 數字格式引擎，依 <c>number:*-style</c> 格式定義節點格式化數值。
/// </summary>
public static class OdfNumberFormatEngine
{
    /// <summary>
    /// 依 ODF 數字/貨幣/百分比格式定義節點格式化浮點數值。
    /// </summary>
    /// <param name="value">要格式化的數值。</param>
    /// <param name="numberStyleNode">格式定義根節點（例如 <c>number:number-style</c>）。</param>
    /// <returns>格式化後的字串。</returns>
    public static string Format(double value, OdfNode numberStyleNode)
    {
        if (numberStyleNode is null)
            throw new ArgumentNullException(nameof(numberStyleNode));
        string styleName = numberStyleNode.LocalName;

        if (styleName == "percentage-style")
            return FormatPercentage(value, numberStyleNode);

        if (styleName == "boolean-style")
            return value != 0 ? "TRUE" : "FALSE";

        if (styleName == "currency-style")
            return FormatCurrency(value, numberStyleNode);

        return FormatNumber(value, numberStyleNode);
    }

    /// <summary>
    /// 依 ODF 日期/時間格式定義節點格式化日期時間值。
    /// </summary>
    /// <param name="value">要格式化的日期時間值。</param>
    /// <param name="dateStyleNode">格式定義根節點（例如 <c>number:date-style</c>）。</param>
    /// <returns>格式化後的字串。</returns>
    public static string Format(DateTime value, OdfNode dateStyleNode)
    {
        if (dateStyleNode is null)
            throw new ArgumentNullException(nameof(dateStyleNode));
        return dateStyleNode.LocalName == "time-style"
            ? FormatTime(value, dateStyleNode)
            : FormatDate(value, dateStyleNode);
    }

    /// <summary>
    /// 依樣式名稱從文件樣式 DOM 查找對應格式節點並格式化。
    /// </summary>
    /// <param name="value">要格式化的值（double、DateTime、bool、string）。</param>
    /// <param name="styleName">ODF 數字樣式名稱（<c>style:data-style-name</c> 的值）。</param>
    /// <param name="stylesDom">styles.xml 或 content.xml 的 DOM 根節點。</param>
    /// <returns>格式化後的字串；若找不到對應樣式則回傳原始值的字串表示。</returns>
    public static string Format(object? value, string styleName, OdfNode stylesDom)
    {
        if (value is null)
            return string.Empty;
        if (string.IsNullOrEmpty(styleName))
            return value.ToString() ?? string.Empty;

        OdfNode? formatNode = FindFormatNode(stylesDom, styleName);
        if (formatNode is null)
            return value.ToString() ?? string.Empty;

        return value switch
        {
            double d => Format(d, formatNode),
            float f => Format((double)f, formatNode),
            int i => Format((double)i, formatNode),
            long l => Format((double)l, formatNode),
            DateTime dt => Format(dt, formatNode),
            bool b => b ? "TRUE" : "FALSE",
            _ => value.ToString() ?? string.Empty
        };
    }

    /// <summary>
    /// 在 stylesDom 中查找指定名稱的 number:*-style 節點。
    /// </summary>
    public static OdfNode? FindFormatNode(OdfNode stylesDom, string styleName)
    {
        return FindInTree(stylesDom, styleName);
    }

    private static OdfNode? FindInTree(OdfNode node, string styleName)
    {
        foreach (var child in node.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Number
                && IsFormatStyleElement(child.LocalName))
            {
                string? name = child.GetAttribute("name", OdfNamespaces.Style);
                if (name == styleName)
                    return child;
            }
            var found = FindInTree(child, styleName);
            if (found is not null)
                return found;
        }
        return null;
    }

    private static bool IsFormatStyleElement(string localName) => localName is
        "number-style" or "currency-style" or "percentage-style" or
        "date-style" or "time-style" or "boolean-style" or "text-style";

    private static string FormatNumber(double value, OdfNode styleNode)
    {
        var sb = new StringBuilder();
        foreach (var child in styleNode.Children)
        {
            if (child.NamespaceUri != OdfNamespaces.Number)
                continue;
            switch (child.LocalName)
            {
                case "number":
                    sb.Append(FormatNumberPart(value, child));
                    break;
                case "text":
                    sb.Append(child.TextContent);
                    break;
                case "currency-symbol":
                    sb.Append(child.TextContent);
                    break;
            }
        }
        return sb.Length > 0 ? sb.ToString() : value.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatCurrency(double value, OdfNode styleNode)
    {
        return FormatNumber(value, styleNode);
    }

    private static string FormatPercentage(double value, OdfNode styleNode)
    {
        var sb = new StringBuilder();
        foreach (var child in styleNode.Children)
        {
            if (child.NamespaceUri != OdfNamespaces.Number)
                continue;
            switch (child.LocalName)
            {
                case "number":
                    sb.Append(FormatNumberPart(value * 100, child));
                    break;
                case "text":
                    sb.Append(child.TextContent);
                    break;
            }
        }
        return sb.Length > 0 ? sb.ToString() : (value * 100).ToString("0.00", CultureInfo.InvariantCulture) + "%";
    }

    private static string FormatNumberPart(double value, OdfNode numberNode)
    {
        string? decStr = numberNode.GetAttribute("decimal-places", OdfNamespaces.Number);
        int decimals = int.TryParse(decStr, out int d) ? d : 2;

        string? minIntStr = numberNode.GetAttribute("min-integer-digits", OdfNamespaces.Number);
        int minInt = int.TryParse(minIntStr, out int m) ? m : 1;

        bool grouping = numberNode.GetAttribute("grouping", OdfNamespaces.Number) == "true";

        string fmt = grouping ? "#,##0" : "0";
        if (minInt > 1)
        {
            fmt = grouping
                ? "#," + new string('0', minInt)
                : new string('0', minInt);
        }
        if (decimals > 0)
        {
            fmt += "." + new string('0', decimals);
        }

        return value.ToString(fmt, CultureInfo.InvariantCulture);
    }

    private static string FormatDate(DateTime dt, OdfNode styleNode)
    {
        var sb = new StringBuilder();
        foreach (var child in styleNode.Children)
        {
            if (child.NamespaceUri != OdfNamespaces.Number)
                continue;
            switch (child.LocalName)
            {
                case "year":
                    bool longYear = child.GetAttribute("style", OdfNamespaces.Number) == "long";
                    sb.Append(longYear ? dt.Year.ToString("D4") : (dt.Year % 100).ToString("D2"));
                    break;
                case "month":
                    bool longMonth = child.GetAttribute("style", OdfNamespaces.Number) == "long";
                    sb.Append(longMonth ? dt.Month.ToString("D2") : dt.Month.ToString());
                    break;
                case "day":
                    bool longDay = child.GetAttribute("style", OdfNamespaces.Number) == "long";
                    sb.Append(longDay ? dt.Day.ToString("D2") : dt.Day.ToString());
                    break;
                case "day-of-week":
                    bool longDow = child.GetAttribute("style", OdfNamespaces.Number) == "long";
                    sb.Append(longDow
                        ? dt.ToString("dddd", CultureInfo.InvariantCulture)
                        : dt.ToString("ddd", CultureInfo.InvariantCulture));
                    break;
                case "text":
                    sb.Append(child.TextContent);
                    break;
            }
        }
        return sb.Length > 0 ? sb.ToString() : dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string FormatTime(DateTime dt, OdfNode styleNode)
    {
        var sb = new StringBuilder();
        foreach (var child in styleNode.Children)
        {
            if (child.NamespaceUri != OdfNamespaces.Number)
                continue;
            switch (child.LocalName)
            {
                case "hours":
                    bool longH = child.GetAttribute("style", OdfNamespaces.Number) == "long";
                    sb.Append(longH ? dt.Hour.ToString("D2") : dt.Hour.ToString());
                    break;
                case "minutes":
                    bool longM = child.GetAttribute("style", OdfNamespaces.Number) == "long";
                    sb.Append(longM ? dt.Minute.ToString("D2") : dt.Minute.ToString());
                    break;
                case "seconds":
                    bool longS = child.GetAttribute("style", OdfNamespaces.Number) == "long";
                    string? decPlaces = child.GetAttribute("decimal-places", OdfNamespaces.Number);
                    int dec = int.TryParse(decPlaces, out int sdec) ? sdec : 0;
                    string secStr = longS ? dt.Second.ToString("D2") : dt.Second.ToString();
                    if (dec > 0)
                    {
                        secStr += "." + (dt.Millisecond / 1000.0).ToString("F" + dec, CultureInfo.InvariantCulture).Substring(2);
                    }
                    sb.Append(secStr);
                    break;
                case "am-pm":
                    sb.Append(dt.Hour < 12 ? "AM" : "PM");
                    break;
                case "text":
                    sb.Append(child.TextContent);
                    break;
            }
        }
        return sb.Length > 0 ? sb.ToString() : dt.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }
}

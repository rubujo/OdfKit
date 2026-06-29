using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

public partial class TextDocument
{
    /// <summary>
    /// Imports an HTML / CSS stylesheet, writing mappable rules into the ODT's <c>styles.xml</c>.
    /// 匯入 HTML / CSS 樣式表，並將可對應的規則寫入 ODT 的 <c>styles.xml</c>。
    /// </summary>
    /// <param name="cssString">The CSS stylesheet content to import. / 要匯入的 CSS 樣式表內容。</param>
    /// <returns>A map from CSS selectors to the generated ODF style names. / CSS selector 與產生的 ODF 樣式名稱對照表。</returns>
    public IReadOnlyDictionary<string, string> ImportHtmlStyles(string cssString)
    {
        if (cssString is null)
            throw new ArgumentNullException(nameof(cssString));

        Dictionary<string, string> imported = new(StringComparer.Ordinal);
        string css = Regex.Replace(cssString, @"/\*[\s\S]*?\*/", string.Empty);
        foreach (Match rule in Regex.Matches(css, @"(?<selectors>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.Multiline))
        {
            Dictionary<string, string> declarations = ParseCssDeclarations(rule.Groups["body"].Value);
            if (declarations.Count == 0)
                continue;

            string[] selectors = rule.Groups["selectors"].Value.Split(',');
            foreach (string rawSelector in selectors)
            {
                string selector = rawSelector.Trim();
                if (selector.Length == 0)
                    continue;

                string family = ResolveStyleFamily(selector);
                string styleName = CreateStyleName(selector, family);
                OdfNode styleNode = FindOrCreateCommonStyle(styleName, family);
                ApplyCssDeclarations(styleNode, declarations);
                imported[selector] = styleName;
            }
        }

        StyleEngine.RebuildStyleIndex();
        return imported;
    }

    private OdfNode FindOrCreateCommonStyle(string styleName, string family)
    {
        OdfNode styles = FindOrCreateChild(StylesDom, "styles", OdfNamespaces.Office, "office");
        foreach (OdfNode child in styles.Children)
        {
            if (child.NodeType == OdfNodeType.Element &&
                child.LocalName == "style" &&
                child.NamespaceUri == OdfNamespaces.Style &&
                child.GetAttribute("name", OdfNamespaces.Style) == styleName)
            {
                child.SetAttribute("family", OdfNamespaces.Style, family, "style");
                return child;
            }
        }

        OdfNode styleNode = new(OdfNodeType.Element, "style", OdfNamespaces.Style, "style");
        styleNode.SetAttribute("name", OdfNamespaces.Style, styleName, "style");
        styleNode.SetAttribute("family", OdfNamespaces.Style, family, "style");
        styles.AppendChild(styleNode);
        return styleNode;
    }

    private static void ApplyCssDeclarations(OdfNode styleNode, Dictionary<string, string> declarations)
    {
        foreach (KeyValuePair<string, string> declaration in declarations)
        {
            if (TryMapTextProperty(declaration.Key, declaration.Value, out string? localName, out string? nsUri, out string? value, out string? prefix))
            {
                SetStyleProperty(styleNode, "text-properties", localName, nsUri, value, prefix);
            }

            if (TryMapParagraphProperty(declaration.Key, declaration.Value, out localName, out nsUri, out value, out prefix))
            {
                SetStyleProperty(styleNode, "paragraph-properties", localName, nsUri, value, prefix);
            }
        }
    }

    private static void SetStyleProperty(OdfNode styleNode, string propertyElementName, string localName, string nsUri, string value, string prefix)
    {
        OdfNode? properties = null;
        foreach (OdfNode child in styleNode.Children)
        {
            if (child.NodeType == OdfNodeType.Element &&
                child.LocalName == propertyElementName &&
                child.NamespaceUri == OdfNamespaces.Style)
            {
                properties = child;
                break;
            }
        }

        if (properties is null)
        {
            properties = new OdfNode(OdfNodeType.Element, propertyElementName, OdfNamespaces.Style, "style");
            styleNode.AppendChild(properties);
        }

        properties.SetAttribute(localName, nsUri, value, prefix);
    }

    private static Dictionary<string, string> ParseCssDeclarations(string body)
    {
        Dictionary<string, string> declarations = new(StringComparer.OrdinalIgnoreCase);
        foreach (string part in body.Split(';'))
        {
            int colonIndex = part.IndexOf(':');
            if (colonIndex <= 0)
                continue;

            string name = part.Substring(0, colonIndex).Trim();
            string value = part.Substring(colonIndex + 1).Trim();
            if (name.Length > 0 && value.Length > 0)
                declarations[name] = value;
        }

        return declarations;
    }

    private static bool TryMapTextProperty(string cssName, string cssValue, out string localName, out string nsUri, out string value, out string prefix)
    {
        localName = string.Empty;
        nsUri = string.Empty;
        value = NormalizeCssValue(cssValue);
        prefix = "fo";

        switch (cssName.ToLowerInvariant())
        {
            case "font-weight":
                localName = "font-weight";
                nsUri = OdfNamespaces.Fo;
                value = IsBoldValue(value) ? "bold" : value;
                return true;
            case "font-style":
                localName = "font-style";
                nsUri = OdfNamespaces.Fo;
                return true;
            case "font-size":
                localName = "font-size";
                nsUri = OdfNamespaces.Fo;
                value = NormalizeCssLength(value);
                return true;
            case "font-family":
                localName = "font-family";
                nsUri = OdfNamespaces.Fo;
                value = TrimCssQuotes(value);
                return true;
            case "color":
                localName = "color";
                nsUri = OdfNamespaces.Fo;
                value = NormalizeColor(value);
                return true;
            case "text-decoration":
                localName = ContainsOrdinalIgnoreCase(value, "underline")
                    ? "text-underline-style"
                    : "text-line-through-style";
                nsUri = OdfNamespaces.Style;
                prefix = "style";
                value = ContainsOrdinalIgnoreCase(value, "none") ? "none" : "solid";
                return true;
            default:
                return false;
        }
    }

    private static bool TryMapParagraphProperty(string cssName, string cssValue, out string localName, out string nsUri, out string value, out string prefix)
    {
        localName = string.Empty;
        nsUri = string.Empty;
        value = NormalizeCssValue(cssValue);
        prefix = "fo";

        switch (cssName.ToLowerInvariant())
        {
            case "text-align":
                localName = "text-align";
                nsUri = OdfNamespaces.Fo;
                return true;
            case "text-indent":
                localName = "text-indent";
                nsUri = OdfNamespaces.Fo;
                value = NormalizeCssLength(value);
                return true;
            case "line-height":
                localName = "line-height";
                nsUri = OdfNamespaces.Fo;
                value = NormalizeCssLength(value);
                return true;
            case "margin-left":
            case "margin-right":
            case "margin-top":
            case "margin-bottom":
                localName = cssName.ToLowerInvariant();
                nsUri = OdfNamespaces.Fo;
                value = NormalizeCssLength(value);
                return true;
            case "background-color":
                localName = "background-color";
                nsUri = OdfNamespaces.Fo;
                value = NormalizeColor(value);
                return true;
            default:
                return false;
        }
    }

    private static string ResolveStyleFamily(string selector)
    {
        string normalized = selector.Trim().ToLowerInvariant();
        return normalized.StartsWith("span", StringComparison.Ordinal) ||
            normalized.StartsWith("strong", StringComparison.Ordinal) ||
            normalized.StartsWith("em", StringComparison.Ordinal) ||
            normalized.StartsWith("a", StringComparison.Ordinal)
            ? "text"
            : "paragraph";
    }

    private static string CreateStyleName(string selector, string family)
    {
        StringBuilder builder = new("HTML_");
        builder.Append(family == "text" ? "T_" : "P_");
        foreach (char ch in selector)
        {
            if (char.IsLetterOrDigit(ch))
                builder.Append(ch);
            else if (ch is '.' or '#' or '-' or '_')
                builder.Append('_');
        }

        return builder.ToString().TrimEnd('_');
    }

    private static string NormalizeCssValue(string value)
        => value.Trim().TrimEnd('!').Trim();

    private static string NormalizeCssLength(string value)
    {
        string trimmed = NormalizeCssValue(value).ToLowerInvariant();
        if (trimmed.EndsWith("px", StringComparison.Ordinal) &&
            double.TryParse(trimmed.Substring(0, trimmed.Length - 2), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double pixels))
        {
            return (pixels * 0.75d).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + "pt";
        }

        return trimmed;
    }

    private static string NormalizeColor(string value)
    {
        string trimmed = NormalizeCssValue(value);
        return trimmed.Equals("black", StringComparison.OrdinalIgnoreCase) ? "#000000" :
            trimmed.Equals("white", StringComparison.OrdinalIgnoreCase) ? "#FFFFFF" :
            trimmed.Equals("red", StringComparison.OrdinalIgnoreCase) ? "#FF0000" :
            trimmed.Equals("blue", StringComparison.OrdinalIgnoreCase) ? "#0000FF" :
            trimmed.Equals("green", StringComparison.OrdinalIgnoreCase) ? "#008000" :
            trimmed;
    }

    private static string TrimCssQuotes(string value)
        => value.Trim().Trim('"', '\'');

    private static bool IsBoldValue(string value)
        => value.Equals("bold", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("bolder", StringComparison.OrdinalIgnoreCase) ||
            value is "700" or "800" or "900";

    private static bool ContainsOrdinalIgnoreCase(string text, string value)
        => text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
}

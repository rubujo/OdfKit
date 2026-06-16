using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Styles;

public partial class OdfNumberFormatter
{
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
                    if (info.Grouping)
                        numNode.SetAttribute("grouping", OdfNamespaces.Number, "true", "number");
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
                    if (info.Grouping)
                        numNode.SetAttribute("grouping", OdfNamespaces.Number, "true", "number");
                    styleNode.AppendChild(numNode);
                }
                break;

            case FormatType.Date:
            case FormatType.Time:
                {
                    bool is12Hour = false;
                    foreach (var t in info.DateTimeTokens)
                    {
                        if (!t.IsLiteral && (t.Token == "tt" || t.Token == "t"))
                            is12Hour = true;
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
                            if (partNode is not null)
                                styleNode.AppendChild(partNode);
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
            List<OdfAttributeName> attrs = [.. child.Attributes.Keys];
            attrs.Sort((x, y) => string.Compare(x.LocalName, y.LocalName, StringComparison.Ordinal));

            foreach (var attr in attrs)
            {
                if (attr.LocalName == "name" && attr.NamespaceUri == OdfNamespaces.Style)
                    continue;
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
        if (parentNode is null)
            return;
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
        if (style is not null)
            return style;

        style = FindStyleInParent(FindChildElement(_stylesRoot, "automatic-styles", OdfNamespaces.Office), name);
        if (style is not null)
            return style;

        return FindStyleInParent(FindChildElement(_stylesRoot, "styles", OdfNamespaces.Office), name);
    }

    private OdfNode? FindStyleInParent(OdfNode? parentNode, string name)
    {
        if (parentNode is null)
            return null;
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

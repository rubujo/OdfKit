using System;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

public partial class OdfCell
{
    #region Borders, Conditional Format & Display

    /// <summary>
    /// Sets the border style for all four sides of this cell.
    /// 設定此儲存格的四面框線樣式。
    /// </summary>
    /// <param name="top">The top border. / 上框線。</param>
    /// <param name="bottom">The bottom border. / 下框線。</param>
    /// <param name="left">The left border. / 左框線。</param>
    /// <param name="right">The right border. / 右框線。</param>
    public void SetBorders(OdfBorder? top, OdfBorder? bottom, OdfBorder? left, OdfBorder? right)
    {
        if (top.HasValue)
            SetStyleProperty("table-cell-properties", "border-top", OdfNamespaces.Fo, top.Value.ToString(), "fo");
        if (bottom.HasValue)
            SetStyleProperty("table-cell-properties", "border-bottom", OdfNamespaces.Fo, bottom.Value.ToString(), "fo");
        if (left.HasValue)
            SetStyleProperty("table-cell-properties", "border-left", OdfNamespaces.Fo, left.Value.ToString(), "fo");
        if (right.HasValue)
            SetStyleProperty("table-cell-properties", "border-right", OdfNamespaces.Fo, right.Value.ToString(), "fo");
    }

    /// <summary>
    /// Adds a conditional formatting map rule.
    /// 新增條件格式對應規則。
    /// </summary>
    /// <param name="condition">The condition value, such as <c>cell-content()=1</c>. / 條件值，例如 <c>cell-content()=1</c>。</param>
    /// <param name="applyStyleName">The formatting style name to apply. / 要套用的格式樣式名稱。</param>
    /// <param name="baseCell">The base cell address. / 基準儲存格位址。</param>
    public void AddConditionalFormatMap(string condition, string applyStyleName, OdfCellAddress? baseCell = null)
    {
        var styleNode = _doc.StyleEngine.GetOrCreateLocalStyle(Node, "table-cell");
        var mapNode = new OdfNode(OdfNodeType.Element, "map", OdfNamespaces.Style, "style");
        mapNode.SetAttribute("condition", OdfNamespaces.Style, condition, "style");
        mapNode.SetAttribute("apply-style-name", OdfNamespaces.Style, applyStyleName, "style");
        if (baseCell.HasValue)
        {
            mapNode.SetAttribute("base-cell-address", OdfNamespaces.Style, baseCell.Value.ToOdfString(false), "style");
        }
        styleNode.AppendChild(mapNode);
    }

    /// <summary>
    /// Gets the display value formatted by the applied number format style, or <see cref="DisplayText"/> when no style definition exists.
    /// 取得依套用數字格式樣式格式化後的顯示值；若無樣式定義則回傳 <see cref="DisplayText"/>。
    /// </summary>
    public string FormattedValue
    {
        get
        {
            string? cellStyleName = StyleName;
            if (string.IsNullOrEmpty(cellStyleName))
                return DisplayText;

            string? dataStyleName = FindDataStyleName(cellStyleName!);
            if (string.IsNullOrEmpty(dataStyleName))
                return DisplayText;

            OdfNode? formatNode = OdfKit.Styles.OdfNumberFormatEngine.FindFormatNode(_doc.ContentDom, dataStyleName!)
                ?? OdfKit.Styles.OdfNumberFormatEngine.FindFormatNode(_doc.StylesDom, dataStyleName!);
            if (formatNode is null)
                return DisplayText;

            return ValueType switch
            {
                "float" when double.TryParse(RawValue, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double dbl)
                    => OdfKit.Styles.OdfNumberFormatEngine.Format(dbl, formatNode),
                "date" when DateTime.TryParse(
                    Node.GetAttribute("date-value", OdfNamespaces.Office),
                    CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dt)
                    => OdfKit.Styles.OdfNumberFormatEngine.Format(dt, formatNode),
                "boolean" when bool.TryParse(
                    Node.GetAttribute("boolean-value", OdfNamespaces.Office), out bool flag)
                    => flag ? "TRUE" : "FALSE",
                _ => DisplayText
            };
        }
    }

    private string? FindDataStyleName(string cellStyle)
    {
        return SearchForDataStyle(_doc.ContentDom, cellStyle)
            ?? SearchForDataStyle(_doc.StylesDom, cellStyle);
    }

    private static string? SearchForDataStyle(OdfNode root, string cellStyle)
    {
        foreach (var child in root.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Office
                && (child.LocalName == "automatic-styles" || child.LocalName == "styles"))
            {
                foreach (var style in child.Children)
                {
                    if (style.NamespaceUri == OdfNamespaces.Style
                        && style.LocalName == "style"
                        && style.GetAttribute("name", OdfNamespaces.Style) == cellStyle)
                    {
                        return style.GetAttribute("data-style-name", OdfNamespaces.Style);
                    }
                }
            }
        }
        return null;
    }

    private void SetStyleProperty(string propertiesElement, string propertyAttr, string propertyNs, string value, string propertyPrefix)
    {
        _doc.StyleEngine.SetLocalStyleProperty(Node, "table-cell", propertiesElement, propertyAttr, propertyNs, value, propertyPrefix);
    }

    #endregion
}

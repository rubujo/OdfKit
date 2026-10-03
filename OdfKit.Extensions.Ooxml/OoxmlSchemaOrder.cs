using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using WP = DocumentFormat.OpenXml.Wordprocessing;

namespace OdfKit.Conversion;

/// <summary>
/// Normalizes the child order of WordprocessingML property containers to the ECMA-376 sequence and adds missing table grids.
/// 將 WordprocessingML 屬性容器的子元素順序整理為 ECMA-376 規定的序列，並補上缺少的表格欄格線。
/// </summary>
/// <remarks>
/// Word 對屬性容器的子元素順序很嚴格：<c>w:pPr</c> 的 <c>w:spacing</c> 必須在 <c>w:ind</c> 之前、
/// <c>w:rPr</c> 的 <c>w:b</c> 必須在 <c>w:sz</c> 之前、<c>w:tblBorders</c> 的 <c>w:left</c> 必須在 <c>w:bottom</c> 之前，
/// 而 <c>w:tbl</c> 必須有 <c>w:tblGrid</c>。轉換器逐項附加屬性，順序取決於程式碼的呼叫順序，
/// 因此在輸出前依 schema 序列統一排序，而不是要求每個附加處都記得順序。
/// </remarks>
internal static class OoxmlSchemaOrder
{
    private static readonly Dictionary<string, Dictionary<string, int>> s_orders = new(StringComparer.Ordinal)
    {
        ["pPr"] = Index(
            "pStyle", "keepNext", "keepLines", "pageBreakBefore", "framePr", "widowControl", "numPr",
            "suppressLineNumbers", "pBdr", "shd", "tabs", "suppressAutoHyphens", "kinsoku", "wordWrap",
            "overflowPunct", "topLinePunct", "autoSpaceDE", "autoSpaceDN", "bidi", "adjustRightInd",
            "snapToGrid", "spacing", "ind", "contextualSpacing", "mirrorIndents", "suppressOverlap", "jc",
            "textDirection", "textAlignment", "textboxTightWrap", "outlineLvl", "divId", "cnfStyle", "rPr",
            "sectPr", "pPrChange"),
        ["rPr"] = Index(
            "ins", "del", "moveFrom", "moveTo", "rStyle", "rFonts", "b", "bCs", "i", "iCs", "caps", "smallCaps",
            "strike", "dstrike", "outline", "shadow", "emboss", "imprint", "noProof", "snapToGrid", "vanish",
            "webHidden", "color", "spacing", "w", "kern", "position", "sz", "szCs", "highlight", "u", "effect",
            "bdr", "shd", "fitText", "vertAlign", "rtl", "cs", "em", "lang", "eastAsianLayout", "specVanish",
            "oMath", "rPrChange"),
        ["tblPr"] = Index(
            "tblStyle", "tblpPr", "tblOverlap", "bidiVisual", "tblStyleRowBandSize", "tblStyleColBandSize",
            "tblW", "jc", "tblCellSpacing", "tblInd", "tblBorders", "shd", "tblLayout", "tblCellMar", "tblLook",
            "tblCaption", "tblDescription", "tblPrChange"),
        ["tblBorders"] = Index("top", "left", "start", "bottom", "right", "end", "insideH", "insideV"),
        ["tcBorders"] = Index("top", "left", "start", "bottom", "right", "end", "insideH", "insideV", "tl2br", "tr2bl"),
        ["pBdr"] = Index("top", "left", "bottom", "right", "between", "bar"),
        ["tcPr"] = Index(
            "cnfStyle", "tcW", "gridSpan", "hMerge", "vMerge", "tcBorders", "shd", "noWrap", "tcMar",
            "textDirection", "tcFitText", "vAlign", "hideMark", "headers", "cellIns", "cellDel", "cellMerge",
            "tcPrChange"),
    };

    private static Dictionary<string, int> Index(params string[] names)
    {
        var result = new Dictionary<string, int>(names.Length, StringComparer.Ordinal);
        for (int position = 0; position < names.Length; position++)
        {
            result[names[position]] = position;
        }

        return result;
    }

    /// <summary>
    /// 整理指定根元素底下所有屬性容器的子元素順序，並替沒有 <c>w:tblGrid</c> 的表格補上欄格線。
    /// </summary>
    internal static void Normalize(OpenXmlElement? root)
    {
        if (root is null)
        {
            return;
        }

        foreach (OpenXmlElement element in root.Descendants().ToList())
        {
            if (element is WP.Table table)
            {
                EnsureTableGrid(table);
            }
            else if (element.NamespaceUri == "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
                && s_orders.TryGetValue(element.LocalName, out Dictionary<string, int>? order))
            {
                Reorder(element, order);
            }
        }
    }

    private static void Reorder(OpenXmlElement element, Dictionary<string, int> order)
    {
        List<OpenXmlElement> children = element.ChildElements.ToList();
        if (children.Count < 2)
        {
            return;
        }

        int Rank(OpenXmlElement child) => order.TryGetValue(child.LocalName, out int rank) ? rank : int.MaxValue;

        bool sorted = true;
        for (int index = 1; index < children.Count && sorted; index++)
        {
            sorted = Rank(children[index - 1]) <= Rank(children[index]);
        }

        if (sorted)
        {
            return;
        }

        foreach (OpenXmlElement child in children)
        {
            child.Remove();
        }

        // OrderBy 是穩定排序：同一名稱（例如多個 w:tab）維持原本的相對順序。
        foreach (OpenXmlElement child in children.OrderBy(Rank))
        {
            element.AppendChild(child);
        }
    }

    // 表格至少要有一個 w:tblGrid；欄數取各列（含 gridSpan）最寬的一列，寬度平分預設的文字區寬度。
    private static void EnsureTableGrid(WP.Table table)
    {
        if (table.Elements<WP.TableGrid>().Any())
        {
            return;
        }

        int columns = 0;
        foreach (WP.TableRow row in table.Elements<WP.TableRow>())
        {
            int count = 0;
            foreach (WP.TableCell cell in row.Elements<WP.TableCell>())
            {
                int span = cell.TableCellProperties?.GridSpan?.Val?.Value ?? 1;
                count += Math.Max(1, span);
            }

            columns = Math.Max(columns, count);
        }

        if (columns == 0)
        {
            return;
        }

        const int textWidthTwips = 9026;
        string width = Math.Max(1, textWidthTwips / columns).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var grid = new WP.TableGrid();
        for (int index = 0; index < columns; index++)
        {
            grid.AppendChild(new WP.GridColumn { Width = width });
        }

        WP.TableProperties? properties = table.GetFirstChild<WP.TableProperties>();
        if (properties is not null)
        {
            properties.InsertAfterSelf(grid);
        }
        else
        {
            table.InsertAt(grid, 0);
        }
    }
}

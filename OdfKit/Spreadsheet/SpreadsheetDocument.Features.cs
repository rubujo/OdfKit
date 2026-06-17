using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using OdfKit.Chart;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

public partial class SpreadsheetDocument
{
    #region Named Ranges, Charts & Validation

    /// <summary>
    /// 取得試算表中所有資料驗證規則的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfDataValidationInfo> GetDataValidations() =>
        SpreadsheetDocumentDataValidationReadEngine.GetDataValidations(this);

    /// <summary>
    /// 取得試算表中所有嵌入圖表的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfEmbeddedChartInfo> GetEmbeddedCharts() =>
        SpreadsheetDocumentEmbeddedChartReadEngine.GetEmbeddedCharts(this);

    /// <summary>
    /// 取得試算表中所有工作表的 LibreOffice calcext 條件格式規則摘要清單。
    /// </summary>
    public IReadOnlyList<OdfConditionalFormatInfo> GetConditionalFormats() =>
        SpreadsheetDocumentConditionalFormatReadEngine.GetConditionalFormats(this);

    /// <summary>
    /// 取得試算表中所有工作表的 LibreOffice calcext 走勢圖群組摘要清單。
    /// </summary>
    public IReadOnlyList<OdfSparklineGroupInfo> GetSparklineGroups() =>
        SpreadsheetDocumentConditionalFormatReadEngine.GetSparklineGroups(this);

    /// <summary>
    /// 取得試算表中所有命名範圍的摘要清單（含文件層與各工作表層）。
    /// </summary>
    public IReadOnlyList<OdfNamedRangeInfo> GetNamedRanges() =>
        SpreadsheetDocumentNamedRangeReadEngine.GetNamedRanges(this);

    /// <summary>
    /// 取得試算表中所有具名運算式的摘要清單（含文件層與各工作表層）。
    /// </summary>
    public IReadOnlyList<OdfNamedExpressionInfo> GetNamedExpressions() =>
        SpreadsheetDocumentNamedRangeReadEngine.GetNamedExpressions(this);

    /// <summary>
    /// 取得試算表中所有資料庫範圍的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfDatabaseRangeInfo> GetDatabaseRanges() =>
        SpreadsheetDocumentDatabaseRangeReadEngine.GetDatabaseRanges(this);

    /// <summary>
    /// 新增命名範圍。
    /// </summary>
    /// <param name="name">命名範圍的名稱</param>
    /// <param name="range">儲存格範圍</param>
    /// <param name="baseCell">基準儲存格位址</param>
    public void AddNamedRange(string name, OdfCellRange range, OdfCellAddress? baseCell = null)
    {
        var namedExpressions = FindOrCreateChild(SheetsRoot, "named-expressions", OdfNamespaces.Table, "table");
        var namedRange = new OdfNode(OdfNodeType.Element, "named-range", OdfNamespaces.Table, "table");
        namedRange.SetAttribute("name", OdfNamespaces.Table, name, "table");
        namedRange.SetAttribute("cell-range-address", OdfNamespaces.Table, range.ToOdfString(false), "table");
        if (baseCell.HasValue)
        {
            namedRange.SetAttribute("base-cell-address", OdfNamespaces.Table, baseCell.Value.ToOdfString(false), "table");
        }
        namedExpressions.AppendChild(namedRange);
    }

    /// <summary>
    /// 新增具名運算式。
    /// </summary>
    /// <param name="name">具名運算式的名稱</param>
    /// <param name="expression">公式運算式字串</param>
    /// <param name="baseCell">基準儲存格位址</param>
    public void AddNamedExpression(string name, string expression, OdfCellAddress? baseCell = null)
    {
        var namedExpressions = FindOrCreateChild(SheetsRoot, "named-expressions", OdfNamespaces.Table, "table");
        var namedExpr = new OdfNode(OdfNodeType.Element, "named-expression", OdfNamespaces.Table, "table");
        namedExpr.SetAttribute("name", OdfNamespaces.Table, name, "table");
        namedExpr.SetAttribute("expression", OdfNamespaces.Table, expression, "table");
        if (baseCell.HasValue)
        {
            namedExpr.SetAttribute("base-cell-address", OdfNamespaces.Table, baseCell.Value.ToOdfString(false), "table");
        }
        namedExpressions.AppendChild(namedExpr);
    }

    /// <summary>
    /// 新增資料庫範圍。
    /// </summary>
    /// <param name="name">資料庫範圍名稱</param>
    /// <param name="range">目標儲存格範圍</param>
    /// <returns>新增的 <see cref="OdfDatabaseRange"/> 執行個體</returns>
    public OdfDatabaseRange AddDatabaseRange(string name, OdfCellRange range)
    {
        var databaseRanges = FindOrCreateChild(SheetsRoot, "database-ranges", OdfNamespaces.Table, "table");
        var dbRangeNode = new OdfNode(OdfNodeType.Element, "database-range", OdfNamespaces.Table, "table");
        dbRangeNode.SetAttribute("name", OdfNamespaces.Table, name, "table");
        dbRangeNode.SetAttribute("target-range-address", OdfNamespaces.Table, range.ToOdfString(false), "table");
        databaseRanges.AppendChild(dbRangeNode);
        return new OdfDatabaseRange(dbRangeNode, this);
    }

    /// <summary>
    /// 在指定工作表的儲存格位置插入圖表。
    /// </summary>
    /// <param name="sheetName">工作表名稱。</param>
    /// <param name="anchor">圖表左上角錨定的儲存格位置。</param>
    /// <param name="chart">圖表設定物件。</param>
    public void AddChart(string sheetName, OdfCellAddress anchor, OdfChartDefinition chart)
    {
        if (string.IsNullOrEmpty(sheetName))
            throw new ArgumentException("工作表名稱不可為空。", nameof(sheetName));
        if (chart is null)
            throw new ArgumentNullException(nameof(chart));

        var sheet = GetSheet(sheetName);
        if (sheet is null)
            throw new KeyNotFoundException($"找不到名稱為 '{sheetName}' 的工作表。");

        // 1. 尋找或建立 table:shapes
        OdfNode? shapesNode = null;
        foreach (var child in sheet.TableNode.Children)
        {
            if (child.LocalName == "shapes" && child.NamespaceUri == OdfNamespaces.Table)
            {
                shapesNode = child;
                break;
            }
        }
        if (shapesNode is null)
        {
            shapesNode = new OdfNode(OdfNodeType.Element, "shapes", OdfNamespaces.Table, "table");
            sheet.TableNode.AppendChild(shapesNode);
        }

        // 2. 計算唯一的 Object 名稱
        int objectIndex = 1;
        while (Package.HasEntry($"Object {objectIndex}/content.xml"))
        {
            objectIndex++;
        }
        string objectName = $"Object {objectIndex}";
        string objectDir = $"{objectName}/";

        // 3. 在 table:shapes 底下建立 draw:frame 與 draw:object
        var frameNode = new OdfNode(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
        frameNode.SetAttribute("z-index", OdfNamespaces.Draw, "0", "draw");
        frameNode.SetAttribute("width", OdfNamespaces.Svg, "12cm", "svg");
        frameNode.SetAttribute("height", OdfNamespaces.Svg, "7cm", "svg");
        frameNode.SetAttribute("x", OdfNamespaces.Svg, "1cm", "svg");
        frameNode.SetAttribute("y", OdfNamespaces.Svg, "1cm", "svg");

        string anchorOdf = anchor.ToOdfString(false);
        frameNode.SetAttribute("start-cell-address", OdfNamespaces.Table, anchorOdf, "table");
        frameNode.SetAttribute("end-cell-address", OdfNamespaces.Table, anchorOdf, "table");
        frameNode.SetAttribute("start-x", OdfNamespaces.Table, "0cm", "table");
        frameNode.SetAttribute("start-y", OdfNamespaces.Table, "0cm", "table");
        frameNode.SetAttribute("end-x", OdfNamespaces.Table, "12cm", "table");
        frameNode.SetAttribute("end-y", OdfNamespaces.Table, "7cm", "table");

        var objectNode = new OdfNode(OdfNodeType.Element, "object", OdfNamespaces.Draw, "draw");
        objectNode.SetAttribute("href", OdfNamespaces.XLink, $"./{objectName}", "xlink");
        objectNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        objectNode.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
        objectNode.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");

        frameNode.AppendChild(objectNode);
        shapesNode.AppendChild(frameNode);

        // 4. 建立子封裝中的檔案
        // 4.1 mimetype
        byte[] mimeBytes = System.Text.Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.chart");
        Package.WriteEntry($"{objectDir}mimetype", mimeBytes, string.Empty);

        // 4.2 styles.xml
        string stylesXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" office:version=\"1.3\"><office:styles/><office:automatic-styles/><office:master-styles/></office:document-styles>";
        Package.WriteEntry($"{objectDir}styles.xml", System.Text.Encoding.UTF8.GetBytes(stylesXml), "text/xml");

        // 4.3 content.xml
        string chartClass = chart.ChartType switch
        {
            OdfChartType.Line => "chart:line",
            OdfChartType.Pie => "chart:pie",
            OdfChartType.Area => "chart:area",
            OdfChartType.Scatter => "chart:scatter",
            OdfChartType.Bubble => "chart:bubble",
            _ => "chart:bar"
        };

        string dataRangeStr = chart.DataRange.ToOdfString(true);

        var sb = new System.Text.StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.Append("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" office:version=\"1.3\">");
        sb.Append("<office:body><office:chart>");
        sb.Append($"<chart:chart chart:class=\"{chartClass}\" table:cell-range-address=\"{dataRangeStr}\"");
        if (chart.HasLegend)
        {
            sb.Append(" chart:legend-position=\"end\"");
        }
        sb.Append(">");

        if (!string.IsNullOrEmpty(chart.Title))
        {
            sb.Append("<chart:title><text:p>");
            sb.Append(System.Security.SecurityElement.Escape(chart.Title));
            sb.Append("</text:p></chart:title>");
        }

        sb.Append("<chart:plot-area chart:data-source-has-labels=\"both\">");
        AppendChartSeriesXml(sb, chart, chartClass);
        sb.Append("<chart:axis chart:dimension=\"x\" chart:name=\"primary-x\"/>");
        sb.Append("<chart:axis chart:dimension=\"y\" chart:name=\"primary-y\"/>");
        sb.Append("</chart:plot-area>");

        if (chart.HasLegend)
        {
            sb.Append("<chart:legend chart:legend-position=\"end\"/>");
        }

        sb.Append("</chart:chart></office:chart></office:body></office:document-content>");

        Package.WriteEntry($"{objectDir}content.xml", System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/xml");
    }

    private static void AppendChartSeriesXml(System.Text.StringBuilder sb, OdfChartDefinition chart, string chartClass)
    {
        OdfCellRange range = chart.DataRange;
        int minRow = Math.Min(range.StartAddress.Row, range.EndAddress.Row);
        int maxRow = Math.Max(range.StartAddress.Row, range.EndAddress.Row);
        int minColumn = Math.Min(range.StartAddress.Column, range.EndAddress.Column);
        int maxColumn = Math.Max(range.StartAddress.Column, range.EndAddress.Column);
        string? sheetName = range.StartAddress.SheetName ?? range.EndAddress.SheetName;
        if (maxRow <= minRow || maxColumn <= minColumn)
        {
            return;
        }

        string labelAddress = new OdfCellAddress(minRow, minColumn + 1, sheetName).ToOdfString(false);
        string categoryRange = ToFullOdfRange(sheetName, minRow + 1, minColumn, maxRow, minColumn);
        string valueRange = ToFullOdfRange(sheetName, minRow + 1, minColumn + 1, maxRow, minColumn + 1);
        sb.Append("<chart:series chart:class=\"");
        sb.Append(System.Security.SecurityElement.Escape(chartClass));
        sb.Append("\" chart:label-cell-address=\"");
        sb.Append(System.Security.SecurityElement.Escape(labelAddress));
        sb.Append("\" chart:values-cell-range-address=\"");
        sb.Append(System.Security.SecurityElement.Escape(valueRange));
        sb.Append("\"><chart:domain table:cell-range-address=\"");
        sb.Append(System.Security.SecurityElement.Escape(categoryRange));
        sb.Append("\"/></chart:series>");
    }

    private static string ToFullOdfRange(string? sheetName, int startRow, int startColumn, int endRow, int endColumn)
    {
        string start = new OdfCellAddress(startRow, startColumn, sheetName).ToOdfString(false);
        string end = new OdfCellAddress(endRow, endColumn, sheetName).ToOdfString(false);
        return start + ":" + end;
    }

    /// <summary>
    /// 在指定的工作表中新增資料驗證規則。
    /// </summary>
    /// <param name="sheetName">工作表名稱。</param>
    /// <param name="validation">資料驗證設定物件。</param>
    public void AddDataValidation(string sheetName, OdfDataValidation validation)
    {
        if (string.IsNullOrEmpty(sheetName))
            throw new ArgumentException("工作表名稱不可為空。", nameof(sheetName));
        if (validation is null)
            throw new ArgumentNullException(nameof(validation));

        var sheet = GetSheet(sheetName);
        if (sheet is null)
            throw new KeyNotFoundException($"找不到名稱為 '{sheetName}' 的工作表。");

        // 1. 取得或建立 table:content-validations 節點
        OdfNode? validationsNode = null;
        foreach (var child in SheetsRoot.Children)
        {
            if (child.LocalName == "content-validations" && child.NamespaceUri == OdfNamespaces.Table)
            {
                validationsNode = child;
                break;
            }
        }
        if (validationsNode is null)
        {
            validationsNode = new OdfNode(OdfNodeType.Element, "content-validations", OdfNamespaces.Table, "table");
            if (SheetsRoot.Children.Count > 0)
                SheetsRoot.InsertBefore(validationsNode, SheetsRoot.Children[0]);
            else
                SheetsRoot.AppendChild(validationsNode);
        }

        // 2. 計算唯一的驗證規則名稱
        int validationIndex = 1;
        bool nameExists;
        string validationName;
        do
        {
            validationName = $"val_{validationIndex}";
            nameExists = false;
            foreach (var rule in validationsNode.Children)
            {
                if (rule.GetAttribute("name", OdfNamespaces.Table) == validationName)
                {
                    nameExists = true;
                    break;
                }
            }
            if (nameExists)
                validationIndex++;
        } while (nameExists);

        // 3. 建立 table:content-validation
        var validationNode = new OdfNode(OdfNodeType.Element, "content-validation", OdfNamespaces.Table, "table");
        validationNode.SetAttribute("name", OdfNamespaces.Table, validationName, "table");
        validationNode.SetAttribute("allow-empty-cell", OdfNamespaces.Table, "true", "table");

        // 根據 Condition 決定 table:condition 屬性值
        string conditionStr = validation.Condition switch
        {
            OdfValidationCondition.DecimalBetween => $"and:oooc:isDecimal()and:oooc:isBetween({validation.Formula1},{validation.Formula2})",
            OdfValidationCondition.TextLengthBetween => $"and:oooc:isText()and:oooc:isBetween(oooc:len(),{validation.Formula1},{validation.Formula2})",
            _ => $"and:oooc:isInteger()and:oooc:isBetween({validation.Formula1},{validation.Formula2})"
        };
        validationNode.SetAttribute("condition", OdfNamespaces.Table, conditionStr, "table");

        // 4. 新增 table:error-message 子節點
        if (!string.IsNullOrEmpty(validation.ErrorMessage))
        {
            var errorNode = new OdfNode(OdfNodeType.Element, "error-message", OdfNamespaces.Table, "table");
            errorNode.SetAttribute("message", OdfNamespaces.Table, validation.ErrorMessage, "table");
            if (!string.IsNullOrEmpty(validation.ErrorTitle))
            {
                errorNode.SetAttribute("title", OdfNamespaces.Table, validation.ErrorTitle, "table");
            }
            string alertStyleStr = validation.AlertStyle switch
            {
                OdfValidationAlertStyle.Warning => "warning",
                OdfValidationAlertStyle.Information => "information",
                _ => "stop"
            };
            errorNode.SetAttribute("message-type", OdfNamespaces.Table, alertStyleStr, "table");
            validationNode.AppendChild(errorNode);
        }

        validationsNode.AppendChild(validationNode);

        // 5. 套用至儲存格範圍
        int minRow = Math.Min(validation.ApplyTo.StartAddress.Row, validation.ApplyTo.EndAddress.Row);
        int maxRow = Math.Max(validation.ApplyTo.StartAddress.Row, validation.ApplyTo.EndAddress.Row);
        int minCol = Math.Min(validation.ApplyTo.StartAddress.Column, validation.ApplyTo.EndAddress.Column);
        int maxCol = Math.Max(validation.ApplyTo.StartAddress.Column, validation.ApplyTo.EndAddress.Column);

        for (int r = minRow; r <= maxRow; r++)
        {
            for (int c = minCol; c <= maxCol; c++)
            {
                var cell = sheet.Cells[r, c];
                cell.Node.SetAttribute("content-validation-name", OdfNamespaces.Table, validationName, "table");
            }
        }
    }

    private readonly Dictionary<string, string> _richTextStyleCache = new(StringComparer.Ordinal);

    internal string GetOrCreateCharacterStyle(bool bold, bool italic, bool underline, OdfColor? color, string? fontFamily)
    {
        string key = $"b:{bold}|i:{italic}|u:{underline}|c:{color?.Value ?? ""}|f:{fontFamily ?? ""}";
        if (_richTextStyleCache.TryGetValue(key, out string? cached))
            return cached;

        var autoStyles = ContentDom.FindChildElement("automatic-styles", OdfNamespaces.Office);
        if (autoStyles is null)
        {
            autoStyles = new OdfNode(OdfNodeType.Element, "automatic-styles", OdfNamespaces.Office, "office");
            if (ContentDom.Children.Count > 0)
                ContentDom.InsertBefore(autoStyles, ContentDom.Children[0]);
            else
                ContentDom.AppendChild(autoStyles);
        }

        int idx = _richTextStyleCache.Count + 1;
        string styleName;
        do
        { styleName = $"RT{idx++}"; } while (StyleEngine.StyleExists(styleName));

        var styleNode = new OdfNode(OdfNodeType.Element, "style", OdfNamespaces.Style, "style");
        styleNode.SetAttribute("name", OdfNamespaces.Style, styleName);
        styleNode.SetAttribute("family", OdfNamespaces.Style, "text");

        var props = new OdfNode(OdfNodeType.Element, "text-properties", OdfNamespaces.Style, "style");
        if (bold)
            props.SetAttribute("font-weight", OdfNamespaces.Fo, "bold", "fo");
        if (italic)
            props.SetAttribute("font-style", OdfNamespaces.Fo, "italic", "fo");
        if (underline)
            props.SetAttribute("text-underline-style", OdfNamespaces.Style, "solid", "style");
        if (color.HasValue)
            props.SetAttribute("color", OdfNamespaces.Fo, color.Value.Value, "fo");
        if (!string.IsNullOrEmpty(fontFamily))
            props.SetAttribute("font-name", OdfNamespaces.Style, fontFamily!, "style");
        styleNode.AppendChild(props);
        autoStyles.AppendChild(styleNode);
        StyleEngine.RebuildStyleIndex();

        _richTextStyleCache[key] = styleName;
        return styleName;
    }

    #endregion
}

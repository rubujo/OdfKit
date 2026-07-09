using System.Globalization;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using OdfKit.Chart;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

using OdfKit.Compliance;
namespace OdfKit.Spreadsheet;
/// <summary>
/// Provides the SpreadsheetDocument API.
/// 提供 SpreadsheetDocument API。
/// </summary>

public partial class SpreadsheetDocument
{
    #region Named Ranges, Charts & Validation

    /// <summary>
    /// Gets summaries for all data validation rules in the spreadsheet.
    /// 取得試算表中所有資料驗證規則的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfDataValidationInfo> GetDataValidations() =>
        SpreadsheetDocumentDataValidationReadEngine.GetDataValidations(this);

    /// <summary>
    /// Gets summaries for all embedded charts in the spreadsheet.
    /// 取得試算表中所有嵌入圖表的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfEmbeddedChartInfo> GetEmbeddedCharts() =>
        SpreadsheetDocumentEmbeddedChartReadEngine.GetEmbeddedCharts(this);

    /// <summary>
    /// Gets summaries for LibreOffice calcext conditional formatting rules across all worksheets in the spreadsheet.
    /// 取得試算表中所有工作表的 LibreOffice calcext 條件格式規則摘要清單。
    /// </summary>
    public IReadOnlyList<OdfConditionalFormatInfo> GetConditionalFormats() =>
        SpreadsheetDocumentConditionalFormatReadEngine.GetConditionalFormats(this);

    /// <summary>
    /// Gets summaries for LibreOffice calcext sparkline groups across all worksheets in the spreadsheet.
    /// 取得試算表中所有工作表的 LibreOffice calcext 走勢圖群組摘要清單。
    /// </summary>
    public IReadOnlyList<OdfSparklineGroupInfo> GetSparklineGroups() =>
        SpreadsheetDocumentConditionalFormatReadEngine.GetSparklineGroups(this);

    /// <summary>
    /// Gets summaries for all named ranges in the spreadsheet, including document-level and worksheet-level ranges.
    /// 取得試算表中所有命名範圍的摘要清單（含文件層與各工作表層）。
    /// </summary>
    public IReadOnlyList<OdfNamedRangeInfo> GetNamedRanges() =>
        SpreadsheetDocumentNamedRangeReadEngine.GetNamedRanges(this);

    /// <summary>
    /// Gets summaries for all named expressions in the spreadsheet, including document-level and worksheet-level expressions.
    /// 取得試算表中所有具名運算式的摘要清單（含文件層與各工作表層）。
    /// </summary>
    public IReadOnlyList<OdfNamedExpressionInfo> GetNamedExpressions() =>
        SpreadsheetDocumentNamedRangeReadEngine.GetNamedExpressions(this);

    /// <summary>
    /// Gets summaries for all database ranges in the spreadsheet.
    /// 取得試算表中所有資料庫範圍的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfDatabaseRangeInfo> GetDatabaseRanges() =>
        SpreadsheetDocumentDatabaseRangeReadEngine.GetDatabaseRanges(this);

    /// <summary>
    /// Gets summaries for all worksheets in the spreadsheet that define print areas.
    /// 取得試算表中所有已設定列印範圍的工作表摘要清單。
    /// </summary>
    public IReadOnlyList<OdfSheetPrintAreaInfo> GetPrintAreas() =>
        SpreadsheetDocumentPrintAreaReadEngine.GetPrintAreas(this);

    /// <summary>
    /// Gets summaries for pivot tables across all worksheets in the spreadsheet.
    /// 取得試算表中所有工作表的樞紐分析表摘要清單。
    /// </summary>
    public IReadOnlyList<OdfPivotTableInfo> GetPivotTables() =>
        SpreadsheetDocumentPivotTableReadEngine.GetPivotTables(this);

    /// <summary>
    /// Gets summaries for all worksheets in the spreadsheet that define frozen panes.
    /// 取得試算表中所有已設定凍結窗格的工作表摘要清單。
    /// </summary>
    public IReadOnlyList<OdfSheetFrozenPanesInfo> GetFrozenPanes() =>
        SpreadsheetDocumentFrozenPanesReadEngine.GetFrozenPanes(this);

    /// <summary>
    /// Gets summaries for all worksheets in the spreadsheet that define split panes.
    /// 取得試算表中所有已設定分割窗格的工作表摘要清單。
    /// </summary>
    public IReadOnlyList<OdfSheetSplitPanesInfo> GetSplitPanes() =>
        SpreadsheetDocumentSplitPanesReadEngine.GetSplitPanes(this);
    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public void AddNamedRange(string name, OdfCellRange range) => AddNamedRange(name, range, null);


    /// <summary>
    /// Adds a named range.
    /// 新增命名範圍。
    /// </summary>
    /// <param name="name">The name or identifier. / 命名範圍的名稱</param>
    /// <param name="range">The cell range. / 儲存格範圍</param>
    /// <param name="baseCell">The cell address. / 基準儲存格位址</param>
    public void AddNamedRange(string name, OdfCellRange range, OdfCellAddress? baseCell)
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
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public void AddNamedExpression(string name, string expression) => AddNamedExpression(name, expression, null);


    /// <summary>
    /// Adds a named expression.
    /// 新增具名運算式。
    /// </summary>
    /// <param name="name">The name or identifier. / 具名運算式的名稱</param>
    /// <param name="expression">The value to use. / 公式運算式字串</param>
    /// <param name="baseCell">The cell address. / 基準儲存格位址</param>
    public void AddNamedExpression(string name, string expression, OdfCellAddress? baseCell)
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
    /// Adds a database range.
    /// 新增資料庫範圍。
    /// </summary>
    /// <param name="name">The name or identifier. / 資料庫範圍名稱</param>
    /// <param name="range">The cell range. / 目標儲存格範圍</param>
    /// <returns>The result. / 新增的 <see cref="OdfDatabaseRange"/> 執行個體</returns>
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
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfSpreadsheetTable CreateTable(string name, OdfCellRange range) => CreateTable(name, range, null);


    /// <summary>
    /// Creates a practical spreadsheet table backed by an ODF database range.
    /// 建立由 ODF 資料庫範圍支援的實務試算表表格。
    /// </summary>
    /// <param name="name">The table name. / 表格名稱。</param>
    /// <param name="range">The source cell range. / 來源儲存格範圍。</param>
    /// <param name="options">The table options. / 表格選項。</param>
    /// <returns>The editable table facade. / 可編輯的表格 facade。</returns>
    public OdfSpreadsheetTable CreateTable(string name, OdfCellRange range, OdfSpreadsheetTableOptions? options)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_SpreadsheetDocument_WorksheetCannotBeEmpty_2"), nameof(name));
        }

        if (FindDatabaseRangeNode(name) is not null)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_DuplicateName", name));
        }

        options ??= new OdfSpreadsheetTableOptions();
        OdfDatabaseRange databaseRange = AddDatabaseRange(name, range);
        databaseRange.DisplayFilterButtons = options.DisplayFilterButtons;
        databaseRange.ContainsHeader = options.FirstRowAsHeader;
        if (options.CreateNamedRange)
        {
            AddNamedRange(name, range);
        }

        return new OdfSpreadsheetTable(databaseRange, this, options.FirstRowAsHeader);
    }


    /// <summary>
    /// Gets practical spreadsheet table summaries.
    /// 取得實務試算表表格摘要。
    /// </summary>
    /// <returns>The table summaries. / 表格摘要。</returns>
    public IReadOnlyList<OdfSpreadsheetTableInfo> GetTables() =>
        GetDatabaseRanges()
            .Select(range => new OdfSpreadsheetTableInfo(
                range.Name,
                range.TargetRangeAddress,
                range.ContainsHeader,
                range.DisplayFilterButtons))
            .ToList()
            .AsReadOnly();

    /// <summary>
    /// Finds an editable practical spreadsheet table by name.
    /// 依名稱尋找可編輯的實務試算表表格。
    /// </summary>
    /// <param name="name">The table name. / 表格名稱。</param>
    /// <returns>The table facade, or <see langword="null"/> when not found. / 表格 facade；找不到時為 <see langword="null"/>。</returns>
    public OdfSpreadsheetTable? FindTable(string name)
    {
        OdfNode? node = FindDatabaseRangeNode(name);
        return node is null
            ? null
            : new OdfSpreadsheetTable(new OdfDatabaseRange(node, this), this, node.GetAttribute("contains-header", OdfNamespaces.Table) != "false");
    }

    /// <summary>
    /// Resizes a practical spreadsheet table by name.
    /// 依名稱調整實務試算表表格範圍。
    /// </summary>
    /// <param name="name">The table name. / 表格名稱。</param>
    /// <param name="range">The new cell range. / 新儲存格範圍。</param>
    /// <returns><see langword="true"/> if updated; otherwise <see langword="false"/>. / 若已更新則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool ResizeTable(string name, OdfCellRange range)
    {
        OdfSpreadsheetTable? table = FindTable(name);
        if (table is null)
        {
            return false;
        }

        table.Resize(range);
        UpdateNamedRangeAddress(name, range);
        return true;
    }

    private void UpdateNamedRangeAddress(string name, OdfCellRange range)
    {
        OdfNode? namedExpressions = OdfTableSheetDomHelper.FindChildElement(
            SheetsRoot,
            "named-expressions",
            OdfNamespaces.Table);
        if (namedExpressions is null)
        {
            return;
        }

        foreach (OdfNode child in namedExpressions.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "named-range" &&
                child.NamespaceUri == OdfNamespaces.Table &&
                string.Equals(child.GetAttribute("name", OdfNamespaces.Table), name, StringComparison.Ordinal))
            {
                child.SetAttribute("cell-range-address", OdfNamespaces.Table, range.ToOdfString(false), "table");
            }
        }
    }

    private OdfNode? FindDatabaseRangeNode(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        OdfNode? databaseRanges = OdfTableSheetDomHelper.FindChildElement(
            SheetsRoot,
            "database-ranges",
            OdfNamespaces.Table);
        if (databaseRanges is null)
        {
            return null;
        }

        foreach (OdfNode child in databaseRanges.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "database-range" &&
                child.NamespaceUri == OdfNamespaces.Table &&
                string.Equals(child.GetAttribute("name", OdfNamespaces.Table), name, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>
    /// Inserts a chart at the specified cell position in a worksheet.
    /// 在指定工作表的儲存格位置插入圖表。
    /// </summary>
    /// <param name="sheetName">The name or identifier. / 工作表名稱</param>
    /// <param name="anchor">The cell address. / 圖表左上角錨定的儲存格位置</param>
    /// <param name="chart">The value to use. / 圖表設定物件</param>
    public void AddChart(string sheetName, OdfCellAddress anchor, OdfChartDefinition chart)
    {
        if (string.IsNullOrEmpty(sheetName))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_SpreadsheetDocument_WorksheetCannotBeEmpty_2"), nameof(sheetName));
        if (chart is null)
            throw new ArgumentNullException(nameof(chart));

        var sheet = FindSheet(sheetName);
        if (sheet is null)
            throw new KeyNotFoundException(OdfLocalizer.GetMessage("Err_SpreadsheetDocument_SheetNamedCannotFound_2", sheetName));

        // 1. 尋找或建立 table:shapes，並維持 table:table 的合法子節點順序。
        OdfNode shapesNode = OdfTableSheetDomHelper.FindOrCreateTableShapes(sheet.TableNode);

        // 2. 計算唯一的 Object 名稱
        int objectIndex = 1;
        while (Package.HasEntry($"Object {objectIndex}/content.xml"))
        {
            objectIndex++;
        }
        string objectName = $"Object {objectIndex}";
        string objectDir = $"{objectName}/";

        // 3. 在 table:shapes 底下建立 draw:frame 與 draw:object
        (double anchorXCm, double anchorYCm) = ComputeAnchorOffset(sheet, anchor);
        var frameNode = new OdfNode(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
        frameNode.SetAttribute("z-index", OdfNamespaces.Draw, "0", "draw");
        frameNode.SetAttribute("width", OdfNamespaces.Svg, "12cm", "svg");
        frameNode.SetAttribute("height", OdfNamespaces.Svg, "7cm", "svg");
        frameNode.SetAttribute("x", OdfNamespaces.Svg, anchorXCm.ToString("0.###", CultureInfo.InvariantCulture) + "cm", "svg");
        frameNode.SetAttribute("y", OdfNamespaces.Svg, anchorYCm.ToString("0.###", CultureInfo.InvariantCulture) + "cm", "svg");

        var objectNode = new OdfNode(OdfNodeType.Element, "object", OdfNamespaces.Draw, "draw");
        objectNode.SetAttribute("href", OdfNamespaces.XLink, $"./{objectName}", "xlink");
        objectNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        objectNode.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
        objectNode.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");

        frameNode.AppendChild(objectNode);
        shapesNode.AppendChild(frameNode);

        // 4. 建立子封裝中的檔案
        // 4.1 mimetype
        byte[] mimeBytes = Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.chart");
        Package.WriteEntry($"{objectDir}mimetype", mimeBytes, string.Empty);

        // 4.2 styles.xml
        string stylesXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" office:version=\"1.3\"><office:styles/><office:automatic-styles/><office:master-styles/></office:document-styles>";
        Package.WriteEntry($"{objectDir}styles.xml", Encoding.UTF8.GetBytes(stylesXml), "text/xml");

        // 4.3 content.xml
        string chartClass = chart.ChartType switch
        {
            OdfChartType.Line => "chart:line",
            OdfChartType.Pie => "chart:circle",
            OdfChartType.Area => "chart:area",
            OdfChartType.Scatter => "chart:scatter",
            OdfChartType.Bubble => "chart:bubble",
            OdfChartType.Ring => "chart:ring",
            OdfChartType.Radar => "chart:radar",
            OdfChartType.Stock => "chart:stock",
            _ => "chart:bar"
        };

        string dataRangeStr = chart.DataRange.ToOdfString(false);

        var sb = new StringBuilder();
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

        Package.WriteEntry($"{objectDir}content.xml", Encoding.UTF8.GetBytes(sb.ToString()), "text/xml");
    }
    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfNode AddChartFromRange(string sheetName, OdfCellAddress anchor, OdfCellRange range) => AddChartFromRange(sheetName, anchor, range, OdfChartPreset.Bar, null);

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfNode AddChartFromRange(string sheetName, OdfCellAddress anchor, OdfCellRange range, OdfChartPreset preset) => AddChartFromRange(sheetName, anchor, range, preset, null);


    /// <summary>
    /// Inserts a practical preset chart bound to a worksheet range.
    /// 插入繫結至工作表範圍的實務預設圖表。
    /// </summary>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="anchor">The top-left anchor cell. / 左上角錨定儲存格。</param>
    /// <param name="range">The source cell range. / 來源儲存格範圍。</param>
    /// <param name="preset">The chart preset. / 圖表預設。</param>
    /// <param name="title">The optional chart title. / 選用的圖表標題。</param>
    /// <returns>The created frame node. / 建立完成的框架節點。</returns>
    public OdfNode AddChartFromRange(string sheetName, OdfCellAddress anchor, OdfCellRange range, OdfChartPreset preset, string? title)
    {
        var definition = new OdfChartDefinition
        {
            ChartType = preset.ToChartType(),
            DataRange = range,
            Title = title ?? string.Empty,
            HasLegend = true
        };

        AddChart(sheetName, anchor, definition);
        return GetEmbeddedChartFrame(sheetName);
    }

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfChartDocument InsertChartFromRange(string sheetName, OdfCellAddress anchor, OdfCellRange range) => InsertChartFromRange(sheetName, anchor, range, null);


    /// <summary>
    /// Inserts an editable embedded chart bound to a worksheet range.
    /// 插入繫結至工作表範圍且可繼續編輯的嵌入圖表。
    /// </summary>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="anchor">The top-left anchor cell. / 左上角錨定儲存格。</param>
    /// <param name="range">The source cell range. / 來源儲存格範圍。</param>
    /// <param name="options">The embedded chart options. / 嵌入圖表選項。</param>
    /// <returns>The embedded chart document. / 嵌入的圖表文件。</returns>
    public OdfChartDocument InsertChartFromRange(string sheetName, OdfCellAddress anchor, OdfCellRange range, OdfEmbeddedChartOptions? options)
    {
        if (string.IsNullOrEmpty(sheetName))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_SpreadsheetDocument_WorksheetCannotBeEmpty_2"), nameof(sheetName));
        }

        OdfTableSheet? sheet = FindSheet(sheetName);
        if (sheet is null)
        {
            throw new KeyNotFoundException(OdfLocalizer.GetMessage("Err_SpreadsheetDocument_SheetNamedCannotFound_2", sheetName));
        }

        options ??= new OdfEmbeddedChartOptions();
        (double anchorXCm, double anchorYCm) = ComputeAnchorOffset(sheet, anchor);
        OdfChartDocument chart = sheet.InsertChart(
            range,
            options.Preset.ToChartType(),
            OdfLength.FromCentimeters(anchorXCm),
            OdfLength.FromCentimeters(anchorYCm),
            options.Width,
            options.Height,
            options.FirstRowAsHeader,
            options.FirstColumnAsLabel);

        if (!string.IsNullOrEmpty(options.Title))
        {
            chart.ChartTitle = options.Title;
        }

        chart.LegendPosition = options.LegendPosition;
        chart.XAxisTitle = options.XAxisTitle;
        chart.YAxisTitle = options.YAxisTitle;
        if (!string.IsNullOrWhiteSpace(options.XAxisNumberFormat))
        {
            chart.SetAxisNumberFormat("x", options.XAxisNumberFormat);
        }

        if (!string.IsNullOrWhiteSpace(options.YAxisNumberFormat))
        {
            chart.SetAxisNumberFormat("y", options.YAxisNumberFormat);
        }

        if (options.ShowMajorGridLines.HasValue)
        {
            chart.SetAxisGrid("y", OdfChartGridKind.Major, options.ShowMajorGridLines.Value);
        }

        if (options.ShowMinorGridLines.HasValue)
        {
            chart.SetAxisGrid("y", OdfChartGridKind.Minor, options.ShowMinorGridLines.Value);
        }

        for (int i = 0; i < chart.SeriesCount; i++)
        {
            if (options.DataLabelPreset.HasValue)
            {
                chart.SetSeriesDataLabelPreset(i, options.DataLabelPreset.Value);
            }

            if (i < options.SeriesStyleNames.Count)
            {
                chart.GetSeriesEditor(i).StyleName = options.SeriesStyleNames[i];
            }

            if (i < options.Palette.Count)
            {
                chart.GetSeriesEditor(i).Style.FillColor = options.Palette[i];
            }

            if (i < options.MarkerStyles.Count)
            {
                chart.GetSeriesEditor(i).ApplyMarkerStyle(options.MarkerStyles[i]);
            }
        }

        if (options.ThreeDOptions is not null)
        {
            chart.Apply3DOptions(options.ThreeDOptions);
        }
        else if (options.Preset.IsThreeDimensional())
        {
            chart.Apply3DOptions(new OdfChart3DOptions
            {
                Enabled = true,
                AngleOffset = 45,
                Projection = OdfDr3dProjection.Perspective,
                LightingMode = true
            });
        }

        chart.Save();
        return chart;
    }
    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfChartDocument RefreshChartDataRange(OdfChartDocument chart, string sheetName, OdfCellRange range) => RefreshChartDataRange(chart, sheetName, range, true, true);

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfChartDocument RefreshChartDataRange(OdfChartDocument chart, string sheetName, OdfCellRange range, bool firstRowAsHeader) => RefreshChartDataRange(chart, sheetName, range, firstRowAsHeader, true);



    /// <summary>
    /// Refreshes an embedded chart data range.
    /// 重新設定嵌入圖表的資料範圍。
    /// </summary>
    /// <param name="chart">The embedded chart document. / 嵌入圖表文件。</param>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="range">The source cell range. / 來源儲存格範圍。</param>
    /// <param name="firstRowAsHeader">Whether the first row is treated as series labels. / 首列是否視為序列標籤。</param>
    /// <param name="firstColumnAsLabel">Whether the first column is treated as category labels. / 首欄是否視為分類標籤。</param>
    /// <returns>The updated embedded chart document. / 已更新的嵌入圖表文件。</returns>
    public OdfChartDocument RefreshChartDataRange(OdfChartDocument chart, string sheetName, OdfCellRange range, bool firstRowAsHeader, bool firstColumnAsLabel)
    {
        if (chart is null)
        {
            throw new ArgumentNullException(nameof(chart));
        }

        chart.SetDataRange(sheetName, range, firstRowAsHeader, firstColumnAsLabel);
        chart.Save();
        return chart;
    }


    /// <summary>
    /// Applies practical updates to embedded chart series.
    /// 將實務更新套用至嵌入圖表序列。
    /// </summary>
    /// <param name="chart">The embedded chart document. / 嵌入圖表文件。</param>
    /// <param name="updates">The series updates. / 序列更新。</param>
    /// <returns>The batch update result. / 批次更新結果。</returns>
    public OdfBatchUpdateResult UpdateEmbeddedChartSeries(
        OdfChartDocument chart,
        IEnumerable<OdfEmbeddedChartSeriesUpdate> updates)
    {
        if (chart is null)
        {
            throw new ArgumentNullException(nameof(chart));
        }

        if (updates is null)
        {
            throw new ArgumentNullException(nameof(updates));
        }

        var result = new OdfBatchUpdateResult();
        foreach (OdfEmbeddedChartSeriesUpdate update in updates)
        {
            if (update.Index < 0 || update.Index >= chart.SeriesCount)
            {
                result.MissingNames.Add(update.Index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                continue;
            }

            OdfChartSeries series = chart.GetSeriesEditor(update.Index);
            if (update.StyleName is not null)
            {
                series.StyleName = update.StyleName;
            }

            if (update.AttachedAxis is not null)
            {
                series.AttachedAxis = update.AttachedAxis;
            }

            if (update.DataLabelPreset.HasValue)
            {
                series.SetDataLabelPreset(update.DataLabelPreset.Value);
            }

            if (update.MarkerStyle is not null)
            {
                series.ApplyMarkerStyle(update.MarkerStyle);
            }

            result.UpdatedCount++;
        }

        chart.Save();
        return result;
    }

    /// <summary>
    /// 累加錨點儲存格前所有列高／欄寬，計算 draw:frame 的絕對 svg:x／svg:y 偏移（單位：公分）。
    /// 未明確設定列高／欄寬的儲存格採用 LibreOffice Calc 預設值（列高 0.45cm、欄寬 2.267cm）估算。
    /// </summary>
    private static (double XCm, double YCm) ComputeAnchorOffset(OdfTableSheet sheet, OdfCellAddress anchor)
    {
        const double DefaultRowHeightCm = 0.45;
        const double DefaultColumnWidthCm = 2.267;

        double yCm = 0;
        for (int row = 0; row < anchor.Row; row++)
        {
            yCm += sheet.GetRowHeight(row)?.ToCentimeters() ?? DefaultRowHeightCm;
        }

        double xCm = 0;
        for (int col = 0; col < anchor.Column; col++)
        {
            xCm += sheet.GetColumnWidth(col)?.ToCentimeters() ?? DefaultColumnWidthCm;
        }

        return (xCm, yCm);
    }

    private OdfNode GetEmbeddedChartFrame(string sheetName)
    {
        OdfTableSheet? sheet = FindSheet(sheetName);
        if (sheet is null)
        {
            throw new KeyNotFoundException(OdfLocalizer.GetMessage("Err_SpreadsheetDocument_SheetNamedCannotFound_2", sheetName));
        }

        OdfNode shapesNode = OdfTableSheetDomHelper.FindOrCreateTableShapes(sheet.TableNode);
        for (int i = shapesNode.Children.Count - 1; i >= 0; i--)
        {
            OdfNode child = shapesNode.Children[i];
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "frame" &&
                child.NamespaceUri == OdfNamespaces.Draw)
            {
                return child;
            }
        }

        return shapesNode;
    }

    private static void AppendChartSeriesXml(StringBuilder sb, OdfChartDefinition chart, string chartClass)
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
    /// Adds a data validation rule to the specified worksheet.
    /// 在指定的工作表中新增資料驗證規則。
    /// </summary>
    /// <param name="sheetName">The name or identifier. / 工作表名稱</param>
    /// <param name="validation">The value to use. / 資料驗證設定物件</param>
    public void AddDataValidation(string sheetName, OdfDataValidation validation)
    {
        if (string.IsNullOrEmpty(sheetName))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_SpreadsheetDocument_WorksheetCannotBeEmpty_2"), nameof(sheetName));
        if (validation is null)
            throw new ArgumentNullException(nameof(validation));

        var sheet = FindSheet(sheetName);
        if (sheet is null)
            throw new KeyNotFoundException(OdfLocalizer.GetMessage("Err_SpreadsheetDocument_SheetNamedCannotFound_2", sheetName));

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

        // 根據 Condition 決定 table:condition 屬性值（語法已用真實 LibreOffice 經 UNO API 建立驗證規則後
        // 反向比對 content.xml 確認，與一般猜測的 "oooc:isXxx()" 語法不同）
        string conditionStr = validation.Condition switch
        {
            OdfValidationCondition.DecimalBetween => $"of:cell-content-is-decimal-number() and cell-content-is-between({validation.Formula1},{validation.Formula2})",
            OdfValidationCondition.TextLengthBetween => $"of:cell-content-text-length-is-between({validation.Formula1},{validation.Formula2})",
            _ => $"of:cell-content-is-whole-number() and cell-content-is-between({validation.Formula1},{validation.Formula2})"
        };
        validationNode.SetAttribute("base-cell-address", OdfNamespaces.Table, $"{sheetName}.A1", "table");
        validationNode.SetAttribute("condition", OdfNamespaces.Table, conditionStr, "table");

        // 4. 新增 table:error-message 子節點
        if (!string.IsNullOrEmpty(validation.ErrorMessage))
        {
            var errorNode = new OdfNode(OdfNodeType.Element, "error-message", OdfNamespaces.Table, "table");
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
            var messageParagraph = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text")
            {
                TextContent = validation.ErrorMessage,
            };
            errorNode.AppendChild(messageParagraph);
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

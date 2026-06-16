using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Chart;

/// <summary>
/// 表示 ODF 圖表文件（Chart Document）的類別。
/// </summary>
/// <param name="package">Odf 套件執行個體</param>
/// <param name="subPath">子路徑</param>
public class OdfChartDocument(OdfPackage package, string subPath) : OdfDocument(package, subPath)
{
    /// <summary>
    /// 初始化 <see cref="OdfChartDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">Odf 套件執行個體</param>
    public OdfChartDocument(OdfPackage package) : this(package, string.Empty)
    {
    }

    private readonly bool _initialized = InitMimeType(package);

    /// <summary>
    /// 建立新的 ODC 圖表文件。
    /// </summary>
    /// <returns>新的 <see cref="OdfChartDocument"/> 執行個體。</returns>
    public static OdfChartDocument Create()
    {
        return (OdfChartDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Chart);
    }

    /// <summary>
    /// 從指定路徑載入 ODC 圖表文件。
    /// </summary>
    /// <param name="path">ODC 文件路徑。</param>
    /// <returns>載入完成的 <see cref="OdfChartDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODC 圖表時擲出。</exception>
    public new static OdfChartDocument Load(string path)
    {
        return EnsureChart(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// 從指定資料流載入 ODC 圖表文件。
    /// </summary>
    /// <param name="stream">包含 ODC 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="OdfChartDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODC 圖表時擲出。</exception>
    public new static OdfChartDocument Load(Stream stream, string? fileName = null)
    {
        return EnsureChart(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    /// <summary>
    /// 取得主要圖表節點。
    /// </summary>
    public OdfNode ChartNode => GetChartNode();

    /// <summary>
    /// 取得或設定圖表類型。
    /// </summary>
    public string ChartClass
    {
        get => GetChartNode().GetAttribute("class", OdfNamespaces.Chart) ?? "line";
        set => GetChartNode().SetAttribute("class", OdfNamespaces.Chart, value, "chart");
    }

    /// <summary>
    /// 取得或設定圖表標題。
    /// </summary>
    public string? ChartTitle
    {
        get
        {
            OdfNode? title = FindChildElement(GetChartNode(), "title", OdfNamespaces.Chart);
            OdfNode? paragraph = title is null ? null : FindChildElement(title, "p", OdfNamespaces.Text);
            return paragraph?.TextContent;
        }
        set
        {
            OdfNode chart = GetChartNode();
            OdfNode title = FindOrCreateChild(chart, "title", OdfNamespaces.Chart, "chart");
            OdfNode paragraph = FindOrCreateChild(title, "p", OdfNamespaces.Text, "text");
            paragraph.TextContent = value ?? string.Empty;
        }
    }

    /// <summary>
    /// 取得或設定圖例位置。
    /// </summary>
    /// <remarks>常見值包含 <c>top</c>、<c>bottom</c>、<c>start</c> 與 <c>end</c>。</remarks>
    public string? LegendPosition
    {
        get
        {
            OdfNode? legend = FindChildElement(GetChartNode(), "legend", OdfNamespaces.Chart);
            return legend?.GetAttribute("legend-position", OdfNamespaces.Chart);
        }
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                OdfNode? existingLegend = FindChildElement(GetChartNode(), "legend", OdfNamespaces.Chart);
                if (existingLegend is not null)
                {
                    GetChartNode().RemoveChild(existingLegend);
                }

                return;
            }

            string position = value!;
            OdfNode legendNode = FindOrCreateChild(GetChartNode(), "legend", OdfNamespaces.Chart, "chart");
            legendNode.SetAttribute("legend-position", OdfNamespaces.Chart, position, "chart");
        }
    }

    /// <summary>
    /// 取得或設定圖表資料來源是否包含標籤。
    /// </summary>
    /// <remarks>ODF 允許的常見值包含 <c>none</c>、<c>row</c>、<c>column</c> 與 <c>both</c>。</remarks>
    public string? DataSourceHasLabels
    {
        get
        {
            OdfNode? plotArea = FindChildElement(GetChartNode(), "plot-area", OdfNamespaces.Chart);
            return plotArea?.GetAttribute("data-source-has-labels", OdfNamespaces.Chart);
        }
        set
        {
            OdfNode? existingPlotArea = FindChildElement(GetChartNode(), "plot-area", OdfNamespaces.Chart);
            if (string.IsNullOrWhiteSpace(value))
            {
                existingPlotArea?.RemoveAttribute("data-source-has-labels", OdfNamespaces.Chart);
                return;
            }

            FindOrCreatePlotArea().SetAttribute("data-source-has-labels", OdfNamespaces.Chart, value!, "chart");
        }
    }

    /// <summary>
    /// 取得或設定 X 軸分類標籤的儲存格範圍位址。
    /// </summary>
    public string? CategoriesCellRangeAddress
    {
        get
        {
            OdfNode? categories = FindCategoriesNode();
            return categories?.GetAttribute("cell-range-address", OdfNamespaces.Table);
        }
        set
        {
            OdfNode? existingCategories = FindCategoriesNode();
            if (string.IsNullOrWhiteSpace(value))
            {
                existingCategories?.RemoveAttribute("cell-range-address", OdfNamespaces.Table);
                return;
            }

            SetCategories(value!);
        }
    }

    /// <summary>
    /// 取得或設定 X 軸標題。
    /// </summary>
    public string? XAxisTitle
    {
        get => GetAxisTitle("x");
        set => SetAxisTitle("x", value);
    }

    /// <summary>
    /// 取得或設定 Y 軸標題。
    /// </summary>
    public string? YAxisTitle
    {
        get => GetAxisTitle("y");
        set => SetAxisTitle("y", value);
    }

    /// <summary>
    /// 取得圖表中的資料序列摘要。
    /// </summary>
    public IReadOnlyList<OdfChartSeriesInfo> Series => GetSeries();

    /// <summary>
    /// 設定圖例位置。
    /// </summary>
    /// <param name="position">圖例位置，例如 top、bottom、start 或 end。</param>
    public void SetLegend(string position)
    {
        LegendPosition = position;
    }

    /// <summary>
    /// 將圖表資料來源綁定至試算表的儲存格範圍。
    /// </summary>
    /// <param name="sheetName">工作表名稱。</param>
    /// <param name="range">儲存格範圍。</param>
    /// <param name="firstRowAsHeader">首列作為序列標題（header），預設 true。</param>
    /// <param name="firstColumnAsLabel">首欄作為分類標籤（X 軸），預設 true。</param>
    public void SetDataRange(string sheetName, OdfKit.Spreadsheet.OdfCellRange range,
        bool firstRowAsHeader = true, bool firstColumnAsLabel = true)
    {
        if (string.IsNullOrEmpty(sheetName))
            throw new ArgumentException("工作表名稱不可為空。", nameof(sheetName));

        OdfNode chart = GetChartNode();

        // 1. 設定 <chart:chart table:cell-range-address="...">
        string fullRange = BuildAbsoluteRange(sheetName, range.StartAddress.Row, range.StartAddress.Column,
                                               range.EndAddress.Row, range.EndAddress.Column);
        chart.SetAttribute("cell-range-address", OdfNamespaces.Table, fullRange, "table");

        // 2. 建立 <chart:data-source>
        OdfNode dataSource = FindOrCreateDataSource(chart);
        dataSource.SetAttribute("has-row-headers", OdfNamespaces.Chart,
            firstRowAsHeader ? "true" : "false", "chart");
        dataSource.SetAttribute("has-column-headers", OdfNamespaces.Chart,
            firstColumnAsLabel ? "true" : "false", "chart");

        // 3. 清除現有 <chart:series>
        OdfNode plotArea = FindOrCreatePlotArea();
        var toRemove = new List<OdfNode>();
        foreach (var child in plotArea.Children)
        {
            if (child.NodeType == OdfNodeType.Element &&
                child.LocalName == "series" &&
                child.NamespaceUri == OdfNamespaces.Chart)
                toRemove.Add(child);
        }
        foreach (var n in toRemove)
            plotArea.RemoveChild(n);

        int dataRowStart = firstRowAsHeader ? range.StartAddress.Row + 1 : range.StartAddress.Row;
        int dataColStart = firstColumnAsLabel ? range.StartAddress.Column + 1 : range.StartAddress.Column;

        // 4. 設定 X 軸分類範圍
        if (firstColumnAsLabel && dataRowStart <= range.EndAddress.Row)
        {
            OdfNode xAxis = FindOrCreateAxis("x");
            OdfNode? existingCat = FindChildElement(xAxis, "categories", OdfNamespaces.Chart);
            if (existingCat is not null)
                xAxis.RemoveChild(existingCat);

            string catRange = BuildAbsoluteRange(sheetName,
                dataRowStart, range.StartAddress.Column,
                range.EndAddress.Row, range.StartAddress.Column);
            OdfNode categories = OdfNodeFactory.CreateElement("categories", OdfNamespaces.Chart, "chart");
            categories.SetAttribute("cell-range-address", OdfNamespaces.Table, catRange, "table");
            xAxis.AppendChild(categories);
        }

        // 5. 為每個資料欄新增 <chart:series>
        for (int col = dataColStart; col <= range.EndAddress.Column; col++)
        {
            if (dataRowStart > range.EndAddress.Row)
                break;

            string dataRange = BuildAbsoluteRange(sheetName,
                dataRowStart, col, range.EndAddress.Row, col);

            OdfNode series = OdfNodeFactory.CreateElement("series", OdfNamespaces.Chart, "chart");
            series.SetAttribute("values-cell-range-address", OdfNamespaces.Chart, dataRange, "chart");
            series.SetAttribute("cell-range-address", OdfNamespaces.Table, dataRange, "table");

            if (firstRowAsHeader)
            {
                string labelAddr = BuildAbsoluteCell(sheetName, range.StartAddress.Row, col);
                series.SetAttribute("label-cell-address", OdfNamespaces.Chart, labelAddr, "chart");
            }

            plotArea.AppendChild(series);
        }
    }

    /// <summary>
    /// 取得圖表目前綁定的試算表儲存格範圍。
    /// </summary>
    /// <returns>工作表名稱與儲存格範圍的元組；若未設定則兩者均為 null。</returns>
    public (string? SheetName, OdfKit.Spreadsheet.OdfCellRange? Range) GetDataRange()
    {
        string? addr = ChartNode.GetAttribute("cell-range-address", OdfNamespaces.Table);
        if (string.IsNullOrEmpty(addr))
            return (null, null);

        string s = addr!.Trim();
        if (s.StartsWith("[", StringComparison.Ordinal))
            s = s.Substring(1);
        if (s.EndsWith("]", StringComparison.Ordinal))
            s = s.Substring(0, s.Length - 1);

        int colon = s.IndexOf(':');
        if (colon < 0)
            return (null, null);

        string startPart = s.Substring(0, colon);
        string endPart = s.Substring(colon + 1);

        if (!TryParseOdfCell(startPart, out string? sheetName, out int startRow, out int startCol))
            return (null, null);
        if (!TryParseOdfCell(endPart, out _, out int endRow, out int endCol))
            return (null, null);

        var range = new OdfKit.Spreadsheet.OdfCellRange(startRow, startCol, endRow, endCol, sheetName);
        return (sheetName, range);
    }

    // ── 私有輔助方法 ──────────────────────────────────────────────────────────

    private OdfNode FindOrCreateDataSource(OdfNode chart)
    {
        OdfNode? ds = FindChildElement(chart, "data-source", OdfNamespaces.Chart);
        if (ds is not null)
            return ds;
        ds = OdfNodeFactory.CreateElement("data-source", OdfNamespaces.Chart, "chart");
        OdfNode? plotArea = FindChildElement(chart, "plot-area", OdfNamespaces.Chart);
        if (plotArea is not null)
            chart.InsertBefore(ds, plotArea);
        else
            chart.AppendChild(ds);
        return ds;
    }

    private static string BuildAbsoluteCell(string sheetName, int row, int col)
    {
        string colName = ColumnIndexToName(col);
        string prefix = string.IsNullOrEmpty(sheetName) ? "." : $"{EscapeSheetName(sheetName)}.";
        return $"{prefix}${colName}${row + 1}";
    }

    private static string BuildAbsoluteRange(string sheetName, int startRow, int startCol, int endRow, int endCol)
    {
        string start = BuildAbsoluteCell(sheetName, startRow, startCol);
        string end = BuildAbsoluteCell(string.Empty, endRow, endCol);
        return $"{start}:{end}";
    }

    private static string EscapeSheetName(string name)
    {
        bool needsQuotes = name.Contains(' ') || name.Contains('\'') || name.Contains('-') || name.Contains('.');
        if (!needsQuotes)
            return name;
        return "'" + name.Replace("'", "''") + "'";
    }

    private static string ColumnIndexToName(int index)
    {
        int n = index + 1;
        var sb = new System.Text.StringBuilder();
        while (n > 0)
        {
            int rem = (n - 1) % 26;
            sb.Insert(0, (char)('A' + rem));
            n = (n - 1) / 26;
        }
        return sb.ToString();
    }

    private static bool TryParseOdfCell(string part, out string? sheetName, out int row, out int col)
    {
        sheetName = null;
        row = 0;
        col = 0;
        string s = part.Trim();

        // 剝除前置 $ (絕對工作表參照)
        if (s.StartsWith("$", StringComparison.Ordinal))
            s = s.Substring(1);

        // 分離 sheet 與 cell：以第一個 '.' 為分隔
        int dot = s.IndexOf('.');
        if (dot < 0)
            return false;

        string sheetPart = s.Substring(0, dot);
        string cellPart = s.Substring(dot + 1);

        // 處理帶引號的工作表名稱
        if (sheetPart.StartsWith("'", StringComparison.Ordinal) &&
            sheetPart.EndsWith("'", StringComparison.Ordinal))
            sheetPart = sheetPart.Substring(1, sheetPart.Length - 2).Replace("''", "'");

        sheetName = string.IsNullOrEmpty(sheetPart) ? null : sheetPart;

        // 解析儲存格：去除 $，分離字母與數字
        cellPart = cellPart.Replace("$", "");
        int i = 0;
        while (i < cellPart.Length && char.IsLetter(cellPart[i]))
            i++;
        if (i == 0 || i >= cellPart.Length)
            return false;

        string colStr = cellPart.Substring(0, i);
        if (!int.TryParse(cellPart.Substring(i), out int rowNum) || rowNum < 1)
            return false;

        col = ColumnNameToIndex(colStr);
        row = rowNum - 1;
        return true;
    }

    private static int ColumnNameToIndex(string name)
    {
        int result = 0;
        foreach (char c in name.ToUpperInvariant())
            result = result * 26 + (c - 'A' + 1);
        return result - 1;
    }

    /// <summary>
    /// 設定 X 軸分類標籤的儲存格範圍位址。
    /// </summary>
    /// <param name="cellRangeAddress">分類標籤的儲存格範圍位址。</param>
    public void SetCategories(string cellRangeAddress)
    {
        if (string.IsNullOrWhiteSpace(cellRangeAddress))
        {
            throw new ArgumentException("分類標籤範圍位址不能為空。", nameof(cellRangeAddress));
        }

        OdfNode axis = FindOrCreateAxis("x");
        OdfNode categories = FindOrCreateChild(axis, "categories", OdfNamespaces.Chart, "chart");
        categories.SetAttribute("cell-range-address", OdfNamespaces.Table, cellRangeAddress, "table");
    }

    /// <summary>
    /// 取得指定維度座標軸的標題。
    /// </summary>
    /// <param name="dimension">座標軸維度，例如 x、y 或 z。</param>
    /// <returns>座標軸標題；若未設定則為 <see langword="null"/>。</returns>
    public string? GetAxisTitle(string dimension)
    {
        ValidateAxisDimension(dimension);

        OdfNode? axis = FindAxis(dimension);
        OdfNode? title = axis is null ? null : FindChildElement(axis, "title", OdfNamespaces.Chart);
        OdfNode? paragraph = title is null ? null : FindChildElement(title, "p", OdfNamespaces.Text);
        string? text = paragraph?.TextContent;
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>
    /// 設定指定維度座標軸的標題。
    /// </summary>
    /// <param name="dimension">座標軸維度，例如 x、y 或 z。</param>
    /// <param name="title">座標軸標題；空白值會移除既有標題。</param>
    public void SetAxisTitle(string dimension, string? title)
    {
        ValidateAxisDimension(dimension);

        OdfNode? existingAxis = FindAxis(dimension);
        if (string.IsNullOrWhiteSpace(title))
        {
            OdfNode? existingTitle = existingAxis is null
                ? null
                : FindChildElement(existingAxis, "title", OdfNamespaces.Chart);
            if (existingTitle is not null)
            {
                existingAxis!.RemoveChild(existingTitle);
            }

            return;
        }

        OdfNode axis = existingAxis ?? FindOrCreateAxis(dimension);
        OdfNode titleNode = FindOrCreateAxisTitle(axis);
        OdfNode paragraph = FindOrCreateChild(titleNode, "p", OdfNamespaces.Text, "text");
        paragraph.TextContent = title!;
    }

    /// <summary>
    /// 新增資料序列佔位節點。
    /// </summary>
    /// <param name="valuesCellRangeAddress">資料值儲存格範圍位址。</param>
    /// <param name="labelCellAddress">選用的標籤儲存格位址。</param>
    /// <returns>新增的序列節點。</returns>
    public OdfNode AddSeries(string valuesCellRangeAddress, string? labelCellAddress = null)
    {
        if (string.IsNullOrWhiteSpace(valuesCellRangeAddress))
        {
            throw new ArgumentException("資料範圍位址不能為空。", nameof(valuesCellRangeAddress));
        }

        OdfNode plotArea = FindOrCreatePlotArea();
        OdfNode series = OdfNodeFactory.CreateElement("series", OdfNamespaces.Chart, "chart");
        series.SetAttribute("values-cell-range-address", OdfNamespaces.Chart, valuesCellRangeAddress, "chart");
        if (!string.IsNullOrWhiteSpace(labelCellAddress))
        {
            series.SetAttribute("label-cell-address", OdfNamespaces.Chart, labelCellAddress!, "chart");
        }

        plotArea.AppendChild(series);
        return series;
    }

    private OdfNode FindOrCreatePlotArea()
    {
        return FindOrCreateChild(GetChartNode(), "plot-area", OdfNamespaces.Chart, "chart");
    }

    private OdfNode FindOrCreateAxis(string dimension)
    {
        OdfNode plotArea = FindOrCreatePlotArea();
        OdfNode? existingAxis = FindAxis(plotArea, dimension);
        if (existingAxis is not null)
        {
            return existingAxis;
        }

        OdfNode axis = OdfNodeFactory.CreateElement("axis", OdfNamespaces.Chart, "chart");
        axis.SetAttribute("dimension", OdfNamespaces.Chart, dimension, "chart");
        OdfNode? firstSeries = FindChildElement(plotArea, "series", OdfNamespaces.Chart);
        if (firstSeries is null)
        {
            plotArea.AppendChild(axis);
        }
        else
        {
            plotArea.InsertBefore(axis, firstSeries);
        }

        return axis;
    }

    private OdfNode? FindAxis(string dimension)
    {
        OdfNode? plotArea = FindChildElement(GetChartNode(), "plot-area", OdfNamespaces.Chart);
        return plotArea is null ? null : FindAxis(plotArea, dimension);
    }

    private static OdfNode? FindAxis(OdfNode plotArea, string dimension)
    {
        foreach (OdfNode child in plotArea.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "axis" &&
                child.NamespaceUri == OdfNamespaces.Chart &&
                string.Equals(child.GetAttribute("dimension", OdfNamespaces.Chart), dimension, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static OdfNode FindOrCreateAxisTitle(OdfNode axis)
    {
        OdfNode? existingTitle = FindChildElement(axis, "title", OdfNamespaces.Chart);
        if (existingTitle is not null)
        {
            return existingTitle;
        }

        OdfNode title = OdfNodeFactory.CreateElement("title", OdfNamespaces.Chart, "chart");
        OdfNode? categories = FindChildElement(axis, "categories", OdfNamespaces.Chart);
        if (categories is not null)
        {
            axis.InsertBefore(title, categories);
        }
        else
        {
            axis.AppendChild(title);
        }

        return title;
    }

    private OdfNode? FindCategoriesNode()
    {
        OdfNode? plotArea = FindChildElement(GetChartNode(), "plot-area", OdfNamespaces.Chart);
        if (plotArea is null)
        {
            return null;
        }

        OdfNode? fallbackCategories = null;
        foreach (OdfNode child in plotArea.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName != "axis" ||
                child.NamespaceUri != OdfNamespaces.Chart)
            {
                continue;
            }

            OdfNode? categories = FindChildElement(child, "categories", OdfNamespaces.Chart);
            if (categories is null)
            {
                continue;
            }

            fallbackCategories ??= categories;
            if (string.Equals(child.GetAttribute("dimension", OdfNamespaces.Chart), "x", StringComparison.Ordinal))
            {
                return categories;
            }
        }

        return fallbackCategories;
    }

    private IReadOnlyList<OdfChartSeriesInfo> GetSeries()
    {
        OdfNode? plotArea = FindChildElement(GetChartNode(), "plot-area", OdfNamespaces.Chart);
        if (plotArea is null)
        {
            return [];
        }

        List<OdfChartSeriesInfo> series = [];
        foreach (OdfNode child in plotArea.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName != "series" ||
                child.NamespaceUri != OdfNamespaces.Chart)
            {
                continue;
            }

            string? valuesCellRangeAddress = child.GetAttribute("values-cell-range-address", OdfNamespaces.Chart);
            if (string.IsNullOrWhiteSpace(valuesCellRangeAddress))
            {
                continue;
            }

            string rangeAddress = valuesCellRangeAddress!;
            series.Add(new OdfChartSeriesInfo(
                rangeAddress,
                child.GetAttribute("label-cell-address", OdfNamespaces.Chart)));
        }

        return series;
    }

    private static void ValidateAxisDimension(string dimension)
    {
        if (string.IsNullOrWhiteSpace(dimension))
        {
            throw new ArgumentException("座標軸維度不能為空。", nameof(dimension));
        }
    }

    private static OdfChartDocument EnsureChart(OdfDocument document)
    {
        if (document is OdfChartDocument chart)
        {
            return chart;
        }

        document.Dispose();
        throw new InvalidOperationException("指定的 ODF 文件不是 ODC 圖表。");
    }

    private static bool InitMimeType(OdfPackage pkg)
    {
        if (string.IsNullOrEmpty(pkg.MimeType))
        {
            pkg.SetMimeType("application/vnd.oasis.opendocument.chart");
        }
        return true;
    }

    /// <summary>
    /// 取得預設的內容 XML 字串。
    /// </summary>
    /// <returns>預設的內容 XML 字串</returns>
    protected override string GetDefaultContentXml()
    {
        return "<office:document-content " +
               "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
               "xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" " +
               "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
               "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
               "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" " +
               "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
               "xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" " +
               "xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
               "office:version=\"" + OdfVersionInfo.DefaultVersionString + "\">" +
               "<office:body>" +
               "<office:chart>" +
               "<chart:chart chart:class=\"line\" />" +
               "</office:chart>" +
               "</office:body>" +
               "</office:document-content>";
    }

    /// <summary>
    /// 取得預設的樣式 XML 字串。
    /// </summary>
    /// <returns>預設的樣式 XML 字串</returns>
    protected override string GetDefaultStylesXml()
    {
        return "<office:document-styles " +
               "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
               "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
               "office:version=\"" + OdfVersionInfo.DefaultVersionString + "\">" +
               "<office:styles></office:styles>" +
               "</office:document-styles>";
    }

    /// <summary>
    /// 合併來源文件的內容節點至本文件中。
    /// </summary>
    /// <param name="sourceDoc">來源 ODF 文件</param>
    /// <param name="options">合併設定選項</param>
    /// <param name="renameMap">樣式名稱重映射字典</param>
    protected override void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap)
    {
        var srcChart = sourceDoc as OdfChartDocument ?? throw new ArgumentException("Source document must be a OdfChartDocument.");

        var body = FindOrCreateChild(ContentDom, "body", OdfNamespaces.Office, "office");
        var destChartRoot = FindOrCreateChild(body, "chart", OdfNamespaces.Office, "office");

        var srcBody = srcChart.FindOrCreateChild(srcChart.ContentDom, "body", OdfNamespaces.Office, "office");
        var srcChartRoot = srcChart.FindOrCreateChild(srcBody, "chart", OdfNamespaces.Office, "office");

        foreach (var child in srcChartRoot.Children)
        {
            if (child.NodeType is OdfNodeType.Element)
            {
                var imported = OdfNode.ImportNode(child, srcChart.Package, Package);
                RemapStylesInNodes(imported, renameMap);
                destChartRoot.AppendChild(imported);
            }
        }
    }

    private OdfNode GetChartNode()
    {
        OdfNode body = FindOrCreateChild(ContentDom, "body", OdfNamespaces.Office, "office");
        OdfNode chartRoot = FindOrCreateChild(body, "chart", OdfNamespaces.Office, "office");
        return FindOrCreateChild(chartRoot, "chart", OdfNamespaces.Chart, "chart");
    }

    private static OdfNode? FindChildElement(OdfNode parent, string localName, string namespaceUri)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
            {
                return child;
            }
        }

        return null;
    }
}


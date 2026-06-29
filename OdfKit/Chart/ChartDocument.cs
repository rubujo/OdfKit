using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;

namespace OdfKit.Chart;

/// <summary>
/// Represents a high-level ODF chart document.
/// 表示高階 ODF 圖表文件（Chart Document）的類別。
/// </summary>
public class ChartDocument : OdfChartDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChartDocument"/> class with the specified ODF package.
    /// 使用指定的 ODF 封裝初始化 <see cref="ChartDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package instance. / ODF 封裝執行個體。</param>
    public ChartDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChartDocument"/> class with the specified ODF package and sub-path.
    /// 使用指定的 ODF 封裝與子路徑初始化 <see cref="ChartDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package instance. / ODF 封裝執行個體。</param>
    /// <param name="subPath">The sub-path within the package. / 封裝內的子路徑。</param>
    public ChartDocument(OdfPackage package, string subPath) : base(package, subPath)
    {
    }

    /// <summary>
    /// Creates a new high-level chart document from a chart definition.
    /// 根據圖表定義建立新的高階圖表文件。
    /// </summary>
    /// <param name="chartDefinition">The chart configuration and definition information. / 圖表設定與定義資訊。</param>
    /// <returns>The created high-level <see cref="ChartDocument"/> instance. / 建立完成的高階 <see cref="ChartDocument"/> 執行個體。</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chartDefinition"/> is <see langword="null"/>. / 當 <paramref name="chartDefinition"/> 為 <see langword="null"/> 時擲出。</exception>
    public static ChartDocument Create(OdfChartDefinition chartDefinition)
    {
        if (chartDefinition is null)
        {
            throw new ArgumentNullException(nameof(chartDefinition));
        }

        var doc = (ChartDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Chart);

        string chartClass = chartDefinition.ChartType switch
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
        doc.ChartClass = chartClass;

        if (!string.IsNullOrEmpty(chartDefinition.Title))
        {
            doc.ChartTitle = chartDefinition.Title;
        }

        doc.LegendPosition = chartDefinition.HasLegend ? "end" : null;

        if (chartDefinition.DataRange != default)
        {
            string dataRangeStr = chartDefinition.DataRange.ToOdfString(false);
            doc.ChartNode.SetAttribute("cell-range-address", OdfNamespaces.Table, dataRangeStr, "table");
        }

        return doc;
    }

    /// <summary>
    /// Creates a fluent builder for a high-level chart document.
    /// 建立高階圖表文件 Fluent builder。
    /// </summary>
    /// <returns>A new <see cref="ChartDocumentBuilder"/> instance. / 新的 <see cref="ChartDocumentBuilder"/> 執行個體。</returns>
    public static ChartDocumentBuilder Builder() =>
        new((ChartDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Chart));

    /// <summary>
    /// Loads a high-level chart document from the specified file path.
    /// 從指定檔案路徑載入高階圖表文件。
    /// </summary>
    /// <param name="path">The ODC document file path. / ODC 文件檔案路徑。</param>
    /// <returns>The loaded high-level <see cref="ChartDocument"/> instance. / 載入完成的高階 <see cref="ChartDocument"/> 執行個體。</returns>
    public new static ChartDocument Load(string path)
    {
        return EnsureChart(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// Asynchronously loads a high-level chart document from the specified file path.
    /// 非同步從指定檔案路徑載入高階圖表文件。
    /// </summary>
    /// <param name="path">The ODC document file path. / ODC 文件檔案路徑。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded high-level <see cref="ChartDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的高階 <see cref="ChartDocument"/>。</returns>
    public new static async Task<ChartDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        EnsureChart(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Loads a high-level chart document from the specified stream.
    /// 從指定資料流載入高階圖表文件。
    /// </summary>
    /// <param name="stream">The stream containing the ODC document content. / 包含 ODC 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>The loaded high-level <see cref="ChartDocument"/> instance. / 載入完成的高階 <see cref="ChartDocument"/> 執行個體。</returns>
    public new static ChartDocument Load(Stream stream, string? fileName = null)
    {
        return EnsureChart(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    /// <summary>
    /// Asynchronously loads a high-level chart document from the specified stream.
    /// 非同步從指定資料流載入高階圖表文件。
    /// </summary>
    /// <param name="stream">The stream containing the ODC document content. / 包含 ODC 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded high-level <see cref="ChartDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的高階 <see cref="ChartDocument"/>。</returns>
    public new static async Task<ChartDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        EnsureChart(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Creates a new high-level chart document from the specified chart template document.
    /// 從指定的圖表範本文件建立新的高階圖表文件。
    /// </summary>
    /// <param name="template">The chart template document. / 圖表範本文件。</param>
    /// <returns>The created <see cref="ChartDocument"/> instance. / 建立完成的 <see cref="ChartDocument"/> 執行個體。</returns>
    public static ChartDocument CreateFromTemplate(ChartTemplateDocument template) =>
        (ChartDocument)CreateFromTemplateInternal(template, OdfDocumentKind.Chart, "application/vnd.oasis.opendocument.chart");

    /// <summary>
    /// Creates an equivalent ODC (ZIP package) chart document from a FODC flat XML chart document, with identical content.
    /// 從 FODC 扁平 XML 圖表文件建立等價的 ODC（ZIP 封裝）圖表文件，內容完全相同。
    /// </summary>
    /// <param name="document">The source FODC flat XML chart document. / 來源 FODC 扁平 XML 圖表文件。</param>
    /// <returns>The created <see cref="ChartDocument"/> instance. / 建立完成的 <see cref="ChartDocument"/> 執行個體。</returns>
    public static ChartDocument CreateFromFlatDocument(FlatChartDocument document) =>
        (ChartDocument)ConvertFlatVariantInternal(document, OdfDocumentKind.Chart, targetIsFlatXml: false);

    private static ChartDocument EnsureChart(OdfDocument document)
    {
        if (document is ChartDocument chart && document.DocumentKind == OdfDocumentKind.Chart)
        {
            return chart;
        }

        document.Dispose();
        throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_ChartDocument_SpecifiedOdfFileHigh"));
    }

    /// <summary>
    /// Updates the chart's embedded local data table (local cached data).
    /// 更新圖表內嵌的本地資料表格（本地快取資料）。
    /// </summary>
    /// <param name="data">The two-dimensional data collection, including labels and values. / 二維資料集合，包含標籤與數值。</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is <see langword="null"/>. / 當 <paramref name="data"/> 為 <see langword="null"/> 時擲出。</exception>
    public void UpdateData(IEnumerable<IEnumerable<object?>> data)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        OdfNode body = FindOrCreateChild(ContentDom, "body", OdfNamespaces.Office, "office");
        OdfNode chartRoot = FindOrCreateChild(body, "chart", OdfNamespaces.Office, "office");

        OdfNode? existingTable = null;
        foreach (OdfNode child in chartRoot.Children)
        {
            if (child.LocalName == "table" && child.NamespaceUri == OdfNamespaces.Table)
            {
                existingTable = child;
                break;
            }
        }
        if (existingTable is not null)
        {
            chartRoot.RemoveChild(existingTable);
        }

        OdfNode tableNode = OdfNodeFactory.CreateElement("table", OdfNamespaces.Table, "table");
        tableNode.SetAttribute("name", OdfNamespaces.Table, "LocalTable", "table");
        chartRoot.AppendChild(tableNode);

        var dataList = new List<List<object?>>();
        int maxCols = 0;
        foreach (var row in data)
        {
            var rowList = new List<object?>(row);
            dataList.Add(rowList);
            if (rowList.Count > maxCols)
            {
                maxCols = rowList.Count;
            }
        }

        for (int i = 0; i < maxCols; i++)
        {
            OdfNode colNode = OdfNodeFactory.CreateElement("table-column", OdfNamespaces.Table, "table");
            tableNode.AppendChild(colNode);
        }

        foreach (var rowList in dataList)
        {
            OdfNode rowNode = OdfNodeFactory.CreateElement("table-row", OdfNamespaces.Table, "table");
            tableNode.AppendChild(rowNode);

            foreach (var val in rowList)
            {
                OdfNode cellNode = OdfNodeFactory.CreateElement("table-cell", OdfNamespaces.Table, "table");
                rowNode.AppendChild(cellNode);

                if (val is null)
                {
                    cellNode.SetAttribute("value-type", OdfNamespaces.Office, "string", "office");
                    OdfNode pNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
                    cellNode.AppendChild(pNode);
                }
                else
                {
                    switch (val)
                    {
                        case string text:
                            cellNode.SetAttribute("value-type", OdfNamespaces.Office, "string", "office");
                            OdfNode pNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
                            pNode.TextContent = text;
                            cellNode.AppendChild(pNode);
                            break;
                        case bool flag:
                            cellNode.SetAttribute("value-type", OdfNamespaces.Office, "boolean", "office");
                            cellNode.SetAttribute("boolean-value", OdfNamespaces.Office, flag ? "true" : "false", "office");
                            OdfNode pNodeBool = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
                            pNodeBool.TextContent = flag ? "TRUE" : "FALSE";
                            cellNode.AppendChild(pNodeBool);
                            break;
                        case DateTime date:
                            cellNode.SetAttribute("value-type", OdfNamespaces.Office, "date", "office");
                            string isoDate = (date == DateTime.MinValue || date == DateTime.MaxValue)
                                ? date.ToString("yyyy-MM-ddTHH:mm:ss") + "Z"
                                : date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
                            cellNode.SetAttribute("date-value", OdfNamespaces.Office, isoDate, "office");
                            OdfNode pNodeDate = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
                            pNodeDate.TextContent = isoDate;
                            cellNode.AppendChild(pNodeDate);
                            break;
                        case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                            double dblVal = Convert.ToDouble(val, CultureInfo.InvariantCulture);
                            cellNode.SetAttribute("value-type", OdfNamespaces.Office, "float", "office");
                            cellNode.SetAttribute("value", OdfNamespaces.Office, dblVal.ToString(CultureInfo.InvariantCulture), "office");
                            OdfNode pNodeFloat = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
                            pNodeFloat.TextContent = dblVal.ToString(CultureInfo.InvariantCulture);
                            cellNode.AppendChild(pNodeFloat);
                            break;
                        default:
                            string otherStr = val.ToString() ?? string.Empty;
                            cellNode.SetAttribute("value-type", OdfNamespaces.Office, "string", "office");
                            OdfNode pNodeOther = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
                            pNodeOther.TextContent = otherStr;
                            cellNode.AppendChild(pNodeOther);
                            break;
                    }
                }
            }
        }

        InvalidateLocalDataCache();
    }
}

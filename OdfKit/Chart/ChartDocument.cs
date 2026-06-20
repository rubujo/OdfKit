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
/// 表示高階 ODF 圖表文件（Chart Document）的類別。
/// </summary>
public class ChartDocument : OdfChartDocument
{
    /// <summary>
    /// 使用指定的 ODF 封裝初始化 <see cref="ChartDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝執行個體。</param>
    public ChartDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// 使用指定的 ODF 封裝與子路徑初始化 <see cref="ChartDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝執行個體。</param>
    /// <param name="subPath">封裝內的子路徑。</param>
    public ChartDocument(OdfPackage package, string subPath) : base(package, subPath)
    {
    }

    /// <summary>
    /// 根據圖表定義建立新的高階圖表文件。
    /// </summary>
    /// <param name="chartDefinition">圖表設定與定義資訊。</param>
    /// <returns>建立完成的高階 <see cref="ChartDocument"/> 執行個體。</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="chartDefinition"/> 為 <see langword="null"/> 時擲出。</exception>
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
            OdfChartType.Pie => "chart:pie",
            OdfChartType.Area => "chart:area",
            OdfChartType.Scatter => "chart:scatter",
            OdfChartType.Bubble => "chart:bubble",
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
    /// 從指定檔案路徑載入高階圖表文件。
    /// </summary>
    /// <param name="path">ODC 文件檔案路徑。</param>
    /// <returns>載入完成的高階 <see cref="ChartDocument"/> 執行個體。</returns>
    public new static ChartDocument Load(string path)
    {
        return EnsureChart(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// 非同步從指定檔案路徑載入高階圖表文件。
    /// </summary>
    /// <param name="path">ODC 文件檔案路徑。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的高階 <see cref="ChartDocument"/>。</returns>
    public new static async Task<ChartDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        EnsureChart(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入高階圖表文件。
    /// </summary>
    /// <param name="stream">包含 ODC 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的高階 <see cref="ChartDocument"/> 執行個體。</returns>
    public new static ChartDocument Load(Stream stream, string? fileName = null)
    {
        return EnsureChart(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    /// <summary>
    /// 非同步從指定資料流載入高階圖表文件。
    /// </summary>
    /// <param name="stream">包含 ODC 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的高階 <see cref="ChartDocument"/>。</returns>
    public new static async Task<ChartDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        EnsureChart(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    private static ChartDocument EnsureChart(OdfDocument document)
    {
        if (document is ChartDocument chart && document.DocumentKind == OdfDocumentKind.Chart)
        {
            return chart;
        }

        document.Dispose();
        throw new InvalidOperationException("指定的 ODF 文件不是高階 ODC 圖表。");
    }

    /// <summary>
    /// 更新圖表內嵌的本地資料表格（本地快取資料）。
    /// </summary>
    /// <param name="data">二維資料集合，包含標籤與數值。</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="data"/> 為 <see langword="null"/> 時擲出。</exception>
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
    }
}

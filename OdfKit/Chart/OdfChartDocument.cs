using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Chart;

/// <summary>
/// 表示 ODF 圖表文件（Chart Document）的類別。
/// </summary>
/// <param name="package">Odf 套件執行個體</param>
/// <param name="subPath">子路徑</param>
public partial class OdfChartDocument(OdfPackage package, string subPath) : OdfDocument(package, subPath)
{
    private OdfChartLegend? _legend;

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
    /// <returns>新的 <see cref="OdfChartDocument"/> 執行個體</returns>
    public static OdfChartDocument Create()
    {
        return (OdfChartDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Chart);
    }

    /// <summary>
    /// 從指定路徑載入 ODC 圖表文件。
    /// </summary>
    /// <param name="path">ODC 文件路徑</param>
    /// <returns>載入完成的 <see cref="OdfChartDocument"/> 執行個體</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODC 圖表時擲出</exception>
    public new static OdfChartDocument Load(string path)
    {
        return EnsureChart(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// 非同步從指定路徑載入 ODC 圖表文件。
    /// </summary>
    /// <param name="path">ODC 文件路徑</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="OdfChartDocument"/></returns>
    public new static async Task<OdfChartDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        EnsureChart(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入 ODC 圖表文件。
    /// </summary>
    /// <param name="stream">包含 ODC 文件內容的資料流</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <returns>載入完成的 <see cref="OdfChartDocument"/> 執行個體</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODC 圖表時擲出</exception>
    public new static OdfChartDocument Load(Stream stream, string? fileName = null)
    {
        return EnsureChart(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    /// <summary>
    /// 非同步從指定資料流載入 ODC 圖表文件。
    /// </summary>
    /// <param name="stream">包含 ODC 文件內容的資料流</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="OdfChartDocument"/></returns>
    public new static async Task<OdfChartDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        EnsureChart(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

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
    /// <remarks>常見值包含 <c>top</c>、<c>bottom</c>、<c>start</c> 與 <c>end</c></remarks>
    public string? LegendPosition
    {
        get => Legend.Position;
        set => Legend.Position = value;
    }

    /// <summary>
    /// 取得或設定圖例的對齊方式（對應 <c>chart:legend-align</c>）。
    /// </summary>
    /// <remarks>常見值包含 <c>start</c>、<c>center</c> 與 <c>end</c></remarks>
    public string? LegendAlignment
    {
        get => Legend.Alignment;
        set => Legend.Alignment = value;
    }

    /// <summary>
    /// 取得圖例的統一可編輯模型。
    /// </summary>
    public OdfChartLegend Legend => _legend ??= new OdfChartLegend(this);

    /// <summary>
    /// 取得或設定圖表資料來源是否包含標籤。
    /// </summary>
    /// <remarks>ODF 允許的常見值包含 <c>none</c>、<c>row</c>、<c>column</c> 與 <c>both</c></remarks>
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
    /// <param name="position">圖例位置，例如 top、bottom、start 或 end</param>
    public void SetLegend(string position)
    {
        LegendPosition = position;
    }

    private static OdfChartDocument EnsureChart(OdfDocument document)
    {
        if (document is OdfChartDocument chart && document.DocumentKind == OdfDocumentKind.Chart)
        {
            return chart;
        }

        document.Dispose();
        throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfChartDocument_SpecifiedOdfFileOdc"));
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
    /// <param name="renameMap">樣式名稱變更的對照字典</param>
    protected override void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap)
    {
        var srcChart = sourceDoc as OdfChartDocument ?? throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfChartDocument_SourceDocumentOdfchartdocument"));

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

    internal OdfNode? FindLegendNode() => FindChildElement(GetChartNode(), "legend", OdfNamespaces.Chart);

    internal OdfNode EnsureLegendNode() => FindOrCreateChild(GetChartNode(), "legend", OdfNamespaces.Chart, "chart");

    internal void RemoveLegendNode()
    {
        OdfNode? legendNode = FindLegendNode();
        if (legendNode is not null)
        {
            GetChartNode().RemoveChild(legendNode);
        }
    }
}

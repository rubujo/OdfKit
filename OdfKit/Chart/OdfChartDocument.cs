using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
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
               "office:version=\"1.3\">" +
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
               "office:version=\"1.3\">" +
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
}

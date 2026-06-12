using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Formula;

/// <summary>
/// 代表 ODF 公式文件。
/// </summary>
public class OdfFormulaDocument : OdfDocument
{
    /// <summary>
    /// 使用指定的 ODF 封裝初始化 <see cref="OdfFormulaDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝</param>
    public OdfFormulaDocument(OdfPackage package) : this(package, string.Empty)
    {
    }

    /// <summary>
    /// 使用指定的 ODF 封裝與子路徑初始化 <see cref="OdfFormulaDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝</param>
    /// <param name="subPath">封裝內的子路徑</param>
    public OdfFormulaDocument(OdfPackage package, string subPath) : base(package, subPath)
    {
        if (string.IsNullOrEmpty(package.MimeType))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.formula");
        }
    }

    /// <summary>
    /// 取得預設的內容 XML 字串。
    /// </summary>
    /// <returns>預設的內容 XML 字串</returns>
    protected override string GetDefaultContentXml()
    {
        return "<office:document-content " +
               "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
               "xmlns:math=\"http://www.w3.org/1998/Math/MathML\" " +
               "office:version=\"1.3\">" +
               "<office:body>" +
               "<office:formula>" +
               "<math:math />" +
               "</office:formula>" +
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
               "office:version=\"1.3\">" +
               "<office:styles></office:styles>" +
               "</office:document-styles>";
    }

    /// <summary>
    /// 合併來源文件的內容節點至此文件。
    /// </summary>
    /// <param name="sourceDoc">來源文件</param>
    /// <param name="options">合併選項</param>
    /// <param name="renameMap">樣式重新命名對照表</param>
    /// <exception cref="ArgumentException">當來源文件不是 <see cref="OdfFormulaDocument"/> 時擲出</exception>
    protected override void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap)
    {
        var srcFormula = sourceDoc as OdfFormulaDocument ?? throw new ArgumentException("Source document must be a OdfFormulaDocument.");
        
        var body = FindOrCreateChild(ContentDom, "body", OdfNamespaces.Office, "office");
        var destFormulaRoot = FindOrCreateChild(body, "formula", OdfNamespaces.Office, "office");
        
        var srcBody = srcFormula.FindOrCreateChild(srcFormula.ContentDom, "body", OdfNamespaces.Office, "office");
        var srcFormulaRoot = srcFormula.FindOrCreateChild(srcBody, "formula", OdfNamespaces.Office, "office");
        
        foreach (var child in srcFormulaRoot.Children)
        {
            if (child.NodeType == OdfNodeType.Element)
            {
                var imported = OdfNode.ImportNode(child, srcFormula.Package, Package);
                RemapStylesInNodes(imported, renameMap);
                destFormulaRoot.AppendChild(imported);
            }
        }
    }
}

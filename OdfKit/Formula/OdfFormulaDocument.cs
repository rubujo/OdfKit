using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OdfKit.Compliance;
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
    /// 建立新的 ODF 公式文件。
    /// </summary>
    /// <returns>新的 <see cref="OdfFormulaDocument"/> 執行個體。</returns>
    public static OdfFormulaDocument Create()
    {
        return (OdfFormulaDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Formula);
    }

    /// <summary>
    /// 從指定路徑載入 ODF 公式文件。
    /// </summary>
    /// <param name="path">ODF 公式文件路徑。</param>
    /// <returns>載入完成的 <see cref="OdfFormulaDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODF 公式時擲出。</exception>
    public new static OdfFormulaDocument Load(string path)
    {
        return EnsureFormula(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// 從指定資料流載入 ODF 公式文件。
    /// </summary>
    /// <param name="stream">包含 ODF 公式文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="OdfFormulaDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODF 公式時擲出。</exception>
    public new static OdfFormulaDocument Load(Stream stream, string? fileName = null)
    {
        return EnsureFormula(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    /// <summary>
    /// 取得主要公式節點。
    /// </summary>
    public OdfNode FormulaNode => GetFormulaNode();

    /// <summary>
    /// 取得目前的 MathML 根節點。
    /// </summary>
    public OdfNode MathNode => FindOrCreateChild(GetFormulaNode(), "math", "http://www.w3.org/1998/Math/MathML", "math");

    /// <summary>
    /// 取得 MathML 內容的純文字摘要。
    /// </summary>
    public string MathText => MathNode.TextContent;

    /// <summary>
    /// 取得或設定完整 MathML XML 字串。
    /// </summary>
    /// <remarks>
    /// 讀取時會序列化目前的 MathML 根節點；寫入時會沿用 <see cref="SetMathMl(string)"/> 的安全解析與根節點檢查。
    /// </remarks>
    public string MathMlXml
    {
        get
        {
            using var stream = new MemoryStream();
            OdfXmlWriter.Write(MathNode, stream);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        set => SetMathMl(value);
    }

    /// <summary>
    /// 以指定 MathML XML 取代公式內容。
    /// </summary>
    /// <param name="mathMlXml">格式正確的 MathML XML。</param>
    /// <returns>匯入後的 MathML 根節點。</returns>
    /// <exception cref="ArgumentException">當 MathML XML 為空或根節點不是 MathML math 時擲出。</exception>
    public OdfNode SetMathMl(string mathMlXml)
    {
        if (string.IsNullOrWhiteSpace(mathMlXml))
        {
            throw new ArgumentException("MathML XML 不能為空。", nameof(mathMlXml));
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(mathMlXml));
        OdfNode math = OdfXmlReader.Parse(stream);
        if (math.LocalName != "math" || math.NamespaceUri != "http://www.w3.org/1998/Math/MathML")
        {
            throw new ArgumentException("MathML 根節點必須是 math:math。", nameof(mathMlXml));
        }

        OdfNode formula = GetFormulaNode();
        foreach (OdfNode child in new List<OdfNode>(formula.Children))
        {
            formula.RemoveChild(child);
        }

        formula.AppendChild(math);
        return math;
    }

    private static OdfFormulaDocument EnsureFormula(OdfDocument document)
    {
        if (document is OdfFormulaDocument formula)
        {
            return formula;
        }

        document.Dispose();
        throw new InvalidOperationException("指定的 ODF 文件不是 ODF 公式。");
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
               "office:version=\"" + OdfVersionInfo.DefaultVersionString + "\">" +
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
               "office:version=\"" + OdfVersionInfo.DefaultVersionString + "\">" +
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

    private OdfNode GetFormulaNode()
    {
        OdfNode body = FindOrCreateChild(ContentDom, "body", OdfNamespaces.Office, "office");
        return FindOrCreateChild(body, "formula", OdfNamespaces.Office, "office");
    }
}

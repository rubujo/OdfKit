using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Formula;

/// <summary>
/// 代表 ODF 公式文件。
/// </summary>
public partial class OdfFormulaDocument : OdfDocument
{
    private const string MathMlNamespace = "http://www.w3.org/1998/Math/MathML";

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

        NormalizeLoadedBareMathRoot();
    }

    /// <summary>
    /// 真實 LibreOffice 產生的 ODF 公式文件，其 <c>content.xml</c> 根節點為裸 <c>math:math</c>
    /// 元素，並未以 <c>office:document-content</c> 包裹（此為 ODF 公式文件專屬的封裝慣例，
    /// 與其他文件類型不同）。OdfKit 內部一律以 <c>office:document-content/office:body/office:formula</c>
    /// 結構表示內容樹，因此載入時若偵測到裸 math 根節點，須先正規化包裝，否則 <see cref="GetFormulaNode"/>
    /// 等方法會在錯誤的節點上尋找子節點而讀不到任何公式內容。
    /// </summary>
    private void NormalizeLoadedBareMathRoot()
    {
        if (ContentRoot.NodeType != OdfNodeType.Element ||
            ContentRoot.LocalName != "math" ||
            ContentRoot.NamespaceUri != MathMlNamespace)
        {
            return;
        }

        OdfNode bareMath = ContentRoot;
        OdfNode wrapped = OdfNodeFactory.CreateElement("document-content", OdfNamespaces.Office, "office");
        wrapped.SetAttribute("version", OdfNamespaces.Office, OdfVersionInfo.DefaultVersionString, "office");
        OdfNode body = OdfNodeFactory.CreateElement("body", OdfNamespaces.Office, "office");
        OdfNode formulaNode = OdfNodeFactory.CreateElement("formula", OdfNamespaces.Office, "office");
        wrapped.AppendChild(body);
        body.AppendChild(formulaNode);
        formulaNode.AppendChild(bareMath);

        ContentRoot = wrapped;
    }

    /// <summary>
    /// 建立新的 ODF 公式文件 Fluent builder。
    /// </summary>
    /// <returns>新的 <see cref="OdfFormulaBuilder"/> 執行個體</returns>
    public static OdfFormulaBuilder Builder()
    {
        return new OdfFormulaBuilder(Create());
    }

    /// <summary>
    /// 建立新的 ODF 公式文件。
    /// </summary>
    /// <returns>新的 <see cref="OdfFormulaDocument"/> 執行個體</returns>
    public static OdfFormulaDocument Create()
    {
        return (OdfFormulaDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Formula);
    }

    /// <summary>
    /// 從指定路徑載入 ODF 公式文件。
    /// </summary>
    /// <param name="path">ODF 公式文件路徑</param>
    /// <returns>載入完成的 <see cref="OdfFormulaDocument"/> 執行個體</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODF 公式時擲出</exception>
    public new static OdfFormulaDocument Load(string path)
    {
        return EnsureFormula(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// 非同步從指定路徑載入 ODF 公式文件。
    /// </summary>
    /// <param name="path">ODF 公式文件路徑</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="OdfFormulaDocument"/></returns>
    public new static async Task<OdfFormulaDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        EnsureFormula(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入 ODF 公式文件。
    /// </summary>
    /// <param name="stream">包含 ODF 公式文件內容的資料流</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <returns>載入完成的 <see cref="OdfFormulaDocument"/> 執行個體</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODF 公式時擲出</exception>
    public new static OdfFormulaDocument Load(Stream stream, string? fileName = null)
    {
        return EnsureFormula(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    /// <summary>
    /// 非同步從指定資料流載入 ODF 公式文件。
    /// </summary>
    /// <param name="stream">包含 ODF 公式文件內容的資料流</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="OdfFormulaDocument"/></returns>
    public new static async Task<OdfFormulaDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        EnsureFormula(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 取得主要公式節點。
    /// </summary>
    public OdfNode FormulaNode => GetFormulaNode();

    /// <summary>
    /// 取得目前的 MathML 根節點。
    /// </summary>
    public OdfNode MathNode => FindOrCreateChild(GetFormulaNode(), "math", MathMlNamespace, "math");

    /// <summary>
    /// 取得 MathML 內容的純文字摘要。
    /// </summary>
    public string MathText => MathNode.TextContent;

    /// <summary>
    /// 取得目前 MathML row 中可辨識的 token 摘要。
    /// </summary>
    public IReadOnlyList<OdfMathToken> MathTokens => ReadMathTokens();

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
    /// <param name="mathMlXml">格式正確的 MathML XML</param>
    /// <returns>匯入後的 MathML 根節點</returns>
    /// <exception cref="ArgumentException">當 MathML XML 為空或根節點不是 MathML math 時擲出</exception>
    public OdfNode SetMathMl(string mathMlXml)
    {
        if (string.IsNullOrWhiteSpace(mathMlXml))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfFormulaDocument_MathmlCannotBeEmpty"), nameof(mathMlXml));
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(mathMlXml));
        OdfNode math = OdfXmlReader.Parse(stream);
        if (math.LocalName != "math" || math.NamespaceUri != MathMlNamespace)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfFormulaDocument_MathmlRootNodeMath"), nameof(mathMlXml));
        }

        OdfNode formula = GetFormulaNode();
        foreach (OdfNode child in new List<OdfNode>(formula.Children))
        {
            formula.RemoveChild(child);
        }

        formula.AppendChild(math);
        return math;
    }

    /// <summary>
    /// 以一組語意 token 建立 MathML row。
    /// </summary>
    /// <param name="tokens">要寫入 row 的 MathML token</param>
    /// <returns>匯入後的 MathML 根節點</returns>
    /// <exception cref="ArgumentException">當 <paramref name="tokens"/> 為空時擲出</exception>
    /// <exception cref="ArgumentNullException">當任一 token 為 <see langword="null"/> 時擲出</exception>
    public OdfNode SetMathRow(params OdfMathToken[] tokens)
    {
        if (tokens is null)
        {
            throw new ArgumentNullException(nameof(tokens));
        }

        if (tokens.Length == 0)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfFormulaDocument_MathmlCannotBeEmpty_2"), nameof(tokens));
        }

        OdfNode math = OdfNodeFactory.CreateElement("math", MathMlNamespace, "math");
        OdfNode row = OdfNodeFactory.CreateElement("mrow", MathMlNamespace, "math");
        foreach (OdfMathToken token in tokens)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(tokens), OdfLocalizer.GetMessage("Err_OdfFormulaDocument_MathmlCannotBeEmpty_3"));
            }

            row.AppendChild(CreateMathTokenNode(token));
        }

        math.AppendChild(row);

        OdfNode formula = GetFormulaNode();
        foreach (OdfNode child in new List<OdfNode>(formula.Children))
        {
            formula.RemoveChild(child);
        }

        formula.AppendChild(math);
        return math;
    }

    /// <summary>
    /// 建立以識別名稱等於另一個識別名稱的簡單 MathML 等式。
    /// </summary>
    /// <param name="leftIdentifier">等號左側識別名稱</param>
    /// <param name="rightIdentifier">等號右側識別名稱</param>
    /// <returns>匯入後的 MathML 根節點</returns>
    public OdfNode SetIdentifierEquation(string leftIdentifier, string rightIdentifier)
    {
        return SetMathRow(
            OdfMathToken.Identifier(leftIdentifier),
            OdfMathToken.Operator("="),
            OdfMathToken.Identifier(rightIdentifier));
    }
}

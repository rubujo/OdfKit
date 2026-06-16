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
    public OdfNode MathNode => FindOrCreateChild(GetFormulaNode(), "math", MathMlNamespace, "math");

    /// <summary>
    /// 取得 MathML 內容的純文字摘要。
    /// </summary>
    public string MathText => MathNode.TextContent;

    /// <summary>
    /// 取得目前 MathML row 中可辨識的 token 摘要。
    /// </summary>
    public IReadOnlyList<OdfMathToken> MathTokens => GetMathTokens();

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
        if (math.LocalName != "math" || math.NamespaceUri != MathMlNamespace)
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

    /// <summary>
    /// 以一組語意 token 建立 MathML row。
    /// </summary>
    /// <param name="tokens">要寫入 row 的 MathML token。</param>
    /// <returns>匯入後的 MathML 根節點。</returns>
    /// <exception cref="ArgumentException">當 <paramref name="tokens"/> 為空時擲出。</exception>
    /// <exception cref="ArgumentNullException">當任一 token 為 <see langword="null"/> 時擲出。</exception>
    public OdfNode SetMathRow(params OdfMathToken[] tokens)
    {
        if (tokens is null)
        {
            throw new ArgumentNullException(nameof(tokens));
        }

        if (tokens.Length == 0)
        {
            throw new ArgumentException("MathML token 不能為空。", nameof(tokens));
        }

        OdfNode math = OdfNodeFactory.CreateElement("math", MathMlNamespace, "math");
        OdfNode row = OdfNodeFactory.CreateElement("mrow", MathMlNamespace, "math");
        foreach (OdfMathToken token in tokens)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(tokens), "MathML token 不能為 null。");
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
    /// <param name="leftIdentifier">等號左側識別名稱。</param>
    /// <param name="rightIdentifier">等號右側識別名稱。</param>
    /// <returns>匯入後的 MathML 根節點。</returns>
    public OdfNode SetIdentifierEquation(string leftIdentifier, string rightIdentifier)
    {
        return SetMathRow(
            OdfMathToken.Identifier(leftIdentifier),
            OdfMathToken.Operator("="),
            OdfMathToken.Identifier(rightIdentifier));
    }
}

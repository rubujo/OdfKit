using System;
using System.Collections.Generic;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Formula;

public partial class OdfFormulaDocument
{
    #region Formula Document Infrastructure

    private static OdfFormulaDocument EnsureFormula(OdfDocument document)
    {
        if (document is OdfFormulaDocument formula && document.DocumentKind == OdfDocumentKind.Formula)
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

    private IReadOnlyList<OdfMathToken> ReadMathTokens()
    {
        OdfNode math = MathNode;
        OdfNode tokenParent = FindChildElement(math, "mrow", MathMlNamespace) ?? math;
        List<OdfMathToken> tokens = [];
        foreach (OdfNode child in tokenParent.Children)
        {
            if (child.NodeType is not OdfNodeType.Element || child.NamespaceUri != MathMlNamespace)
            {
                continue;
            }

            OdfMathToken? token = CreateTokenOrDefault(child);
            if (token is not null)
            {
                tokens.Add(token);
            }
        }

        return tokens.AsReadOnly();
    }

    private static OdfNode CreateMathTokenNode(OdfMathToken token)
    {
        OdfNode node = OdfNodeFactory.CreateElement(GetMathTokenElementName(token.Kind), MathMlNamespace, "math");
        node.TextContent = token.Text;
        return node;
    }

    private static OdfMathToken? CreateTokenOrDefault(OdfNode node)
    {
        return node.LocalName switch
        {
            "mi" => OdfMathToken.Identifier(node.TextContent),
            "mn" => OdfMathToken.Number(node.TextContent),
            "mo" => OdfMathToken.Operator(node.TextContent),
            "mtext" => OdfMathToken.TextToken(node.TextContent),
            _ => null
        };
    }

    private static string GetMathTokenElementName(OdfMathTokenKind kind)
    {
        return kind switch
        {
            OdfMathTokenKind.Identifier => "mi",
            OdfMathTokenKind.Number => "mn",
            OdfMathTokenKind.Operator => "mo",
            OdfMathTokenKind.Text => "mtext",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "未知的 MathML token 類型。")
        };
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

    #endregion
}

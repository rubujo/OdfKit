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
        throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfFormulaDocument_SpecifiedOdfFileOdf"));
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
        var srcFormula = sourceDoc as OdfFormulaDocument ?? throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfFormulaDocument_SourceDocumentOdfformuladocument"));

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

    /// <summary>
    /// 取得儲存至封裝容器時，<c>content.xml</c> 實際應寫入的根節點。
    /// </summary>
    /// <remarks>
    /// 真實 LibreOffice 對 ODF 公式文件（<c>application/vnd.oasis.opendocument.formula</c>）
    /// 的 <c>content.xml</c> 採用裸 <c>math:math</c> 根節點，並不包裹於
    /// <c>office:document-content/office:body/office:formula</c> 結構內（與其他文件類型不同的封裝慣例）。
    /// 若以 OdfKit 內部慣用的包裹結構直接寫出，LibreOffice 的 <c>math8</c> 匯入篩選器會回報
    /// 「source file could not be loaded」並拒絕開啟。此處在序列化邊界轉換為真機相容的裸根節點形狀，
    /// 內部 <see cref="OdfDocument.ContentDom"/> 仍維持包裹結構，不影響其餘共用基礎結構（統計、版本戳記等）。
    /// </remarks>
    internal override OdfNode GetContentXmlForPersistence()
    {
        return MathNode.CloneNode(true);
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
        OdfNode node = token.Kind switch
        {
            OdfMathTokenKind.Superscript => CreateScriptNode("msup", token),
            OdfMathTokenKind.Subscript => CreateScriptNode("msub", token),
            OdfMathTokenKind.Fraction => CreateScriptNode("mfrac", token),
            OdfMathTokenKind.Under => CreateScriptNode("munder", token),
            OdfMathTokenKind.Over => CreateScriptNode("mover", token),
            OdfMathTokenKind.Radical => CreateRadicalNode(token),
            OdfMathTokenKind.Row => CreateRowNode("mrow", token),
            OdfMathTokenKind.Matrix => CreateMatrixNode(token),
            OdfMathTokenKind.UnderOver => CreateUnderOverNode(token),
            OdfMathTokenKind.Fenced => CreateFencedNode(token),
            OdfMathTokenKind.Style => CreateStyleNode(token),
            _ => CreateLeafMathTokenNode(token),
        };

        if (token.Attributes is not null)
        {
            foreach (KeyValuePair<string, string> attribute in token.Attributes)
            {
                node.SetAttribute(attribute.Key, string.Empty, attribute.Value);
            }
        }

        return node;
    }

    private static OdfNode CreateRadicalNode(OdfMathToken token)
    {
        if (token.Base is null)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfFormulaDocument_RadicalTokenContainRadicand"));
        }

        string elementName = token.Script is null ? "msqrt" : "mroot";
        OdfNode node = OdfNodeFactory.CreateElement(elementName, MathMlNamespace, "math");
        node.AppendChild(CreateMathTokenNode(token.Base));
        if (token.Script is not null)
        {
            node.AppendChild(CreateMathTokenNode(token.Script));
        }

        return node;
    }

    private static OdfNode CreateRowNode(string elementName, OdfMathToken token)
    {
        if (token.Children is null || token.Children.Count == 0)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfFormulaDocument_GroupTokenContainLeast"));
        }

        OdfNode node = OdfNodeFactory.CreateElement(elementName, MathMlNamespace, "math");
        foreach (OdfMathToken child in token.Children)
        {
            node.AppendChild(CreateMathTokenNode(child));
        }

        return node;
    }

    private static OdfNode CreateMatrixNode(OdfMathToken token)
    {
        if (token.Children is null || token.Children.Count == 0)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfFormulaDocument_MatrixTokenContainLeast"));
        }

        OdfNode table = OdfNodeFactory.CreateElement("mtable", MathMlNamespace, "math");
        foreach (OdfMathToken rowToken in token.Children)
        {
            OdfNode rowNode = OdfNodeFactory.CreateElement("mtr", MathMlNamespace, "math");
            IReadOnlyList<OdfMathToken> cells = rowToken.Children ?? [];
            foreach (OdfMathToken cell in cells)
            {
                OdfNode cellNode = OdfNodeFactory.CreateElement("mtd", MathMlNamespace, "math");
                cellNode.AppendChild(CreateMathTokenNode(cell));
                rowNode.AppendChild(cellNode);
            }

            table.AppendChild(rowNode);
        }

        return table;
    }

    private static OdfNode CreateUnderOverNode(OdfMathToken token)
    {
        if (token.Children is null || token.Children.Count != 3)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfFormulaDocument_UpperLowerMarkTokens"));
        }

        OdfNode node = OdfNodeFactory.CreateElement("munderover", MathMlNamespace, "math");
        foreach (OdfMathToken child in token.Children)
        {
            node.AppendChild(CreateMathTokenNode(child));
        }

        return node;
    }

    private static OdfNode CreateFencedNode(OdfMathToken token)
    {
        if (token.Base is null)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfFormulaDocument_BracketGroupTokenContain"));
        }

        string[] delimiters = token.Text.Split('|');
        string open = delimiters.Length > 0 ? delimiters[0] : "(";
        string close = delimiters.Length > 1 ? delimiters[1] : ")";

        OdfNode row = OdfNodeFactory.CreateElement("mrow", MathMlNamespace, "math");
        OdfNode openNode = OdfNodeFactory.CreateElement("mo", MathMlNamespace, "math");
        openNode.TextContent = open;
        row.AppendChild(openNode);
        row.AppendChild(CreateMathTokenNode(token.Base));
        OdfNode closeNode = OdfNodeFactory.CreateElement("mo", MathMlNamespace, "math");
        closeNode.TextContent = close;
        row.AppendChild(closeNode);
        return row;
    }

    private static OdfNode CreateStyleNode(OdfMathToken token)
    {
        if (token.Base is null)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfFormulaDocument_StyleGroupTokenContain"));
        }

        OdfNode node = OdfNodeFactory.CreateElement("mstyle", MathMlNamespace, "math");
        if (!string.IsNullOrEmpty(token.Text))
        {
            node.SetAttribute("displaystyle", string.Empty, token.Text);
        }

        node.AppendChild(CreateMathTokenNode(token.Base));
        return node;
    }

    private static OdfNode CreateLeafMathTokenNode(OdfMathToken token)
    {
        OdfNode node = OdfNodeFactory.CreateElement(GetLeafMathTokenElementName(token.Kind), MathMlNamespace, "math");
        node.TextContent = token.Text;
        return node;
    }

    private static OdfNode CreateScriptNode(string elementName, OdfMathToken token)
    {
        if (token.Base is null || token.Script is null)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfFormulaDocument_SuperscriptSubscriptTokensContain"));
        }

        OdfNode node = OdfNodeFactory.CreateElement(elementName, MathMlNamespace, "math");
        node.AppendChild(CreateMathTokenNode(token.Base));
        node.AppendChild(CreateMathTokenNode(token.Script));
        return node;
    }

    private static readonly string[] KnownMathAttributeNames =
    [
        "mathvariant", "mathsize", "mathcolor", "mathbackground",
        "displaystyle", "stretchy", "lspace", "rspace",
    ];

    private static OdfMathToken? CreateTokenOrDefault(OdfNode node)
    {
        OdfMathToken? token = node.LocalName switch
        {
            "mi" => OdfMathToken.Identifier(node.TextContent),
            "mn" => OdfMathToken.Number(node.TextContent),
            "mo" => OdfMathToken.Operator(node.TextContent),
            "mtext" => OdfMathToken.TextToken(node.TextContent),
            "msup" => CreateScriptToken(node, OdfMathTokenKind.Superscript),
            "msub" => CreateScriptToken(node, OdfMathTokenKind.Subscript),
            "mfrac" => CreateScriptToken(node, OdfMathTokenKind.Fraction),
            "munder" => CreateScriptToken(node, OdfMathTokenKind.Under),
            "mover" => CreateScriptToken(node, OdfMathTokenKind.Over),
            "msqrt" => CreateRadicalToken(node, hasIndex: false),
            "mroot" => CreateRadicalToken(node, hasIndex: true),
            "mrow" => CreateRowToken(node),
            "mtable" => CreateMatrixToken(node),
            "munderover" => CreateUnderOverToken(node),
            "mstyle" => CreateStyleToken(node),
            _ => null
        };

        return token is null ? null : AttachKnownMathAttributes(token, node);
    }

    private static OdfMathToken AttachKnownMathAttributes(OdfMathToken token, OdfNode node)
    {
        OdfMathToken result = token;
        foreach (string name in KnownMathAttributeNames)
        {
            string? value = node.GetAttribute(name, string.Empty);
            if (value is not null)
            {
                result = result.WithAttribute(name, value);
            }
        }

        return result;
    }

    private static OdfMathToken? CreateScriptToken(OdfNode node, OdfMathTokenKind kind)
    {
        List<OdfNode> children = CollectElementChildren(node);
        if (children.Count < 2)
        {
            return null;
        }

        OdfMathToken? baseToken = CreateTokenOrDefault(children[0]);
        OdfMathToken? scriptToken = CreateTokenOrDefault(children[1]);
        if (baseToken is null || scriptToken is null)
        {
            return null;
        }

        return kind switch
        {
            OdfMathTokenKind.Subscript => OdfMathToken.Subscript(baseToken, scriptToken),
            OdfMathTokenKind.Fraction => OdfMathToken.Fraction(baseToken, scriptToken),
            OdfMathTokenKind.Under => OdfMathToken.Under(baseToken, scriptToken),
            OdfMathTokenKind.Over => OdfMathToken.Over(baseToken, scriptToken),
            _ => OdfMathToken.Superscript(baseToken, scriptToken),
        };
    }

    private static OdfMathToken? CreateRadicalToken(OdfNode node, bool hasIndex)
    {
        List<OdfNode> children = CollectElementChildren(node);
        if (children.Count == 0)
        {
            return null;
        }

        OdfMathToken? radicand = CreateTokenOrDefault(children[0]);
        if (radicand is null)
        {
            return null;
        }

        if (!hasIndex || children.Count < 2)
        {
            return OdfMathToken.Radical(radicand);
        }

        OdfMathToken? index = CreateTokenOrDefault(children[1]);
        return index is null ? OdfMathToken.Radical(radicand) : OdfMathToken.Radical(radicand, index);
    }

    private static OdfMathToken? CreateRowToken(OdfNode node)
    {
        List<OdfMathToken> tokens = [];
        foreach (OdfNode child in CollectElementChildren(node))
        {
            OdfMathToken? token = CreateTokenOrDefault(child);
            if (token is not null)
            {
                tokens.Add(token);
            }
        }

        return tokens.Count == 0 ? null : OdfMathToken.Row(tokens.ToArray());
    }

    private static OdfMathToken? CreateMatrixToken(OdfNode node)
    {
        List<OdfMathToken> rows = [];
        foreach (OdfNode rowNode in CollectElementChildren(node))
        {
            if (rowNode.LocalName != "mtr")
            {
                continue;
            }

            List<OdfMathToken> cells = [];
            foreach (OdfNode cellNode in CollectElementChildren(rowNode))
            {
                if (cellNode.LocalName != "mtd")
                {
                    continue;
                }

                List<OdfNode> cellChildren = CollectElementChildren(cellNode);
                if (cellChildren.Count == 0)
                {
                    continue;
                }

                OdfMathToken? cellToken = CreateTokenOrDefault(cellChildren[0]);
                if (cellToken is not null)
                {
                    cells.Add(cellToken);
                }
            }

            if (cells.Count > 0)
            {
                rows.Add(OdfMathToken.Row(cells.ToArray()));
            }
        }

        return rows.Count == 0 ? null : OdfMathToken.Matrix(rows.ToArray());
    }

    private static OdfMathToken? CreateUnderOverToken(OdfNode node)
    {
        List<OdfNode> children = CollectElementChildren(node);
        if (children.Count < 3)
        {
            return null;
        }

        OdfMathToken? baseToken = CreateTokenOrDefault(children[0]);
        OdfMathToken? under = CreateTokenOrDefault(children[1]);
        OdfMathToken? over = CreateTokenOrDefault(children[2]);
        if (baseToken is null || under is null || over is null)
        {
            return null;
        }

        return OdfMathToken.UnderOver(baseToken, under, over);
    }

    private static OdfMathToken? CreateStyleToken(OdfNode node)
    {
        List<OdfNode> children = CollectElementChildren(node);
        if (children.Count == 0)
        {
            return null;
        }

        OdfMathToken? inner = CreateTokenOrDefault(children[0]);
        if (inner is null)
        {
            return null;
        }

        string? displayStyleAttr = node.GetAttribute("displaystyle", string.Empty);
        bool? displayStyle = displayStyleAttr switch
        {
            "true" => true,
            "false" => false,
            _ => null,
        };

        return OdfMathToken.Style(inner, displayStyle);
    }

    private static List<OdfNode> CollectElementChildren(OdfNode node)
    {
        List<OdfNode> children = [];
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element && child.NamespaceUri == MathMlNamespace)
            {
                children.Add(child);
            }
        }

        return children;
    }

    private static string GetLeafMathTokenElementName(OdfMathTokenKind kind)
    {
        return kind switch
        {
            OdfMathTokenKind.Identifier => "mi",
            OdfMathTokenKind.Number => "mn",
            OdfMathTokenKind.Operator => "mo",
            OdfMathTokenKind.Text => "mtext",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, OdfLocalizer.GetMessage("Err_OdfFormulaDocument_UnknownMathmlLeafNode"))
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

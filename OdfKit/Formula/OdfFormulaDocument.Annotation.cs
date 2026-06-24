using System;
using System.Collections.Generic;
using OdfKit.Compliance;
using OdfKit.DOM;

namespace OdfKit.Formula;

public partial class OdfFormulaDocument
{
    /// <summary>
    /// 取得指定編碼的 MathML 標註內容（<c>math:semantics</c>／<c>math:annotation</c>）。
    /// </summary>
    /// <param name="encoding">標註編碼（例如 <c>application/x-tex</c>、<c>StarMath 5.0</c>）</param>
    /// <returns>標註內容；若公式未以 <c>math:semantics</c> 包裹或不存在對應編碼的標註則為 <see langword="null"/></returns>
    /// <exception cref="ArgumentException">當 <paramref name="encoding"/> 為空白時擲出</exception>
    public string? GetAnnotation(string encoding)
    {
        if (string.IsNullOrWhiteSpace(encoding))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfFormulaDocument_MathmlCannotBeEmpty"), nameof(encoding));
        }

        OdfNode? semantics = FindChildElement(MathNode, "semantics", MathMlNamespace);
        OdfNode? annotation = semantics is null ? null : FindAnnotationByEncoding(semantics, encoding);
        return annotation is null ? null : annotation.TextContent;
    }

    /// <summary>
    /// 設定或移除指定編碼的 MathML 標註（<c>math:annotation</c>），用於附帶原始來源（例如建立
    /// 公式時使用的 LaTeX 或 StarMath 字串），使後續可精確還原而非僅 best-effort 重建。
    /// </summary>
    /// <param name="encoding">標註編碼（例如 <c>application/x-tex</c>）</param>
    /// <param name="content">標註內容；傳入 <see langword="null"/> 表示移除既有標註</param>
    /// <exception cref="ArgumentException">當 <paramref name="encoding"/> 為空白時擲出</exception>
    public void SetAnnotation(string encoding, string? content)
    {
        if (string.IsNullOrWhiteSpace(encoding))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfFormulaDocument_MathmlCannotBeEmpty"), nameof(encoding));
        }

        OdfNode math = MathNode;
        OdfNode? semantics = FindChildElement(math, "semantics", MathMlNamespace);

        if (content is null)
        {
            OdfNode? existing = semantics is null ? null : FindAnnotationByEncoding(semantics, encoding);
            if (existing is not null)
            {
                semantics!.RemoveChild(existing);
            }

            return;
        }

        if (semantics is null)
        {
            semantics = WrapPresentationContentInSemantics(math);
        }

        OdfNode? annotation = FindAnnotationByEncoding(semantics, encoding);
        if (annotation is null)
        {
            annotation = OdfNodeFactory.CreateElement("annotation", MathMlNamespace, "math");
            annotation.SetAttribute("encoding", string.Empty, encoding);
            semantics.AppendChild(annotation);
        }

        foreach (OdfNode child in new List<OdfNode>(annotation.Children))
        {
            annotation.RemoveChild(child);
        }

        annotation.TextContent = content;
    }

    /// <summary>
    /// 將 <c>math:math</c> 底下既有的呈現內容（presentation MathML，例如 <c>mrow</c>）包裹進新建立的
    /// <c>math:semantics</c>，以便附加 <c>math:annotation</c> 標註，且不影響既有呈現內容的結構。
    /// </summary>
    private static OdfNode WrapPresentationContentInSemantics(OdfNode math)
    {
        OdfNode semantics = OdfNodeFactory.CreateElement("semantics", MathMlNamespace, "math");
        foreach (OdfNode child in new List<OdfNode>(math.Children))
        {
            math.RemoveChild(child);
            semantics.AppendChild(child);
        }

        math.AppendChild(semantics);
        return semantics;
    }

    private static OdfNode? FindAnnotationByEncoding(OdfNode semantics, string encoding)
    {
        foreach (OdfNode child in semantics.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "annotation" &&
                child.NamespaceUri == MathMlNamespace &&
                string.Equals(child.GetAttribute("encoding", string.Empty), encoding, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>
    /// 取得僅含呈現內容（presentation MathML）的純文字摘要，排除
    /// <c>math:semantics</c> 底下的 <c>math:annotation</c>／<c>math:annotation-xml</c> 標註文字。
    /// </summary>
    /// <remarks>
    /// 真實 LibreOffice 的 <c>math8</c> 匯出篩選器會將原始 StarMath 來源以
    /// <c>math:annotation</c> 標註附加於 <c>math:semantics</c> 底下，若直接對整個
    /// <see cref="MathNode"/> 取 <see cref="OdfNode.TextContent"/>，會將標註文字與呈現內容文字
    /// 混雜串接，因此此處改為僅遍歷呈現內容子節點（略過 <c>annotation</c>／<c>annotation-xml</c>）。
    /// </remarks>
    private static string GetPresentationTextContent(OdfNode math)
    {
        OdfNode? semantics = FindChildElement(math, "semantics", MathMlNamespace);
        if (semantics is null)
        {
            return math.TextContent;
        }

        var sb = new System.Text.StringBuilder();
        foreach (OdfNode child in semantics.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.NamespaceUri == MathMlNamespace &&
                (child.LocalName == "annotation" || child.LocalName == "annotation-xml"))
            {
                continue;
            }

            sb.Append(child.TextContent);
        }

        return sb.ToString();
    }
}

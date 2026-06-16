using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 表示 ODF 文件中的文獻標記。
/// </summary>
/// <param name="node">文獻標記 the OdfNode 節點</param>
public class OdfBibliographyMark(OdfNode node)
{
    /// <summary>
    /// 取得與此標記相關聯的 OdfNode 節點。
    /// </summary>
    public OdfNode Node { get; } = node ?? throw new ArgumentNullException(nameof(node));

    /// <summary>
    /// 取得或設定此文獻標記的識別碼。
    /// </summary>
    public string Identifier
    {
        get => Node.GetAttribute("identifier", OdfNamespaces.Text) ?? string.Empty;
        set => Node.SetAttribute("identifier", OdfNamespaces.Text, value, "text");
    }

    /// <summary>
    /// 取得或設定此文獻標記的類型。
    /// </summary>
    public string BibliographyType
    {
        get => Node.GetAttribute("bibliography-type", OdfNamespaces.Text) ?? "book";
        set => Node.SetAttribute("bibliography-type", OdfNamespaces.Text, value, "text");
    }

    /// <summary>
    /// 取得或設定此文獻標記的作者。
    /// </summary>
    public string? Author
    {
        get => Node.GetAttribute("author", OdfNamespaces.Text);
        set => Node.SetAttribute("author", OdfNamespaces.Text, value ?? string.Empty, "text");
    }

    /// <summary>
    /// 取得或設定此文獻標記的標題。
    /// </summary>
    public string? Title
    {
        get => Node.GetAttribute("title", OdfNamespaces.Text);
        set => Node.SetAttribute("title", OdfNamespaces.Text, value ?? string.Empty, "text");
    }

    /// <summary>
    /// 取得或設定此文獻標記的年份。
    /// </summary>
    public string? Year
    {
        get => Node.GetAttribute("year", OdfNamespaces.Text);
        set => Node.SetAttribute("year", OdfNamespaces.Text, value ?? string.Empty, "text");
    }
}

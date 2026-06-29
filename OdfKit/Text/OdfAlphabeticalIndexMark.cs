using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Represents an alphabetical index mark in an ODF document.
/// 表示 ODF 文件中的字母索引標記。
/// </summary>
/// <param name="node">The OdfNode of the alphabetical index mark. / 字母索引標記的 OdfNode 節點。</param>
public class OdfAlphabeticalIndexMark(OdfNode node)
{
    /// <summary>
    /// Gets the OdfNode associated with this mark.
    /// 取得與此標記相關聯的 OdfNode 節點。
    /// </summary>
    public OdfNode Node { get; } = node ?? throw new ArgumentNullException(nameof(node));

    /// <summary>
    /// Gets or sets the string value of this index mark.
    /// 取得或設定此索引標記的字串值。
    /// </summary>
    public string StringValue
    {
        get => Node.GetAttribute("string-value", OdfNamespaces.Text) ?? string.Empty;
        set => Node.SetAttribute("string-value", OdfNamespaces.Text, value, "text");
    }

    /// <summary>
    /// Gets or sets the primary key of this index mark.
    /// 取得或設定此索引標記的主要鍵值。
    /// </summary>
    public string? Key1
    {
        get => Node.GetAttribute("key1", OdfNamespaces.Text);
        set => Node.SetAttribute("key1", OdfNamespaces.Text, value ?? string.Empty, "text");
    }

    /// <summary>
    /// Gets or sets the secondary key of this index mark.
    /// 取得或設定此索引標記的次要鍵值。
    /// </summary>
    public string? Key2
    {
        get => Node.GetAttribute("key2", OdfNamespaces.Text);
        set => Node.SetAttribute("key2", OdfNamespaces.Text, value ?? string.Empty, "text");
    }
}


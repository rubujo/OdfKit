using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 表示 ODF 文件中的字母索引標記。
/// </summary>
/// <param name="node">字母索引標記的 OdfNode 節點</param>
public class OdfAlphabeticalIndexMark(OdfNode node)
{
    /// <summary>
    /// 取得與此標記相關聯的 OdfNode 節點。
    /// </summary>
    public OdfNode Node { get; } = node ?? throw new ArgumentNullException(nameof(node));

    /// <summary>
    /// 取得或設定此索引標記的字串值。
    /// </summary>
    public string StringValue
    {
        get => Node.GetAttribute("string-value", OdfNamespaces.Text) ?? string.Empty;
        set => Node.SetAttribute("string-value", OdfNamespaces.Text, value, "text");
    }

    /// <summary>
    /// 取得或設定此索引標記的主要鍵值。
    /// </summary>
    public string? Key1
    {
        get => Node.GetAttribute("key1", OdfNamespaces.Text);
        set => Node.SetAttribute("key1", OdfNamespaces.Text, value ?? string.Empty, "text");
    }

    /// <summary>
    /// 取得或設定此索引標記的次要鍵值。
    /// </summary>
    public string? Key2
    {
        get => Node.GetAttribute("key2", OdfNamespaces.Text);
        set => Node.SetAttribute("key2", OdfNamespaces.Text, value ?? string.Empty, "text");
    }
}


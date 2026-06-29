using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Represents odf ruby.
/// 表示用於東亞注音（旁註標記）的 ruby 版面配置專案。
/// </summary>
/// <param name="node">The value to use. / 基礎的 OdfNode 節點</param>
/// <param name="doc">The value to use. / 所屬的文字文件</param>
public class OdfRuby(OdfNode node, TextDocument doc)
{
    /// <summary>
    /// Gets argument null exception.
    /// 取得表示 ruby 專案的基礎 ODF 節點。
    /// </summary>
    public OdfNode Node { get; } = node ?? throw new ArgumentNullException(nameof(node));

    private readonly TextDocument _doc = doc ?? throw new ArgumentNullException(nameof(doc));

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定與 ruby 專案關聯的樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => Node.GetAttribute("style-name", OdfNamespaces.Text);
        set => Node.SetAttribute("style-name", OdfNamespaces.Text, value ?? string.Empty, "text");
    }

    /// <summary>
    /// Gets this member.
    /// 取得 ruby 的基底節點。
    /// </summary>
    public OdfNode? RubyBaseNode
    {
        get
        {
            foreach (var child in Node.Children)
            {
                if (child.LocalName == "ruby-base" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    return child;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Gets this member.
    /// 取得 ruby 的文字節點。
    /// </summary>
    public OdfNode? RubyTextNode
    {
        get
        {
            foreach (var child in Node.Children)
            {
                if (child.LocalName == "ruby-text" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    return child;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定與 ruby 基底專案關聯的樣式名稱。
    /// </summary>
    public string? RubyBaseStyleName
    {
        get => RubyBaseNode?.GetAttribute("style-name", OdfNamespaces.Text);
        set => RubyBaseNode?.SetAttribute("style-name", OdfNamespaces.Text, value ?? string.Empty, "text");
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定與 ruby 文字專案關聯的樣式名稱。
    /// </summary>
    public string? RubyTextStyleName
    {
        get => RubyTextNode?.GetAttribute("style-name", OdfNamespaces.Text);
        set => RubyTextNode?.SetAttribute("style-name", OdfNamespaces.Text, value ?? string.Empty, "text");
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定 ruby 的位置（例如 "above" 或 "below"）。
    /// </summary>
    public string? RubyPosition
    {
        get => _doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "ruby-position", OdfNamespaces.Style, "ruby");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "ruby", "ruby-properties", "ruby-position", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定 ruby 的對齊方式（例如 "left"、"center"、"right"、"distribute-letter"、"distribute-space"）。
    /// </summary>
    public string? RubyAlign
    {
        get => _doc.StyleEngine.GetStyleProperty(StyleName ?? string.Empty, "ruby-align", OdfNamespaces.Style, "ruby");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "ruby", "ruby-properties", "ruby-align", OdfNamespaces.Style, value ?? string.Empty, "style");
    }
}

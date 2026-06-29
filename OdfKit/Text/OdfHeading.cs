using System;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Represents a heading in a text document.
/// 表示文字文件中的標題。
/// </summary>
public class OdfHeading : OdfParagraph
{
    internal OdfHeading(OdfNode node, TextDocument doc) : base(node, doc) { }

    /// <summary>
    /// Gets or sets the outline level of the heading.
    /// 取得或設定標題的大綱階層。
    /// </summary>
    public int OutlineLevel
    {
        get => int.TryParse(Node.GetAttribute("outline-level", OdfNamespaces.Text), NumberStyles.Integer, CultureInfo.InvariantCulture, out var lvl) ? lvl : 1;
        set => Node.SetAttribute("outline-level", OdfNamespaces.Text, value.ToString(CultureInfo.InvariantCulture), "text");
    }
}

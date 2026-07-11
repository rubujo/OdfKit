using System;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Drawing;

/// <summary>
/// Represents a named ODF drawing gradient definition.
/// 表示具名稱的 ODF 繪圖漸層定義。
/// </summary>
public sealed class OdfGradient
{
    internal OdfGradient(OdfNode node)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
    }

    internal OdfNode Node { get; }

    /// <summary>
    /// Gets the gradient name.
    /// 取得漸層名稱。
    /// </summary>
    public string Name => Node.GetAttribute("name", OdfNamespaces.Draw) ?? string.Empty;

    /// <summary>
    /// Gets or sets the gradient style token, such as <c>linear</c> or <c>radial</c>.
    /// 取得或設定漸層樣式詞彙，例如 <c>linear</c> 或 <c>radial</c>。
    /// </summary>
    public string Style
    {
        get => Node.GetAttribute("style", OdfNamespaces.Draw) ?? string.Empty;
        set => Node.SetAttribute("style", OdfNamespaces.Draw, value, "draw");
    }

    /// <summary>
    /// Gets or sets the starting color.
    /// 取得或設定起始色彩。
    /// </summary>
    public string StartColor
    {
        get => Node.GetAttribute("start-color", OdfNamespaces.Draw) ?? string.Empty;
        set => Node.SetAttribute("start-color", OdfNamespaces.Draw, value, "draw");
    }

    /// <summary>
    /// Gets or sets the ending color.
    /// 取得或設定結束色彩。
    /// </summary>
    public string EndColor
    {
        get => Node.GetAttribute("end-color", OdfNamespaces.Draw) ?? string.Empty;
        set => Node.SetAttribute("end-color", OdfNamespaces.Draw, value, "draw");
    }

    /// <summary>
    /// Gets or sets the gradient angle in tenths of a degree.
    /// 取得或設定漸層角度，單位為十分之一度。
    /// </summary>
    public int Angle
    {
        get => int.TryParse(
            Node.GetAttribute("angle", OdfNamespaces.Draw),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int angle)
            ? angle
            : 0;
        set => Node.SetAttribute("angle", OdfNamespaces.Draw, value.ToString(CultureInfo.InvariantCulture), "draw");
    }
}

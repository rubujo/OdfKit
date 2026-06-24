using System.Drawing;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 <see cref="OdfColor"/> 的隱式轉換行為。
/// </summary>
public class OdfColorTests
{
    /// <summary>
    /// 驗證可由 HTML 色碼字串隱式轉換為 <see cref="OdfColor"/>。
    /// </summary>
    [Fact]
    public void ImplicitFromString_ParsesHtmlColor()
    {
        OdfColor color = "#4472C4";
        Assert.Equal("#4472C4", color.Value);
    }

    /// <summary>
    /// 驗證可由 <see cref="Color"/> 隱式轉換為 <see cref="OdfColor"/>。
    /// </summary>
    [Fact]
    public void ImplicitFromSystemDrawingColor_FormatsHexValue()
    {
        OdfColor color = Color.FromArgb(68, 114, 196);
        Assert.Equal("#4472c4", color.Value);
    }

    /// <summary>
    /// 驗證可由 <see cref="OdfColor"/> 隱式轉換為字串。
    /// </summary>
    [Fact]
    public void ImplicitToString_ReturnsHexLexicalValue()
    {
        OdfColor color = "#1A2B3C";
        string text = color;
        Assert.Equal("#1A2B3C", text);
    }
}

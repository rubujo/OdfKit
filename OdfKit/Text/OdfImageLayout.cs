using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 封裝影像版面配置設定，例如外框線、影像間距、環繞模式、裁剪及透明度等。
/// </summary>
public sealed class OdfImageLayout
{
    private readonly OdfImage _image;
    private readonly OdfDocument? _document;

    internal OdfImageLayout(OdfImage image, OdfDocument? document)
    {
        _image = image ?? throw new ArgumentNullException(nameof(image));
        _document = document;
    }

    /// <summary>
    /// 取得或設定外框線（對應 <c>fo:border</c>，例如 <c>"0.06pt solid #000000"</c>）。
    /// </summary>
    public string? Border
    {
        get => GetStyleProperty("border", OdfNamespaces.Fo);
        set => SetStyleProperty("border", OdfNamespaces.Fo, value, "fo");
    }

    /// <summary>
    /// 取得或設定影像的四邊間距（對應 <c>fo:margin</c>，例如 <c>"0.5cm"</c>）。
    /// </summary>
    public string? Margin
    {
        get => GetStyleProperty("margin", OdfNamespaces.Fo);
        set => SetStyleProperty("margin", OdfNamespaces.Fo, value, "fo");
    }

    /// <summary>
    /// 取得或設定影像的上方間距（對應 <c>fo:margin-top</c>）。
    /// </summary>
    public string? MarginTop
    {
        get => GetStyleProperty("margin-top", OdfNamespaces.Fo);
        set => SetStyleProperty("margin-top", OdfNamespaces.Fo, value, "fo");
    }

    /// <summary>
    /// 取得或設定影像的下方間距（對應 <c>fo:margin-bottom</c>）。
    /// </summary>
    public string? MarginBottom
    {
        get => GetStyleProperty("margin-bottom", OdfNamespaces.Fo);
        set => SetStyleProperty("margin-bottom", OdfNamespaces.Fo, value, "fo");
    }

    /// <summary>
    /// 取得或設定影像的左方間距（對應 <c>fo:margin-left</c>）。
    /// </summary>
    public string? MarginLeft
    {
        get => GetStyleProperty("margin-left", OdfNamespaces.Fo);
        set => SetStyleProperty("margin-left", OdfNamespaces.Fo, value, "fo");
    }

    /// <summary>
    /// 取得或設定影像的右方間距（對應 <c>fo:margin-right</c>）。
    /// </summary>
    public string? MarginRight
    {
        get => GetStyleProperty("margin-right", OdfNamespaces.Fo);
        set => SetStyleProperty("margin-right", OdfNamespaces.Fo, value, "fo");
    }

    /// <summary>
    /// 取得或設定文繞圖的環繞模式（對應 <c>style:wrap</c>，常見值為 <c>none</c>, <c>left</c>, <c>right</c>, <c>parallel</c>, <c>run-through</c>）。
    /// </summary>
    public string? Wrap
    {
        get => GetStyleProperty("wrap", OdfNamespaces.Style);
        set
        {
            SetStyleProperty("wrap", OdfNamespaces.Style, value, "style");
            // 同步設定 frame 上的 wrap-style 屬性以相容部分讀取器
            if (string.IsNullOrEmpty(value))
                _image.FrameNode.RemoveAttribute("wrap-style", OdfNamespaces.Style);
            else
                _image.FrameNode.SetAttribute("wrap-style", OdfNamespaces.Style, value!, "style");
        }
    }

    /// <summary>
    /// 取得或設定影像的裁剪區域設定（對應 <c>fo:clip</c>，格式如 <c>"rect(0cm, 0cm, 0cm, 0cm)"</c>）。
    /// </summary>
    public string? Crop
    {
        get => _image.ImageNode.GetAttribute("clip", OdfNamespaces.Fo);
        set
        {
            if (string.IsNullOrEmpty(value))
                _image.ImageNode.RemoveAttribute("clip", OdfNamespaces.Fo);
            else
                _image.ImageNode.SetAttribute("clip", OdfNamespaces.Fo, value!, "fo");
        }
    }

    /// <summary>
    /// 取得或設定影像透明度（例如 <c>"50%"</c> 或對應百分比，寫入 <c>draw:image-opacity</c> 或 <c>draw:opacity</c>）。
    /// </summary>
    public string? Opacity
    {
        get => GetStyleProperty("image-opacity", OdfNamespaces.Draw) ?? GetStyleProperty("opacity", OdfNamespaces.Draw);
        set
        {
            SetStyleProperty("image-opacity", OdfNamespaces.Draw, value, "draw");
            SetStyleProperty("opacity", OdfNamespaces.Draw, value, "draw");
        }
    }

    private string? GetStyleProperty(string attributeName, string namespaceUri)
    {
        if (_document is null)
            return null;

        string? styleName = _image.FrameNode.GetAttribute("style-name", OdfNamespaces.Draw);
        if (string.IsNullOrEmpty(styleName))
            return null;

        return _document.StyleEngine.GetStyleProperty(styleName!, attributeName, namespaceUri, "graphic");
    }

    private void SetStyleProperty(string attributeName, string namespaceUri, string? value, string prefix)
    {
        if (_document is null)
            return;

        string? styleName = _image.FrameNode.GetAttribute("style-name", OdfNamespaces.Draw);
        if (string.IsNullOrEmpty(styleName))
        {
            styleName = "img-style-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            _image.FrameNode.SetAttribute("style-name", OdfNamespaces.Draw, styleName, "draw");
        }

        _document.StyleEngine.SetLocalStyleProperty(_image.FrameNode, "graphic", "graphic-properties", attributeName, namespaceUri, value ?? string.Empty, prefix);
    }
}

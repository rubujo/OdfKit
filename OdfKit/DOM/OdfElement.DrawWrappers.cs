using System;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.DOM;

#region Draw Wrappers


/// <summary>
/// 表示 ODF 中的 draw:frame 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class DrawFrameElement(string? prefix = null) : OdfElement("frame", OdfNamespaces.Draw, prefix)
{
    /// <summary>
    /// 取得或設定此框架的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Draw, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Draw);
            else
                SetAttributeValue("name", OdfNamespaces.Draw, value, OdfNamespaces.GetPrefix(OdfNamespaces.Draw), GetDocumentVersion());
        }
    }

    /// <summary>
    /// 取得或設定此繪圖框架的仿射變換矩陣。
    /// </summary>
    public System.Numerics.Matrix3x2 Transform
    {
        get => OdfTransformHelper.ParseTransform(GetAttributeValue("transform", OdfNamespaces.Draw, GetDocumentVersion()));
        set
        {
            if (value.IsIdentity)
                RemoveAttribute("transform", OdfNamespaces.Draw);
            else
                SetAttributeValue("transform", OdfNamespaces.Draw, OdfTransformHelper.FormatTransform(value), OdfNamespaces.GetPrefix(OdfNamespaces.Draw), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 draw:image 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class DrawImageElement(string? prefix = null) : OdfElement("image", OdfNamespaces.Draw, prefix)
{
    /// <summary>
    /// 取得或設定影像的超連結 URL。
    /// </summary>
    public string? Href
    {
        get => GetAttributeValue("href", OdfNamespaces.XLink, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("href", OdfNamespaces.XLink);
            else
                SetAttributeValue("href", OdfNamespaces.XLink, value, OdfNamespaces.GetPrefix(OdfNamespaces.XLink), GetDocumentVersion());
        }
    }

    /// <summary>
    /// 取得或設定此影像的裁切矩形 lexical form。
    /// </summary>
    public string? CropClip
    {
        get => GetAttributeValue("clip", OdfNamespaces.Fo, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("clip", OdfNamespaces.Fo);
            else
                SetAttributeValue("clip", OdfNamespaces.Fo, value, OdfNamespaces.GetPrefix(OdfNamespaces.Fo), GetDocumentVersion());
        }
    }

    /// <summary>
    /// 設定此影像的裁切矩形。
    /// </summary>
    /// <param name="top">上方裁切距離</param>
    /// <param name="bottom">下方裁切距離</param>
    /// <param name="left">左方裁切距離</param>
    /// <param name="right">右方裁切距離</param>
    /// <returns>目前的影像元素，供鏈式呼叫使用</returns>
    public DrawImageElement Crop(OdfLength top, OdfLength bottom, OdfLength left, OdfLength right)
    {
        CropClip = $"rect({top}, {right}, {bottom}, {left})";
        return this;
    }

    /// <summary>
    /// 清除此影像的裁切設定。
    /// </summary>
    /// <returns>目前的影像元素，供鏈式呼叫使用</returns>
    public DrawImageElement ClearCrop()
    {
        CropClip = null;
        return this;
    }

    /// <summary>
    /// 設定此影像的繪圖效果。
    /// </summary>
    /// <param name="configure">用來設定影像效果的委派</param>
    /// <returns>目前的影像元素，供鏈式呼叫使用</returns>
    public DrawImageElement SetEffects(Action<OdfImageEffectsBuilder> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        configure(new OdfImageEffectsBuilder(this));
        return this;
    }

    /// <summary>
    /// 將圖片二進位資料寫入文件封裝並繫結至此 <c>draw:image</c>。
    /// </summary>
    /// <param name="bytes">圖片二進位資料</param>
    /// <param name="fileName">偏好的圖片檔名</param>
    /// <returns>圖片在封裝中的相對路徑</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="bytes"/> 或 <paramref name="fileName"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="InvalidOperationException">當此影像尚未附加到文件 DOM，因此無法取得封裝容器時擲出</exception>
    public string SetImageSource(byte[] bytes, string fileName)
    {
        if (bytes is null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        if (fileName is null)
        {
            throw new ArgumentNullException(nameof(fileName));
        }

        OdfDocument document = Document
            ?? throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_DrawImageElement_DocumentRequiredForImageSource"));
        var mediaManager = new OdfMediaManager(document.Package);
        string href = mediaManager.AddImage(bytes, fileName);

        Href = href;
        SetAttributeValue("type", OdfNamespaces.XLink, "simple", OdfNamespaces.GetPrefix(OdfNamespaces.XLink), GetDocumentVersion());
        SetAttributeValue("show", OdfNamespaces.XLink, "embed", OdfNamespaces.GetPrefix(OdfNamespaces.XLink), GetDocumentVersion());
        SetAttributeValue("actuate", OdfNamespaces.XLink, "onLoad", OdfNamespaces.GetPrefix(OdfNamespaces.XLink), GetDocumentVersion());
        return href;
    }
}

/// <summary>
/// 提供 <see cref="DrawImageElement"/> 影像效果的鏈式設定 API。
/// </summary>
/// <param name="image">要設定效果的影像元素</param>
public sealed class OdfImageEffectsBuilder(DrawImageElement image)
{
    /// <summary>
    /// 設定影像濾鏡名稱。
    /// </summary>
    /// <param name="filterName">濾鏡名稱；傳入 null 時移除設定</param>
    /// <returns>目前的建構器</returns>
    public OdfImageEffectsBuilder Filter(string? filterName)
    {
        SetOptionalDrawAttribute("filter-name", filterName);
        return this;
    }

    /// <summary>
    /// 設定影像柔邊半徑。
    /// </summary>
    /// <param name="radius">柔邊半徑</param>
    /// <returns>目前的建構器</returns>
    public OdfImageEffectsBuilder SoftEdge(OdfLength radius) => Filter($"soft-edge({radius})");

    /// <summary>
    /// 設定影像不透明度。
    /// </summary>
    /// <param name="opacity">不透明度百分比</param>
    /// <returns>目前的建構器</returns>
    public OdfImageEffectsBuilder Opacity(OdfPercent opacity)
    {
        SetOptionalDrawAttribute("image-opacity", opacity.ToString());
        return this;
    }

    /// <summary>
    /// 設定影像亮度調整。
    /// </summary>
    /// <param name="luminance">亮度百分比</param>
    /// <returns>目前的建構器</returns>
    public OdfImageEffectsBuilder Luminance(OdfPercent luminance)
    {
        SetOptionalDrawAttribute("luminance", luminance.ToString());
        return this;
    }

    /// <summary>
    /// 設定影像對比調整。
    /// </summary>
    /// <param name="contrast">對比 lexical form；傳入 null 時移除設定</param>
    /// <returns>目前的建構器</returns>
    public OdfImageEffectsBuilder Contrast(string? contrast)
    {
        SetOptionalDrawAttribute("contrast", contrast);
        return this;
    }

    /// <summary>
    /// 設定影像 gamma 調整。
    /// </summary>
    /// <param name="gamma">Gamma lexical form；傳入 null 時移除設定</param>
    /// <returns>目前的建構器</returns>
    public OdfImageEffectsBuilder Gamma(string? gamma)
    {
        SetOptionalDrawAttribute("gamma", gamma);
        return this;
    }

    /// <summary>
    /// 設定影像圓角半徑。
    /// </summary>
    /// <param name="radius">圓角半徑</param>
    /// <returns>目前的建構器</returns>
    public OdfImageEffectsBuilder CornerRadius(OdfLength radius)
    {
        SetOptionalDrawAttribute("corner-radius", radius.ToString());
        return this;
    }

    /// <summary>
    /// 設定影像陰影效果。
    /// </summary>
    /// <param name="color">陰影色彩</param>
    /// <param name="offsetX">X 軸位移</param>
    /// <param name="offsetY">Y 軸位移</param>
    /// <param name="opacity">選用的陰影不透明度</param>
    /// <returns>目前的建構器</returns>
    public OdfImageEffectsBuilder Shadow(OdfColor color, OdfLength offsetX, OdfLength offsetY, OdfPercent? opacity = null)
    {
        SetOptionalDrawAttribute("shadow", "visible");
        SetOptionalDrawAttribute("shadow-color", color.ToString());
        SetOptionalDrawAttribute("shadow-offset-x", offsetX.ToString());
        SetOptionalDrawAttribute("shadow-offset-y", offsetY.ToString());
        SetOptionalDrawAttribute("shadow-opacity", opacity?.ToString());
        return this;
    }

    /// <summary>
    /// 清除影像陰影效果。
    /// </summary>
    /// <returns>目前的建構器</returns>
    public OdfImageEffectsBuilder ClearShadow()
    {
        SetOptionalDrawAttribute("shadow", null);
        SetOptionalDrawAttribute("shadow-color", null);
        SetOptionalDrawAttribute("shadow-offset-x", null);
        SetOptionalDrawAttribute("shadow-offset-y", null);
        SetOptionalDrawAttribute("shadow-opacity", null);
        return this;
    }

    private void SetOptionalDrawAttribute(string localName, string? value)
    {
        if (value is null)
            image.RemoveAttribute(localName, OdfNamespaces.Draw);
        else
            image.SetAttributeValue(localName, OdfNamespaces.Draw, value, OdfNamespaces.GetPrefix(OdfNamespaces.Draw), image.GetDocumentVersion());
    }
}

/// <summary>
/// 表示 ODF 中的 draw:object 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class DrawObjectElement(string? prefix = null) : OdfElement("object", OdfNamespaces.Draw, prefix)
{
    /// <summary>
    /// 取得或設定內嵌物件的超連結 URL。
    /// </summary>
    public string? Href
    {
        get => GetAttributeValue("href", OdfNamespaces.XLink, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("href", OdfNamespaces.XLink);
            else
                SetAttributeValue("href", OdfNamespaces.XLink, value, OdfNamespaces.GetPrefix(OdfNamespaces.XLink), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的繪圖形狀元素。
/// </summary>
/// <param name="shapeKind">形狀種類</param>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class DrawShapeElement(string shapeKind, string? prefix = null) : OdfElement(shapeKind, OdfNamespaces.Draw, prefix)
{
    /// <summary>
    /// 取得或設定此形狀的名稱。
    /// </summary>
    public string? Name
    {
        get => GetAttributeValue("name", OdfNamespaces.Draw, GetDocumentVersion());
        set
        {
            if (value is null)
                RemoveAttribute("name", OdfNamespaces.Draw);
            else
                SetAttributeValue("name", OdfNamespaces.Draw, value, OdfNamespaces.GetPrefix(OdfNamespaces.Draw), GetDocumentVersion());
        }
    }

    /// <summary>
    /// 取得或設定此繪圖形狀的仿射變換矩陣。
    /// </summary>
    public System.Numerics.Matrix3x2 Transform
    {
        get => OdfTransformHelper.ParseTransform(GetAttributeValue("transform", OdfNamespaces.Draw, GetDocumentVersion()));
        set
        {
            if (value.IsIdentity)
                RemoveAttribute("transform", OdfNamespaces.Draw);
            else
                SetAttributeValue("transform", OdfNamespaces.Draw, OdfTransformHelper.FormatTransform(value), OdfNamespaces.GetPrefix(OdfNamespaces.Draw), GetDocumentVersion());
        }
    }
}

/// <summary>
/// 表示 ODF 中的 draw:g 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class DrawGroupElement(string? prefix = null) : OdfElement("g", OdfNamespaces.Draw, prefix);

/// <summary>
/// 表示 ODF 中的 draw:connector 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class DrawConnectorElement(string? prefix = null) : OdfElement("connector", OdfNamespaces.Draw, prefix);


#endregion

/// <summary>
/// 提供 ODF 中繪圖元素仿射變換的解析與格式化輔助功能。
/// </summary>
internal static class OdfTransformHelper
{
    private static readonly System.Text.RegularExpressions.Regex MatrixRegex = new(@"matrix\s*\(\s*([^)]+)\s*\)", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public static System.Numerics.Matrix3x2 ParseTransform(string? transformStr)
    {
        if (string.IsNullOrEmpty(transformStr))
            return System.Numerics.Matrix3x2.Identity;

        var match = MatrixRegex.Match(transformStr);
        if (match.Success)
        {
            var parts = match.Groups[1].Value.Split(new[] { ' ', ',', ';', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 6 &&
                float.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float m11) &&
                float.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float m12) &&
                float.TryParse(parts[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float m21) &&
                float.TryParse(parts[3], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float m22) &&
                float.TryParse(parts[4], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float m31) &&
                float.TryParse(parts[5], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float m32))
            {
                return new System.Numerics.Matrix3x2(m11, m12, m21, m22, m31, m32);
            }
        }

        var partsList = transformStr!.Split(')');
        var result = System.Numerics.Matrix3x2.Identity;
        foreach (var p in partsList)
        {
            var trimmed = p.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            int openParen = trimmed.IndexOf('(');
            if (openParen < 0)
                continue;

            string type = trimmed.Substring(0, openParen).Trim().ToLowerInvariant();
            string argsStr = trimmed.Substring(openParen + 1).Trim();
            var args = argsStr.Split(new[] { ' ', ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            if (type == "matrix" && args.Length >= 6)
            {
                if (float.TryParse(args[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float m11) &&
                    float.TryParse(args[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float m12) &&
                    float.TryParse(args[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float m21) &&
                    float.TryParse(args[3], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float m22) &&
                    float.TryParse(args[4], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float m31) &&
                    float.TryParse(args[5], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float m32))
                {
                    result *= new System.Numerics.Matrix3x2(m11, m12, m21, m22, m31, m32);
                }
            }
            else if (type == "translate" && args.Length >= 1)
            {
                if (float.TryParse(args[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float dx))
                {
                    float dy = 0;
                    if (args.Length >= 2)
                        float.TryParse(args[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out dy);
                    result *= System.Numerics.Matrix3x2.CreateTranslation(dx, dy);
                }
            }
            else if (type == "scale" && args.Length >= 1)
            {
                if (float.TryParse(args[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float sx))
                {
                    float sy = sx;
                    if (args.Length >= 2)
                        float.TryParse(args[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out sy);
                    result *= System.Numerics.Matrix3x2.CreateScale(sx, sy);
                }
            }
            else if (type == "rotate" && args.Length >= 1)
            {
                if (float.TryParse(args[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float angle))
                {
                    float rad = angle * (float)Math.PI / 180f;
                    result *= System.Numerics.Matrix3x2.CreateRotation(rad);
                }
            }
        }

        return result;
    }

    public static string FormatTransform(System.Numerics.Matrix3x2 matrix)
    {
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "matrix({0} {1} {2} {3} {4} {5})",
            matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32);
    }
}

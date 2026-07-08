using OdfKit.Styles;

namespace OdfKit;

/// <summary>
/// Represents an image value used by <c>{{Image:Name}}</c> template placeholders.
/// 表示供 <c>{{Image:Name}}</c> 模板圖片占位符使用的圖片值。
/// </summary>
/// <param name="Bytes">The image bytes. / 圖片位元組。</param>
/// <param name="FileName">The preferred package file name. / 偏好的封裝檔名。</param>
/// <param name="MediaType">The image media type. / 圖片 MIME 類型。</param>
/// <param name="Width">The optional display width. / 選用顯示寬度。</param>
/// <param name="Height">The optional display height. / 選用顯示高度。</param>
/// <param name="AltText">The optional alternative text. / 選用替代文字。</param>
public sealed record OdfTemplateImageValue(
    byte[] Bytes,
    string FileName,
    string MediaType = "image/png",
    OdfLength? Width = null,
    OdfLength? Height = null,
    string? AltText = null);

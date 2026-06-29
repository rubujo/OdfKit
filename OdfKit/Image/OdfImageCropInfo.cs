using System;
using System.Globalization;

namespace OdfKit.Image;

/// <summary>
/// Represents the crop bounds of an image (corresponding to the <c>rect(...)</c> syntax of the <c>fo:clip</c> attribute).
/// 表示影像的裁切邊界（對應 <c>fo:clip</c> 屬性的 <c>rect(...)</c> 語法）。
/// </summary>
/// <param name="top">The top crop bound. / 頂部裁切邊界。</param>
/// <param name="right">The right crop bound. / 右側裁切邊界。</param>
/// <param name="bottom">The bottom crop bound. / 底部裁切邊界。</param>
/// <param name="left">The left crop bound. / 左側裁切邊界。</param>
public sealed class OdfImageCropInfo(string top, string right, string bottom, string left)
{
    /// <summary>
    /// Gets the top crop bound.
    /// 取得頂部裁切邊界。
    /// </summary>
    public string Top { get; } = top;

    /// <summary>
    /// Gets the right crop bound.
    /// 取得右側裁切邊界。
    /// </summary>
    public string Right { get; } = right;

    /// <summary>
    /// Gets the bottom crop bound.
    /// 取得底部裁切邊界。
    /// </summary>
    public string Bottom { get; } = bottom;

    /// <summary>
    /// Gets the left crop bound.
    /// 取得左側裁切邊界。
    /// </summary>
    public string Left { get; } = left;

    /// <summary>
    /// Attempts to parse an <c>fo:clip</c> attribute value into an <see cref="OdfImageCropInfo"/>.
    /// 嘗試將 <c>fo:clip</c> 屬性值剖析為 <see cref="OdfImageCropInfo"/>。
    /// </summary>
    /// <param name="clip">The raw <c>fo:clip</c> attribute text, in the form <c>rect(top, right, bottom, left)</c>. / <c>fo:clip</c> 屬性原文，格式為 <c>rect(top, right, bottom, left)</c>。</param>
    /// <param name="crop">The crop bounds returned on successful parsing. / 剖析成功時傳回的裁切邊界。</param>
    /// <returns><see langword="true"/> if parsing succeeded. / 若剖析成功則為 <see langword="true"/>。</returns>
    public static bool TryParse(string? clip, out OdfImageCropInfo? crop)
    {
        crop = null;
        if (string.IsNullOrWhiteSpace(clip))
        {
            return false;
        }

        string text = clip!.Trim();
        if (!text.StartsWith("rect(", StringComparison.OrdinalIgnoreCase) || !text.EndsWith(")", StringComparison.Ordinal))
        {
            return false;
        }

        string inner = text.Substring(5, text.Length - 6);
        string[] parts = inner.Split(',');
        if (parts.Length != 4)
        {
            parts = inner.Split(' ');
        }

        if (parts.Length != 4)
        {
            return false;
        }

        crop = new OdfImageCropInfo(parts[0].Trim(), parts[1].Trim(), parts[2].Trim(), parts[3].Trim());
        return true;
    }

    /// <summary>
    /// Formats the current crop bounds as an <c>fo:clip</c> attribute value.
    /// 將目前的裁切邊界格式化為 <c>fo:clip</c> 屬性值。
    /// </summary>
    /// <returns>The attribute string in <c>rect(top, right, bottom, left)</c> format. / <c>rect(top, right, bottom, left)</c> 格式的屬性字串。</returns>
    public override string ToString() =>
        string.Format(CultureInfo.InvariantCulture, "rect({0}, {1}, {2}, {3})", Top, Right, Bottom, Left);
}

using System;
using System.Globalization;

namespace OdfKit.Image;

/// <summary>
/// 表示影像的裁切邊界（對應 <c>fo:clip</c> 屬性的 <c>rect(...)</c> 語法）。
/// </summary>
/// <param name="top">頂部裁切邊界</param>
/// <param name="right">右側裁切邊界</param>
/// <param name="bottom">底部裁切邊界</param>
/// <param name="left">左側裁切邊界</param>
public sealed class OdfImageCropInfo(string top, string right, string bottom, string left)
{
    /// <summary>
    /// 取得頂部裁切邊界。
    /// </summary>
    public string Top { get; } = top;

    /// <summary>
    /// 取得右側裁切邊界。
    /// </summary>
    public string Right { get; } = right;

    /// <summary>
    /// 取得底部裁切邊界。
    /// </summary>
    public string Bottom { get; } = bottom;

    /// <summary>
    /// 取得左側裁切邊界。
    /// </summary>
    public string Left { get; } = left;

    /// <summary>
    /// 嘗試將 <c>fo:clip</c> 屬性值剖析為 <see cref="OdfImageCropInfo"/>。
    /// </summary>
    /// <param name="clip"><c>fo:clip</c> 屬性原文，格式為 <c>rect(top, right, bottom, left)</c></param>
    /// <param name="crop">剖析成功時傳回的裁切邊界</param>
    /// <returns>若剖析成功則為 <see langword="true"/></returns>
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
    /// 將目前的裁切邊界格式化為 <c>fo:clip</c> 屬性值。
    /// </summary>
    /// <returns><c>rect(top, right, bottom, left)</c> 格式的屬性字串</returns>
    public override string ToString() =>
        string.Format(CultureInfo.InvariantCulture, "rect({0}, {1}, {2}, {3})", Top, Right, Bottom, Left);
}

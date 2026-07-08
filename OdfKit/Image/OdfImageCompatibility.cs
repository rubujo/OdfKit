using System;
using System.IO;

namespace OdfKit.Image;

/// <summary>
/// Provides practical image compatibility helpers for ODF documents.
/// 提供 ODF 文件的實務影像相容性輔助工具。
/// </summary>
public static class OdfImageCompatibility
{
    /// <summary>
    /// Gets whether the preferred image name uses a portable bitmap or vector format.
    /// 取得偏好影像檔名是否使用可攜點陣或向量格式。
    /// </summary>
    /// <param name="preferredName">The preferred image file name. / 偏好影像檔名。</param>
    /// <returns><see langword="true"/> if the name ends with PNG, JPG, JPEG or SVG. / 若檔名以 PNG、JPG、JPEG 或 SVG 結尾則為 <see langword="true"/>。</returns>
    public static bool IsPortableImageName(string? preferredName)
    {
        string extension = Path.GetExtension(preferredName ?? string.Empty);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".svg", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normalizes a preferred image file name to a portable default when no extension is present.
    /// 當偏好影像檔名沒有副檔名時，將其正規化為可攜預設檔名。
    /// </summary>
    /// <param name="preferredName">The preferred image file name. / 偏好影像檔名。</param>
    /// <returns>The normalized image file name. / 正規化後的影像檔名。</returns>
    public static string NormalizeRequest(string? preferredName)
    {
        if (string.IsNullOrWhiteSpace(preferredName))
        {
            return "image.png";
        }

        string name = preferredName!;
        return string.IsNullOrEmpty(Path.GetExtension(name))
            ? name + ".png"
            : name;
    }

    /// <summary>
    /// Builds a practical compatibility recommendation for an image request.
    /// 為影像要求建立實務相容性建議。
    /// </summary>
    /// <param name="preferredName">The preferred image file name. / 偏好影像檔名。</param>
    /// <param name="mediaType">The detected media type. / 偵測到的媒體類型。</param>
    /// <returns>The normalization recommendation. / 正規化建議。</returns>
    public static OdfImageNormalizationRequest NormalizeRequest(string? preferredName, string? mediaType)
    {
        string normalizedName = NormalizeRequest(preferredName);
        bool portable = IsPortableImageName(normalizedName) && IsPortableImageMediaType(mediaType);
        if (portable)
        {
            return new OdfImageNormalizationRequest(normalizedName, mediaType, true, null, null);
        }

        return new OdfImageNormalizationRequest(
            normalizedName,
            mediaType,
            false,
            "image/png",
            Path.ChangeExtension(normalizedName, ".png") ?? normalizedName);
    }

    private static bool IsPortableImageMediaType(string? mediaType) =>
        string.Equals(mediaType, "image/png", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mediaType, "image/jpeg", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mediaType, "image/jpg", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mediaType, "image/svg+xml", StringComparison.OrdinalIgnoreCase);
}

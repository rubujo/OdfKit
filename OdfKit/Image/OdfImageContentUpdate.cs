using System;

namespace OdfKit.Image;

/// <summary>
/// Describes replacement image content for a named frame.
/// 描述具名框架的替換影像內容。
/// </summary>
public sealed class OdfImageContentUpdate
{
    /// <summary>
    /// Initializes replacement image content for a named frame.
    /// 初始化具名框架的替換影像內容。
    /// </summary>
    /// <param name="name">The frame name. / 框架名稱。</param>
    /// <param name="imageBytes">The replacement image bytes. / 替換影像位元組。</param>
    public OdfImageContentUpdate(string name, byte[] imageBytes)
        : this(name, imageBytes, null)
    {
    }

    /// <summary>
    /// Initializes replacement image content for a named frame.
    /// 初始化具名框架的替換影像內容。
    /// </summary>
    /// <param name="name">The frame name. / 框架名稱。</param>
    /// <param name="imageBytes">The replacement image bytes. / 替換影像位元組。</param>
    /// <param name="preferredName">The preferred package file name. / 偏好的封裝檔名。</param>
    public OdfImageContentUpdate(string name, byte[] imageBytes, string? preferredName)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        ImageBytes = imageBytes ?? throw new ArgumentNullException(nameof(imageBytes));
        PreferredName = preferredName;
    }

    /// <summary>
    /// Gets the frame name.
    /// 取得框架名稱。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the replacement image bytes.
    /// 取得替換影像位元組。
    /// </summary>
    public byte[] ImageBytes { get; }

    /// <summary>
    /// Gets the preferred package file name.
    /// 取得偏好的封裝檔名。
    /// </summary>
    public string? PreferredName { get; }
}

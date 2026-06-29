using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Presentation;

/// <summary>
/// Represents a media object in a presentation slide.
/// 表示簡報投影片中的媒體物件。
/// </summary>
/// <param name="packagePath">The media path inside the package. / 媒體在封裝包內的路徑。</param>
/// <param name="mimeType">The media MIME type. / 媒體 MIME 類型。</param>
public sealed class OdfMediaObject(string packagePath, string mimeType)
{
    /// <summary>
    /// Gets the media path inside the package.
    /// 取得媒體在封裝包內的路徑。
    /// </summary>
    public string PackagePath { get; } = packagePath;

    /// <summary>
    /// Gets the media MIME type.
    /// 取得媒體 MIME 類型。
    /// </summary>
    public string MimeType { get; } = mimeType;
}

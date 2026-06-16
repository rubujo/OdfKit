using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Presentation;

/// <summary>
/// 表示簡報投影片中的媒體物件。
/// </summary>
/// <param name="packagePath">媒體在封裝包內的路徑。</param>
/// <param name="mimeType">媒體 MIME 類型。</param>
public sealed class OdfMediaObject(string packagePath, string mimeType)
{
    /// <summary>
    /// 取得媒體在封裝包內的路徑。
    /// </summary>
    public string PackagePath { get; } = packagePath;

    /// <summary>
    /// 取得媒體 MIME 類型。
    /// </summary>
    public string MimeType { get; } = mimeType;
}

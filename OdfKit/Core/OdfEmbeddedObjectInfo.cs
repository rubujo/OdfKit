using System.Collections.Generic;
using System.IO;
using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// Describes an embedded ODF subdocument in a package.
/// 描述封裝中的內嵌 ODF 子文件。
/// </summary>
public sealed class OdfEmbeddedObjectInfo
{
    private readonly OdfPackage _package;

    internal OdfEmbeddedObjectInfo(
        OdfPackage package,
        string path,
        string mediaType,
        IReadOnlyList<string> entries)
    {
        _package = package;
        Path = path;
        MediaType = mediaType;
        Entries = entries;
    }

    /// <summary>
    /// Gets the package-relative directory path.
    /// 取得相對於封裝的目錄路徑。
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the embedded document MIME media type.
    /// 取得內嵌文件的 MIME 媒體類型。
    /// </summary>
    public string MediaType { get; }

    /// <summary>
    /// Gets the detected embedded document kind.
    /// 取得偵測到的內嵌文件種類。
    /// </summary>
    public OdfDocumentKind DocumentKind => OdfDocumentKindDetector.FromMimeType(MediaType);

    /// <summary>
    /// Gets the package entries that belong to the embedded document.
    /// 取得屬於此內嵌文件的封裝項目。
    /// </summary>
    public IReadOnlyList<string> Entries { get; }

    /// <summary>
    /// Opens the embedded document's primary content XML stream.
    /// 開啟內嵌文件的主要內容 XML 資料流。
    /// </summary>
    /// <returns>A readable stream for the embedded content XML. / 可讀取內嵌內容 XML 的資料流。</returns>
    public Stream OpenContent() => _package.GetEntryStream(Path + "/content.xml");

    /// <summary>
    /// Opens an entry relative to the embedded document directory.
    /// 開啟相對於內嵌文件目錄的項目。
    /// </summary>
    /// <param name="relativePath">The relative entry path. / 相對項目路徑。</param>
    /// <returns>A readable stream for the entry. / 可讀取該項目的資料流。</returns>
    public Stream OpenEntry(string relativePath) => _package.GetEntryStream(Path + "/" + relativePath);
}

using System.Globalization;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 提供以資料流方式逐段落寫入 ODT 文字文件的功能，適用於大型文件生成。
/// </summary>
public sealed class OdtStreamWriter : IDisposable, IAsyncDisposable
{
    private const string MimeType = "application/vnd.oasis.opendocument.text";
    private const string PageBreakStyleName = "OdtStreamPageBreak";

    private readonly Stream? _ownedStream;
    private readonly ZipArchive _zip;
    private readonly Stream _contentEntryStream;
    private readonly XmlWriter _writer;
    private readonly OdfVersion _version;
    private bool _isListStarted;
    private bool _disposed;

    /// <summary>
    /// 初始化 <see cref="OdtStreamWriter"/> 類別的新執行個體。
    /// </summary>
    /// <param name="outputStream">用來輸出 ODT 文件的目標資料流</param>
    /// <param name="version">要寫入的 ODF 規格版本</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="outputStream"/> 為 null 時擲出</exception>
    public OdtStreamWriter(Stream outputStream, OdfVersion version = OdfVersion.Odf14)
        : this(outputStream, version, ownsStream: false)
    {
    }

    private OdtStreamWriter(Stream outputStream, OdfVersion version, bool ownsStream)
    {
        if (outputStream is null)
        {
            throw new ArgumentNullException(nameof(outputStream));
        }

        _ownedStream = ownsStream ? outputStream : null;
        _version = version;

        // 若底層資料流支援尋覽 (CanSeek)，則直接使用以避免 ZipArchive 強制寫入 Data Descriptor
        // 這能確保 mimetype 檔案不含 Data Descriptor，符合 ODF 封裝規格以防止 LibreOffice 報錯毀損
        Stream targetStream = outputStream.CanSeek ? outputStream : new NonSeekableStreamWrapper(outputStream);
        _zip = new ZipArchive(targetStream, ZipArchiveMode.Create, leaveOpen: true);

        WriteMimeType();
        WriteManifest();
        WriteMeta();
        WriteStyles();

        var contentEntry = _zip.CreateEntry("content.xml", CompressionLevel.Fastest);
        _contentEntryStream = contentEntry.Open();
        _writer = XmlWriter.Create(_contentEntryStream, CreateXmlWriterSettings());

        WriteContentStart();
    }

    /// <summary>
    /// 從檔案路徑初始化 <see cref="OdtStreamWriter"/> 類別的新執行個體。
    /// </summary>
    /// <param name="path">要建立或覆寫的 ODT 檔案路徑</param>
    /// <param name="version">要寫入的 ODF 規格版本</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="path"/> 為 null 時擲出</exception>
    public OdtStreamWriter(string path, OdfVersion version = OdfVersion.Odf14)
        : this(CreateFileStream(path), version, ownsStream: true)
    {
    }

    /// <summary>
    /// 加入一個段落。
    /// </summary>
    /// <param name="text">段落文字</param>
    /// <param name="styleName">段落樣式名稱</param>
    public void AddParagraph(string text, string? styleName = null)
    {
        EnsureNotDisposed();
        WriteTextElement("p", text.AsSpan(), styleName, headingLevel: null);
    }

    /// <summary>
    /// 加入一個段落。
    /// </summary>
    /// <param name="text">段落文字</param>
    /// <param name="styleName">段落樣式名稱</param>
    public void AddParagraph(ReadOnlySpan<char> text, string? styleName = null)
    {
        EnsureNotDisposed();
        WriteTextElement("p", text, styleName, headingLevel: null);
    }

    /// <summary>
    /// 加入一個段落。
    /// </summary>
    /// <param name="text">段落文字</param>
    /// <param name="styleName">段落樣式名稱</param>
    public void AddParagraph(ReadOnlyMemory<char> text, string? styleName = null) =>
        AddParagraph(text.Span, styleName);

    /// <summary>
    /// 加入標題段落。
    /// </summary>
    /// <param name="text">標題文字</param>
    /// <param name="level">標題層級，範圍為 1 到 6</param>
    /// <exception cref="ArgumentOutOfRangeException">當 <paramref name="level"/> 不在 1 到 6 之間時擲出</exception>
    public void AddHeading(string text, int level = 1)
    {
        EnsureNotDisposed();
        if (level is < 1 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(level), OdfLocalizer.GetMessage("Err_OdtStreamWriter_TitleLevelBetween1"));
        }

        WriteTextElement("h", text.AsSpan(), styleName: null, headingLevel: level);
    }

    /// <summary>
    /// 加入標題段落。
    /// </summary>
    /// <param name="text">標題文字</param>
    /// <param name="level">標題層級，範圍為 1 到 6</param>
    /// <exception cref="ArgumentOutOfRangeException">當 <paramref name="level"/> 不在 1 到 6 之間時擲出</exception>
    public void AddHeading(ReadOnlySpan<char> text, int level = 1)
    {
        EnsureNotDisposed();
        if (level is < 1 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(level), OdfLocalizer.GetMessage("Err_OdtStreamWriter_TitleLevelBetween1"));
        }

        WriteTextElement("h", text, styleName: null, headingLevel: level);
    }

    /// <summary>
    /// 加入標題段落。
    /// </summary>
    /// <param name="text">標題文字</param>
    /// <param name="level">標題層級，範圍為 1 到 6</param>
    /// <exception cref="ArgumentOutOfRangeException">當 <paramref name="level"/> 不在 1 到 6 之間時擲出</exception>
    public void AddHeading(ReadOnlyMemory<char> text, int level = 1) =>
        AddHeading(text.Span, level);

    /// <summary>
    /// 開始清單。
    /// </summary>
    /// <param name="styleName">清單樣式名稱</param>
    /// <exception cref="InvalidOperationException">當清單已經開始時擲出</exception>
    public void BeginList(string? styleName = null)
    {
        EnsureNotDisposed();
        if (_isListStarted)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdtStreamWriter_ListStarted"));
        }

        _writer.WriteStartElement("text", "list", OdfNamespaces.Text);
        if (!string.IsNullOrWhiteSpace(styleName))
        {
            _writer.WriteAttributeString("text", "style-name", OdfNamespaces.Text, styleName);
        }

        _isListStarted = true;
    }

    /// <summary>
    /// 加入清單專案。
    /// </summary>
    /// <param name="text">清單專案文字</param>
    /// <exception cref="InvalidOperationException">當尚未呼叫 <see cref="BeginList(string?)"/> 時擲出</exception>
    public void AddListItem(string text)
    {
        EnsureNotDisposed();
        if (!_isListStarted)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdtStreamWriter_AddlistitemCalledBetweenBeginlist"));
        }

        _writer.WriteStartElement("text", "list-item", OdfNamespaces.Text);
        WriteTextElement("p", text.AsSpan(), styleName: null, headingLevel: null);
        _writer.WriteEndElement();
    }

    /// <summary>
    /// 加入清單專案。
    /// </summary>
    /// <param name="text">清單專案文字</param>
    /// <exception cref="InvalidOperationException">當尚未呼叫 <see cref="BeginList(string?)"/> 時擲出</exception>
    public void AddListItem(ReadOnlySpan<char> text)
    {
        EnsureNotDisposed();
        if (!_isListStarted)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdtStreamWriter_AddlistitemCalledBetweenBeginlist"));
        }

        _writer.WriteStartElement("text", "list-item", OdfNamespaces.Text);
        WriteTextElement("p", text, styleName: null, headingLevel: null);
        _writer.WriteEndElement();
    }

    /// <summary>
    /// 加入清單專案。
    /// </summary>
    /// <param name="text">清單專案文字</param>
    /// <exception cref="InvalidOperationException">當尚未呼叫 <see cref="BeginList(string?)"/> 時擲出</exception>
    public void AddListItem(ReadOnlyMemory<char> text) => AddListItem(text.Span);

    /// <summary>
    /// 結束目前清單。
    /// </summary>
    public void EndList()
    {
        EnsureNotDisposed();
        if (!_isListStarted)
        {
            return;
        }

        _writer.WriteEndElement();
        _isListStarted = false;
    }

    /// <summary>
    /// 加入強制分頁。
    /// </summary>
    public void AddPageBreak()
    {
        EnsureNotDisposed();
        AddParagraph(string.Empty, PageBreakStyleName);
    }

    /// <summary>
    /// 將既有 DOM 子樹直接寫入目前文字文件位置。
    /// </summary>
    /// <param name="node">要寫入的 DOM 節點</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="node"/> 為 null 時擲出</exception>
    public void WriteNode(OdfNode node)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        EnsureNotDisposed();
        Dictionary<string, string> namespaces = new(StringComparer.Ordinal)
        {
            [OdfNamespaces.Office] = "office",
            [OdfNamespaces.Text] = "text",
            [OdfNamespaces.Style] = "style",
            [OdfNamespaces.Fo] = "fo",
            [OdfNamespaces.Draw] = "draw",
            [OdfNamespaces.XLink] = "xlink"
        };

        if (!string.IsNullOrEmpty(node.NamespaceUri) && !namespaces.ContainsKey(node.NamespaceUri))
        {
            namespaces[node.NamespaceUri] = node.Prefix ?? string.Empty;
        }

        int openElementsCount = 0;
        OdfXmlWriter.WriteNode(node, _writer, namespaces, ref openElementsCount, isRoot: false, depth: 1);
    }

    /// <summary>
    /// 釋放資源並最終化文件 ZIP 結構。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_isListStarted)
        {
            _writer.WriteEndElement();
            _isListStarted = false;
        }

        _writer.WriteEndElement();
        _writer.WriteEndElement();
        _writer.WriteEndElement();
        _writer.WriteEndDocument();
        _writer.Dispose();
        _contentEntryStream.Dispose();
        _zip.Dispose();
        _ownedStream?.Dispose();
    }

    /// <summary>
    /// 非同步釋放資源並最終化文件 ZIP 結構。
    /// </summary>
    /// <returns>代表非同步處置作業的 <see cref="ValueTask"/></returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_isListStarted)
        {
            _writer.WriteEndElement();
            _isListStarted = false;
        }

        _writer.WriteEndElement();
        _writer.WriteEndElement();
        _writer.WriteEndElement();
        _writer.WriteEndDocument();
        _writer.Dispose();
        _contentEntryStream.Dispose();
        _zip.Dispose();

        if (_ownedStream is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            _ownedStream?.Dispose();
        }
    }

    private static FileStream CreateFileStream(string path)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        return new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
    }

    private static XmlWriterSettings CreateXmlWriterSettings() => new()
    {
        Encoding = new UTF8Encoding(false),
        Indent = false
    };

    private void WriteMimeType()
    {
        var entry = _zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
        using var stream = entry.Open();
        byte[] bytes = Encoding.UTF8.GetBytes(MimeType);
        stream.Write(bytes, 0, bytes.Length);
    }

    private void WriteManifest()
    {
        var entry = _zip.CreateEntry("META-INF/manifest.xml", CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, CreateXmlWriterSettings());

        writer.WriteStartDocument();
        writer.WriteStartElement("manifest", "manifest", OdfNamespaces.Manifest);
        writer.WriteAttributeString("manifest", "version", OdfNamespaces.Manifest, FormatVersion(_version));
        WriteManifestEntry(writer, "/", MimeType);
        WriteManifestEntry(writer, "content.xml", "text/xml");
        WriteManifestEntry(writer, "styles.xml", "text/xml");
        WriteManifestEntry(writer, "meta.xml", "text/xml");
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteManifestEntry(XmlWriter writer, string path, string mediaType)
    {
        writer.WriteStartElement("manifest", "file-entry", OdfNamespaces.Manifest);
        writer.WriteAttributeString("manifest", "full-path", OdfNamespaces.Manifest, path);
        writer.WriteAttributeString("manifest", "media-type", OdfNamespaces.Manifest, mediaType);
        writer.WriteEndElement();
    }

    private void WriteMeta()
    {
        var entry = _zip.CreateEntry("meta.xml", CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, CreateXmlWriterSettings());

        writer.WriteStartDocument();
        writer.WriteStartElement("office", "document-meta", OdfNamespaces.Office);
        writer.WriteAttributeString("office", "version", OdfNamespaces.Office, FormatVersion(_version));
        writer.WriteAttributeString("xmlns", "office", null, OdfNamespaces.Office);
        writer.WriteAttributeString("xmlns", "meta", null, OdfNamespaces.Meta);
        writer.WriteStartElement("office", "meta", OdfNamespaces.Office);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private void WriteStyles()
    {
        var entry = _zip.CreateEntry("styles.xml", CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, CreateXmlWriterSettings());

        writer.WriteStartDocument();
        writer.WriteStartElement("office", "document-styles", OdfNamespaces.Office);
        writer.WriteAttributeString("office", "version", OdfNamespaces.Office, FormatVersion(_version));
        writer.WriteAttributeString("xmlns", "office", null, OdfNamespaces.Office);
        writer.WriteStartElement("office", "styles", OdfNamespaces.Office);
        writer.WriteEndElement();
        writer.WriteStartElement("office", "automatic-styles", OdfNamespaces.Office);
        writer.WriteEndElement();
        writer.WriteStartElement("office", "master-styles", OdfNamespaces.Office);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private void WriteContentStart()
    {
        _writer.WriteStartDocument();
        _writer.WriteStartElement("office", "document-content", OdfNamespaces.Office);
        _writer.WriteAttributeString("office", "version", OdfNamespaces.Office, FormatVersion(_version));
        _writer.WriteAttributeString("xmlns", "office", null, OdfNamespaces.Office);
        _writer.WriteAttributeString("xmlns", "style", null, OdfNamespaces.Style);
        _writer.WriteAttributeString("xmlns", "text", null, OdfNamespaces.Text);
        _writer.WriteAttributeString("xmlns", "fo", null, OdfNamespaces.Fo);

        _writer.WriteStartElement("office", "automatic-styles", OdfNamespaces.Office);
        _writer.WriteStartElement("style", "style", OdfNamespaces.Style);
        _writer.WriteAttributeString("style", "name", OdfNamespaces.Style, PageBreakStyleName);
        _writer.WriteAttributeString("style", "family", OdfNamespaces.Style, "paragraph");
        _writer.WriteStartElement("style", "paragraph-properties", OdfNamespaces.Style);
        _writer.WriteAttributeString("fo", "break-before", OdfNamespaces.Fo, "page");
        _writer.WriteEndElement();
        _writer.WriteEndElement();
        _writer.WriteEndElement();

        _writer.WriteStartElement("office", "body", OdfNamespaces.Office);
        _writer.WriteStartElement("office", "text", OdfNamespaces.Office);
    }

    private void WriteTextElement(string localName, ReadOnlySpan<char> text, string? styleName, int? headingLevel)
    {
        _writer.WriteStartElement("text", localName, OdfNamespaces.Text);
        if (!string.IsNullOrWhiteSpace(styleName))
        {
            _writer.WriteAttributeString("text", "style-name", OdfNamespaces.Text, styleName);
        }

        if (headingLevel.HasValue)
        {
            _writer.WriteAttributeString("text", "outline-level", OdfNamespaces.Text, headingLevel.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (!text.IsEmpty)
        {
            _writer.WriteString(ToStringValue(text));
        }

        _writer.WriteEndElement();
    }

    private static string ToStringValue(ReadOnlySpan<char> value)
    {
#if NETSTANDARD2_0
        return new string(value.ToArray());
#else
        return new string(value);
#endif
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(OdtStreamWriter));
        }
    }

    private static string FormatVersion(OdfVersion version) => version switch
    {
        OdfVersion.Odf10 => "1.0",
        OdfVersion.Odf11 => "1.1",
        OdfVersion.Odf12 => "1.2",
        OdfVersion.Odf13 => "1.3",
        OdfVersion.Odf14 => "1.4",
        _ => "1.4"
    };
}

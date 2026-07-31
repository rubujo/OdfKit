using System.Globalization;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Core;

using OdfKit.Compliance;
namespace OdfKit.Text;

/// <summary>
/// Reads an ODT text document paragraph by paragraph in a low-memory streaming fashion, suitable for text extraction from large documents.
/// 以低記憶體流式方式逐段落讀取 ODT 文字文件，適用於大型文件文字擷取。
/// </summary>
public sealed class OdtStreamReader : IDisposable
{
    private readonly ZipArchive _zip;
    private readonly OdtStreamReaderOptions _options;
    private Stream? _contentStream;
    private XmlReader? _reader;
    private bool _started;
    private int _nodeCount;
    private int _readInProgress;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdtStreamReader"/> class from a stream.
    /// 從資料流初始化 <see cref="OdtStreamReader"/> 類別的新執行個體。
    /// </summary>
    /// <param name="stream">The ODT file stream. / ODT 檔案資料流。</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null. / 當 <paramref name="stream"/> 為 null 時擲出。</exception>
    public OdtStreamReader(Stream stream) : this(stream, new OdtStreamReaderOptions())
    {
    }

    /// <summary>
    /// Initializes a reader from a stream with explicit resource limits.
    /// 使用明確資源限制，從資料流初始化讀取器。
    /// </summary>
    /// <param name="stream">The ODT file stream. / ODT 檔案資料流。</param>
    /// <param name="options">The reader options. / 讀取器選項。</param>
    public OdtStreamReader(Stream stream, OdtStreamReaderOptions options)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(stream, nameof(stream));

        _options = ValidateOptions(options);
        _zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: _options.LeaveOpen);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OdtStreamReader"/> class from a file path.
    /// 從檔案路徑初始化 <see cref="OdtStreamReader"/> 類別的新執行個體。
    /// </summary>
    /// <param name="path">The ODT file path. / ODT 檔案路徑。</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null. / 當 <paramref name="path"/> 為 null 時擲出。</exception>
    public OdtStreamReader(string path) : this(path, new OdtStreamReaderOptions())
    {
    }

    /// <summary>
    /// Initializes a reader from a path with explicit resource limits.
    /// 使用明確資源限制，從路徑初始化讀取器。
    /// </summary>
    /// <param name="path">The ODT file path. / ODT 檔案路徑。</param>
    /// <param name="options">The reader options. / 讀取器選項。</param>
    public OdtStreamReader(string path, OdtStreamReaderOptions options)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(path, nameof(path));

        _options = ValidateOptions(options);
        _zip = ZipFile.OpenRead(path);
    }

    /// <summary>
    /// Gets the type of the current element.
    /// 取得目前元素的類型。
    /// </summary>
    public OdtNodeType NodeType { get; private set; } = OdtNodeType.Other;

    /// <summary>
    /// Gets the plain text content of the current element, including embedded <c>text:span</c> text.
    /// 取得目前元素的純文字內容，包含內嵌 <c>text:span</c> 文字。
    /// </summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the paragraph style name of the current element.
    /// 取得目前元素的段落樣式名稱。
    /// </summary>
    public string? StyleName { get; private set; }

    /// <summary>
    /// Gets the heading level, valid only when <see cref="NodeType"/> is <see cref="OdtNodeType.Heading"/>.
    /// 取得標題層級，僅在 <see cref="NodeType"/> 為 <see cref="OdtNodeType.Heading"/> 時有效。
    /// </summary>
    public int HeadingLevel { get; private set; }

    /// <summary>
    /// Reads the next text element; returns false to indicate the end of the document.
    /// 讀取下一個文字元素；回傳 false 代表文件結束。
    /// </summary>
    /// <returns><see langword="true"/> if an element was successfully read; otherwise <see langword="false"/>. / 若成功讀取元素則為 true，否則為 false。</returns>
    public bool Read()
    {
        EnterRead();
        try
        {
            return ReadCore();
        }
        finally
        {
            Volatile.Write(ref _readInProgress, 0);
        }
    }

    private bool ReadCore()
    {
        if (!_started)
        {
            OpenContentReader();
            _started = true;
        }

        if (_reader is null)
        {
            return false;
        }

        while (_reader.Read())
        {
            if (_reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (_reader.NamespaceURI == OdfNamespaces.Text)
            {
                if (_reader.LocalName == "p")
                {
                    CaptureCurrentElement(OdtNodeType.Paragraph, headingLevel: 0);
                    return true;
                }

                if (_reader.LocalName == "h")
                {
                    int headingLevel = ParseHeadingLevel(_reader.GetAttribute("outline-level", OdfNamespaces.Text));
                    CaptureCurrentElement(OdtNodeType.Heading, headingLevel);
                    return true;
                }

                if (_reader.LocalName == "list-item")
                {
                    CaptureCurrentElement(OdtNodeType.ListItem, headingLevel: 0);
                    return true;
                }
            }

            if (_reader.NamespaceURI == OdfNamespaces.Table && _reader.LocalName == "table-cell")
            {
                CaptureCurrentElement(OdtNodeType.TableCell, headingLevel: 0);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Asynchronously reads the next text element and observes cancellation during XML traversal.
    /// 非同步讀取下一個文字元素，並在 XML 走訪期間回應取消要求。
    /// </summary>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns><see langword="true"/> when an element is available; otherwise, <see langword="false"/>. / 有可用元素時為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        EnterRead();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_started)
            {
                OpenContentReader();
                _started = true;
            }
            if (_reader is null)
                return false;

            while (await _reader.ReadAsync().ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_reader.NodeType != XmlNodeType.Element)
                    continue;
                OdtNodeType? nodeType = GetCurrentNodeType(_reader);
                if (nodeType is null)
                    continue;
                int level = nodeType == OdtNodeType.Heading
                    ? ParseHeadingLevel(_reader.GetAttribute("outline-level", OdfNamespaces.Text)) : 0;
                await CaptureCurrentElementAsync(nodeType.Value, level, cancellationToken).ConfigureAwait(false);
                return true;
            }
            return false;
        }
        finally
        {
            Volatile.Write(ref _readInProgress, 0);
        }
    }

    /// <summary>
    /// Releases the reader and underlying ZIP resources.
    /// 釋放讀取器與底層 ZIP 資源。
    /// </summary>
    public void Dispose()
    {
        // 前段任一 Dispose 拋出都不得略過 _zip.Dispose()，否則 ZipArchive 持有的
        // 底層串流會一併洩漏。讀取模式不寫中央目錄，因此僅是資源問題而非輸出損毀。
        try
        {
            _reader?.Dispose();
            _contentStream?.Dispose();
        }
        finally
        {
            _zip.Dispose();
        }
    }

    private void OpenContentReader()
    {
        var entry = _zip.GetEntry("content.xml")
            ?? throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdtStreamReader_OdtNotFound"));
        _contentStream = entry.Open();
        _reader = XmlReader.Create(_contentStream, CreateXmlReaderSettings());
    }

    private void CaptureCurrentElement(OdtNodeType nodeType, int headingLevel)
    {
        if (++_nodeCount > _options.MaxNodes)
            ThrowResourceLimit(_nodeCount, _options.MaxNodes);
        NodeType = nodeType;
        HeadingLevel = headingLevel;
        string styleNamespace = nodeType == OdtNodeType.TableCell ? OdfNamespaces.Table : OdfNamespaces.Text;
        StyleName = _reader!.GetAttribute("style-name", styleNamespace);
        Text = ReadCurrentElementText(_reader);
        if (Text.Length > _options.MaxNodeTextCharacters)
            ThrowResourceLimit(Text.Length, _options.MaxNodeTextCharacters);
    }

    private async Task CaptureCurrentElementAsync(OdtNodeType nodeType, int headingLevel, CancellationToken cancellationToken)
    {
        if (++_nodeCount > _options.MaxNodes)
            ThrowResourceLimit(_nodeCount, _options.MaxNodes);
        NodeType = nodeType;
        HeadingLevel = headingLevel;
        string styleNamespace = nodeType == OdtNodeType.TableCell ? OdfNamespaces.Table : OdfNamespaces.Text;
        StyleName = _reader!.GetAttribute("style-name", styleNamespace);
        Text = await ReadCurrentElementTextAsync(_reader, cancellationToken).ConfigureAwait(false);
        if (Text.Length > _options.MaxNodeTextCharacters)
            ThrowResourceLimit(Text.Length, _options.MaxNodeTextCharacters);
    }

    private static async Task<string> ReadCurrentElementTextAsync(XmlReader reader, CancellationToken cancellationToken)
    {
        if (reader.IsEmptyElement)
            return string.Empty;
        var builder = new StringBuilder();
        using var subtree = reader.ReadSubtree();
        await subtree.ReadAsync().ConfigureAwait(false);
        while (await subtree.ReadAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (subtree.NodeType is XmlNodeType.Text or XmlNodeType.CDATA)
                builder.Append(subtree.Value);
            else if (subtree.NodeType == XmlNodeType.Element && subtree.NamespaceURI == OdfNamespaces.Text)
                AppendTextControl(builder, subtree);
        }
        return builder.ToString();
    }

    private static OdtNodeType? GetCurrentNodeType(XmlReader reader)
    {
        if (reader.NamespaceURI == OdfNamespaces.Text)
        {
            if (reader.LocalName == "p")
                return OdtNodeType.Paragraph;
            if (reader.LocalName == "h")
                return OdtNodeType.Heading;
            if (reader.LocalName == "list-item")
                return OdtNodeType.ListItem;
        }
        return reader.NamespaceURI == OdfNamespaces.Table && reader.LocalName == "table-cell"
            ? OdtNodeType.TableCell : null;
    }

    private void EnterRead()
    {
        if (Interlocked.Exchange(ref _readInProgress, 1) != 0)
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));
    }

    private static void ThrowResourceLimit(int value, int limit) =>
        throw new InvalidDataException(OdfLocalizer.GetMessage("Err_StreamReader_ResourceLimitExceeded",
            value.ToString(CultureInfo.InvariantCulture), limit.ToString(CultureInfo.InvariantCulture)));

    private static string ReadCurrentElementText(XmlReader reader)
    {
        if (reader.IsEmptyElement)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        using var subtree = reader.ReadSubtree();
        subtree.Read();
        while (subtree.Read())
        {
            if (subtree.NodeType is XmlNodeType.Text or XmlNodeType.CDATA)
            {
                builder.Append(subtree.Value);
            }
            else if (subtree.NodeType == XmlNodeType.Element && subtree.NamespaceURI == OdfNamespaces.Text)
            {
                AppendTextControl(builder, subtree);
            }
        }

        return builder.ToString();
    }

    private static void AppendTextControl(StringBuilder builder, XmlReader reader)
    {
        if (reader.LocalName == "s")
        {
            int count = ParsePositiveInt(reader.GetAttribute("c", OdfNamespaces.Text), defaultValue: 1);
            builder.Append(' ', count);
        }
        else if (reader.LocalName == "tab")
        {
            builder.Append('\t');
        }
        else if (reader.LocalName == "line-break")
        {
            builder.Append('\n');
        }
    }

    private static int ParseHeadingLevel(string? value)
    {
        int level = ParsePositiveInt(value, defaultValue: 1);
        return Math.Min(level, 6);
    }

    private static int ParsePositiveInt(string? value, int defaultValue)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0
            ? parsed
            : defaultValue;
    }

    private XmlReaderSettings CreateXmlReaderSettings() => new()
    {
        NameTable = OdfXmlNameTable.Create(),
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = _options.MaxXmlCharactersInDocument,
        Async = true
    };

    private static OdtStreamReaderOptions ValidateOptions(OdtStreamReaderOptions options)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(options, nameof(options));
        return options;
    }
}

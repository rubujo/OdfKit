using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System;
using System.Data.Common;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
using Sylvan.Data.Csv;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Writes ODS spreadsheets with strict sequential sheet calls for high-performance, low-memory output.
/// 提供以資料流方式寫入 ODS 試算表文件的功能；使用 <see cref="WriteStartSheet(string)"/>
/// 與 <see cref="WriteEndSheet"/> 的嚴格順序模式時，可支援高效能、低記憶體耗用的寫入作業。
/// </summary>
public partial class OdsStreamWriter : IDisposable, IAsyncDisposable
{
    private static readonly byte[] MimeTypeBytes = Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.spreadsheet");

    #region Stream Writing
    private readonly Stream _outputStream;
    private readonly ZipArchive _zip;
    private readonly Stream _contentEntryStream;
    private readonly XmlWriter _writer;
    private readonly OdfRawXmlWriter _rawWriter;
    private readonly List<SheetBuffer> _sheetBuffers = [];
    private readonly Dictionary<string, SheetBuffer> _sheetBuffersByName = new(StringComparer.Ordinal);
    private SheetBuffer? _activeSheetBuffer;
    private bool _isRowStarted;
    private bool _isSheetStarted;
    private bool _disposed;
    private readonly List<(string styleName, OdfLength width)> _columnStyles = [];
    private readonly List<(string styleName, OdfLength? height, bool useOptimalHeight)> _rowStyles = [];
    private int _autoColumnStyleIndex;
    private int _autoRowStyleIndex;
    private OdfVersion _version = OdfVersionInfo.DefaultVersion;

    internal int BufferedSheetCountForTests => _sheetBuffers.Count;

    internal bool UsesBufferedSheetModeForTests => _sheetBuffers.Count > 0;

    /// <summary>
    /// Gets or sets the ODF version of the ODS document to write.
    /// 取得或設定寫入之 ODS 文件的 ODF 版本。
    /// </summary>
    public OdfVersion Version
    {
        get => _version;
        set => _version = value;
    }

    private static string FormatVersion(OdfVersion version)
    {
        return version switch
        {
            OdfVersion.Odf10 => "1.0",
            OdfVersion.Odf11 => "1.1",
            OdfVersion.Odf12 => "1.2",
            OdfVersion.Odf13 => "1.3",
            OdfVersion.Odf14 => "1.4",
            _ => "1.4"
        };
    }
    /// <summary>
    /// Short overload of OdsStreamWriter that accepts outputStream; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 outputStream；其餘可選參數使用預設值並轉呼叫最長 OdsStreamWriter 多載。
    /// </summary>
    public OdsStreamWriter(Stream outputStream) : this(outputStream, OdfVersion.Odf14) { }


    /// <summary>
    /// Initializes a new instance of the <see cref="OdsStreamWriter"/> class.
    /// 初始化 <see cref="OdsStreamWriter"/> 類別的新執行個體。
    /// </summary>
    /// <param name="outputStream">The target stream used to output the ODS document. / 用來輸出 ODS 文件的目標資料流。</param>
    /// <param name="version">The ODF specification version to write. / 要寫入的 ODF 規格版本。</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="outputStream"/> is <see langword="null"/>. / 當 <paramref name="outputStream"/> 為 <see langword="null"/> 時擲出。</exception>
    public OdsStreamWriter(Stream outputStream, OdfVersion version)
    {
        _outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
        _version = version;

        // 若底層資料流支援尋覽 (CanSeek)，則直接使用以避免 ZipArchive 強制寫入 Data Descriptor
        // 這能確保 mimetype 檔案不含 Data Descriptor，符合 ODF 封裝規格以防止 LibreOffice 報錯毀損
        Stream targetStream = _outputStream.CanSeek ? _outputStream : new NonSeekableStreamWrapper(_outputStream);
        _zip = new ZipArchive(targetStream, ZipArchiveMode.Create, leaveOpen: true);

        // 1. 先寫入未壓縮的 mimetype
        var mimeEntry = _zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
        using (var s = mimeEntry.Open())
        {
            s.Write(MimeTypeBytes, 0, MimeTypeBytes.Length);
        }

        // 2. 寫入預設的中階資料、樣式與資訊清單專案
        WriteDefaultMetaFiles();

        // 3. 開啟 content.xml 以進行資料流寫入
        var contentEntry = _zip.CreateEntry("content.xml", CompressionLevel.Fastest);
        _contentEntryStream = contentEntry.Open();

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false, // 最小化大小
            NewLineChars = "\r\n",
            // 關閉 XmlWriter 內建逐字元檢查以降低大量資料寫入時的熱迴圈成本；
            // 使用者提供的文字與屬性值改由 OdfXmlCharacterGuard 在寫入前做輕量的
            // XML 1.0 合法性驗證，維持「非法字元快速失敗」的既有語意。
            CheckCharacters = false,
            Async = true
        };
        _writer = XmlWriter.Create(_contentEntryStream, settings);
        // 熱迴圈（工作表／資料列／儲存格）改由原始 XML 組裝器批次寫入，
        // 經 WriteRaw 流入同一個 _writer，與 WriteNode 等 XmlWriter 直接路徑共享輸出順序。
        _rawWriter = new OdfRawXmlWriter(_writer);

        // 寫入 ODF XML 標頭與根 document-content 標籤
        _writer.WriteStartDocument();
        _writer.WriteStartElement("office", "document-content", OdfNamespaces.Office);
        _writer.WriteAttributeString("office", "version", OdfNamespaces.Office, FormatVersion(_version));
        _writer.WriteAttributeString("xmlns", "office", null, OdfNamespaces.Office);
        _writer.WriteAttributeString("xmlns", "table", null, OdfNamespaces.Table);
        _writer.WriteAttributeString("xmlns", "text", null, OdfNamespaces.Text);
        _writer.WriteAttributeString("xmlns", "style", null, OdfNamespaces.Style);

        // 寫入 body 與 spreadsheet 包裝器
        _writer.WriteStartElement("office", "body", OdfNamespaces.Office);
        _writer.WriteStartElement("office", "spreadsheet", OdfNamespaces.Office);
    }


    /// <summary>
    /// Starts writing a new worksheet.
    /// 開始寫入一個新的工作表。
    /// </summary>
    /// <param name="sheetName">The sheet name. / 工作表名稱。</param>
    /// <remarks>
    /// This method writes directly to the current output stream and is suitable for strictly sequential, low-memory sheet output. For interleaved writes across multiple sheets, use <see cref="SwitchToSheet(string)"/>; that mode buffers each sheet fragment for convenience, but memory use grows with buffered content.
    /// 此方法會直接寫入目前輸出資料流，適合嚴格順序、低記憶體的工作表輸出。
    /// 若需要在多張工作表之間交錯寫入，請使用 <see cref="SwitchToSheet(string)"/>；
    /// 該模式會暫存各工作表片段，便利性較高但記憶體用量會隨已緩衝內容增加。
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sheetName"/> contains a character that is not valid in XML 1.0. / 當 <paramref name="sheetName"/> 含有 XML 1.0 不允許的字元時擲出。</exception>
    public void WriteStartSheet(string sheetName)
    {
        if (_disposed)
            return;
        OdfXmlCharacterGuard.ValidateText(sheetName.AsSpan(), nameof(sheetName));
        if (_isSheetStarted)
            WriteEndSheet();
        _activeSheetBuffer = null;
        _rawWriter.WriteStartElement("table:table");
        _rawWriter.WriteAttribute("table:name", sheetName.AsSpan());
        _isSheetStarted = true;
    }

    /// <summary>
    /// Switches to the specified worksheet and uses temporary buffering to support interleaved multi-sheet writes.
    /// 切換至指定工作表，以暫存緩衝支援多工作表交錯寫入。
    /// </summary>
    /// <param name="sheetName">The worksheet name to switch to or create. / 要切換或建立的工作表名稱。</param>
    /// <remarks>
    /// This method is the buffered convenience path: every switched sheet keeps a temporary XML fragment and is emitted in first-seen order when the writer is disposed. To preserve strict low-memory streaming semantics, use <see cref="WriteStartSheet(string)"/> and <see cref="WriteEndSheet"/> to complete each sheet sequentially.
    /// 此方法是緩衝便利路徑：每張曾切換的工作表都會保留一段暫存 XML，
    /// 並於釋放寫入器時依首次出現順序輸出。若要維持嚴格低記憶體串流語意，請使用
    /// <see cref="WriteStartSheet(string)"/> 與 <see cref="WriteEndSheet"/> 依序完成每張工作表。
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sheetName"/> is <see langword="null"/> or whitespace, or contains a character that is not valid in XML 1.0. / 當 <paramref name="sheetName"/> 為 <see langword="null"/>、空白，或含有 XML 1.0 不允許的字元時擲出。</exception>
    public void SwitchToSheet(string sheetName)
    {
        if (_disposed)
            return;
        if (string.IsNullOrWhiteSpace(sheetName))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdsStreamWriter_SheetNameRequired"), nameof(sheetName));
        // 與 WriteStartSheet 一致：在建立緩衝前先驗證名稱，避免只在 SheetBuffer 建構時才失敗。
        OdfXmlCharacterGuard.ValidateText(sheetName.AsSpan(), nameof(sheetName));

        if (_isRowStarted)
            WriteEndRow();

        if (_activeSheetBuffer is null && _isSheetStarted)
            WriteEndSheet();

        if (!_sheetBuffersByName.TryGetValue(sheetName, out SheetBuffer? sheet))
        {
            sheet = new SheetBuffer(sheetName);
            _sheetBuffersByName.Add(sheetName, sheet);
            _sheetBuffers.Add(sheet);
        }

        _activeSheetBuffer = sheet;
        _isSheetStarted = true;
    }
    /// <summary>
    /// Short overload of WriteColumn that accepts width; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 width；其餘可選參數使用預設值並轉呼叫最長 WriteColumn 多載。
    /// </summary>
    public void WriteColumn(OdfLength width) => WriteColumn(width, null);


    /// <summary>
    /// Writes a column definition.
    /// 寫入資料欄定義。
    /// </summary>
    /// <param name="width">The column width. / 資料欄寬度。</param>
    /// <param name="styleName">The style name; if <see langword="null"/>, one is generated automatically. / 樣式名稱，如果為 <see langword="null"/> 則自動產生。</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="styleName"/> contains a character that is not valid in XML 1.0. / 當 <paramref name="styleName"/> 含有 XML 1.0 不允許的字元時擲出。</exception>
    public void WriteColumn(OdfLength width, string? styleName)
    {
        if (_disposed)
            return;
        OdfXmlCharacterGuard.ValidateText(styleName.AsSpan(), nameof(styleName));
        string name = string.IsNullOrEmpty(styleName)
            ? $"co_auto_{++_autoColumnStyleIndex}"
            : styleName!;

        OdfRawXmlWriter raw = CurrentRawWriter;
        raw.WriteStartElement("table:table-column");
        raw.WriteAttribute("table:style-name", name.AsSpan());
        raw.WriteEndElement("table:table-column");

        _columnStyles.Add((name, width));
    }


    /// <summary>
    /// Starts a table row with default options.
    /// 以預設選項開始資料列。
    /// </summary>
    public void WriteStartRow() => WriteStartRow(OdsRowWriteOptions.Default);

    /// <summary>
    /// Starts a table row using an options object.
    /// 以 options 物件開始資料列。
    /// </summary>
    /// <param name="options">The row write options. / 資料列寫入選項。</param>
    public void WriteStartRow(OdsRowWriteOptions options)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(options, nameof(options));

        if (_disposed)
            return;
        string? styleName = options.StyleName;
        double? height = options.Height;
        bool useOptimalHeight = options.UseOptimalHeight;
        OdfXmlCharacterGuard.ValidateText(styleName.AsSpan(), nameof(options.StyleName));
        if (_isRowStarted)
            WriteEndRow();
        _isRowStarted = true;

        string? resolvedStyleName = styleName;
        if (height.HasValue || useOptimalHeight)
        {
            resolvedStyleName = string.IsNullOrEmpty(styleName)
                ? $"ro_auto_{++_autoRowStyleIndex}"
                : styleName;
            OdfLength? rowHeight = height.HasValue
                ? OdfLength.FromPoints(height.Value)
                : (OdfLength?)null;
            _rowStyles.Add((resolvedStyleName!, rowHeight, useOptimalHeight));
        }

        OdfRawXmlWriter raw = CurrentRawWriter;
        raw.WriteStartElement("table:table-row");
        if (!string.IsNullOrEmpty(resolvedStyleName))
        {
            raw.WriteAttribute("table:style-name", resolvedStyleName.AsSpan());
        }
    }

    /// <summary>
    /// Writes a string cell.
    /// 寫入字串型態的儲存格。
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> or  contains a character that is not valid in XML 1.0. / 當 <paramref name="value"/> 或  含有 XML 1.0 不允許的字元時擲出。</exception>
    public void WriteCell(string value) => WriteCell(value, null);

    /// <summary>
    /// Short overload of WriteCell that accepts value and styleName; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 value 與 styleName；其餘可選參數使用預設值並轉呼叫最長 WriteCell 多載。
    /// </summary>
    public void WriteCell(string value, string? styleName)
    {
        if (_disposed)
            return;
        // null 與空字串語意維持既有行為：寫出空的 text:p，不擲出例外。
        ReadOnlySpan<char> text = value is null ? default : value.AsSpan();
        // 於寫入任何元素前先驗證，避免例外發生時留下未關閉的儲存格標籤。
        OdfXmlCharacterGuard.ValidateText(text, nameof(value));
        OdfXmlCharacterGuard.ValidateText(styleName.AsSpan(), nameof(styleName));
        OdfRawXmlWriter raw = CurrentRawWriter;
        raw.WriteStartElement("table:table-cell");
        raw.WriteAttribute("office:value-type", "string".AsSpan());
        if (!string.IsNullOrEmpty(styleName))
        {
            raw.WriteAttribute("table:style-name", styleName.AsSpan());
        }
        raw.WriteStartElement("text:p");
        if (!text.IsEmpty)
        {
            raw.WriteText(text);
        }
        raw.WriteEndElement("text:p");
        raw.WriteEndElement("table:table-cell");
    }

    /// <summary>
    /// Writes a string cell.
    /// 寫入字串型態的儲存格。
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> or  contains a character that is not valid in XML 1.0. / 當 <paramref name="value"/> 或  含有 XML 1.0 不允許的字元時擲出。</exception>
    public void WriteCell(ReadOnlySpan<char> value) => WriteCell(value, null);

    /// <summary>
    /// Short overload of WriteCell that accepts value and styleName; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 value 與 styleName；其餘可選參數使用預設值並轉呼叫最長 WriteCell 多載。
    /// </summary>
    public void WriteCell(ReadOnlySpan<char> value, string? styleName)
    {
        if (_disposed)
            return;
        // 於寫入任何元素前先驗證，避免例外發生時留下未關閉的儲存格標籤。
        OdfXmlCharacterGuard.ValidateText(value, nameof(value));
        OdfXmlCharacterGuard.ValidateText(styleName.AsSpan(), nameof(styleName));
        OdfRawXmlWriter raw = CurrentRawWriter;
        raw.WriteStartElement("table:table-cell");
        raw.WriteAttribute("office:value-type", "string".AsSpan());
        if (!string.IsNullOrEmpty(styleName))
        {
            raw.WriteAttribute("table:style-name", styleName.AsSpan());
        }
        raw.WriteStartElement("text:p");
        if (!value.IsEmpty)
        {
            // 直寫路徑可直接消費 span，不再需要 ToStringValue 的字元複本。
            raw.WriteText(value);
        }
        raw.WriteEndElement("text:p");
        raw.WriteEndElement("table:table-cell");
    }

    /// <summary>
    /// Writes a string cell.
    /// 寫入字串型態的儲存格。
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> or  contains a character that is not valid in XML 1.0. / 當 <paramref name="value"/> 或  含有 XML 1.0 不允許的字元時擲出。</exception>
    public void WriteCell(ReadOnlyMemory<char> value) => WriteCell(value.Span, null);

    /// <summary>
    /// Short overload of WriteCell that accepts value and styleName; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 value 與 styleName；其餘可選參數使用預設值並轉呼叫最長 WriteCell 多載。
    /// </summary>
    public void WriteCell(ReadOnlyMemory<char> value, string? styleName) => WriteCell(value.Span, styleName);

    /// <summary>
    /// Writes a numeric cell.
    /// 寫入數值型態的儲存格。
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when  contains a character that is not valid in XML 1.0. / 當  含有 XML 1.0 不允許的字元時擲出。</exception>
    public void WriteCell(double value) => WriteCell(value, null);

    /// <summary>
    /// Short overload of WriteCell that accepts value and styleName; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 value 與 styleName；其餘可選參數使用預設值並轉呼叫最長 WriteCell 多載。
    /// </summary>
    public void WriteCell(double value, string? styleName)
    {
        if (_disposed)
            return;
        OdfXmlCharacterGuard.ValidateText(styleName.AsSpan(), nameof(styleName));
        // office:value 屬性與 text:p 內文共用同一次格式化結果，避免重複的 ToString 配置。
        string text = value.ToString(CultureInfo.InvariantCulture);
        OdfRawXmlWriter raw = CurrentRawWriter;
        raw.WriteStartElement("table:table-cell");
        raw.WriteAttribute("office:value-type", "float".AsSpan());
        raw.WriteAttribute("office:value", text.AsSpan());
        if (!string.IsNullOrEmpty(styleName))
        {
            raw.WriteAttribute("table:style-name", styleName.AsSpan());
        }
        raw.WriteStartElement("text:p");
        raw.WriteText(text.AsSpan());
        raw.WriteEndElement("text:p");
        raw.WriteEndElement("table:table-cell");
    }

    /// <summary>
    /// Writes a date and time cell.
    /// 寫入日期時間型態的儲存格。
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when  contains a character that is not valid in XML 1.0. / 當  含有 XML 1.0 不允許的字元時擲出。</exception>
    public void WriteCell(DateTime value) => WriteCell(value, null, false);

    /// <summary>
    /// Writes a date-time cell with an optional style name.
    /// 寫入日期時間儲存格，並可指定樣式名稱。
    /// </summary>
    /// <param name="value">The date-time value. / 日期時間值。</param>
    /// <param name="styleName">The optional style name. / 選用的樣式名稱。</param>
    public void WriteCell(DateTime value, string? styleName) => WriteCell(value, styleName, false);

    /// <summary>
    /// Writes a date-time cell with an explicit timezone-naive flag.
    /// 寫入日期時間儲存格，並明確指定是否為無時區值。
    /// </summary>
    /// <param name="value">The date-time value. / 日期時間值。</param>
    /// <param name="timezoneNaive">Whether to emit a timezone-naive value. / 是否輸出無時區值。</param>
    public void WriteCell(DateTime value, bool timezoneNaive) => WriteCell(value, null, timezoneNaive);

    /// <summary>
    /// Writes a date-time cell with style name and timezone-naive flag.
    /// 寫入日期時間儲存格，並指定樣式名稱與無時區旗標。
    /// </summary>
    /// <param name="value">The date-time value. / 日期時間值。</param>
    /// <param name="styleName">The optional style name. / 選用的樣式名稱。</param>
    /// <param name="timezoneNaive">Whether to emit a timezone-naive value. / 是否輸出無時區值。</param>
    public void WriteCell(DateTime value, string? styleName, bool timezoneNaive)
    {
        if (_disposed)
            return;
        OdfXmlCharacterGuard.ValidateText(styleName.AsSpan(), nameof(styleName));

        string isoDate;
        if (value == DateTime.MinValue || value == DateTime.MaxValue)
        {
            isoDate = timezoneNaive
                ? value.ToString("s", CultureInfo.InvariantCulture)
                : value.ToString("s", CultureInfo.InvariantCulture) + "Z";
        }
        else
        {
            isoDate = timezoneNaive
                ? value.ToString("s", CultureInfo.InvariantCulture)
                : value.ToUniversalTime().ToString("s", CultureInfo.InvariantCulture) + "Z";
        }

        OdfRawXmlWriter raw = CurrentRawWriter;
        raw.WriteStartElement("table:table-cell");
        raw.WriteAttribute("office:value-type", "date".AsSpan());
        raw.WriteAttribute("office:date-value", isoDate.AsSpan());
        if (!string.IsNullOrEmpty(styleName))
        {
            raw.WriteAttribute("table:style-name", styleName.AsSpan());
        }
        raw.WriteStartElement("text:p");
        raw.WriteText(isoDate.AsSpan());
        raw.WriteEndElement("text:p");
        raw.WriteEndElement("table:table-cell");
    }

    /// <summary>
    /// Writes a Boolean cell.
    /// 寫入布林值型態的儲存格。
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when  contains a character that is not valid in XML 1.0. / 當  含有 XML 1.0 不允許的字元時擲出。</exception>
    public void WriteCell(bool value) => WriteCell(value, null);

    /// <summary>
    /// Short overload of WriteCell that accepts value and styleName; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 value 與 styleName；其餘可選參數使用預設值並轉呼叫最長 WriteCell 多載。
    /// </summary>
    public void WriteCell(bool value, string? styleName)
    {
        if (_disposed)
            return;
        OdfXmlCharacterGuard.ValidateText(styleName.AsSpan(), nameof(styleName));
        OdfRawXmlWriter raw = CurrentRawWriter;
        raw.WriteStartElement("table:table-cell");
        raw.WriteAttribute("office:value-type", "boolean".AsSpan());
        raw.WriteAttribute("office:boolean-value", value ? "true".AsSpan() : "false".AsSpan());
        if (!string.IsNullOrEmpty(styleName))
        {
            raw.WriteAttribute("table:style-name", styleName.AsSpan());
        }
        raw.WriteStartElement("text:p");
        raw.WriteText(value ? "TRUE".AsSpan() : "FALSE".AsSpan());
        raw.WriteEndElement("text:p");
        raw.WriteEndElement("table:table-cell");
    }

    /// <summary>
    /// Writes an existing DOM subtree directly to the current worksheet or row position.
    /// 將既有 DOM 子樹直接寫入目前工作表或資料列位置。
    /// </summary>
    /// <param name="node">The DOM node to write. / 要寫入的 DOM 節點。</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is <see langword="null"/>. / 當 <paramref name="node"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="ArgumentException">Thrown when the subtree contains text or attribute values with characters that are not valid in XML 1.0. / 當子樹的文字或屬性值含有 XML 1.0 不允許的字元時擲出。</exception>
    public void WriteNode(OdfNode node)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(node, nameof(node));
        if (_disposed)
            return;

        // 底層 XmlWriter 已關閉 CheckCharacters，寫入前先以輕量防線驗證整棵子樹，
        // 維持與先前「XmlWriter 於寫入時擲出」等價的快速失敗語意。
        ValidateNodeXmlCharacters(node);
        // 交接同步點：任意 DOM 子樹維持走 XmlWriter，寫入前先閉合快速路徑的
        // 未完成起始標籤並清空其字元緩衝，確保輸出順序正確。
        CurrentRawWriter.FlushToTarget();
        Dictionary<string, string> namespaces = CreateFragmentNamespaceMap(node);
        int openElementsCount = 0;
        OdfXmlWriter.WriteNode(node, CurrentWriter, namespaces, ref openElementsCount, isRoot: false, depth: 1);
    }

    private static void ValidateNodeXmlCharacters(OdfNode node)
    {
        // 惰性節點（尚未實體化且無已載入子節點）由 TryWriteLazyXml 走 WriteRaw 原始位元組路徑；
        // WriteRaw 在 CheckCharacters 開啟時本來就不受 XmlWriter 字元檢查，且其內容來自既有
        // 文件的解析結果，故略過驗證以保留惰性寫入最佳化，語意與先前一致。
        if (node._isLazy && node.Children.LoadedCount == 0)
            return;

        if (node.NodeType != OdfNodeType.Element)
        {
            OdfXmlCharacterGuard.ValidateText(node.TextContent.AsSpan(), nameof(node));
            return;
        }

        foreach (KeyValuePair<OdfAttributeName, string> attribute in node.Attributes)
        {
            OdfXmlCharacterGuard.ValidateText(attribute.Value.AsSpan(), nameof(node));
        }

        foreach (OdfNode child in node.Children)
        {
            ValidateNodeXmlCharacters(child);
        }
    }

    /// <summary>
    /// Writes a CSV stream to the current worksheet row by row with low memory usage.
    /// 將 CSV 資料流以低記憶體方式逐列寫入目前工作表。
    /// </summary>
    /// <returns>A task that represents the asynchronous write operation. / 代表非同步寫入作業的工作。</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="csvStream"/> is <see langword="null"/>. / 當 <paramref name="csvStream"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="InvalidOperationException">Thrown when no worksheet has been started. / 當目前尚未開始任何工作表時擲出。</exception>
    public Task WriteCsvStreamAsync(Stream csvStream) => WriteCsvStreamAsync(csvStream, false, default);

    /// <summary>
    /// Short overload of WriteCsvStreamAsync that accepts csvStream and firstRowAsHeader; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 csvStream 與 firstRowAsHeader；其餘可選參數使用預設值並轉呼叫最長 WriteCsvStreamAsync 多載。
    /// </summary>
    public Task WriteCsvStreamAsync(Stream csvStream, bool firstRowAsHeader) => WriteCsvStreamAsync(csvStream, firstRowAsHeader, default);

    /// <summary>
    /// Short overload of WriteCsvStreamAsync that accepts csvStream, firstRowAsHeader, and cancellationToken; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 csvStream、firstRowAsHeader 與 cancellationToken；其餘可選參數使用預設值並轉呼叫最長 WriteCsvStreamAsync 多載。
    /// </summary>
    public async Task WriteCsvStreamAsync(
        Stream csvStream,
        bool firstRowAsHeader,
        CancellationToken cancellationToken)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(csvStream, nameof(csvStream));

        global::OdfKit.Internal.OdfThrowHelper.ThrowIfDisposed(_disposed, nameof(OdsStreamWriter));

        if (!_isSheetStarted)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdsStreamWriter_SheetNotStarted"));
        }

        using var textReader = new StreamReader(
            csvStream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true);
        using CsvDataReader csv = CsvDataReader.Create(
            textReader,
            new CsvDataReaderOptions
            {
                HasHeaders = false
            });

        bool skipHeader = firstRowAsHeader;
        while (await csv.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (skipHeader)
            {
                skipHeader = false;
                continue;
            }

            WriteStartRow();
            for (int i = 0; i < csv.FieldCount; i++)
            {
                string value = csv.IsDBNull(i) ? string.Empty : csv.GetString(i);
                WriteCell(value);
            }
            WriteEndRow();
        }
    }

    /// <summary>
    /// Writes the current result set from a <see cref="DbDataReader"/> to the current worksheet row by row with low memory usage.
    /// 將 <see cref="DbDataReader"/> 目前結果集以低記憶體方式逐列寫入目前工作表。
    /// </summary>
    /// <returns>A task that represents the asynchronous write operation. / 代表非同步寫入作業的工作。</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reader"/> is <see langword="null"/>. / 當 <paramref name="reader"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the writer has been disposed. / 當寫入器已釋放時擲出。</exception>
    /// <exception cref="InvalidOperationException">Thrown when no worksheet has been started. / 當目前尚未開始任何工作表時擲出。</exception>
    public Task WriteDataAsync(DbDataReader reader) => WriteDataAsync(reader, false, default);

    /// <summary>
    /// Short overload of WriteDataAsync that accepts reader and includeColumnNames; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 reader 與 includeColumnNames；其餘可選參數使用預設值並轉呼叫最長 WriteDataAsync 多載。
    /// </summary>
    public Task WriteDataAsync(DbDataReader reader, bool includeColumnNames) => WriteDataAsync(reader, includeColumnNames, default);

    /// <summary>
    /// Short overload of WriteDataAsync that accepts reader, includeColumnNames, and cancellationToken; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 reader、includeColumnNames 與 cancellationToken；其餘可選參數使用預設值並轉呼叫最長 WriteDataAsync 多載。
    /// </summary>
    public async Task WriteDataAsync(
        DbDataReader reader,
        bool includeColumnNames,
        CancellationToken cancellationToken)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(reader, nameof(reader));

        global::OdfKit.Internal.OdfThrowHelper.ThrowIfDisposed(_disposed, nameof(OdsStreamWriter));

        if (!_isSheetStarted)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdsStreamWriter_SheetNotStarted"));
        }

        if (includeColumnNames)
        {
            WriteStartRow();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                WriteCell(reader.GetName(i));
            }
            WriteEndRow();
        }

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            WriteStartRow();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                WriteCellValue(reader.IsDBNull(i) ? null : reader.GetValue(i));
            }
            WriteEndRow();
        }
    }

    private void WriteCellValue(object? value)
    {
        switch (value)
        {
            case null:
            case DBNull:
                WriteCell(string.Empty);
                break;
            case string text:
                WriteCell(text);
                break;
            case bool boolean:
                WriteCell(boolean);
                break;
            case DateTime dateTime:
                WriteCell(dateTime);
                break;
            case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                WriteCell(Convert.ToDouble(value, CultureInfo.InvariantCulture));
                break;
            default:
                WriteCell(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                break;
        }
    }

    /// <summary>
    /// Ends writing the current data row.
    /// 結束目前資料列的寫入。
    /// </summary>
    public void WriteEndRow()
    {
        if (_disposed)
            return;
        if (_isRowStarted)
        {
            CurrentRawWriter.WriteEndElement("table:table-row");
            _isRowStarted = false;
        }
    }

    /// <summary>
    /// Ends writing the current worksheet.
    /// 結束目前工作表的寫入。
    /// </summary>
    public void WriteEndSheet()
    {
        if (_disposed)
            return;
        if (_isRowStarted)
            WriteEndRow();
        if (_isSheetStarted)
        {
            if (_activeSheetBuffer is null)
                _rawWriter.WriteEndElement("table:table");
            _isSheetStarted = false;
        }
    }

    /// <summary>
    /// Closes all underlying streams and releases resources used by <see cref="OdsStreamWriter"/>.
    /// 關閉所有底層資料流並釋放 <see cref="OdsStreamWriter"/> 使用的資源。
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously releases resources used by the <see cref="OdsStreamWriter"/> class.
    /// 非同步釋放 <see cref="OdsStreamWriter"/> 類別所使用的資源。
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous dispose operation. / 代表非同步處置作業的 <see cref="ValueTask"/>。</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        await FlushAsync(CancellationToken.None).ConfigureAwait(false);
        Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously flushes buffered worksheet XML to the current package entry.
    /// 非同步將已緩衝的工作表 XML 沖洗至目前封裝項目。
    /// </summary>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>A task representing the flush operation. / 代表沖洗作業的工作。</returns>
    /// <remarks>
    /// Package finalization remains synchronous because <see cref="ZipArchive"/> exposes only synchronous disposal.
    /// 封裝最終化仍為同步作業，因為 <see cref="ZipArchive"/> 僅提供同步處置。
    /// </remarks>
    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfDisposed(_disposed, nameof(OdsStreamWriter));
        cancellationToken.ThrowIfCancellationRequested();
        CurrentRawWriter.FlushToTarget();
        await CurrentWriter.FlushAsync().ConfigureAwait(false);
        await _contentEntryStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Releases unmanaged resources used by the <see cref="OdsStreamWriter"/> class and optionally releases managed resources.
    /// 釋放 <see cref="OdsStreamWriter"/> 類別所使用的非受控資源，並選擇性釋放受控資源。
    /// </summary>
    /// <param name="disposing"><see langword="true"/> to release managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources. / 為 <see langword="true"/> 則釋放受控與非受控資源；為 <see langword="false"/> 則僅釋放非受控資源。</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // 收尾寫入（結束工作表、沖洗緩衝、關閉根元素）全部包在 try 內：
            // 這些步驟任何一步拋出，下方 finally 的 _zip.Dispose() 就是寫出 ZIP 中央目錄的
            // 唯一機會。少了它，輸出檔案會是無法被任何 ZIP 工具開啟的殘骸。
            try
            {
                if (_isSheetStarted)
                    WriteEndSheet();

                // 快速路徑交接：先將主要原始緩衝沖入 _writer，之後的緩衝工作表
                // 與收尾標籤才能以正確順序寫出。
                _rawWriter.FlushToTarget();

                // 緩衝工作表片段一律透過 _writer.WriteRaw 寫入（而非直接寫原始位元組到
                // _contentEntryStream），讓 _writer 自己知道先前延後關閉的 <office:spreadsheet>
                // 起始標籤已被後續寫入操作結束，才能正確補上 '>'；否則會產生
                // <office:spreadsheet<table:table ...> 這種缺少 '>' 分隔的畸形 XML。
                WriteBufferedSheets();

                _rawWriter.Dispose();

                // 關閉 spreadsheet、body、document-content 標籤
                _writer.WriteEndElement(); // office:spreadsheet
                _writer.WriteEndElement(); // office:body
                _writer.WriteEndElement(); // office:document-content
                _writer.WriteEndDocument();

                try
                { _writer.Dispose(); }
                catch (Exception ex)
                {
                    OdfKitDiagnostics.Warn($"OdsStreamWriter 釋放 XmlWriter 時發生次要錯誤：{ex.Message}", ex);
                }

                try
                { _contentEntryStream.Dispose(); }
                catch (Exception ex)
                {
                    OdfKitDiagnostics.Warn($"OdsStreamWriter 釋放 content 串流時發生次要錯誤：{ex.Message}", ex);
                }

                // 與上方 XmlWriter／content 串流的次要清理錯誤不同，styles.xml 寫入失敗代表輸出封裝
                // 實際缺少 manifest 已宣告的內容、屬於不完整／損毀的封裝，因此不吞例外，讓呼叫端可感知。
                WriteStyles();
            }
            finally
            {
                _zip.Dispose();
            }
        }

        _disposed = true;
    }

    #endregion

    private XmlWriter CurrentWriter => _activeSheetBuffer?.Writer ?? _writer;

    private OdfRawXmlWriter CurrentRawWriter => _activeSheetBuffer?.RawWriter ?? _rawWriter;
    /// <summary>
    /// Short overload of WriteSheetsAsync that accepts jobs; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 jobs；其餘可選參數使用預設值並轉呼叫最長 WriteSheetsAsync 多載。
    /// </summary>
    public Task WriteSheetsAsync(IEnumerable<OdsSheetWriteJob> jobs) => WriteSheetsAsync(jobs, 0, default);

    /// <summary>
    /// Short overload of WriteSheetsAsync that accepts jobs and maxConcurrency; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 jobs 與 maxConcurrency；其餘可選參數使用預設值並轉呼叫最長 WriteSheetsAsync 多載。
    /// </summary>
    public Task WriteSheetsAsync(IEnumerable<OdsSheetWriteJob> jobs, int maxConcurrency) => WriteSheetsAsync(jobs, maxConcurrency, default);


    /// <summary>
    /// Generates XML fragments for multiple worksheets in parallel and writes them to the current ODS package in job list order.
    /// 並行產生多個工作表的 XML 片段，並依工作清單順序寫入目前 ODS 封裝。
    /// </summary>
    /// <param name="jobs">The worksheet write job list. / 工作表寫入工作清單。</param>
    /// <param name="maxConcurrency">The maximum concurrency; values less than 1 use the processor count. / 最大並行度；小於 1 時使用處理器核心數。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>A task that represents the asynchronous write operation. / 代表非同步寫入作業的工作。</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="jobs"/> is <see langword="null"/>. / 當 <paramref name="jobs"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the writer has been disposed. / 當寫入器已釋放時擲出。</exception>
    public async Task WriteSheetsAsync(IEnumerable<OdsSheetWriteJob> jobs, int maxConcurrency, CancellationToken cancellationToken)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(jobs, nameof(jobs));

        global::OdfKit.Internal.OdfThrowHelper.ThrowIfDisposed(_disposed, nameof(OdsStreamWriter));

        if (_isRowStarted)
        {
            WriteEndRow();
        }

        if (_isSheetStarted)
        {
            WriteEndSheet();
        }

        OdsSheetWriteJob[] jobArray = jobs.ToArray();
        if (jobArray.Length == 0)
        {
            return;
        }

        int concurrency = OdfParallelScheduler.GetEffectiveConcurrency(maxConcurrency);
        using SemaphoreSlim semaphore = new(concurrency);
        SheetBuffer[] buffers = new SheetBuffer[jobArray.Length];
        Task[] tasks = new Task[jobArray.Length];
        for (int index = 0; index < jobArray.Length; index++)
        {
            int jobIndex = index;
            tasks[jobIndex] = Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    OdsSheetWriteJob job = jobArray[jobIndex];
                    // 建構後立即指派，確保寫入工作中途失敗時 catch 能處置到此緩衝
                    var buffer = new SheetBuffer(job.SheetName);
                    buffers[jobIndex] = buffer;
                    var sheetWriter = new OdsSheetWriter(buffer.RawWriter);
                    await job.WriteAsync(sheetWriter, cancellationToken).ConfigureAwait(false);
                    sheetWriter.CloseOpenRow();
                    buffer.Close();
                }
                catch
                {
                    buffers[jobIndex]?.Dispose();
                    throw;
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
            foreach (SheetBuffer buffer in buffers)
            {
                _sheetBuffers.Add(buffer);
            }
        }
        catch
        {
            foreach (SheetBuffer? buffer in buffers)
            {
                buffer?.Dispose();
            }

            throw;
        }
    }


    private void WriteBufferedSheets()
    {
        foreach (SheetBuffer sheet in _sheetBuffers)
        {
            sheet.Close();
            sheet.WriteTo(_writer);
            sheet.Dispose();
        }

        _sheetBuffers.Clear();
        _sheetBuffersByName.Clear();
        _activeSheetBuffer = null;
    }

    private static Dictionary<string, string> CreateFragmentNamespaceMap(OdfNode node)
    {
        Dictionary<string, string> namespaces = new(StringComparer.Ordinal)
        {
            [OdfNamespaces.Office] = "office",
            [OdfNamespaces.Table] = "table",
            [OdfNamespaces.Text] = "text",
            [OdfNamespaces.Style] = "style",
            [OdfNamespaces.Fo] = "fo",
            [OdfNamespaces.Draw] = "draw",
            [OdfNamespaces.XLink] = "xlink"
        };

        if (!string.IsNullOrEmpty(node.NamespaceUri) && !namespaces.ContainsKey(node.NamespaceUri))
            namespaces[node.NamespaceUri] = node.Prefix ?? string.Empty;

        return namespaces;
    }

    private sealed class SheetBuffer : IDisposable
    {
        private readonly MemoryStream _stream = new();
        private bool _closed;

        /// <summary>
        /// Full overload of SheetBuffer that accepts sheetName.
        /// SheetBuffer 完整多載：接受 sheetName。
        /// </summary>
        public SheetBuffer(string sheetName)
        {
            // 與主要 content.xml 寫入器一致：關閉內建逐字元檢查，
            // 使用者輸入由 OdfXmlCharacterGuard 於各寫入入口驗證。
            OdfXmlCharacterGuard.ValidateText(sheetName.AsSpan(), nameof(sheetName));
            Writer = XmlWriter.Create(
                _stream,
                new XmlWriterSettings
                {
                    Encoding = new UTF8Encoding(false),
                    Indent = false,
                    NewLineChars = "\r\n",
                    ConformanceLevel = ConformanceLevel.Fragment,
                    CheckCharacters = false,
                    Async = true
                });
            Writer.WriteStartElement("table", "table", OdfNamespaces.Table);
            Writer.WriteAttributeString("table", "name", OdfNamespaces.Table, sheetName);
            RawWriter = new OdfRawXmlWriter(Writer);
        }

        public XmlWriter Writer { get; }

        /// <summary>
        /// 熱迴圈原始 XML 組裝器；內容經 WriteRaw 流入 <see cref="Writer"/>，
        /// 與主要 content.xml 路徑輸出相同的標記形狀。
        /// </summary>
        public OdfRawXmlWriter RawWriter { get; }

        /// <summary>
        /// Short overload of Close that uses default values for all optional parameters and forwards to the full overload.
        /// 便利多載：Close 的所有可選參數使用預設值並轉呼叫最長多載。
        /// </summary>
        public void Close()
        {
            if (_closed)
                return;

            // 先將快速路徑緩衝沖入片段寫入器，再關閉 table:table 起始標籤。
            RawWriter.FlushToTarget();
            Writer.WriteEndElement();
            Writer.Flush();
            _closed = true;
        }

        /// <summary>
        /// 以分塊解碼方式將緩衝的工作表 XML 直接寫入目標寫入器，
        /// 避免 ToArray 位元組複本與整份 XML 大字串的雙重 Heap 配置。
        /// </summary>
        public void WriteTo(XmlWriter target)
        {
            Close();

            if (!_stream.TryGetBuffer(out ArraySegment<byte> buffer))
            {
                target.WriteRaw(Encoding.UTF8.GetString(_stream.ToArray()));
                return;
            }

            const int ByteChunkSize = 4096;
            var decoder = Encoding.UTF8.GetDecoder();
            char[] chars = ArrayPool<char>.Shared.Rent(ByteChunkSize + 4);
            try
            {
                int byteIndex = buffer.Offset;
                int remaining = buffer.Count;
                while (remaining > 0)
                {
                    int byteChunk = Math.Min(remaining, ByteChunkSize);
                    bool isLast = byteChunk == remaining;
                    int charCount = decoder.GetChars(buffer.Array!, byteIndex, byteChunk, chars, 0, flush: isLast);
                    if (charCount > 0)
                    {
                        target.WriteRaw(chars, 0, charCount);
                    }

                    byteIndex += byteChunk;
                    remaining -= byteChunk;
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(chars);
            }
        }

        /// <summary>
        /// Short overload of Dispose that uses default values for all optional parameters and forwards to the full overload.
        /// 便利多載：Dispose 的所有可選參數使用預設值並轉呼叫最長多載。
        /// </summary>
        public void Dispose()
        {
            RawWriter.Dispose();
            Writer.Dispose();
            _stream.Dispose();
        }
    }

    /// <summary>
    /// Converts an ODS write operation to an asynchronous stream of read-only memory byte chunks for chunked HTTP transfer.
    /// 將 ODS 文件寫入作業轉換為非同步的唯讀記憶體位元組資料流，可用於 Chunked HTTP 傳輸。
    /// </summary>
    /// <returns>An asynchronous enumerator of read-only memory byte chunks. / 非同步唯讀記憶體位元組區段的列舉器。</returns>
    public static IAsyncEnumerable<ReadOnlyMemory<byte>> ToAsyncEnumerable(Func<OdsStreamWriter, Task> writeAction) => ToAsyncEnumerable(writeAction, OdfVersion.Odf14, default);

    /// <summary>
    /// Converts an asynchronous ODS write action to chunked HTTP-friendly memory segments with cancellation.
    /// 以取消語彙基元將非同步 ODS 寫入動作轉換為適合 Chunked HTTP 的記憶體區段。
    /// </summary>
    /// <param name="writeAction">The asynchronous write callback. / 非同步寫入回呼。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>An asynchronous enumerator of read-only memory byte chunks. / 非同步唯讀記憶體位元組區段的列舉器。</returns>
    public static IAsyncEnumerable<ReadOnlyMemory<byte>> ToAsyncEnumerable(
        Func<OdsStreamWriter, Task> writeAction,
        CancellationToken cancellationToken) =>
        ToAsyncEnumerable(writeAction, OdfVersion.Odf14, cancellationToken);

    /// <summary>
    /// Short overload of ToAsyncEnumerable that accepts version; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 version；其餘可選參數使用預設值並轉呼叫最長 ToAsyncEnumerable 多載。
    /// </summary>
    public static IAsyncEnumerable<ReadOnlyMemory<byte>> ToAsyncEnumerable(Func<OdsStreamWriter, Task> writeAction, OdfVersion version) => ToAsyncEnumerable(writeAction, version, default);

    /// <summary>
    /// Short overload of ToAsyncEnumerable that accepts version and cancellationToken; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 version 與 cancellationToken；其餘可選參數使用預設值並轉呼叫最長 ToAsyncEnumerable 多載。
    /// </summary>
    public static async IAsyncEnumerable<ReadOnlyMemory<byte>> ToAsyncEnumerable(
        Func<OdsStreamWriter, Task> writeAction,
        OdfVersion version,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(writeAction, nameof(writeAction));

        var stream = new AsyncProducerConsumerStream();

        _ = Task.Run(async () =>
        {
            try
            {
                using (var writer = new OdsStreamWriter(stream, version))
                {
                    await writeAction(writer).ConfigureAwait(false);
                }
                stream.Complete();
            }
            catch (Exception ex)
            {
                stream.Fault(ex);
            }
        }, cancellationToken);

        while (true)
        {
            var chunk = await stream.ReadChunkAsync(cancellationToken).ConfigureAwait(false);
            if (chunk is null)
                break;

            yield return chunk;
        }
    }

    /// <summary>
    /// Converts an ODS write operation to an asynchronous stream of read-only memory byte chunks for chunked HTTP transfer.
    /// 將 ODS 文件寫入作業轉換為非同步的唯讀記憶體位元組資料流，可用於 Chunked HTTP 傳輸。
    /// </summary>
    /// <returns>An asynchronous enumerator of read-only memory byte chunks. / 非同步唯讀記憶體位元組區段的列舉器。</returns>
    public static IAsyncEnumerable<ReadOnlyMemory<byte>> ToAsyncEnumerable(Action<OdsStreamWriter> writeAction) => ToAsyncEnumerable(writeAction, OdfVersion.Odf14, default);

    /// <summary>
    /// Converts a synchronous ODS write action to chunked HTTP-friendly memory segments with cancellation.
    /// 以取消語彙基元將同步 ODS 寫入動作轉換為適合 Chunked HTTP 的記憶體區段。
    /// </summary>
    /// <param name="writeAction">The synchronous write callback. / 同步寫入回呼。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>An asynchronous enumerator of read-only memory byte chunks. / 非同步唯讀記憶體位元組區段的列舉器。</returns>
    public static IAsyncEnumerable<ReadOnlyMemory<byte>> ToAsyncEnumerable(
        Action<OdsStreamWriter> writeAction,
        CancellationToken cancellationToken) =>
        ToAsyncEnumerable(writeAction, OdfVersion.Odf14, cancellationToken);

    /// <summary>
    /// Short overload of ToAsyncEnumerable that accepts writeAction and version; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 writeAction 與 version；其餘可選參數使用預設值並轉呼叫最長 ToAsyncEnumerable 多載。
    /// </summary>
    public static IAsyncEnumerable<ReadOnlyMemory<byte>> ToAsyncEnumerable(Action<OdsStreamWriter> writeAction, OdfVersion version) => ToAsyncEnumerable(writeAction, version, default);

    /// <summary>
    /// Short overload of ToAsyncEnumerable that accepts writeAction, version, and cancellationToken; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 writeAction、version 與 cancellationToken；其餘可選參數使用預設值並轉呼叫最長 ToAsyncEnumerable 多載。
    /// </summary>
    public static async IAsyncEnumerable<ReadOnlyMemory<byte>> ToAsyncEnumerable(
        Action<OdsStreamWriter> writeAction,
        OdfVersion version,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(writeAction, nameof(writeAction));

        var stream = new AsyncProducerConsumerStream();

        _ = Task.Run(() =>
        {
            try
            {
                using (var writer = new OdsStreamWriter(stream, version))
                {
                    writeAction(writer);
                }
                stream.Complete();
            }
            catch (Exception ex)
            {
                stream.Fault(ex);
            }
        }, cancellationToken);

        while (true)
        {
            var chunk = await stream.ReadChunkAsync(cancellationToken).ConfigureAwait(false);
            if (chunk is null)
                break;

            yield return chunk;
        }
    }

    private sealed class AsyncProducerConsumerStream : Stream
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<byte[]> _queue = new();
        private readonly SemaphoreSlim _semaphore = new(0);
        private bool _isCompleted;
        private Exception? _exception;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;

        /// <summary>
        /// Gets the length of the stream. Not supported for this write-only stream.
        /// 取得資料流長度。此唯寫資料流不支援此作業。
        /// </summary>
        /// <exception cref="NotSupportedException">Always thrown. / 一律擲出。</exception>
        public override long Length => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));
        public override long Position
        {
            get => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));
            set => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));
        }

        /// <summary>
        /// Short overload of Complete that uses default values for all optional parameters and forwards to the full overload.
        /// 便利多載：Complete 的所有可選參數使用預設值並轉呼叫最長多載。
        /// </summary>
        public void Complete()
        {
            _isCompleted = true;
            _semaphore.Release();
        }

        /// <summary>
        /// Short overload of Fault that accepts ex; remaining optional parameters use defaults and forward to the full overload.
        /// 便利多載：提供 ex；其餘可選參數使用預設值並轉呼叫最長 Fault 多載。
        /// </summary>
        public void Fault(Exception ex)
        {
            _exception = ex;
            _isCompleted = true;
            _semaphore.Release();
        }

        /// <summary>
        /// Short overload of Write that accepts buffer, offset, and count; remaining optional parameters use defaults and forward to the full overload.
        /// 便利多載：提供 buffer、offset 與 count；其餘可選參數使用預設值並轉呼叫最長 Write 多載。
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count == 0)
                return;
            var copy = new byte[count];
            Buffer.BlockCopy(buffer, offset, copy, 0, count);
            _queue.Enqueue(copy);
            _semaphore.Release();
        }

        /// <summary>
        /// Short overload of ReadChunkAsync that accepts cancellationToken; remaining optional parameters use defaults and forward to the full overload.
        /// 便利多載：提供 cancellationToken；其餘可選參數使用預設值並轉呼叫最長 ReadChunkAsync 多載。
        /// </summary>
        public async Task<byte[]?> ReadChunkAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                if (_queue.TryDequeue(out var chunk))
                {
                    return chunk;
                }

                if (_isCompleted)
                {
                    if (_exception is not null)
                    {
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(_exception).Throw();
                    }
                    return null;
                }

                await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Short overload of Flush that uses default values for all optional parameters and forwards to the full overload.
        /// 便利多載：Flush 的所有可選參數使用預設值並轉呼叫最長多載。
        /// </summary>
        public override void Flush() { }
        /// <summary>
        /// Short overload of Read that accepts buffer, offset, and count; remaining optional parameters use defaults and forward to the full overload.
        /// 便利多載：提供 buffer、offset 與 count；其餘可選參數使用預設值並轉呼叫最長 Read 多載。
        /// </summary>
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));
        /// <summary>
        /// Short overload of Seek that accepts offset and origin; remaining optional parameters use defaults and forward to the full overload.
        /// 便利多載：提供 offset 與 origin；其餘可選參數使用預設值並轉呼叫最長 Seek 多載。
        /// </summary>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));
        /// <summary>
        /// Short overload of SetLength that accepts value; remaining optional parameters use defaults and forward to the full overload.
        /// 便利多載：提供 value；其餘可選參數使用預設值並轉呼叫最長 SetLength 多載。
        /// </summary>
        public override void SetLength(long value) => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));
    }
}


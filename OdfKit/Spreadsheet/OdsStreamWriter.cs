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
    #region Stream Writing
    private readonly Stream _outputStream;
    private readonly ZipArchive _zip;
    private readonly Stream _contentEntryStream;
    private readonly XmlWriter _writer;
    private readonly List<SheetBuffer> _sheetBuffers = [];
    private readonly Dictionary<string, SheetBuffer> _sheetBuffersByName = new(StringComparer.Ordinal);
    private SheetBuffer? _activeSheetBuffer;
    private bool _isRowStarted;
    private bool _isSheetStarted;
    private bool _disposed;
    private readonly List<(string styleName, OdfLength width)> _columnStyles = [];
    private readonly List<(string styleName, OdfLength? height, bool useOptimalHeight)> _rowStyles = [];
    private int _autoColumnStyleIndex = 0;
    private int _autoRowStyleIndex = 0;
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
    /// Initializes a new instance of the <see cref="OdsStreamWriter"/> class.
    /// 初始化 <see cref="OdsStreamWriter"/> 類別的新執行個體。
    /// </summary>
    /// <param name="outputStream">The target stream used to output the ODS document. / 用來輸出 ODS 文件的目標資料流。</param>
    /// <param name="version">The ODF specification version to write. / 要寫入的 ODF 規格版本。</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="outputStream"/> is <see langword="null"/>. / 當 <paramref name="outputStream"/> 為 <see langword="null"/> 時擲出。</exception>
    public OdsStreamWriter(Stream outputStream, OdfVersion version = OdfVersion.Odf14)
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
            byte[] bytes = Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.spreadsheet");
            s.Write(bytes, 0, bytes.Length);
        }

        // 2. 寫入預設的中階資料、樣式與資訊清單專案
        WriteDefaultMetaFiles();

        // 3. 開啟 content.xml 以進行資料流寫入
        var contentEntry = _zip.CreateEntry("content.xml", CompressionLevel.Fastest);
        _contentEntryStream = contentEntry.Open();

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false // 最小化大小
        };
        _writer = XmlWriter.Create(_contentEntryStream, settings);

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
    public void WriteStartSheet(string sheetName)
    {
        if (_disposed)
            return;
        if (_isSheetStarted)
            WriteEndSheet();
        _activeSheetBuffer = null;
        _writer.WriteStartElement("table", "table", OdfNamespaces.Table);
        _writer.WriteAttributeString("table", "name", OdfNamespaces.Table, sheetName);
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
    /// <exception cref="ArgumentException">Thrown when <paramref name="sheetName"/> is <see langword="null"/> or whitespace. / 當 <paramref name="sheetName"/> 為 <see langword="null"/> 或空白時擲出。</exception>
    public void SwitchToSheet(string sheetName)
    {
        if (_disposed)
            return;
        if (string.IsNullOrWhiteSpace(sheetName))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdsStreamWriter_SheetNameRequired"), nameof(sheetName));

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
    /// Writes a column definition.
    /// 寫入資料欄定義。
    /// </summary>
    /// <param name="width">The column width. / 資料欄寬度。</param>
    /// <param name="styleName">The style name; if <see langword="null"/>, one is generated automatically. / 樣式名稱，如果為 <see langword="null"/> 則自動產生。</param>
    public void WriteColumn(OdfLength width, string? styleName = null)
    {
        if (_disposed)
            return;
        string name = string.IsNullOrEmpty(styleName)
            ? $"co_auto_{++_autoColumnStyleIndex}"
            : styleName!;

        XmlWriter writer = CurrentWriter;
        writer.WriteStartElement("table", "table-column", OdfNamespaces.Table);
        writer.WriteAttributeString("table", "style-name", OdfNamespaces.Table, name);
        writer.WriteEndElement();

        _columnStyles.Add((name, width));
    }

    /// <summary>
    /// Starts writing a new data row.
    /// 開始寫入一個新的資料列。
    /// </summary>
    /// <param name="height">The row height. / 資料列高度。</param>
    /// <param name="styleName">The style name. / 樣式名稱。</param>
    /// <param name="useOptimalHeight">Whether to use optimal height. / 是否使用最佳高度。</param>
    public void WriteStartRow(double? height = null, string? styleName = null, bool useOptimalHeight = false)
    {
        if (_disposed)
            return;
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

        XmlWriter writer = CurrentWriter;
        writer.WriteStartElement("table", "table-row", OdfNamespaces.Table);
        if (!string.IsNullOrEmpty(resolvedStyleName))
        {
            writer.WriteAttributeString("table", "style-name", OdfNamespaces.Table, resolvedStyleName);
        }
    }

    /// <summary>
    /// Writes a string cell.
    /// 寫入字串型態的儲存格。
    /// </summary>
    /// <param name="value">The cell value. / 儲存格的值。</param>
    /// <param name="styleName">The style name. / 樣式名稱。</param>
    public void WriteCell(string value, string? styleName = null)
    {
        WriteCell(value.AsSpan(), styleName);
    }

    /// <summary>
    /// Writes a string cell.
    /// 寫入字串型態的儲存格。
    /// </summary>
    /// <param name="value">The cell value. / 儲存格的值。</param>
    /// <param name="styleName">The style name. / 樣式名稱。</param>
    public void WriteCell(ReadOnlySpan<char> value, string? styleName = null)
    {
        if (_disposed)
            return;
        XmlWriter writer = CurrentWriter;
        writer.WriteStartElement("table", "table-cell", OdfNamespaces.Table);
        writer.WriteAttributeString("office", "value-type", OdfNamespaces.Office, "string");
        if (!string.IsNullOrEmpty(styleName))
        {
            writer.WriteAttributeString("table", "style-name", OdfNamespaces.Table, styleName);
        }
        writer.WriteStartElement("text", "p", OdfNamespaces.Text);
        if (!value.IsEmpty)
        {
            writer.WriteString(ToStringValue(value));
        }
        writer.WriteEndElement(); // text:p
        writer.WriteEndElement(); // table:cell
    }

    /// <summary>
    /// Writes a string cell.
    /// 寫入字串型態的儲存格。
    /// </summary>
    /// <param name="value">The cell value. / 儲存格的值。</param>
    /// <param name="styleName">The style name. / 樣式名稱。</param>
    public void WriteCell(ReadOnlyMemory<char> value, string? styleName = null) =>
        WriteCell(value.Span, styleName);

    /// <summary>
    /// Writes a numeric cell.
    /// 寫入數值型態的儲存格。
    /// </summary>
    /// <param name="value">The cell numeric value. / 儲存格的數值。</param>
    /// <param name="styleName">The style name. / 樣式名稱。</param>
    public void WriteCell(double value, string? styleName = null)
    {
        if (_disposed)
            return;
        XmlWriter writer = CurrentWriter;
        writer.WriteStartElement("table", "table-cell", OdfNamespaces.Table);
        writer.WriteAttributeString("office", "value-type", OdfNamespaces.Office, "float");
        writer.WriteAttributeString("office", "value", OdfNamespaces.Office, value.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(styleName))
        {
            writer.WriteAttributeString("table", "style-name", OdfNamespaces.Table, styleName);
        }
        writer.WriteStartElement("text", "p", OdfNamespaces.Text);
        writer.WriteString(value.ToString(CultureInfo.InvariantCulture));
        writer.WriteEndElement(); // text:p
        writer.WriteEndElement(); // table:cell
    }

    /// <summary>
    /// Writes a date and time cell.
    /// 寫入日期時間型態的儲存格。
    /// </summary>
    /// <param name="value">The cell date and time value. / 儲存格的日期時間值。</param>
    /// <param name="styleName">The style name. / 樣式名稱。</param>
    /// <param name="timezoneNaive">Whether to ignore time zone conversion. / 是否忽略時區轉換。</param>
    public void WriteCell(DateTime value, string? styleName = null, bool timezoneNaive = false)
    {
        if (_disposed)
            return;
        XmlWriter writer = CurrentWriter;
        writer.WriteStartElement("table", "table-cell", OdfNamespaces.Table);
        writer.WriteAttributeString("office", "value-type", OdfNamespaces.Office, "date");

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

        writer.WriteAttributeString("office", "date-value", OdfNamespaces.Office, isoDate);
        if (!string.IsNullOrEmpty(styleName))
        {
            writer.WriteAttributeString("table", "style-name", OdfNamespaces.Table, styleName);
        }
        writer.WriteStartElement("text", "p", OdfNamespaces.Text);
        writer.WriteString(isoDate);
        writer.WriteEndElement(); // text:p
        writer.WriteEndElement(); // table:cell
    }

    /// <summary>
    /// Writes a Boolean cell.
    /// 寫入布林值型態的儲存格。
    /// </summary>
    /// <param name="value">The cell Boolean value. / 儲存格的布林值。</param>
    /// <param name="styleName">The style name. / 樣式名稱。</param>
    public void WriteCell(bool value, string? styleName = null)
    {
        if (_disposed)
            return;
        XmlWriter writer = CurrentWriter;
        writer.WriteStartElement("table", "table-cell", OdfNamespaces.Table);
        writer.WriteAttributeString("office", "value-type", OdfNamespaces.Office, "boolean");
        writer.WriteAttributeString("office", "boolean-value", OdfNamespaces.Office, value ? "true" : "false");
        if (!string.IsNullOrEmpty(styleName))
        {
            writer.WriteAttributeString("table", "style-name", OdfNamespaces.Table, styleName);
        }
        writer.WriteStartElement("text", "p", OdfNamespaces.Text);
        writer.WriteString(value ? "TRUE" : "FALSE");
        writer.WriteEndElement(); // text:p
        writer.WriteEndElement(); // table:cell
    }

    /// <summary>
    /// Writes an existing DOM subtree directly to the current worksheet or row position.
    /// 將既有 DOM 子樹直接寫入目前工作表或資料列位置。
    /// </summary>
    /// <param name="node">The DOM node to write. / 要寫入的 DOM 節點。</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is <see langword="null"/>. / 當 <paramref name="node"/> 為 <see langword="null"/> 時擲出。</exception>
    public void WriteNode(OdfNode node)
    {
        if (node is null)
            throw new ArgumentNullException(nameof(node));
        if (_disposed)
            return;

        Dictionary<string, string> namespaces = CreateFragmentNamespaceMap(node);
        int openElementsCount = 0;
        OdfXmlWriter.WriteNode(node, CurrentWriter, namespaces, ref openElementsCount, isRoot: false, depth: 1);
    }

    private static string ToStringValue(ReadOnlySpan<char> value)
    {
#if NETSTANDARD2_0
        return new string(value.ToArray());
#else
        return new string(value);
#endif
    }

    /// <summary>
    /// Writes a CSV stream to the current worksheet row by row with low memory usage.
    /// 將 CSV 資料流以低記憶體方式逐列寫入目前工作表。
    /// </summary>
    /// <param name="csvStream">The source CSV stream. / CSV 來源資料流。</param>
    /// <param name="firstRowAsHeader">Whether to treat the first row as column headers and skip writing it as data. / 是否將第一列視為欄位標題而略過資料寫入。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>A task that represents the asynchronous write operation. / 代表非同步寫入作業的工作。</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="csvStream"/> is <see langword="null"/>. / 當 <paramref name="csvStream"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="InvalidOperationException">Thrown when no worksheet has been started. / 當目前尚未開始任何工作表時擲出。</exception>
    public async Task WriteCsvStreamAsync(
        Stream csvStream,
        bool firstRowAsHeader = false,
        CancellationToken cancellationToken = default)
    {
        if (csvStream is null)
        {
            throw new ArgumentNullException(nameof(csvStream));
        }

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(OdsStreamWriter));
        }

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
    /// <param name="reader">The source data reader. / 資料來源讀取器。</param>
    /// <param name="includeColumnNames">Whether to write a row of column names first. / 是否先寫入資料行名稱列。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>A task that represents the asynchronous write operation. / 代表非同步寫入作業的工作。</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reader"/> is <see langword="null"/>. / 當 <paramref name="reader"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the writer has been disposed. / 當寫入器已釋放時擲出。</exception>
    /// <exception cref="InvalidOperationException">Thrown when no worksheet has been started. / 當目前尚未開始任何工作表時擲出。</exception>
    public async Task WriteDataAsync(
        DbDataReader reader,
        bool includeColumnNames = false,
        CancellationToken cancellationToken = default)
    {
        if (reader is null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(OdsStreamWriter));
        }

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
            CurrentWriter.WriteEndElement(); // table-row
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
                _writer.WriteEndElement(); // table:table
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
    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
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
            if (_isSheetStarted)
                WriteEndSheet();

            // 緩衝工作表片段一律透過 _writer.WriteRaw 寫入（而非直接寫原始位元組到
            // _contentEntryStream），讓 _writer 自己知道先前延後關閉的 <office:spreadsheet>
            // 起始標籤已被後續寫入操作結束，才能正確補上 '>'；否則會產生
            // <office:spreadsheet<table:table ...> 這種缺少 '>' 分隔的畸形 XML。
            WriteBufferedSheets();

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

            try
            { WriteStyles(); }
            catch (Exception ex)
            {
                OdfKitDiagnostics.Warn($"OdsStreamWriter 於 Dispose 寫入 styles.xml 失敗：{ex.Message}", ex);
            }
            _zip.Dispose();
        }

        _disposed = true;
    }

    #endregion

    private XmlWriter CurrentWriter => _activeSheetBuffer?.Writer ?? _writer;

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
    public async Task WriteSheetsAsync(
        IEnumerable<OdsSheetWriteJob> jobs,
        int maxConcurrency = 0,
        CancellationToken cancellationToken = default)
    {
        if (jobs is null)
        {
            throw new ArgumentNullException(nameof(jobs));
        }

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(OdsStreamWriter));
        }

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
                    var buffer = new SheetBuffer(job.SheetName);
                    var sheetWriter = new OdsSheetWriter(buffer.Writer);
                    await job.WriteAsync(sheetWriter, cancellationToken).ConfigureAwait(false);
                    sheetWriter.CloseOpenRow();
                    buffer.Close();
                    buffers[jobIndex] = buffer;
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
            _writer.WriteRaw(sheet.GetXml());
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

        public SheetBuffer(string sheetName)
        {
            Writer = XmlWriter.Create(
                _stream,
                new XmlWriterSettings
                {
                    Encoding = new UTF8Encoding(false),
                    Indent = false,
                    ConformanceLevel = ConformanceLevel.Fragment
                });
            Writer.WriteStartElement("table", "table", OdfNamespaces.Table);
            Writer.WriteAttributeString("table", "name", OdfNamespaces.Table, sheetName);
        }

        public XmlWriter Writer { get; }

        public void Close()
        {
            if (_closed)
                return;

            Writer.WriteEndElement();
            Writer.Flush();
            _closed = true;
        }

        public string GetXml()
        {
            Close();
            return Encoding.UTF8.GetString(_stream.ToArray());
        }

        public void Dispose()
        {
            Writer.Dispose();
            _stream.Dispose();
        }
    }

    /// <summary>
    /// Converts an ODS write operation to an asynchronous stream of read-only memory byte chunks for chunked HTTP transfer.
    /// 將 ODS 文件寫入作業轉換為非同步的唯讀記憶體位元組資料流，可用於 Chunked HTTP 傳輸。
    /// </summary>
    /// <param name="writeAction">The asynchronous delegate that performs writing. / 執行寫入的非同步委派。</param>
    /// <param name="version">The ODF specification version to write. / 要寫入的 ODF 規格版本。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>An asynchronous enumerator of read-only memory byte chunks. / 非同步唯讀記憶體位元組區段的列舉器。</returns>
    public static async IAsyncEnumerable<ReadOnlyMemory<byte>> ToAsyncEnumerable(
        Func<OdsStreamWriter, Task> writeAction,
        OdfVersion version = OdfVersion.Odf14,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (writeAction is null)
            throw new ArgumentNullException(nameof(writeAction));

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
    /// <param name="writeAction">The synchronous delegate that performs writing. / 執行寫入的同步委派。</param>
    /// <param name="version">The ODF specification version to write. / 要寫入的 ODF 規格版本。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>An asynchronous enumerator of read-only memory byte chunks. / 非同步唯讀記憶體位元組區段的列舉器。</returns>
    public static async IAsyncEnumerable<ReadOnlyMemory<byte>> ToAsyncEnumerable(
        Action<OdsStreamWriter> writeAction,
        OdfVersion version = OdfVersion.Odf14,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (writeAction is null)
            throw new ArgumentNullException(nameof(writeAction));

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
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public void Complete()
        {
            _isCompleted = true;
            _semaphore.Release();
        }

        public void Fault(Exception ex)
        {
            _exception = ex;
            _isCompleted = true;
            _semaphore.Release();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count == 0)
                return;
            var copy = new byte[count];
            Buffer.BlockCopy(buffer, offset, copy, 0, count);
            _queue.Enqueue(copy);
            _semaphore.Release();
        }

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

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}

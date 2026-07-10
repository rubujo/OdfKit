using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Core;

using OdfKit.Compliance;
namespace OdfKit.Spreadsheet;

/// <summary>
/// Provides the OdsStreamReader API.
/// 以低記憶體流式方式逐列讀取 ODS 試算表，適用於大型資料集。
/// </summary>
public sealed partial class OdsStreamReader : System.Data.Common.DbDataReader
{
    private readonly ZipArchive _zip;
    private readonly OdsStreamReaderOptions _options;
    private int _selectedSheetIndex;
    private bool _started;
    private bool _isFirstRowBuffered;
    private bool _closed;
    private XmlReader? _xmlReader;
    private Stream? _contentStream;
    private int _rowRepeatRemaining;
    private int _rowIndex = -1;
    private readonly List<object?> _currentRowData = new List<object?>();
    private readonly List<OdsCellValue> _currentRowCells = new List<OdsCellValue>();
    private readonly List<string> _sheetNames = new List<string>();
    private int _readInProgress;

    /// <summary>
    /// Gets the sheet name list scanned from the top level of <c>content.xml</c>.
    /// 工作表名稱清單（從 content.xml 頂層掃描取得）
    /// </summary>
    public IReadOnlyList<string> SheetNames => _sheetNames;

    /// <summary>
    /// Gets the current zero-based row number.
    /// 取得目前列號（0-based）
    /// </summary>
    public int RowIndex => _rowIndex;

    /// <summary>
    /// Gets the number of fields in the current row.
    /// 取得目前列的欄位數
    /// </summary>
    public override int FieldCount
    {
        get
        {
            if (!_started)
            {
                InitializeAndBufferFirstRow();
            }
            return _currentRowData.Count;
        }
    }

    /// <summary>
    /// Initializes an <see cref="OdsStreamReader"/> from a stream.
    /// 從資料流初始化 <see cref="OdsStreamReader"/>。
    /// </summary>
    /// <param name="stream">The ODS file stream, which must be ZIP-compatible. / ODS 檔案資料流，需為 ZIP 相容格式。</param>
    public OdsStreamReader(Stream stream) : this(stream, new OdsStreamReaderOptions())
    {
    }

    /// <summary>
    /// Initializes an <see cref="OdsStreamReader"/> from a stream with explicit resource limits.
    /// 使用明確資源限制，從資料流初始化 <see cref="OdsStreamReader"/>。
    /// </summary>
    /// <param name="stream">The ODS file stream. / ODS 檔案資料流。</param>
    /// <param name="options">The reader options. / 讀取器選項。</param>
    public OdsStreamReader(Stream stream, OdsStreamReaderOptions options)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));
        _options = ValidateOptions(options);
        _zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: _options.LeaveOpen);
        ScanSheetNames();
    }

    /// <summary>
    /// Initializes an <see cref="OdsStreamReader"/> from a path.
    /// 從路徑初始化 <see cref="OdsStreamReader"/>。
    /// </summary>
    /// <param name="path">The ODS file path. / ODS 檔案路徑。</param>
    public OdsStreamReader(string path) : this(path, new OdsStreamReaderOptions())
    {
    }

    /// <summary>
    /// Initializes an <see cref="OdsStreamReader"/> from a path with explicit resource limits.
    /// 使用明確資源限制，從路徑初始化 <see cref="OdsStreamReader"/>。
    /// </summary>
    /// <param name="path">The ODS file path. / ODS 檔案路徑。</param>
    /// <param name="options">The reader options. / 讀取器選項。</param>
    public OdsStreamReader(string path, OdsStreamReaderOptions options)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));
        _options = ValidateOptions(options);
        _zip = ZipFile.OpenRead(path);
        ScanSheetNames();
    }

    private void ScanSheetNames()
    {
        var entry = _zip.GetEntry("content.xml")
            ?? throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdsStreamReader_OdsNotFound_2"));

        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, CreateXmlSettings(_options));

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element &&
                reader.LocalName == "table" &&
                reader.NamespaceURI == OdfNamespaces.Table)
            {
                string? name = reader.GetAttribute("name", OdfNamespaces.Table);
                _sheetNames.Add(name ?? string.Empty);
                // 注意：XmlReader.Skip() 在 while(Read()) 循環中會跳過下一個兄弟節點；
                // 改用逐節點掃描（僅收集 table 元素名稱，忽略其內容）
            }
        }
    }

    /// <summary>
    /// Switches to the worksheet with the specified index. This must be called before the first <see cref="Read"/>.
    /// 切換至指定索引的工作表（必須在第一次 Read() 前呼叫）
    /// </summary>
    /// <param name="sheetIndex">The zero-based worksheet index. / 採 0 為基準的工作表索引。</param>
    public void SelectSheet(int sheetIndex)
    {
        if (_started)
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdsStreamReader_SelectsheetCalledBeforeFirst"));
        if (sheetIndex < 0 || sheetIndex >= _sheetNames.Count)
            throw new ArgumentOutOfRangeException(nameof(sheetIndex),
                OdfLocalizer.GetMessage("Err_OdsStreamReader_SheetIndexOutOfRange", sheetIndex.ToString(CultureInfo.InvariantCulture), _sheetNames.Count.ToString(CultureInfo.InvariantCulture)));
        _selectedSheetIndex = sheetIndex;
    }

    /// <summary>
    /// Reads the next row; returns <see langword="false"/> when the worksheet has ended.
    /// 讀取下一列；回傳 false 代表工作表結束
    /// </summary>
    public override bool Read()
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
            InitializeAndBufferFirstRow();
        }

        if (_isFirstRowBuffered)
        {
            _isFirstRowBuffered = false;
            return _currentRowData.Count > 0;
        }

        if (_rowRepeatRemaining > 0)
        {
            EnsureWithinLimit(_rowIndex + 2, _options.MaxRows);
            _rowRepeatRemaining--;
            _rowIndex++;
            return true;
        }

        return ReadNextRow();
    }

    /// <summary>
    /// Asynchronously reads the next row and observes cancellation during package XML I/O.
    /// 非同步讀取下一列，並在封裝 XML I/O 期間回應取消要求。
    /// </summary>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns><see langword="true"/> when a row is available; otherwise, <see langword="false"/>. / 有可用資料列時為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        EnterRead();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_started)
            {
                _started = true;
                await OpenReaderAtSheetAsync(cancellationToken).ConfigureAwait(false);
            }
            if (_isFirstRowBuffered)
            {
                _isFirstRowBuffered = false;
                return _currentRowData.Count > 0;
            }
            if (_rowRepeatRemaining > 0)
            {
                EnsureWithinLimit(_rowIndex + 2, _options.MaxRows);
                _rowRepeatRemaining--;
                _rowIndex++;
                return true;
            }
            return await ReadNextRowAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _readInProgress, 0);
        }
    }

    private void InitializeAndBufferFirstRow()
    {
        _started = true;
        OpenReaderAtSheet();
        if (ReadNextRow())
        {
            _isFirstRowBuffered = true;
        }
    }

    private void OpenReaderAtSheet()
    {
        var entry = _zip.GetEntry("content.xml")
            ?? throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdsStreamReader_OdsNotFound_2"));

        _contentStream = entry.Open();
        _xmlReader = XmlReader.Create(_contentStream, CreateXmlSettings(_options));

        int tableIndex = 0;
        while (_xmlReader.Read())
        {
            if (_xmlReader.NodeType == XmlNodeType.Element &&
                _xmlReader.LocalName == "table" &&
                _xmlReader.NamespaceURI == OdfNamespaces.Table)
            {
                if (tableIndex == _selectedSheetIndex)
                    return;

                tableIndex++;
                // ReadSubtree drain：disposal 後 _xmlReader 停在 </table:table> EndElement，
                // 外層 Read() 才能正確推進到下一個工作表
                if (!_xmlReader.IsEmptyElement)
                {
                    using var sub = _xmlReader.ReadSubtree();
                    while (sub.Read())
                    { }
                }
            }
        }
    }

    private async Task OpenReaderAtSheetAsync(CancellationToken cancellationToken)
    {
        var entry = _zip.GetEntry("content.xml")
            ?? throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdsStreamReader_OdsNotFound_2"));
        _contentStream = entry.Open();
        _xmlReader = XmlReader.Create(_contentStream, CreateXmlSettings(_options));
        int tableIndex = 0;
        while (await _xmlReader.ReadAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_xmlReader.NodeType != XmlNodeType.Element || _xmlReader.LocalName != "table" ||
                _xmlReader.NamespaceURI != OdfNamespaces.Table)
                continue;
            if (tableIndex++ == _selectedSheetIndex)
                return;
            if (!_xmlReader.IsEmptyElement)
                await _xmlReader.ReadOuterXmlAsync().ConfigureAwait(false);
        }
    }

    private bool ReadNextRow()
    {
        if (_xmlReader is null)
            return false;

        while (_xmlReader.Read())
        {
            if (_xmlReader.NodeType == XmlNodeType.Element)
            {
                if (_xmlReader.LocalName == "table-row" &&
                    _xmlReader.NamespaceURI == OdfNamespaces.Table)
                {
                    ParseCurrentRow(_xmlReader);
                    EnsureWithinLimit(_rowIndex + 2, _options.MaxRows);
                    _rowIndex++;
                    return true;
                }

                if (_xmlReader.LocalName == "table" &&
                    _xmlReader.NamespaceURI == OdfNamespaces.Table)
                    return false;
            }
            else if (_xmlReader.NodeType == XmlNodeType.EndElement &&
                     _xmlReader.LocalName == "table" &&
                     _xmlReader.NamespaceURI == OdfNamespaces.Table)
            {
                return false;
            }
        }

        return false;
    }

    private async Task<bool> ReadNextRowAsync(CancellationToken cancellationToken)
    {
        if (_xmlReader is null)
            return false;
        while (await _xmlReader.ReadAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_xmlReader.NodeType == XmlNodeType.Element && _xmlReader.LocalName == "table-row" &&
                _xmlReader.NamespaceURI == OdfNamespaces.Table)
            {
                string rowXml = await _xmlReader.ReadOuterXmlAsync().ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                using var stringReader = new StringReader(rowXml);
                using var rowReader = XmlReader.Create(stringReader, CreateXmlSettings(_options));
                rowReader.MoveToContent();
                ParseCurrentRow(rowReader);
                EnsureWithinLimit(_rowIndex + 2, _options.MaxRows);
                _rowIndex++;
                return true;
            }
            if ((_xmlReader.NodeType is XmlNodeType.Element or XmlNodeType.EndElement) &&
                _xmlReader.LocalName == "table" && _xmlReader.NamespaceURI == OdfNamespaces.Table)
                return false;
        }
        return false;
    }

    private void ParseCurrentRow(XmlReader rowReader)
    {
        int rowRepeat = ParseRepeat(rowReader.GetAttribute("number-rows-repeated", OdfNamespaces.Table), _options.MaxRepeatedRows);

        bool isEmpty = true;
        var cells = new List<(int Col, OdsCellValue Cell)>();
        int colIndex = 0;

        if (!rowReader.IsEmptyElement)
        {
            using var rowSub = rowReader.ReadSubtree();
            rowSub.Read();

            while (rowSub.Read())
            {
                if (rowSub.NodeType == XmlNodeType.Element &&
                    (rowSub.LocalName == "table-cell" || rowSub.LocalName == "covered-table-cell") &&
                    rowSub.NamespaceURI == OdfNamespaces.Table)
                {
                    int colRepeat = ParseRepeat(rowSub.GetAttribute("number-columns-repeated", OdfNamespaces.Table), _options.MaxRepeatedColumns);

                    string? valueType = rowSub.GetAttribute("value-type", OdfNamespaces.Office);
                    string? numValue = rowSub.GetAttribute("value", OdfNamespaces.Office);
                    string? dateValue = rowSub.GetAttribute("date-value", OdfNamespaces.Office);
                    string? boolValue = rowSub.GetAttribute("boolean-value", OdfNamespaces.Office);
                    string? timeValue = rowSub.GetAttribute("time-value", OdfNamespaces.Office);
                    string? stringValue = rowSub.GetAttribute("string-value", OdfNamespaces.Office);
                    string? currency = rowSub.GetAttribute("currency", OdfNamespaces.Office);
                    string? formula = rowSub.GetAttribute("formula", OdfNamespaces.Table);

                    string? textContent = ReadCellText(rowSub);
                    OdsCellValue cell = ParseCellValue(valueType, numValue, dateValue, boolValue, timeValue,
                        stringValue, formula, currency, textContent);
                    if (cell.Kind != OdsCellValueKind.Empty || cell.Formula is not null)
                    {
                        isEmpty = false;
                        for (int i = 0; i < colRepeat; i++)
                            cells.Add((colIndex + i, cell));
                    }

                    colIndex += colRepeat;
                    EnsureWithinLimit(colIndex, _options.MaxColumns);
                }
            }
        }

        // LibreOffice 以大型 number-rows-repeated 表示結尾空白列 — 跳過重複
        _rowRepeatRemaining = isEmpty ? 0 : rowRepeat - 1;

        _currentRowData.Clear();
        _currentRowCells.Clear();
        if (cells.Count > 0)
        {
            int maxCol = -1;
            foreach (var (col, _) in cells)
                if (col > maxCol)
                    maxCol = col;

            for (int i = 0; i <= maxCol; i++)
            {
                _currentRowData.Add(null);
                _currentRowCells.Add(new OdsCellValue(OdsCellValueKind.Empty, null, null, null, null, null));
            }

            foreach (var (col, cell) in cells)
            {
                _currentRowCells[col] = cell;
                _currentRowData[col] = cell.Value;
            }
        }
    }

    private static OdsCellValue ParseCellValue(
        string? valueType,
        string? numValue,
        string? dateValue,
        string? boolValue,
        string? timeValue,
        string? stringValue,
        string? formula,
        string? currency,
        string? textContent)
    {
        object? value;
        OdsCellValueKind kind;
        switch (valueType)
        {
            case "float":
                kind = OdsCellValueKind.Number;
                value = ParseNumber(numValue);
                break;
            case "percentage":
                kind = OdsCellValueKind.Percentage;
                value = ParseNumber(numValue);
                break;
            case "currency":
                kind = OdsCellValueKind.Currency;
                value = ParseNumber(numValue);
                break;

            case "boolean":
                kind = OdsCellValueKind.Boolean;
                value = bool.TryParse(boolValue, out bool boolean) ? boolean : null;
                break;

            case "date":
                kind = OdsCellValueKind.Date;
                value = DateTime.TryParse(dateValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime date)
                    ? date : dateValue;
                break;
            case "time":
                kind = OdsCellValueKind.Time;
                try
                { value = string.IsNullOrEmpty(timeValue) ? null : XmlConvert.ToTimeSpan(timeValue); }
                catch (FormatException) { value = timeValue; }
                break;

            case "string":
                kind = OdsCellValueKind.String;
                value = stringValue ?? textContent;
                break;

            default:
                kind = string.IsNullOrEmpty(valueType) && string.IsNullOrEmpty(textContent)
                    ? OdsCellValueKind.Empty : OdsCellValueKind.Unknown;
                value = textContent;
                break;
        }

        return new OdsCellValue(kind, value, formula, currency, textContent, valueType);
    }

    private static double? ParseNumber(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ? number : null;

    private string? ReadCellText(XmlReader cellReader)
    {
        if (cellReader.IsEmptyElement)
            return null;
        var paragraphs = new List<string>();
        using var subtree = cellReader.ReadSubtree();
        subtree.Read();
        while (subtree.Read())
        {
            if (subtree.NodeType == XmlNodeType.Element && subtree.LocalName == "p" &&
                subtree.NamespaceURI == OdfNamespaces.Text)
            {
                string paragraph = subtree.ReadElementContentAsString();
                paragraphs.Add(paragraph);
                EnsureWithinLimit(paragraphs.Sum(static value => value.Length) + paragraphs.Count - 1,
                    _options.MaxCellTextCharacters);
            }
        }
        return paragraphs.Count == 0 ? null : string.Join("\n", paragraphs);
    }

    /// <summary>
    /// Gets the raw value of the specified column in the current row. Float values become <see cref="double"/>, Boolean values become <see cref="bool"/>, date values become <see cref="DateTime"/>, and other values become strings.
    /// 取得目前列指定欄的原始值（float→double、boolean→bool、date→DateTime、其餘→string）
    /// </summary>
    /// <param name="ordinal">The zero-based field index. / 採 0 為基準的欄位索引。</param>
    public override object GetValue(int ordinal)
    {
        if (ordinal < 0 || ordinal >= _currentRowData.Count)
            return DBNull.Value;
        return _currentRowData[ordinal] ?? DBNull.Value;
    }

    /// <summary>
    /// Gets the structured value and source metadata for a cell in the current row.
    /// 取得目前資料列中儲存格的結構化值與來源中繼資料。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns>The structured cell value. / 結構化儲存格值。</returns>
    public OdsCellValue GetCell(int ordinal)
    {
        if (ordinal < 0 || ordinal >= _currentRowCells.Count)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        return _currentRowCells[ordinal];
    }

    private static int ParseRepeat(string? attr, int max)
    {
        if (!string.IsNullOrEmpty(attr) &&
            int.TryParse(attr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n > 1)
        {
            EnsureWithinLimit(n, max);
            return n;
        }
        return 1;
    }

    private static void EnsureWithinLimit(int value, int limit)
    {
        if (value > limit)
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_StreamReader_ResourceLimitExceeded",
                value.ToString(CultureInfo.InvariantCulture), limit.ToString(CultureInfo.InvariantCulture)));
    }

    private static XmlReaderSettings CreateXmlSettings(OdsStreamReaderOptions options) => new XmlReaderSettings
    {
        NameTable = OdfXmlNameTable.Create(),
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = options.MaxXmlCharactersInDocument,
        Async = true,
    };

    private void EnterRead()
    {
        if (Interlocked.Exchange(ref _readInProgress, 1) != 0)
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));
    }

    private static OdsStreamReaderOptions ValidateOptions(OdsStreamReaderOptions options)
    {
        if (options is null)
            throw new ArgumentNullException(nameof(options));
        if (options.MaxXmlCharactersInDocument < 0)
            throw new ArgumentOutOfRangeException(nameof(options));
        if (options.MaxRows <= 0 || options.MaxColumns <= 0 || options.MaxRepeatedRows <= 0 ||
            options.MaxRepeatedColumns <= 0 || options.MaxCellTextCharacters <= 0)
            throw new ArgumentOutOfRangeException(nameof(options));
        return options;
    }

    /// <summary>
    /// Releases unmanaged resources.
    /// 釋放非受控資源。
    /// </summary>
    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _closed = true;
            _xmlReader?.Dispose();
            _contentStream?.Dispose();
            _zip.Dispose();
        }
        base.Dispose(disposing);
    }
}

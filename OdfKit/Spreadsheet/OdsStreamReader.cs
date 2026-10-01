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
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1010:Generic interface should also be implemented",
    Justification = "DbDataReader defines the non-generic enumeration contract; adding a stateful generic sequence would conflict with its cursor semantics.")]
public sealed partial class OdsStreamReader : System.Data.Common.DbDataReader
{
    private readonly ZipArchive _zip;
    private readonly OdsStreamReaderOptions _options;
    private int _selectedSheetIndex;
    private bool _started;
    private bool _isFirstRowBuffered;
    private bool _hasRows;
    private bool _closed;
    private XmlReader? _xmlReader;
    private Stream? _contentStream;
    private int _rowRepeatRemaining;

    // 空白列區塊（number-rows-repeated）只回傳一列，其餘被略過的空白列數暫存於此，
    // 在讀到下一個 table-row 時計入 RowIndex，使後續列的列號與實際列號一致。
    private int _skippedEmptyRows;
    private int _rowIndex = -1;
    private readonly List<object?> _currentRowData = [];
    private readonly List<OdsCellValue> _currentRowCells = [];
    private readonly List<string> _sheetNames = [];
    private bool _sheetNamesScanned;
    private int _readInProgress;

    /// <summary>
    /// Gets the sheet name list scanned from the top level of <c>content.xml</c>.
    /// 工作表名稱清單（從 content.xml 頂層掃描取得）
    /// </summary>
    public IReadOnlyList<string> SheetNames
    {
        get
        {
            EnsureSheetNamesScanned();
            return _sheetNames;
        }
    }

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
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(stream, nameof(stream));
        _options = ValidateOptions(options);
        _zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: _options.LeaveOpen);
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
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(path, nameof(path));
        _options = ValidateOptions(options);
        _zip = ZipFile.OpenRead(path);
    }

    private void EnsureSheetNamesScanned()
    {
        if (_sheetNamesScanned)
            return;

        var entry = _zip.GetEntry("content.xml")
            ?? throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdsStreamReader_OdsNotFound_2"));

        _sheetNames.Clear();
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

        _sheetNamesScanned = true;
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
        if (sheetIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(sheetIndex),
                OdfLocalizer.GetMessage("Err_OdsStreamReader_SheetIndexOutOfRange", sheetIndex.ToString(CultureInfo.InvariantCulture), _sheetNames.Count.ToString(CultureInfo.InvariantCulture)));
        if (_sheetNamesScanned && sheetIndex >= _sheetNames.Count)
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
            // 只有 ReadNextRow() 解析到列時才會緩衝；第一列即使是空列也是一列（與非同步路徑及其後的空列一致）。
            _isFirstRowBuffered = false;
            return true;
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
                return true;
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

        ThrowSheetIndexOutOfRange(_selectedSheetIndex, tableIndex);
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
            if (tableIndex == _selectedSheetIndex)
                return;
            tableIndex++;
            if (!_xmlReader.IsEmptyElement)
            {
                using var subtree = _xmlReader.ReadSubtree();
                while (await subtree.ReadAsync().ConfigureAwait(false))
                    cancellationToken.ThrowIfCancellationRequested();
            }
        }
        ThrowSheetIndexOutOfRange(_selectedSheetIndex, tableIndex);
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
                    // 先取走上一個空白列區塊略過的列數：ParseCurrentRow 會以目前這一列覆寫它。
                    int skippedBefore = _skippedEmptyRows;
                    _skippedEmptyRows = 0;
                    ParseCurrentRow(_xmlReader);
                    _rowIndex += skippedBefore;
                    EnsureWithinLimit(_rowIndex + 2, _options.MaxRows);
                    _rowIndex++;
                    _hasRows = true;
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
                int skippedBefore = _skippedEmptyRows;
                _skippedEmptyRows = 0;
                await ParseCurrentRowAsync(_xmlReader, cancellationToken).ConfigureAwait(false);
                _rowIndex += skippedBefore;
                EnsureWithinLimit(_rowIndex + 2, _options.MaxRows);
                _rowIndex++;
                _hasRows = true;
                return true;
            }
            if ((_xmlReader.NodeType is XmlNodeType.Element or XmlNodeType.EndElement) &&
                _xmlReader.LocalName == "table" && _xmlReader.NamespaceURI == OdfNamespaces.Table)
                return false;
        }
        return false;
    }

    private static void ThrowSheetIndexOutOfRange(int sheetIndex, int sheetCount) =>
        throw new ArgumentOutOfRangeException(nameof(sheetIndex),
            OdfLocalizer.GetMessage("Err_OdsStreamReader_SheetIndexOutOfRange",
                sheetIndex.ToString(CultureInfo.InvariantCulture), sheetCount.ToString(CultureInfo.InvariantCulture)));

    // 單一儲存格元素上會用到的屬性。一次走訪屬性取得，而不是對每個屬性各做一次以名稱查詢的 GetAttribute。
    private struct CellAttributes
    {
        public string? ColumnsRepeated;
        public string? ValueType;
        public string? Value;
        public string? DateValue;
        public string? BooleanValue;
        public string? TimeValue;
        public string? StringValue;
        public string? Currency;
        public string? Formula;
    }

    // 空白儲存格為不可變物件，所有空白欄位共用同一個實例，避免每列為每個欄位配置一個佔位物件。
    private static readonly OdsCellValue s_emptyCell = new(OdsCellValueKind.Empty, null, null, null, null, null);

    // 每列重複使用的暫存清單；讀取由 EnterRead 保證不會同時進入，因此不需要額外同步。
    private readonly List<(int Col, OdsCellValue Cell)> _rowCellsScratch = [];

    private static CellAttributes ReadCellAttributes(XmlReader reader)
    {
        var attributes = new CellAttributes();
        if (!reader.HasAttributes || !reader.MoveToFirstAttribute())
        {
            return attributes;
        }

        do
        {
            string namespaceUri = reader.NamespaceURI;
            if (string.Equals(namespaceUri, OdfNamespaces.Office, StringComparison.Ordinal))
            {
                switch (reader.LocalName)
                {
                    case "value-type":
                        attributes.ValueType = reader.Value;
                        break;
                    case "value":
                        attributes.Value = reader.Value;
                        break;
                    case "date-value":
                        attributes.DateValue = reader.Value;
                        break;
                    case "boolean-value":
                        attributes.BooleanValue = reader.Value;
                        break;
                    case "time-value":
                        attributes.TimeValue = reader.Value;
                        break;
                    case "string-value":
                        attributes.StringValue = reader.Value;
                        break;
                    case "currency":
                        attributes.Currency = reader.Value;
                        break;
                }
            }
            else if (string.Equals(namespaceUri, OdfNamespaces.Table, StringComparison.Ordinal))
            {
                switch (reader.LocalName)
                {
                    case "number-columns-repeated":
                        attributes.ColumnsRepeated = reader.Value;
                        break;
                    case "formula":
                        attributes.Formula = reader.Value;
                        break;
                }
            }
        }
        while (reader.MoveToNextAttribute());

        reader.MoveToElement();
        return attributes;
    }

    private static bool IsCellElement(XmlReader reader) =>
        reader.NodeType == XmlNodeType.Element &&
        (reader.LocalName == "table-cell" || reader.LocalName == "covered-table-cell") &&
        string.Equals(reader.NamespaceURI, OdfNamespaces.Table, StringComparison.Ordinal);

    private static void AddCell(
        List<(int Col, OdsCellValue Cell)> cells,
        int colIndex,
        int colRepeat,
        in CellAttributes attributes,
        string? textContent,
        ref bool isEmpty)
    {
        OdsCellValue cell = ParseCellValue(
            attributes.ValueType, attributes.Value, attributes.DateValue, attributes.BooleanValue,
            attributes.TimeValue, attributes.StringValue, attributes.Formula, attributes.Currency, textContent);
        if (cell.Kind != OdsCellValueKind.Empty || cell.Formula is not null)
        {
            isEmpty = false;
            for (int i = 0; i < colRepeat; i++)
                cells.Add((colIndex + i, cell));
        }
    }

    // 不使用 ReadSubtree：每列與每格各建一個 XmlSubtreeReader（連同其命名空間與節點暫存）在實測中占了
    // 約一半的配置量。改以深度判斷列的範圍；讀完後讀取器停在列的 EndElement，與子樹釋放後的位置相同。
    private void ParseCurrentRow(XmlReader rowReader)
    {
        int rowRepeat = ParseRepeat(rowReader.GetAttribute("number-rows-repeated", OdfNamespaces.Table), _options.MaxRepeatedRows);

        bool isEmpty = true;
        List<(int Col, OdsCellValue Cell)> cells = _rowCellsScratch;
        cells.Clear();
        int colIndex = 0;

        if (!rowReader.IsEmptyElement)
        {
            int rowDepth = rowReader.Depth;
            while (rowReader.Read() && rowReader.Depth > rowDepth)
            {
                if (!IsCellElement(rowReader))
                {
                    continue;
                }

                CellAttributes attributes = ReadCellAttributes(rowReader);
                int colRepeat = ParseRepeat(attributes.ColumnsRepeated, _options.MaxRepeatedColumns);
                string? textContent = ReadCellText(rowReader);
                AddCell(cells, colIndex, colRepeat, in attributes, textContent, ref isEmpty);

                colIndex += colRepeat;
                EnsureWithinLimit(colIndex, _options.MaxColumns);
            }
        }

        CompleteCurrentRow(rowRepeat, isEmpty, cells);
    }

    private async Task ParseCurrentRowAsync(XmlReader rowReader, CancellationToken cancellationToken)
    {
        int rowRepeat = ParseRepeat(rowReader.GetAttribute("number-rows-repeated", OdfNamespaces.Table), _options.MaxRepeatedRows);
        bool isEmpty = true;
        List<(int Col, OdsCellValue Cell)> cells = _rowCellsScratch;
        cells.Clear();
        int colIndex = 0;

        if (!rowReader.IsEmptyElement)
        {
            int rowDepth = rowReader.Depth;
            while (await rowReader.ReadAsync().ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (rowReader.Depth <= rowDepth)
                {
                    break;
                }

                if (!IsCellElement(rowReader))
                {
                    continue;
                }

                CellAttributes attributes = ReadCellAttributes(rowReader);
                int colRepeat = ParseRepeat(attributes.ColumnsRepeated, _options.MaxRepeatedColumns);
                string? textContent = await ReadCellTextAsync(rowReader, cancellationToken).ConfigureAwait(false);
                AddCell(cells, colIndex, colRepeat, in attributes, textContent, ref isEmpty);

                colIndex += colRepeat;
                EnsureWithinLimit(colIndex, _options.MaxColumns);
            }
        }

        CompleteCurrentRow(rowRepeat, isEmpty, cells);
    }

    private void CompleteCurrentRow(int rowRepeat, bool isEmpty, List<(int Col, OdsCellValue Cell)> cells)
    {
        // LibreOffice 以大型 number-rows-repeated 表示結尾空白列 — 跳過重複
        _rowRepeatRemaining = isEmpty ? 0 : rowRepeat - 1;
        _skippedEmptyRows = isEmpty ? rowRepeat - 1 : 0;

        _currentRowData.Clear();
        _currentRowCells.Clear();
        if (cells.Count > 0)
        {
            // 欄索引嚴格遞增，最後一個項目就是最大欄。
            int maxCol = cells[cells.Count - 1].Col;

            for (int i = 0; i <= maxCol; i++)
            {
                _currentRowData.Add(null);
                _currentRowCells.Add(s_emptyCell);
            }

            foreach (var (col, cell) in cells)
            {
                _currentRowCells[col] = cell;
                _currentRowData[col] = cell.Value;
            }
        }

        cells.Clear();
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

    // 讀取儲存格的顯示文字：每個 text:p 各自讀成一段，段落之間以 "\n" 相接。
    // 不使用 ReadSubtree（理由見 ParseCurrentRow）。讀完每個段落後，讀取器停在該段落的 EndElement
    // （空元素段落則停在元素本身），外層迴圈的下一次 Read() 才會推進；不可在此之後再多讀一個節點，
    // 否則會略過緊接在後的下一個 text:p（相鄰段落中每隔一個會遺失）。
    private string? ReadCellText(XmlReader cellReader)
    {
        if (cellReader.IsEmptyElement)
            return null;
        int cellDepth = cellReader.Depth;
        string? first = null;
        StringBuilder? joined = null;
        int count = 0;
        int totalLength = 0;
        while (cellReader.Read() && cellReader.Depth > cellDepth)
        {
            if (!IsTextParagraph(cellReader))
                continue;
            if (count > 0)
            {
                totalLength = checked(totalLength + 1);
                EnsureWithinLimit(totalLength, _options.MaxCellTextCharacters);
            }

            AppendParagraph(ref first, ref joined, ref count, ReadParagraph(cellReader, ref totalLength));
        }

        return count == 0 ? null : joined?.ToString() ?? first;
    }

    private async Task<string?> ReadCellTextAsync(XmlReader cellReader, CancellationToken cancellationToken)
    {
        if (cellReader.IsEmptyElement)
            return null;
        int cellDepth = cellReader.Depth;
        string? first = null;
        StringBuilder? joined = null;
        int count = 0;
        int totalLength = 0;
        while (await cellReader.ReadAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (cellReader.Depth <= cellDepth)
                break;
            if (!IsTextParagraph(cellReader))
                continue;
            if (count > 0)
            {
                totalLength = checked(totalLength + 1);
                EnsureWithinLimit(totalLength, _options.MaxCellTextCharacters);
            }

            (string paragraph, int newTotal) = await ReadParagraphAsync(cellReader, totalLength, cancellationToken)
                .ConfigureAwait(false);
            totalLength = newTotal;
            AppendParagraph(ref first, ref joined, ref count, paragraph);
        }

        return count == 0 ? null : joined?.ToString() ?? first;
    }

    private static bool IsTextParagraph(XmlReader reader) =>
        reader.NodeType == XmlNodeType.Element &&
        reader.LocalName == "p" &&
        string.Equals(reader.NamespaceURI, OdfNamespaces.Text, StringComparison.Ordinal);

    // 讀取單一 text:p 的文字內容。行內元素依 ODF 的顯示文字語意處理：
    // text:span、text:a 等容器元素只取其內文；text:s 為 text:c 個空白（預設 1）；text:tab 為定位字元；
    // text:line-break 為換行；office:annotation 與 text:note 不是儲存格的顯示文字，整段略過。
    // 完成後讀取器停在 text:p 的 EndElement（空元素則停在元素本身）。
    private string ReadParagraph(XmlReader reader, ref int totalLength)
    {
        if (reader.IsEmptyElement)
            return string.Empty;
        int paragraphDepth = reader.Depth;
        string? single = null;
        StringBuilder? builder = null;
        bool advance = true;
        while (true)
        {
            if (advance && !reader.Read())
                break;
            advance = true;
            if (reader.Depth <= paragraphDepth)
                break;

            switch (reader.NodeType)
            {
                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace:
                    AddParagraphText(reader.Value, ref totalLength, ref single, ref builder);
                    break;
                case XmlNodeType.Element:
                    string? inlineText = GetInlineElementText(reader, ref totalLength, out bool skipSubtree);
                    if (inlineText is not null)
                    {
                        AddParagraphText(inlineText, ref totalLength, ref single, ref builder);
                    }
                    else if (skipSubtree)
                    {
                        reader.Skip();
                        advance = false;
                    }

                    break;
            }
        }

        return builder?.ToString() ?? single ?? string.Empty;
    }

    private async Task<(string Paragraph, int TotalLength)> ReadParagraphAsync(
        XmlReader reader,
        int totalLength,
        CancellationToken cancellationToken)
    {
        if (reader.IsEmptyElement)
            return (string.Empty, totalLength);
        int paragraphDepth = reader.Depth;
        string? single = null;
        StringBuilder? builder = null;
        bool advance = true;
        while (true)
        {
            if (advance && !await reader.ReadAsync().ConfigureAwait(false))
                break;
            advance = true;
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.Depth <= paragraphDepth)
                break;

            switch (reader.NodeType)
            {
                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace:
                    AddParagraphText(reader.Value, ref totalLength, ref single, ref builder);
                    break;
                case XmlNodeType.Element:
                    string? inlineText = GetInlineElementText(reader, ref totalLength, out bool skipSubtree);
                    if (inlineText is not null)
                    {
                        AddParagraphText(inlineText, ref totalLength, ref single, ref builder);
                    }
                    else if (skipSubtree)
                    {
                        await reader.SkipAsync().ConfigureAwait(false);
                        advance = false;
                    }

                    break;
            }
        }

        return (builder?.ToString() ?? single ?? string.Empty, totalLength);
    }

    // 行內元素對顯示文字的貢獻：回傳非 null 表示要加入的文字；skipSubtree 為 true 表示整個元素（含子節點）略過。
    // 其他元素（例如 text:span、text:a）回傳 null 且不略過，讀取器會自然進入其內容。
    private string? GetInlineElementText(XmlReader reader, ref int totalLength, out bool skipSubtree)
    {
        skipSubtree = false;
        if (string.Equals(reader.NamespaceURI, OdfNamespaces.Text, StringComparison.Ordinal))
        {
            switch (reader.LocalName)
            {
                case "s":
                    int spaces = ParseSpaceCount(reader.GetAttribute("c", OdfNamespaces.Text));
                    // 先檢查上限再配置，避免 text:c 宣告極大值時配置巨量字串。
                    totalLength = checked(totalLength + spaces);
                    EnsureWithinLimit(totalLength, _options.MaxCellTextCharacters);
                    totalLength -= spaces;
                    return new string(' ', spaces);
                case "tab":
                    return "\t";
                case "line-break":
                    return "\n";
                case "note":
                    skipSubtree = true;
                    return null;
            }
        }
        else if (string.Equals(reader.NamespaceURI, OdfNamespaces.Office, StringComparison.Ordinal) &&
                 (reader.LocalName == "annotation" || reader.LocalName == "annotation-end"))
        {
            skipSubtree = true;
        }

        return null;
    }

    private int ParseSpaceCount(string? attribute)
    {
        if (attribute is null || attribute.Length == 0)
            return 1;
        // 宣告值非數字時視為 1；溢位（純數字但超過 int）時視為超過上限，由呼叫端的上限檢查拒絕。
        if (int.TryParse(attribute, NumberStyles.None, CultureInfo.InvariantCulture, out int count))
            return Math.Min(count, _options.MaxCellTextCharacters + 1);
        foreach (char digit in attribute)
        {
            if (digit < '0' || digit > '9')
                return 1;
        }

        return _options.MaxCellTextCharacters + 1;
    }

    private void AddParagraphText(string text, ref int totalLength, ref string? single, ref StringBuilder? builder)
    {
        totalLength = checked(totalLength + text.Length);
        EnsureWithinLimit(totalLength, _options.MaxCellTextCharacters);
        if (builder is not null)
        {
            builder.Append(text);
        }
        else if (single is null)
        {
            single = text;
        }
        else
        {
            string previous = single;
            builder = new StringBuilder(previous, previous.Length + text.Length).Append(text);
            single = null;
        }
    }

    // 單一段落是最常見的情況，直接回傳該字串；第二段起才建立 StringBuilder，結果與 string.Join("\n", ...) 相同。
    private static void AppendParagraph(ref string? first, ref StringBuilder? joined, ref int count, string paragraph)
    {
        if (count == 0)
        {
            first = paragraph;
        }
        else
        {
            joined ??= new StringBuilder(first);
            joined.Append('\n').Append(paragraph);
        }

        count++;
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
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(options, nameof(options));
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
            // 前段任一 Dispose 拋出都不得略過 _zip.Dispose()，否則 ZipArchive 持有的
            // 底層串流會一併洩漏。讀取模式不寫中央目錄，因此僅是資源問題而非輸出損毀。
            try
            {
                _xmlReader?.Dispose();
                _contentStream?.Dispose();
            }
            finally
            {
                _zip.Dispose();
            }
        }
        base.Dispose(disposing);
    }
}

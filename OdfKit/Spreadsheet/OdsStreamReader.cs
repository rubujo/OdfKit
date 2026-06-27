using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml;
using OdfKit.Core;

using OdfKit.Compliance;
namespace OdfKit.Spreadsheet;

/// <summary>
/// 以低記憶體流式方式逐列讀取 ODS 試算表，適用於大型資料集。
/// 整個讀取過程使用 SAX 風格 XmlReader，記憶體佔用遠低於 DOM 讀取路徑。
/// </summary>
public sealed partial class OdsStreamReader : System.Data.Common.DbDataReader
{
    private const int MaxRowRepeat = 1_048_576;
    private const int MaxColRepeat = 16_384;

    private readonly ZipArchive _zip;
    private int _selectedSheetIndex;
    private bool _started;
    private bool _isFirstRowBuffered;
    private bool _closed;
    private XmlReader? _xmlReader;
    private Stream? _contentStream;
    private int _rowRepeatRemaining;
    private int _rowIndex = -1;
    private readonly List<object?> _currentRowData = new List<object?>();
    private readonly List<string> _sheetNames = new List<string>();

    /// <summary>
    /// 工作表名稱清單（從 content.xml 頂層掃描取得）
    /// </summary>
    public IReadOnlyList<string> SheetNames => _sheetNames;

    /// <summary>
    /// 取得目前列號（0-based）
    /// </summary>
    public int RowIndex => _rowIndex;

    /// <summary>
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
    /// 從資料流初始化 <see cref="OdsStreamReader"/>。
    /// </summary>
    /// <param name="stream">ODS 檔案資料流（需為 ZIP 相容格式）</param>
    public OdsStreamReader(Stream stream)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));
        _zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        ScanSheetNames();
    }

    /// <summary>
    /// 從路徑初始化 <see cref="OdsStreamReader"/>。
    /// </summary>
    /// <param name="path">ODS 檔案路徑</param>
    public OdsStreamReader(string path)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));
        _zip = ZipFile.OpenRead(path);
        ScanSheetNames();
    }

    private void ScanSheetNames()
    {
        var entry = _zip.GetEntry("content.xml")
            ?? throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdsStreamReader_OdsNotFound_2"));

        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, CreateXmlSettings());

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
    /// 切換至指定索引的工作表（必須在第一次 Read() 前呼叫）
    /// </summary>
    /// <param name="sheetIndex">工作表索引（0-based）</param>
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
    /// 讀取下一列；回傳 false 代表工作表結束
    /// </summary>
    public override bool Read()
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
            _rowRepeatRemaining--;
            _rowIndex++;
            return true;
        }

        return ReadNextRow();
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
        _xmlReader = XmlReader.Create(_contentStream, CreateXmlSettings());

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
                    ParseCurrentRow();
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

    private void ParseCurrentRow()
    {
        int rowRepeat = ParseRepeat(_xmlReader!.GetAttribute("number-rows-repeated", OdfNamespaces.Table), MaxRowRepeat);

        bool isEmpty = true;
        var cells = new List<(int Col, object? Val)>();
        int colIndex = 0;

        if (!_xmlReader.IsEmptyElement)
        {
            using var rowSub = _xmlReader.ReadSubtree();
            rowSub.Read();

            while (rowSub.Read())
            {
                if (rowSub.NodeType == XmlNodeType.Element &&
                    (rowSub.LocalName == "table-cell" || rowSub.LocalName == "covered-table-cell") &&
                    rowSub.NamespaceURI == OdfNamespaces.Table)
                {
                    int colRepeat = ParseRepeat(rowSub.GetAttribute("number-columns-repeated", OdfNamespaces.Table), MaxColRepeat);

                    string? valueType = rowSub.GetAttribute("value-type", OdfNamespaces.Office);
                    string? numValue = rowSub.GetAttribute("value", OdfNamespaces.Office);
                    string? dateValue = rowSub.GetAttribute("date-value", OdfNamespaces.Office);
                    string? boolValue = rowSub.GetAttribute("boolean-value", OdfNamespaces.Office);
                    string? formula = rowSub.GetAttribute("formula", OdfNamespaces.Table);

                    string? textContent = null;
                    if (!rowSub.IsEmptyElement)
                    {
                        using var cellSub = rowSub.ReadSubtree();
                        cellSub.Read();
                        while (cellSub.Read())
                        {
                            if (cellSub.NodeType == XmlNodeType.Element &&
                                cellSub.LocalName == "p" &&
                                cellSub.NamespaceURI == OdfNamespaces.Text)
                            {
                                textContent = cellSub.ReadElementContentAsString();
                                break;
                            }
                        }
                    }

                    object? val = GetCellValue(valueType, numValue, dateValue, boolValue, formula, textContent);
                    if (val is not null)
                    {
                        isEmpty = false;
                        for (int i = 0; i < colRepeat; i++)
                            cells.Add((colIndex + i, val));
                    }

                    colIndex += colRepeat;
                }
            }
        }

        // LibreOffice 以大型 number-rows-repeated 表示結尾空白列 — 跳過重複
        _rowRepeatRemaining = isEmpty ? 0 : rowRepeat - 1;

        _currentRowData.Clear();
        if (cells.Count > 0)
        {
            int maxCol = -1;
            foreach (var (col, _) in cells)
                if (col > maxCol)
                    maxCol = col;

            for (int i = 0; i <= maxCol; i++)
                _currentRowData.Add(null);

            foreach (var (col, val) in cells)
                _currentRowData[col] = val;
        }
    }

    private static object? GetCellValue(
        string? valueType,
        string? numValue,
        string? dateValue,
        string? boolValue,
        string? formula,
        string? textContent)
    {
        if (!string.IsNullOrEmpty(formula))
            return formula;

        switch (valueType)
        {
            case "float":
                if (!string.IsNullOrEmpty(numValue) &&
                    double.TryParse(numValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    return d;
                return null;

            case "boolean":
                if (!string.IsNullOrEmpty(boolValue) &&
                    bool.TryParse(boolValue, out bool b))
                    return b;
                return null;

            case "date":
                if (!string.IsNullOrEmpty(dateValue))
                {
                    if (DateTime.TryParse(dateValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dt))
                        return dt;
                    return dateValue;
                }
                return null;

            case "string":
                return string.IsNullOrEmpty(textContent) ? null : (object)textContent!;

            default:
                return string.IsNullOrEmpty(textContent) ? null : (object)textContent!;
        }
    }

    /// <summary>
    /// 取得目前列指定欄的原始值（float→double、boolean→bool、date→DateTime、其餘→string）
    /// </summary>
    /// <param name="ordinal">欄位索引（0-based）</param>
    public override object GetValue(int ordinal)
    {
        if (ordinal < 0 || ordinal >= _currentRowData.Count)
            return null!;
        return _currentRowData[ordinal]!;
    }

    private static int ParseRepeat(string? attr, int max)
    {
        if (!string.IsNullOrEmpty(attr) &&
            int.TryParse(attr, out int n) && n > 1)
            return Math.Min(n, max);
        return 1;
    }

    private static XmlReaderSettings CreateXmlSettings() => new XmlReaderSettings
    {
        NameTable = OdfXmlNameTable.Create(),
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
    };

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

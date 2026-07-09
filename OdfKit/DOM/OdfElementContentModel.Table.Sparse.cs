using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Text;
using OdfKit.Core;
using OdfKit.Spreadsheet;

namespace OdfKit.DOM;

/// <summary>
/// Partial: sparse cell pages and materialization for TableTableElement.
/// Partial：TableTableElement 的稀疏儲存格分頁與具現化。
/// </summary>
public partial class TableTableElement
{
    internal struct TablePageState
    {
        public byte[]? CompressedBytes;
        public bool IsHot;
        public long LastAccessTick;
    }
    internal TablePageState[][]? _pageStates;
    internal Dictionary<long, SafeHandle>? _hotFormulaPtrs;
    private readonly object _hotFormulaLock = new();

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct NativeCell
    {
        [FieldOffset(0)] public byte Type; // 0=None/Empty, 1=Double, 2=Bool, 3=Ticks
        [FieldOffset(8)] public double FloatValue;
        [FieldOffset(8)] public long Ticks;
        [FieldOffset(8)] public bool BoolValue;

        [FieldOffset(16)] public IntPtr StyleNamePtr;
        [FieldOffset(24)] public IntPtr FormulaPtr;
        [FieldOffset(32)] public IntPtr StringValuePtr;
    }

    private long _accessCounter;
    private const int DefaultMaxHotPages = 16;
    private int _maxHotPages = DefaultMaxHotPages;

    /// <summary>
    /// Gets or sets the maximum number of "hot" (uncompressed) native cell pages kept resident in memory for this table before the least-recently-used page is evicted to compressed cold storage; each page is 128×128 cells (~655 KB uncompressed), so the default of 16 caps hot memory at roughly 10.5 MB regardless of total sheet size.
    /// 取得或設定此表格保留在記憶體中的「熱」（未壓縮）原生儲存格頁面數量上限，超過時最久未存取的頁面會被淘汰至壓縮冷儲存；每頁為 128×128 個儲存格（未壓縮約 655 KB），因此預設值 16 會將熱記憶體鎖定在約 10.5 MB，與工作表總大小無關。
    /// </summary>
    /// <remarks>
    /// Raising this value trades memory for fewer decompress/recompress cycles when editing large sparse sheets with a wide, scattered access pattern; lowering it further bounds memory usage at the cost of more frequent (de)compression.
    /// 調高此值可用記憶體換取較少的解壓/重新壓縮次數，適合存取範圍分散的大型稀疏表格；調低則可進一步限制記憶體用量，但會增加（解）壓縮次數。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a value less than 1. / 當設定值小於 1 時擲出。</exception>
    public int MaxHotPages
    {
        get => _maxHotPages;
        set
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _maxHotPages = value;
        }
    }

    private bool _isDisposed;

    private static IntPtr StringToUtf8Ptr(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return IntPtr.Zero;
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(s);
        IntPtr ptr = Marshal.AllocHGlobal(bytes.Length + 1);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        Marshal.WriteByte(ptr, bytes.Length, 0); // null terminator
        return ptr;
    }

    private static string? Utf8PtrToString(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
            return null;
        int len = 0;
        while (Marshal.ReadByte(ptr, len) != 0)
        {
            len++;
        }
        byte[] bytes = new byte[len];
        Marshal.Copy(ptr, bytes, 0, len);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private static void FreePtr(ref IntPtr ptr)
    {
        if (ptr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(ptr);
            ptr = IntPtr.Zero;
        }
    }

    private static long GetSparseCellKey(int rowIndex, int columnIndex)
        => ((long)rowIndex << 32) | (uint)columnIndex;

    private static int GetRowFromSparseCellKey(long key)
        => (int)(key >> 32);

    private static int GetColumnFromSparseCellKey(long key)
        => (int)(key & 0xFFFFFFFF);

    private readonly record struct SparseCellSnapshot(
        int RowIndex,
        int ColumnIndex,
        byte Type,
        double NumberValue,
        bool BooleanValue,
        long Ticks,
        string? Text,
        string? StyleName,
        string? Formula);

    private void ShiftSparseRows(int sourceStartRow, int rowDelta, int? clearFromRow = null)
    {
        GetSparseCellBounds(out int maxRow, out int maxColumn);
        if (maxRow < 0 || maxColumn < 0)
        {
            return;
        }

        List<SparseCellSnapshot> snapshots = SnapshotSparseRows(sourceStartRow, maxRow - sourceStartRow + 1, maxColumn);

        int clearStart = clearFromRow ?? sourceStartRow;
        for (int row = clearStart; row <= maxRow; row++)
        {
            for (int column = 0; column <= maxColumn; column++)
            {
                ClearSparseCell(row, column);
            }
        }

        foreach (SparseCellSnapshot snapshot in snapshots)
        {
            int targetRow = snapshot.RowIndex + rowDelta;
            if (targetRow >= 0)
            {
                SetSparseCellSnapshot(targetRow, snapshot.ColumnIndex, snapshot);
            }
        }
    }

    private List<SparseCellSnapshot> SnapshotSparseRows(int sourceStartRow, int count)
    {
        GetSparseCellBounds(out int maxRow, out int maxColumn);
        if (maxRow < 0 || maxColumn < 0 || count <= 0)
        {
            return [];
        }

        return SnapshotSparseRows(sourceStartRow, count, maxColumn);
    }

    private List<SparseCellSnapshot> SnapshotSparseRows(int sourceStartRow, int count, int maxColumn)
    {
        var snapshots = new List<SparseCellSnapshot>();
        int endRow = sourceStartRow + count - 1;
        for (int row = sourceStartRow; row <= endRow; row++)
        {
            for (int column = 0; column <= maxColumn; column++)
            {
                if (TryGetSparseCellData(
                    row,
                    column,
                    out byte type,
                    out double numberValue,
                    out bool booleanValue,
                    out long ticks,
                    out string? text,
                    out string? styleName,
                    out string? formula))
                {
                    snapshots.Add(new SparseCellSnapshot(row, column, type, numberValue, booleanValue, ticks, text, styleName, formula));
                }
            }
        }

        return snapshots;
    }

    private void RestoreSparseRowSnapshots(List<SparseCellSnapshot> snapshots, int sourcePosition, int targetPosition)
    {
        foreach (SparseCellSnapshot snapshot in snapshots)
        {
            int targetRow = targetPosition + (snapshot.RowIndex - sourcePosition);
            if (targetRow >= 0)
            {
                SetSparseCellSnapshot(targetRow, snapshot.ColumnIndex, snapshot);
            }
        }
    }

    private void GetSparseCellBounds(out int maxRow, out int maxColumn)
    {
        maxRow = -1;
        maxColumn = -1;

        if (_nativePages is not null)
        {
            for (int pageRow = 0; pageRow < _nativePages.Length; pageRow++)
            {
                IntPtr[]? rowPages = _nativePages[pageRow];
                if (rowPages is null)
                    continue;

                for (int pageColumn = 0; pageColumn < rowPages.Length; pageColumn++)
                {
                    IntPtr ptr = rowPages[pageColumn];
                    if (ptr == IntPtr.Zero && _pageStates is not null && pageRow < _pageStates.Length && _pageStates[pageRow] is not null && pageColumn < _pageStates[pageRow].Length && _pageStates[pageRow][pageColumn].CompressedBytes is not null)
                    {
                        EnsurePageAllocated(pageRow * PageSize, pageColumn * PageSize);
                        ptr = _nativePages[pageRow][pageColumn];
                    }

                    if (ptr == IntPtr.Zero)
                        continue;

                    unsafe
                    {
                        NativeCell* cells = (NativeCell*)ptr;
                        for (int index = 0; index < PageSize * PageSize; index++)
                        {
                            if (cells[index].Type != 0 || cells[index].StyleNamePtr != IntPtr.Zero || cells[index].FormulaPtr != IntPtr.Zero || cells[index].StringValuePtr != IntPtr.Zero)
                            {
                                int row = (pageRow * PageSize) + (index / PageSize);
                                int column = (pageColumn * PageSize) + (index % PageSize);
                                if (row > maxRow)
                                    maxRow = row;
                                if (column > maxColumn)
                                    maxColumn = column;
                            }
                        }
                    }
                }
            }
        }

        lock (_hotFormulaLock)
        {
            if (_hotFormulaPtrs is not null)
            {
                foreach (long key in _hotFormulaPtrs.Keys)
                {
                    int row = GetRowFromSparseCellKey(key);
                    int column = GetColumnFromSparseCellKey(key);
                    if (row > maxRow)
                        maxRow = row;
                    if (column > maxColumn)
                        maxColumn = column;
                }
            }
        }
    }

    private bool TryGetSparseCellView(int rowIndex, int columnIndex, out OdfCellView view)
    {
        if (TryGetSparseCellData(rowIndex, columnIndex, out byte type, out double number, out bool boolean, out long ticks, out string? text, out string? style, out string? formula))
        {
            OdfCellData data = type switch
            {
                1 => OdfCellData.FromNumber(number, style, formula),
                2 => OdfCellData.FromBoolean(boolean, style, formula),
                3 => OdfCellData.FromDateTime(new DateTime(ticks), style, formula),
                _ when text is not null => OdfCellData.FromText(text, style, formula),
                _ => OdfCellData.Empty(style, formula),
            };
            view = new OdfCellView(rowIndex, columnIndex, data);
            return true;
        }

        view = default;
        return false;
    }

    private static OdfCellView CreateDomCellView(int rowIndex, int columnIndex, TableTableCellElement cell)
    {
        string? styleName = cell.StyleName;
        string? formula = cell.GetAttribute("formula", OdfNamespaces.Table);
        string? valueType = cell.ValueType;
        OdfCellData data = valueType switch
        {
            "float" => double.TryParse(cell.GetAttribute("value", OdfNamespaces.Office), NumberStyles.Any, CultureInfo.InvariantCulture, out double number)
                ? OdfCellData.FromNumber(number, styleName, formula)
                : OdfCellData.FromText(cell.TextContent, styleName, formula),
            "boolean" => OdfCellData.FromBoolean(string.Equals(cell.GetAttribute("boolean-value", OdfNamespaces.Office), "true", StringComparison.OrdinalIgnoreCase), styleName, formula),
            "date" => DateTime.TryParse(cell.GetAttribute("date-value", OdfNamespaces.Office), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime date)
                ? OdfCellData.FromDateTime(date, styleName, formula)
                : OdfCellData.FromText(cell.TextContent, styleName, formula),
            "string" => OdfCellData.FromText(cell.GetAttribute("string-value", OdfNamespaces.Office) ?? cell.TextContent, styleName, formula),
            _ when formula is not null || styleName is not null => OdfCellData.Empty(styleName, formula),
            _ => OdfCellData.FromText(cell.TextContent, styleName, formula),
        };

        return new OdfCellView(rowIndex, columnIndex, data);
    }

    private static int GetPositiveRepeat(OdfElement element, string localName)
    {
        string? value = element.GetAttribute(localName, OdfNamespaces.Table);
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int repeat) && repeat > 0
            ? repeat
            : 1;
    }

    private void SetHotFormula(int rowIndex, int columnIndex, string? formula)
    {
        long key = GetSparseCellKey(rowIndex, columnIndex);
        lock (_hotFormulaLock)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(TableTableElement));
            }

            if (_hotFormulaPtrs is not null && _hotFormulaPtrs.TryGetValue(key, out SafeHandle? existing))
            {
                existing.Dispose();
                if (formula is null)
                {
                    _hotFormulaPtrs.Remove(key);
                    if (_hotFormulaPtrs.Count == 0)
                    {
                        _hotFormulaPtrs = null;
                    }
                    return;
                }

                _hotFormulaPtrs[key] = HotFormulaHandle.FromString(formula);
                return;
            }

            if (formula is null)
                return;

            _hotFormulaPtrs ??= new Dictionary<long, SafeHandle>();
            _hotFormulaPtrs[key] = HotFormulaHandle.FromString(formula);
        }
    }

    private string? GetHotFormula(int rowIndex, int columnIndex)
    {
        long key = GetSparseCellKey(rowIndex, columnIndex);
        lock (_hotFormulaLock)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(TableTableElement));
            }

            return _hotFormulaPtrs is not null && _hotFormulaPtrs.TryGetValue(key, out SafeHandle? handle)
                ? Utf8PtrToString(handle.DangerousGetHandle())
                : null;
        }
    }

    private void RemoveHotFormula(int rowIndex, int columnIndex)
        => SetHotFormula(rowIndex, columnIndex, null);

    private void FreeHotFormulaStore()
    {
        lock (_hotFormulaLock)
        {
            if (_hotFormulaPtrs is null)
                return;

            foreach (SafeHandle handle in _hotFormulaPtrs.Values)
            {
                handle.Dispose();
            }

            _hotFormulaPtrs = null;
        }
    }

    private sealed class HotFormulaHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private HotFormulaHandle()
            : base(ownsHandle: true)
        {
        }

        public static HotFormulaHandle FromString(string formula)
        {
            var handle = new HotFormulaHandle();
            handle.SetHandle(StringToUtf8Ptr(formula));
            return handle;
        }

        protected override bool ReleaseHandle()
        {
            IntPtr ptr = handle;
            FreePtr(ref ptr);
            handle = IntPtr.Zero;
            return true;
        }
    }

    /// <summary>
    /// 解構子，用於最後防禦性釋放非託管記憶體。
    /// </summary>
    ~TableTableElement()
    {
        Dispose(false);
    }

    /// <summary>
    /// Releases unmanaged resources.
    /// 釋放非託管頁面記憶體。
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            FreeNativePages();
            FreeHotFormulaStore();
            _isDisposed = true;
        }
    }

    private void FreeNativePages()
    {
        if (_nativePages is not null)
        {
            for (int i = 0; i < _nativePages.Length; i++)
            {
                if (_nativePages[i] is not null)
                {
                    for (int j = 0; j < _nativePages[i].Length; j++)
                    {
                        IntPtr ptr = _nativePages[i][j];
                        if (ptr == IntPtr.Zero && _pageStates is not null && i < _pageStates.Length && _pageStates[i] is not null && j < _pageStates[i].Length)
                        {
                            var state = _pageStates[i][j];
                            if (state.CompressedBytes is not null)
                            {
                                _pageStates[i][j].CompressedBytes = null;
                            }
                        }

                        if (ptr != IntPtr.Zero)
                        {
                            ReleasePageMemory(ptr);
                            if (_nativePages[i] is not null && j < _nativePages[i].Length)
                            {
                                _nativePages[i][j] = IntPtr.Zero;
                            }
                        }
                    }
                }
            }
            _nativePages = null;
        }
        _pageStates = null;
    }

    private bool TryGetSparseCellData(
        int rowIndex,
        int columnIndex,
        out byte type,
        out double dVal,
        out bool bVal,
        out long ticks,
        out string? text,
        out string? style,
        out string? formula)
    {
        type = 0;
        dVal = 0;
        bVal = false;
        ticks = 0;
        text = null;
        style = null;
        formula = null;

        bool hasData = false;

        string? hotFormula = GetHotFormula(rowIndex, columnIndex);
        if (hotFormula is not null)
        {
            formula = hotFormula;
            hasData = true;
        }

        if (_nativePages is not null)
        {
            int pageRow = rowIndex / PageSize;
            int pageCol = columnIndex / PageSize;
            if (pageRow < _nativePages.Length && _nativePages[pageRow] is not null && pageCol < _nativePages[pageRow].Length)
            {
                IntPtr ptr = _nativePages[pageRow][pageCol];
                if (ptr == IntPtr.Zero && _pageStates is not null && pageRow < _pageStates.Length && _pageStates[pageRow] is not null && pageCol < _pageStates[pageRow].Length && _pageStates[pageRow][pageCol].CompressedBytes is not null)
                {
                    EnsurePageAllocated(rowIndex, columnIndex);
                    ptr = _nativePages[pageRow][pageCol];
                }
                else if (ptr != IntPtr.Zero && _pageStates is not null && pageRow < _pageStates.Length && _pageStates[pageRow] is not null && pageCol < _pageStates[pageRow].Length)
                {
                    _pageStates[pageRow][pageCol].LastAccessTick = System.Threading.Interlocked.Increment(ref _accessCounter);
                }

                if (ptr != IntPtr.Zero)
                {
                    int index = (rowIndex % PageSize) * PageSize + (columnIndex % PageSize);
                    unsafe
                    {
                        NativeCell* cells = (NativeCell*)ptr;
                        NativeCell* cell = &cells[index];
                        if (cell->Type != 0 || cell->StyleNamePtr != IntPtr.Zero || cell->FormulaPtr != IntPtr.Zero || cell->StringValuePtr != IntPtr.Zero)
                        {
                            type = cell->Type;
                            dVal = cell->FloatValue;
                            bVal = cell->BoolValue;
                            ticks = cell->Ticks;
                            style = Utf8PtrToString(cell->StyleNamePtr);
                            formula ??= Utf8PtrToString(cell->FormulaPtr);
                            text = Utf8PtrToString(cell->StringValuePtr);
                            hasData = true;
                        }
                    }
                }
            }
        }

        return hasData;
    }

    private void ClearSparseCell(int rowIndex, int columnIndex)
    {
        if (_nativePages is not null)
        {
            int pageRow = rowIndex / PageSize;
            int pageCol = columnIndex / PageSize;
            if (pageRow < _nativePages.Length && _nativePages[pageRow] is not null && pageCol < _nativePages[pageRow].Length)
            {
                IntPtr ptr = _nativePages[pageRow][pageCol];
                if (ptr == IntPtr.Zero && _pageStates is not null && pageRow < _pageStates.Length && _pageStates[pageRow] is not null && pageCol < _pageStates[pageRow].Length && _pageStates[pageRow][pageCol].CompressedBytes is not null)
                {
                    EnsurePageAllocated(rowIndex, columnIndex);
                    ptr = _nativePages[pageRow][pageCol];
                }
                else if (ptr != IntPtr.Zero && _pageStates is not null && pageRow < _pageStates.Length && _pageStates[pageRow] is not null && pageCol < _pageStates[pageRow].Length)
                {
                    _pageStates[pageRow][pageCol].LastAccessTick = System.Threading.Interlocked.Increment(ref _accessCounter);
                }

                if (ptr != IntPtr.Zero)
                {
                    int index = (rowIndex % PageSize) * PageSize + (columnIndex % PageSize);
                    unsafe
                    {
                        NativeCell* cells = (NativeCell*)ptr;
                        NativeCell* cell = &cells[index];
                        cell->Type = 0;
                        FreePtr(ref cell->StyleNamePtr);
                        FreePtr(ref cell->FormulaPtr);
                        FreePtr(ref cell->StringValuePtr);
                    }
                }
            }
        }

        RemoveHotFormula(rowIndex, columnIndex);
    }

    private void EnsurePageStatesAllocated(int pageRow, int pageCol)
    {
        if (_pageStates is null)
        {
            _pageStates = new TablePageState[256][];
        }
        if (pageRow >= _pageStates.Length)
        {
            var newStates = new TablePageState[pageRow + 64][];
            Array.Copy(_pageStates, newStates, _pageStates.Length);
            _pageStates = newStates;
        }
        if (_pageStates[pageRow] is null)
        {
            _pageStates[pageRow] = new TablePageState[256];
        }
        if (pageCol >= _pageStates[pageRow].Length)
        {
            var newCols = new TablePageState[pageCol + 64];
            Array.Copy(_pageStates[pageRow], newCols, _pageStates[pageRow].Length);
            _pageStates[pageRow] = newCols;
        }
    }

    private void EnsurePageAllocated(int rowIndex, int columnIndex)
    {
        if (rowIndex > OdfSpreadsheetLimits.MaxRowIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(rowIndex));
        }

        if (columnIndex > OdfSpreadsheetLimits.MaxColumnIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(columnIndex));
        }

        if (_nativePages is null)
        {
            _nativePages = new IntPtr[256][];
        }

        int pageRow = rowIndex / PageSize;
        int pageCol = columnIndex / PageSize;

        if (pageRow >= _nativePages.Length)
        {
            var newPages = new IntPtr[pageRow + 64][];
            Array.Copy(_nativePages, newPages, _nativePages.Length);
            _nativePages = newPages;
        }

        if (_nativePages[pageRow] is null)
        {
            _nativePages[pageRow] = new IntPtr[256];
        }

        if (pageCol >= _nativePages[pageRow].Length)
        {
            var newCols = new IntPtr[pageCol + 64];
            Array.Copy(_nativePages[pageRow], newCols, _nativePages[pageRow].Length);
            _nativePages[pageRow] = newCols;
        }

        EnsurePageStatesAllocated(pageRow, pageCol);

        if (_nativePages[pageRow][pageCol] == IntPtr.Zero)
        {
            var state = _pageStates![pageRow][pageCol];
            if (state.CompressedBytes is not null)
            {
                _nativePages[pageRow][pageCol] = DecompressPage(state.CompressedBytes);
                _pageStates[pageRow][pageCol].CompressedBytes = null;
                _pageStates[pageRow][pageCol].IsHot = true;
            }
            else
            {
                _nativePages[pageRow][pageCol] = AllocatePageMemory();
                _pageStates[pageRow][pageCol].IsHot = true;
            }
        }

        _pageStates![pageRow][pageCol].LastAccessTick = System.Threading.Interlocked.Increment(ref _accessCounter);
        EvictHotPagesIfNeeded();
    }

    private void EvictHotPagesIfNeeded()
    {
        if (_nativePages is null || _pageStates is null)
            return;

        int hotCount = 0;
        for (int i = 0; i < _pageStates.Length; i++)
        {
            if (_pageStates[i] is null)
                continue;
            for (int j = 0; j < _pageStates[i].Length; j++)
            {
                if (_pageStates[i][j].IsHot)
                {
                    hotCount++;
                }
            }
        }

        while (hotCount > _maxHotPages)
        {
            int bestRow = -1;
            int bestCol = -1;
            long minTick = long.MaxValue;

            for (int i = 0; i < _pageStates.Length; i++)
            {
                if (_pageStates[i] is null)
                    continue;
                for (int j = 0; j < _pageStates[i].Length; j++)
                {
                    if (_pageStates[i][j].IsHot)
                    {
                        long tick = _pageStates[i][j].LastAccessTick;
                        if (tick < minTick)
                        {
                            minTick = tick;
                            bestRow = i;
                            bestCol = j;
                        }
                    }
                }
            }

            if (bestRow != -1 && bestCol != -1)
            {
                CompressPageToCold(bestRow, bestCol);
                hotCount--;
            }
            else
            {
                break;
            }
        }
    }

    internal void SetSparseCellStyle(int rowIndex, int columnIndex, string styleName)
    {
        EnsurePageAllocated(rowIndex, columnIndex);
        int pageRow = rowIndex / PageSize;
        int pageCol = columnIndex / PageSize;
        int cellRow = rowIndex % PageSize;
        int cellCol = columnIndex % PageSize;
        IntPtr pagePtr = _nativePages![pageRow][pageCol];
        int index = cellRow * PageSize + cellCol;
        unsafe
        {
            NativeCell* cells = (NativeCell*)pagePtr;
            NativeCell* cell = &cells[index];
            FreePtr(ref cell->StyleNamePtr);
            cell->StyleNamePtr = StringToUtf8Ptr(styleName);
        }
    }

    internal void SetSparseCellFormula(int rowIndex, int columnIndex, string formula)
    {
        EnsurePageAllocated(rowIndex, columnIndex);
        int pageRow = rowIndex / PageSize;
        int pageCol = columnIndex / PageSize;
        int cellRow = rowIndex % PageSize;
        int cellCol = columnIndex % PageSize;
        IntPtr pagePtr = _nativePages![pageRow][pageCol];
        int index = cellRow * PageSize + cellCol;
        unsafe
        {
            NativeCell* cells = (NativeCell*)pagePtr;
            NativeCell* cell = &cells[index];
            FreePtr(ref cell->FormulaPtr);
        }
        SetHotFormula(rowIndex, columnIndex, formula);
    }

    private void SetSparseCellValue(int rowIndex, int columnIndex, object? value)
    {
        EnsurePageAllocated(rowIndex, columnIndex);
        int pageRow = rowIndex / PageSize;
        int pageCol = columnIndex / PageSize;
        int cellRow = rowIndex % PageSize;
        int cellCol = columnIndex % PageSize;

        IntPtr pagePtr = _nativePages![pageRow][pageCol];
        int index = cellRow * PageSize + cellCol;

        unsafe
        {
            NativeCell* cells = (NativeCell*)pagePtr;
            NativeCell* cell = &cells[index];

            if (value is null)
            {
                cell->Type = 0;
                FreePtr(ref cell->StyleNamePtr);
                FreePtr(ref cell->FormulaPtr);
                FreePtr(ref cell->StringValuePtr);
                cell->StringValuePtr = StringToUtf8Ptr(string.Empty);
                RemoveHotFormula(rowIndex, columnIndex);
                return;
            }

            FreePtr(ref cell->StringValuePtr);

            switch (value)
            {
                case bool boolValue:
                    cell->Type = 2;
                    cell->BoolValue = boolValue;
                    break;
                case DateTime dateTimeValue:
                    cell->Type = 3;
                    cell->Ticks = dateTimeValue.Ticks;
                    break;
                case sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal:
                    cell->Type = 1;
                    cell->FloatValue = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    break;
                default:
                    cell->Type = 0;
                    cell->StringValuePtr = StringToUtf8Ptr(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                    break;
            }
        }
    }

    private void SetSparseCellSnapshot(int rowIndex, int columnIndex, SparseCellSnapshot snapshot)
    {
        EnsurePageAllocated(rowIndex, columnIndex);
        int pageRow = rowIndex / PageSize;
        int pageCol = columnIndex / PageSize;
        int cellRow = rowIndex % PageSize;
        int cellCol = columnIndex % PageSize;

        IntPtr pagePtr = _nativePages![pageRow][pageCol];
        int index = (cellRow * PageSize) + cellCol;

        unsafe
        {
            NativeCell* cells = (NativeCell*)pagePtr;
            NativeCell* cell = &cells[index];
            cell->Type = snapshot.Type;
            switch (snapshot.Type)
            {
                case 1:
                    cell->FloatValue = snapshot.NumberValue;
                    break;
                case 2:
                    cell->BoolValue = snapshot.BooleanValue;
                    break;
                case 3:
                    cell->Ticks = snapshot.Ticks;
                    break;
            }

            FreePtr(ref cell->StyleNamePtr);
            FreePtr(ref cell->FormulaPtr);
            FreePtr(ref cell->StringValuePtr);
            cell->StyleNamePtr = StringToUtf8Ptr(snapshot.StyleName);
            cell->StringValuePtr = StringToUtf8Ptr(snapshot.Text);
        }

        SetHotFormula(rowIndex, columnIndex, snapshot.Formula);
    }

    /// <summary>
    /// Performs materialize sparse cells.
    /// 將尚未具現化的稀疏儲存格全部加載至 DOM。在文件序列化存檔前呼叫。
    /// </summary>
    public void MaterializeSparseCells()
    {
        var locations = new HashSet<(int r, int c)>();

        if (_nativePages is not null)
        {
            for (int pr = 0; pr < _nativePages.Length; pr++)
            {
                var rowPages = _nativePages[pr];
                if (rowPages is null)
                    continue;

                for (int pc = 0; pc < rowPages.Length; pc++)
                {
                    IntPtr ptr = rowPages[pc];
                    if (ptr == IntPtr.Zero && _pageStates is not null && pr < _pageStates.Length && _pageStates[pr] is not null && pc < _pageStates[pr].Length && _pageStates[pr][pc].CompressedBytes is not null)
                    {
                        EnsurePageAllocated(pr * PageSize, pc * PageSize);
                        ptr = _nativePages[pr][pc];
                    }
                    if (ptr == IntPtr.Zero)
                        continue;

                    unsafe
                    {
                        NativeCell* cells = (NativeCell*)ptr;
                        for (int i = 0; i < PageSize * PageSize; i++)
                        {
                            if (cells[i].Type != 0 || cells[i].StyleNamePtr != IntPtr.Zero || cells[i].FormulaPtr != IntPtr.Zero || cells[i].StringValuePtr != IntPtr.Zero)
                            {
                                int r = pr * PageSize + (i / PageSize);
                                int c = pc * PageSize + (i % PageSize);
                                locations.Add((r, c));
                            }
                        }
                    }
                }
            }
        }

        foreach (var loc in locations)
        {
            GetOrCreateCell(loc.r, loc.c);
        }
        FreeNativePages();
    }

    /// <summary>
    /// Tries to write override.
    /// 嘗試寫入 Override。
    /// </summary>
    /// <inheritdoc />
    public override bool TryWriteOverride(System.Xml.XmlWriter writer, Dictionary<string, string> nsDict)
    {
        bool hasSparseData = false;
        if (_nativePages is not null)
        {
            for (int pr = 0; pr < _nativePages.Length; pr++)
            {
                var rowPages = _nativePages[pr];
                if (rowPages is null)
                    continue;
                for (int pc = 0; pc < rowPages.Length; pc++)
                {
                    IntPtr ptr = rowPages[pc];
                    if (ptr != IntPtr.Zero || (_pageStates is not null && pr < _pageStates.Length && _pageStates[pr] is not null && pc < _pageStates[pr].Length && _pageStates[pr][pc].CompressedBytes is not null))
                    {
                        hasSparseData = true;
                        break;
                    }
                }
                if (hasSparseData)
                    break;
            }
        }

        if (!hasSparseData && _hotFormulaPtrs is null)
            return false;

        string prefix = nsDict.TryGetValue(NamespaceUri, out var p) ? p : Prefix ?? "table";
        writer.WriteStartElement(prefix, LocalName, NamespaceUri);

        foreach (var attr in Attributes)
        {
            string attrPrefix = nsDict.TryGetValue(attr.Key.NamespaceUri, out var ap) ? ap : "table";
            if (!string.IsNullOrEmpty(attr.Key.NamespaceUri))
            {
                writer.WriteAttributeString(attrPrefix, attr.Key.LocalName, attr.Key.NamespaceUri, attr.Value);
            }
            else
            {
                writer.WriteAttributeString(attr.Key.LocalName, attr.Value);
            }
        }

        int maxRow = -1;
        int maxColOverall = -1;
        if (_nativePages is not null)
        {
            for (int pr = 0; pr < _nativePages.Length; pr++)
            {
                var rowPages = _nativePages[pr];
                if (rowPages is null)
                    continue;
                for (int pc = 0; pc < rowPages.Length; pc++)
                {
                    IntPtr ptr = rowPages[pc];
                    if (ptr == IntPtr.Zero && _pageStates is not null && pr < _pageStates.Length && _pageStates[pr] is not null && pc < _pageStates[pr].Length && _pageStates[pr][pc].CompressedBytes is not null)
                    {
                        EnsurePageAllocated(pr * PageSize, pc * PageSize);
                        ptr = _nativePages[pr][pc];
                    }
                    if (ptr == IntPtr.Zero)
                        continue;
                    unsafe
                    {
                        NativeCell* cells = (NativeCell*)ptr;
                        for (int i = 0; i < PageSize * PageSize; i++)
                        {
                            if (cells[i].Type != 0 || cells[i].StyleNamePtr != IntPtr.Zero || cells[i].FormulaPtr != IntPtr.Zero || cells[i].StringValuePtr != IntPtr.Zero)
                            {
                                int r = pr * PageSize + (i / PageSize);
                                if (r > maxRow)
                                    maxRow = r;
                                int c = pc * PageSize + (i % PageSize);
                                if (c > maxColOverall)
                                    maxColOverall = c;
                            }
                        }
                    }
                }
            }
        }

        if (_hotFormulaPtrs is not null)
        {
            foreach (long key in _hotFormulaPtrs.Keys)
            {
                int row = GetRowFromSparseCellKey(key);
                if (row > maxRow)
                {
                    maxRow = row;
                }

                int column = GetColumnFromSparseCellKey(key);
                if (column > maxColOverall)
                {
                    maxColOverall = column;
                }
            }
        }

        int dummyCount = 0;
        foreach (var child in Children)
        {
            if (child is OdfElement elem && !OdfElementContentModel.IsTableRowStructure(elem))
            {
                OdfXmlWriter.WriteNode(child, writer, nsDict, ref dummyCount, false, 1);
            }
        }

        string tablePrefix = prefix;
        string officePrefix = nsDict.TryGetValue(OdfNamespaces.Office, out var op) ? op : "office";
        string textPrefix = nsDict.TryGetValue(OdfNamespaces.Text, out var tp) ? tp : "text";

        for (int r = 0; r <= maxRow; r++)
        {
            writer.WriteStartElement(tablePrefix, "table-row", OdfNamespaces.Table);

            int maxCol = -1;
            for (int c = 0; c <= maxColOverall; c++)
            {
                if (TryGetSparseCellData(r, c, out _, out _, out _, out _, out _, out _, out _))
                {
                    if (c > maxCol)
                        maxCol = c;
                }
            }

            for (int c = 0; c <= maxCol; c++)
            {
                if (TryGetSparseCellData(r, c, out byte type, out double dVal, out bool bVal, out long ticks, out string? text, out string? style, out string? formula))
                {
                    writer.WriteStartElement(tablePrefix, "table-cell", OdfNamespaces.Table);
                    if (style is not null)
                    {
                        writer.WriteAttributeString(tablePrefix, "style-name", OdfNamespaces.Table, style);
                    }
                    if (formula is not null)
                    {
                        writer.WriteAttributeString(tablePrefix, "formula", OdfNamespaces.Table, formula);
                    }

                    string textContent = "";
                    if (type == 1)
                    {
                        writer.WriteAttributeString(officePrefix, "value-type", OdfNamespaces.Office, "float");
                        string dStr = Convert.ToString(dVal, CultureInfo.InvariantCulture);
                        writer.WriteAttributeString(officePrefix, "value", OdfNamespaces.Office, dStr);
                        textContent = dStr;
                    }
                    else if (type == 2)
                    {
                        writer.WriteAttributeString(officePrefix, "value-type", OdfNamespaces.Office, "boolean");
                        writer.WriteAttributeString(officePrefix, "boolean-value", OdfNamespaces.Office, bVal ? "true" : "false");
                        textContent = bVal ? "TRUE" : "FALSE";
                    }
                    else if (type == 3)
                    {
                        writer.WriteAttributeString(officePrefix, "value-type", OdfNamespaces.Office, "date");
                        var dt = new DateTime(ticks);
                        string isoDate = dt.Kind == DateTimeKind.Utc
                            ? dt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
                            : dt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
                        writer.WriteAttributeString(officePrefix, "date-value", OdfNamespaces.Office, isoDate);
                        textContent = isoDate;
                    }
                    else
                    {
                        writer.WriteAttributeString(officePrefix, "value-type", OdfNamespaces.Office, "string");
                        writer.WriteAttributeString(officePrefix, "string-value", OdfNamespaces.Office, text ?? string.Empty);
                        textContent = text ?? string.Empty;
                    }

                    writer.WriteStartElement(textPrefix, "p", OdfNamespaces.Text);
                    writer.WriteString(textContent);
                    writer.WriteEndElement(); // text:p

                    writer.WriteEndElement(); // table:table-cell
                }
                else
                {
                    writer.WriteStartElement(tablePrefix, "table-cell", OdfNamespaces.Table);
                    writer.WriteEndElement();
                }
            }

            writer.WriteEndElement(); // table:table-row
        }

        writer.WriteEndElement(); // table:table
        return true;
    }

    private void ExtractPageFormulasToHotStore(int pageRow, int pageCol, IntPtr ptr)
    {
        unsafe
        {
            NativeCell* cells = (NativeCell*)ptr;
            for (int k = 0; k < PageSize * PageSize; k++)
            {
                if (cells[k].FormulaPtr == IntPtr.Zero)
                    continue;

                int rowIndex = pageRow * PageSize + (k / PageSize);
                int columnIndex = pageCol * PageSize + (k % PageSize);
                SetHotFormula(rowIndex, columnIndex, Utf8PtrToString(cells[k].FormulaPtr));
                FreePtr(ref cells[k].FormulaPtr);
            }
        }
    }

    private void CompressPageToCold(int pageRow, int pageCol)
    {
        IntPtr ptr = _nativePages![pageRow][pageCol];
        if (ptr == IntPtr.Zero)
            return;

        ExtractPageFormulasToHotStore(pageRow, pageCol, ptr);

        byte[] compressed = CompressPage(ptr);

        ReleasePageMemory(ptr);

        _pageStates![pageRow][pageCol].CompressedBytes = compressed;
        _pageStates![pageRow][pageCol].IsHot = false;
        _pageStates![pageRow][pageCol].LastAccessTick = 0;
        _nativePages[pageRow][pageCol] = IntPtr.Zero;
    }

    /// <summary>
    /// Compresses cold pages.
    /// 將所有已分配的熱頁 (Hot-Page) 進行壓縮並轉為冷頁 (Cold-Page) 以節省記憶體。
    /// </summary>
    public void CompressColdPages()
    {
        if (_nativePages is null)
            return;
        for (int i = 0; i < _nativePages.Length; i++)
        {
            if (_nativePages[i] is null)
                continue;
            for (int j = 0; j < _nativePages[i].Length; j++)
            {
                if (_nativePages[i][j] != IntPtr.Zero)
                {
                    CompressPageToCold(i, j);
                }
            }
        }
    }

    private static byte[] CompressPage(IntPtr ptr)
    {
        using var ms = new MemoryStream();
        using (var ds = new DeflateStream(ms, CompressionLevel.Fastest))
        using (var writer = new BinaryWriter(ds, Encoding.UTF8, leaveOpen: true))
        {
            unsafe
            {
                NativeCell* cells = (NativeCell*)ptr;
                for (int i = 0; i < PageSize * PageSize; i++)
                {
                    writer.Write(cells[i].Type);
                    writer.Write(cells[i].FloatValue);
                    writer.Write(cells[i].Ticks);
                    writer.Write(cells[i].BoolValue);
                    writer.Write(Utf8PtrToString(cells[i].StyleNamePtr) ?? string.Empty);
                    writer.Write(Utf8PtrToString(cells[i].FormulaPtr) ?? string.Empty);
                    writer.Write(Utf8PtrToString(cells[i].StringValuePtr) ?? string.Empty);
                }
            }
        }

        return ms.ToArray();
    }

    private static IntPtr DecompressPage(byte[] compressed)
    {
        IntPtr ptr = AllocatePageMemory();

        using (var ms = new MemoryStream(compressed))
        using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
        using (var reader = new BinaryReader(ds, Encoding.UTF8, leaveOpen: false))
        {
            unsafe
            {
                NativeCell* cells = (NativeCell*)ptr;
                for (int i = 0; i < PageSize * PageSize; i++)
                {
                    cells[i].Type = reader.ReadByte();
                    cells[i].FloatValue = reader.ReadDouble();
                    cells[i].Ticks = reader.ReadInt64();
                    cells[i].BoolValue = reader.ReadBoolean();
                    cells[i].StyleNamePtr = StringToUtf8Ptr(reader.ReadString());
                    cells[i].FormulaPtr = StringToUtf8Ptr(reader.ReadString());
                    cells[i].StringValuePtr = StringToUtf8Ptr(reader.ReadString());
                }
            }
        }

        return ptr;
    }

    private static void ReleasePageMemory(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
        {
            return;
        }

        unsafe
        {
            NativeCell* cells = (NativeCell*)ptr;
            for (int k = 0; k < PageSize * PageSize; k++)
            {
                FreePtr(ref cells[k].StyleNamePtr);
                FreePtr(ref cells[k].FormulaPtr);
                FreePtr(ref cells[k].StringValuePtr);
            }
        }

#if NET10_0_OR_GREATER
        unsafe
        {
            NativeMemory.Free((void*)ptr);
        }
#else
        Marshal.FreeHGlobal(ptr);
#endif
    }

    private static unsafe IntPtr AllocatePageMemory()
    {
#if NET10_0_OR_GREATER
        IntPtr ptr = (IntPtr)NativeMemory.AllocZeroed((nuint)(PageSize * PageSize), 40);
#else
        int byteSize = PageSize * PageSize * 40;
        IntPtr ptr = Marshal.AllocHGlobal(byteSize);
        new Span<byte>((void*)ptr, byteSize).Clear();
#endif
        return ptr;
    }

    private static class ValueAccessorCache<T>
    {
        public static readonly Func<T, object?>[] Accessors = BuildAccessors();
        public static int BuildCount;

        private static Func<T, object?>[] BuildAccessors()
        {
            BuildCount++;
            PropertyInfo[] properties = typeof(T)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.CanRead && property.GetMethod is not null)
                .ToArray();

            if (properties.Length == 0)
            {
                return [static _ => null];
            }

            Func<T, object?>[] accessors = new Func<T, object?>[properties.Length];
            for (int i = 0; i < properties.Length; i++)
            {
#if NET10_0_OR_GREATER
                if (!RuntimeFeature.IsDynamicCodeCompiled)
                {
                    accessors[i] = CreateGetterDelegate(properties[i]);
                    continue;
                }
#endif
                ParameterExpression parameter = Expression.Parameter(typeof(T), "item");
                Expression member = Expression.Property(parameter, properties[i]);
                UnaryExpression cast = Expression.Convert(member, typeof(object));
                accessors[i] = Expression.Lambda<Func<T, object?>>(cast, parameter).Compile();
            }

            return accessors;
        }

        private static Func<T, object?> CreateGetterDelegate(PropertyInfo property)
        {
            MethodInfo method = typeof(ValueAccessorCache<T>)
                .GetMethod(nameof(CreateTypedGetterDelegate), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(property.PropertyType);
            return (Func<T, object?>)method.Invoke(null, [property])!;
        }

        private static Func<T, object?> CreateTypedGetterDelegate<TValue>(PropertyInfo property)
        {
            var getter = (Func<T, TValue>)Delegate.CreateDelegate(typeof(Func<T, TValue>), property.GetMethod!);
            return item => getter(item);
        }
    }

    internal static int GetImportDataAccessorBuildCountForTests<T>()
        => ValueAccessorCache<T>.BuildCount;
}

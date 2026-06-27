using System;
using System.Collections.Generic;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 管理試算表跨文件公式引用的外部文件載入委派與本地快取。
/// </summary>
public sealed class OdfExternalLinkManager
{
    private readonly Dictionary<ExternalCellKey, object?> _cellCache = new();

    /// <summary>
    /// 取得或設定外部文件載入委派。參數為公式參照中的文件識別碼，例如 <c>file:///other.ods</c>。
    /// </summary>
    public Func<string, SpreadsheetDocument?>? DocumentResolver { get; set; }

    /// <summary>
    /// 清除所有外部儲存格快取。
    /// </summary>
    public void ClearCache() => _cellCache.Clear();

    /// <summary>
    /// 設定外部儲存格快取值。
    /// </summary>
    /// <param name="documentId">外部文件識別碼</param>
    /// <param name="sheetName">外部工作表名稱</param>
    /// <param name="address">外部儲存格位址</param>
    /// <param name="value">快取值</param>
    public void SetCachedValue(string documentId, string sheetName, OdfCellAddress address, object? value)
    {
        _cellCache[new ExternalCellKey(documentId, sheetName, address.Row, address.Column)] = value;
    }

    internal IEnumerable<CachedCell> GetCachedCells()
    {
        foreach (var pair in _cellCache)
        {
            yield return new CachedCell(
                pair.Key.DocumentId,
                pair.Key.SheetName,
                pair.Key.Row,
                pair.Key.Column,
                pair.Value);
        }
    }

    internal bool TryGetCellValue(OdfCellAddress address, out object? value)
    {
        value = null;
        if (!TryParseExternalSheet(address.SheetName, out string documentId, out string sheetName))
        {
            return false;
        }

        var key = new ExternalCellKey(documentId, sheetName, address.Row, address.Column);
        if (_cellCache.TryGetValue(key, out value))
        {
            return true;
        }

        SpreadsheetDocument? document = DocumentResolver?.Invoke(documentId);
        OdfTableSheet? sheet = document?.GetSheet(sheetName);
        if (sheet is null)
        {
            return false;
        }

        value = sheet.Cells[address.Row, address.Column].CellValue ?? 0.0;
        _cellCache[key] = value;
        return true;
    }

    internal bool TryGetRangeValues(OdfCellRange range, out object[,] values)
    {
        values = new object[0, 0];
        if (!TryParseExternalSheet(range.StartAddress.SheetName, out string documentId, out string sheetName))
        {
            return false;
        }

        int minRow = Math.Min(range.StartAddress.Row, range.EndAddress.Row);
        int maxRow = Math.Max(range.StartAddress.Row, range.EndAddress.Row);
        int minCol = Math.Min(range.StartAddress.Column, range.EndAddress.Column);
        int maxCol = Math.Max(range.StartAddress.Column, range.EndAddress.Column);
        values = new object[maxRow - minRow + 1, maxCol - minCol + 1];

        for (int row = minRow; row <= maxRow; row++)
        {
            for (int column = minCol; column <= maxCol; column++)
            {
                var cellAddress = new OdfCellAddress(row, column, range.StartAddress.SheetName);
                values[row - minRow, column - minCol] = TryGetCellValue(cellAddress, out object? value)
                    ? value ?? 0.0
                    : 0.0;
            }
        }

        return true;
    }

    internal static bool TryParseExternalSheet(string? sheetToken, out string documentId, out string sheetName)
    {
        documentId = string.Empty;
        sheetName = string.Empty;
        if (string.IsNullOrEmpty(sheetToken))
            return false;

        int markerIndex = sheetToken!.IndexOf("#$", StringComparison.Ordinal);
        int markerLength = 2;
        if (markerIndex < 0)
        {
            markerIndex = sheetToken.IndexOf('#');
            markerLength = 1;
        }

        if (markerIndex <= 0 || markerIndex + markerLength >= sheetToken.Length)
            return false;

        documentId = Unquote(sheetToken.Substring(0, markerIndex));
        sheetName = Unquote(sheetToken.Substring(markerIndex + markerLength));
        return documentId.Length > 0 && sheetName.Length > 0;
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && value[0] == '\'' && value[value.Length - 1] == '\'')
        {
            value = value.Substring(1, value.Length - 2).Replace("''", "'");
        }

        return value;
    }

    private readonly struct ExternalCellKey : IEquatable<ExternalCellKey>
    {
        private readonly string _documentId;
        private readonly string _sheetName;
        private readonly int _row;
        private readonly int _column;

        internal ExternalCellKey(string documentId, string sheetName, int row, int column)
        {
            _documentId = documentId;
            _sheetName = sheetName;
            _row = row;
            _column = column;
        }

        internal string DocumentId => _documentId;

        internal string SheetName => _sheetName;

        internal int Row => _row;

        internal int Column => _column;

        public bool Equals(ExternalCellKey other) =>
            _row == other._row &&
            _column == other._column &&
            string.Equals(_documentId, other._documentId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(_sheetName, other._sheetName, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) => obj is ExternalCellKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(_documentId);
                hash = (hash * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(_sheetName);
                hash = (hash * 397) ^ _row;
                hash = (hash * 397) ^ _column;
                return hash;
            }
        }
    }

    internal readonly struct CachedCell
    {
        internal CachedCell(string documentId, string sheetName, int row, int column, object? value)
        {
            DocumentId = documentId;
            SheetName = sheetName;
            Row = row;
            Column = column;
            Value = value;
        }

        internal string DocumentId { get; }

        internal string SheetName { get; }

        internal int Row { get; }

        internal int Column { get; }

        internal object? Value { get; }
    }
}

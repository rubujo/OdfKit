using System;
using OdfKit.Core;

namespace OdfKit.Spreadsheet;

public readonly partial struct OdfCellAddress
{
    #region Address Parsing

    /// <summary>
    /// 解析 Excel 格式的儲存格位址字串。
    /// </summary>
    /// <param name="address">Excel 格式的位址字串</param>
    /// <returns>解析後的 <see cref="OdfCellAddress"/> 執行個體</returns>
    public static OdfCellAddress ParseExcel(string address) => Parse(address.AsSpan(), false);

    /// <summary>
    /// 解析 ODF 格式的儲存格位址字串。
    /// </summary>
    /// <param name="address">ODF 格式的位址字串</param>
    /// <returns>解析後的 <see cref="OdfCellAddress"/> 執行個體</returns>
    public static OdfCellAddress ParseOdf(string address) => Parse(address.AsSpan(), true);

    /// <summary>
    /// 嘗試解析儲存格位址字串。
    /// </summary>
    /// <param name="value">要解析的位址字串</param>
    /// <param name="address">解析成功時傳回的儲存格位址</param>
    /// <returns>如果解析成功則為 true，否則為 false</returns>
    public static bool TryParse(string value, out OdfCellAddress address)
    {
        try
        {
            address = ParseExcel(value);
            return true;
        }
        catch
        {
            try
            {
                address = ParseOdf(value);
                return true;
            }
            catch
            {
                address = default;
                return false;
            }
        }
    }

    /// <summary>
    /// 從字元範圍解析儲存格位址。
    /// </summary>
    /// <param name="span">包含儲存格位址的唯讀字元範圍</param>
    /// <param name="isOdfStyle">是否使用 ODF 格式樣式</param>
    /// <returns>解析後的 <see cref="OdfCellAddress"/> 結構</returns>
    /// <exception cref="FormatException">當字串格式無效時擲出</exception>
    public static OdfCellAddress Parse(ReadOnlySpan<char> span, bool isOdfStyle)
    {
        span = span.Trim();
        if (span.IsEmpty)
            throw new FormatException("Address string cannot be empty.");

        string? sheetName = null;
        bool isSheetAbsolute = false;
        bool isColAbsolute = false;
        bool isRowAbsolute = false;

        // 1. Separate Sheet Name and Cell coordinates
        int sepIndex = -1;
        if (isOdfStyle)
        {
            // ODF style: local sheet has dot prefix like '.A1'.
            // Sheet-qualified reference has 'Sheet1.A1' or 'Sheet 1'.A1
            // If it starts with '.' and contains no other '.', there is no sheet name.
            if (span.StartsWith(".") && span.Slice(1).IndexOf('.') == -1)
            {
                sepIndex = 0;
            }
            else
            {
                // Find the last dot '.' (handling single quotes for sheets with spaces)
                bool inQuotes = false;
                for (int j = span.Length - 1; j >= 0; j--)
                {
                    char c = span[j];
                    if (c == '\'')
                        inQuotes = !inQuotes;
                    if (!inQuotes && c == '.')
                    {
                        sepIndex = j;
                        break;
                    }
                }
            }
        }
        else
        {
            // Excel style: Separator is '!'. Find last '!' outside single quotes
            bool inQuotes = false;
            for (int j = span.Length - 1; j >= 0; j--)
            {
                char c = span[j];
                if (c == '\'')
                    inQuotes = !inQuotes;
                if (!inQuotes && c == '!')
                {
                    sepIndex = j;
                    break;
                }
            }
        }

        ReadOnlySpan<char> cellSpan = span;
        if (sepIndex != -1)
        {
            ReadOnlySpan<char> sheetSpan = span.Slice(0, sepIndex);
            cellSpan = span.Slice(sepIndex + 1);

            // Process sheet name
            if (!sheetSpan.IsEmpty)
            {
                // Detect absolute sheet reference (prefixed with '$')
                if (sheetSpan.StartsWith("$"))
                {
                    isSheetAbsolute = true;
                    sheetSpan = sheetSpan.Slice(1);
                }

                // Strip single quotes if present
                if (sheetSpan.StartsWith("'") && sheetSpan.EndsWith("'"))
                {
                    sheetSpan = sheetSpan.Slice(1, sheetSpan.Length - 2);
                    // Unescape double single quotes ''
                    sheetName = sheetSpan.ToString().Replace("''", "'");
                }
                else
                {
                    sheetName = sheetSpan.ToString();
                }
            }
        }

        // 2. Parse Column and Row components from cellSpan
        int i = 0;
        if (i < cellSpan.Length && cellSpan[i] == '$')
        {
            isColAbsolute = true;
            i++;
        }

        int colStart = i;
        while (i < cellSpan.Length && char.IsLetter(cellSpan[i]))
        {
            i++;
        }
        if (i == colStart)
            throw new FormatException("Invalid cell address: missing column letters.");

        ReadOnlySpan<char> colLetters = cellSpan.Slice(colStart, i - colStart);

        if (i < cellSpan.Length && cellSpan[i] == '$')
        {
            isRowAbsolute = true;
            i++;
        }

        int rowStart = i;
        while (i < cellSpan.Length && char.IsDigit(cellSpan[i]))
        {
            i++;
        }
        if (i == rowStart || i < cellSpan.Length)
            throw new FormatException("Invalid cell address: row index must be numeric digits and terminate the string.");

        ReadOnlySpan<char> rowDigits = cellSpan.Slice(rowStart, i - rowStart);

        // Convert column letters (A-Z, AA-ZZ) to 0-based index
        int column = 0;
        for (int k = 0; k < colLetters.Length; k++)
        {
            char c = colLetters[k];
            int val = char.ToUpperInvariant(c) - 'A' + 1;
            column = column * 26 + val;
        }
        column--; // 0-based

        // Convert row digits to 0-based index (1-based in text)
#if NET10_0_OR_GREATER
        int row = int.Parse(rowDigits) - 1;
#else
        int row = int.Parse(rowDigits.ToString()) - 1;
#endif

        return new OdfCellAddress(row, column, sheetName, isRowAbsolute, isColAbsolute, isSheetAbsolute);
    }

    #endregion
}

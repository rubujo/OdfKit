using System;
using System.Text;
using OdfKit.Core;

namespace OdfKit.Spreadsheet
{
    public readonly struct OdfCellAddress : IEquatable<OdfCellAddress>
    {
        public int Row { get; }      // 0-based row index
        public int Column { get; }   // 0-based column index
        public string? SheetName { get; }
        
        public bool IsRowAbsolute { get; }
        public bool IsColumnAbsolute { get; }
        public bool IsSheetAbsolute { get; }

        public OdfCellAddress(int row, int column, string? sheetName = null, 
            bool isRowAbsolute = false, bool isColumnAbsolute = false, bool isSheetAbsolute = false)
        {
            if (row < 0) throw new ArgumentOutOfRangeException(nameof(row), "Row index must be non-negative.");
            if (column < 0) throw new ArgumentOutOfRangeException(nameof(column), "Column index must be non-negative.");

            Row = row;
            Column = column;
            SheetName = sheetName;
            IsRowAbsolute = isRowAbsolute;
            IsColumnAbsolute = isColumnAbsolute;
            IsSheetAbsolute = isSheetAbsolute;
        }

        public bool Equals(OdfCellAddress other)
        {
            return Row == other.Row &&
                   Column == other.Column &&
                   string.Equals(SheetName, other.SheetName, StringComparison.OrdinalIgnoreCase) &&
                   IsRowAbsolute == other.IsRowAbsolute &&
                   IsColumnAbsolute == other.IsColumnAbsolute &&
                   IsSheetAbsolute == other.IsSheetAbsolute;
        }

        public override bool Equals(object? obj) => obj is OdfCellAddress other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Row;
                hash = hash * 23 + Column;
                hash = hash * 23 + (SheetName != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(SheetName) : 0);
                hash = hash * 23 + IsRowAbsolute.GetHashCode();
                hash = hash * 23 + IsColumnAbsolute.GetHashCode();
                hash = hash * 23 + IsSheetAbsolute.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(OdfCellAddress left, OdfCellAddress right) => left.Equals(right);
        public static bool operator !=(OdfCellAddress left, OdfCellAddress right) => !left.Equals(right);

        public static OdfCellAddress ParseExcel(string address) => Parse(address.AsSpan(), false);
        public static OdfCellAddress ParseOdf(string address) => Parse(address.AsSpan(), true);

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
                        if (c == '\'') inQuotes = !inQuotes;
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
                    if (c == '\'') inQuotes = !inQuotes;
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

        public string ToExcelString()
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(SheetName))
            {
                bool needsQuotes = SheetName!.Contains(" ") || SheetName.Contains("'") || SheetName.Contains("-") || SheetName.Contains("!");
                if (needsQuotes)
                    sb.Append("'").Append(SheetName.Replace("'", "''")).Append("'!");
                else
                    sb.Append(SheetName).Append("!");
            }
            if (IsColumnAbsolute) sb.Append("$");
            sb.Append(IndexToColumnName(Column));
            if (IsRowAbsolute) sb.Append("$");
            sb.Append(Row + 1);
            return sb.ToString();
        }

        public string ToOdfString(bool includeBrackets = false)
        {
            var sb = new StringBuilder();
            if (includeBrackets) sb.Append("[");

            if (!string.IsNullOrEmpty(SheetName))
            {
                if (IsSheetAbsolute) sb.Append("$");
                bool needsQuotes = SheetName!.Contains(" ") || SheetName.Contains("'") || SheetName.Contains("-") || SheetName.Contains(".");
                if (needsQuotes)
                    sb.Append("'").Append(SheetName.Replace("'", "''")).Append("'");
                else
                    sb.Append(SheetName);
                sb.Append(".");
            }
            else
            {
                sb.Append("."); // Local reference prefix
            }

            if (IsColumnAbsolute) sb.Append("$");
            sb.Append(IndexToColumnName(Column));
            if (IsRowAbsolute) sb.Append("$");
            sb.Append(Row + 1);

            if (includeBrackets) sb.Append("]");
            return sb.ToString();
        }

        private static string IndexToColumnName(int columnIndex)
        {
            int temp = columnIndex + 1;
            var name = new StringBuilder();
            while (temp > 0)
            {
                int modulo = (temp - 1) % 26;
                name.Insert(0, (char)('A' + modulo));
                temp = (temp - modulo) / 26;
            }
            return name.ToString();
        }

        public OdfCellAddress ShiftStructural(int insertRowIndex, int rowCount, int insertColIndex, int colCount)
        {
            int newRow = Row;
            int newCol = Column;

            // Row shifts (rowCount can be negative for deletions)
            if (rowCount != 0)
            {
                if (rowCount > 0) // Insertion
                {
                    if (Row >= insertRowIndex) newRow += rowCount;
                }
                else // Deletion
                {
                    int deletedCount = -rowCount;
                    if (Row >= insertRowIndex + deletedCount)
                        newRow -= deletedCount;
                    else if (Row >= insertRowIndex)
                        newRow = insertRowIndex; // Clamp to start of deleted region
                }
            }

            // Column shifts (colCount can be negative for deletions)
            if (colCount != 0)
            {
                if (colCount > 0) // Insertion
                {
                    if (Column >= insertColIndex) newCol += colCount;
                }
                else // Deletion
                {
                    int deletedCount = -colCount;
                    if (Column >= insertColIndex + deletedCount)
                        newCol -= deletedCount;
                    else if (Column >= insertColIndex)
                        newCol = insertColIndex; // Clamp to start of deleted region
                }
            }

            return new OdfCellAddress(newRow, newCol, SheetName, IsRowAbsolute, IsColumnAbsolute, IsSheetAbsolute);
        }

        public override string ToString() => ToExcelString();
    }
}

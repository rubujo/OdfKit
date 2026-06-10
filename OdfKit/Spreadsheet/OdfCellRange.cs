using System;
using System.Text;

namespace OdfKit.Spreadsheet
{
    public readonly struct OdfCellRange : IEquatable<OdfCellRange>
    {
        public OdfCellAddress StartAddress { get; }
        public OdfCellAddress EndAddress { get; }

        public OdfCellRange(OdfCellAddress start, OdfCellAddress end)
        {
            StartAddress = start;
            EndAddress = end;
        }

        public bool Equals(OdfCellRange other) => 
            StartAddress == other.StartAddress && EndAddress == other.EndAddress;

        public override bool Equals(object? obj) => obj is OdfCellRange other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (StartAddress.GetHashCode() * 397) ^ EndAddress.GetHashCode();
            }
        }

        public static bool operator ==(OdfCellRange left, OdfCellRange right) => left.Equals(right);
        public static bool operator !=(OdfCellRange left, OdfCellRange right) => !left.Equals(right);

        public static bool TryParse(string value, out OdfCellRange range)
        {
            try
            {
                range = ParseExcel(value);
                return true;
            }
            catch
            {
                try
                {
                    range = ParseOdf(value);
                    return true;
                }
                catch
                {
                    range = default;
                    return false;
                }
            }
        }

        public static OdfCellRange ParseExcel(string rangeStr)
        {
            int colonIdx = FindUnquotedColon(rangeStr);
            if (colonIdx == -1)
            {
                var addr = OdfCellAddress.ParseExcel(rangeStr);
                return new OdfCellRange(addr, addr);
            }

            var start = OdfCellAddress.ParseExcel(rangeStr.Substring(0, colonIdx));
            var end = OdfCellAddress.ParseExcel(rangeStr.Substring(colonIdx + 1));

            // Propagate sheet name if missing on the end coordinate
            if (end.SheetName == null && start.SheetName != null)
            {
                end = new OdfCellAddress(end.Row, end.Column, start.SheetName, 
                    end.IsRowAbsolute, end.IsColumnAbsolute, start.IsSheetAbsolute);
            }

            return new OdfCellRange(start, end);
        }

        public static OdfCellRange ParseOdf(string rangeStr)
        {
            int colonIdx = FindUnquotedColon(rangeStr);
            if (colonIdx == -1)
            {
                var addr = OdfCellAddress.ParseOdf(rangeStr);
                return new OdfCellRange(addr, addr);
            }

            var start = OdfCellAddress.ParseOdf(rangeStr.Substring(0, colonIdx));
            var end = OdfCellAddress.ParseOdf(rangeStr.Substring(colonIdx + 1));

            if (end.SheetName == null && start.SheetName != null)
            {
                end = new OdfCellAddress(end.Row, end.Column, start.SheetName, 
                    end.IsRowAbsolute, end.IsColumnAbsolute, start.IsSheetAbsolute);
            }

            return new OdfCellRange(start, end);
        }

        private static int FindUnquotedColon(string str)
        {
            bool inQuotes = false;
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                if (c == '\'') inQuotes = !inQuotes;
                if (!inQuotes && c == ':') return i;
            }
            return -1;
        }

        public bool Contains(OdfCellAddress address)
        {
            // Check sheet equivalence
            if (!string.Equals(StartAddress.SheetName, address.SheetName, StringComparison.OrdinalIgnoreCase))
                return false;

            int minRow = Math.Min(StartAddress.Row, EndAddress.Row);
            int maxRow = Math.Max(StartAddress.Row, EndAddress.Row);
            int minCol = Math.Min(StartAddress.Column, EndAddress.Column);
            int maxCol = Math.Max(StartAddress.Column, EndAddress.Column);

            return address.Row >= minRow && address.Row <= maxRow &&
                   address.Column >= minCol && address.Column <= maxCol;
        }

        public bool Intersects(OdfCellRange other)
        {
            if (!string.Equals(StartAddress.SheetName, other.StartAddress.SheetName, StringComparison.OrdinalIgnoreCase))
                return false;

            int minRow1 = Math.Min(StartAddress.Row, EndAddress.Row);
            int maxRow1 = Math.Max(StartAddress.Row, EndAddress.Row);
            int minCol1 = Math.Min(StartAddress.Column, EndAddress.Column);
            int maxCol1 = Math.Max(StartAddress.Column, EndAddress.Column);

            int minRow2 = Math.Min(other.StartAddress.Row, other.EndAddress.Row);
            int maxRow2 = Math.Max(other.StartAddress.Row, other.EndAddress.Row);
            int minCol2 = Math.Min(other.StartAddress.Column, other.EndAddress.Column);
            int maxCol2 = Math.Max(other.StartAddress.Column, other.EndAddress.Column);

            return minRow1 <= maxRow2 && maxRow1 >= minRow2 &&
                   minCol1 <= maxCol2 && maxCol1 >= minCol2;
        }

        public OdfCellRange ShiftStructural(int insertRowIndex, int rowCount, int insertColIndex, int colCount)
        {
            var start = StartAddress.ShiftStructural(insertRowIndex, rowCount, insertColIndex, colCount);
            var end = EndAddress.ShiftStructural(insertRowIndex, rowCount, insertColIndex, colCount);
            return new OdfCellRange(start, end);
        }

        public string ToExcelString()
        {
            if (StartAddress.Equals(EndAddress))
            {
                return StartAddress.ToExcelString();
            }

            var startStr = StartAddress.ToExcelString();
            
            string endStr;
            if (StartAddress.SheetName != null && string.Equals(StartAddress.SheetName, EndAddress.SheetName, StringComparison.OrdinalIgnoreCase))
            {
                var temp = new OdfCellAddress(EndAddress.Row, EndAddress.Column, null, EndAddress.IsRowAbsolute, EndAddress.IsColumnAbsolute, EndAddress.IsSheetAbsolute);
                endStr = temp.ToExcelString();
            }
            else
            {
                endStr = EndAddress.ToExcelString();
            }

            return $"{startStr}:{endStr}";
        }

        public string ToOdfString(bool includeBrackets = false)
        {
            var sb = new StringBuilder();
            if (includeBrackets) sb.Append("[");

            var startStr = StartAddress.ToOdfString(false);
            
            string endStr;
            if (StartAddress.SheetName != null && string.Equals(StartAddress.SheetName, EndAddress.SheetName, StringComparison.OrdinalIgnoreCase))
            {
                var temp = new OdfCellAddress(EndAddress.Row, EndAddress.Column, null, EndAddress.IsRowAbsolute, EndAddress.IsColumnAbsolute, EndAddress.IsSheetAbsolute);
                endStr = temp.ToOdfString(false);
            }
            else
            {
                endStr = EndAddress.ToOdfString(false);
            }

            sb.Append(startStr).Append(":").Append(endStr);

            if (includeBrackets) sb.Append("]");
            return sb.ToString();
        }

        public override string ToString() => ToExcelString();
    }
}

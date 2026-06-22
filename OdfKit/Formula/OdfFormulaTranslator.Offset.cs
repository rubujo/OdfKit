using System;
using System.Text;
using OdfKit.Spreadsheet;

using OdfKit.Compliance;
namespace OdfKit.Formula;

public static partial class OdfFormulaTranslator
{
    #region Formula Offset

    /// <summary>
    /// 偏移公式中的相對儲存格參照。
    /// </summary>
    /// <param name="formula">要偏移的公式字串</param>
    /// <param name="rowOffset">列偏移量</param>
    /// <param name="colOffset">欄偏移量</param>
    /// <returns>偏移後的公式字串</returns>
    public static string TranslateFormulaOffset(string formula, int rowOffset, int colOffset)
    {
        if (string.IsNullOrEmpty(formula) || (rowOffset == 0 && colOffset == 0))
            return formula;

        bool isOdf = false;
        string prefix = "";
        string inner = formula;

        if (formula.StartsWith("oooc:="))
        { isOdf = true; prefix = "oooc:="; inner = formula.Substring(6); }
        else if (formula.StartsWith("of:="))
        { isOdf = true; prefix = "of:="; inner = formula.Substring(4); }
        else if (formula.StartsWith("="))
        { prefix = "="; inner = formula.Substring(1); }

        var tokens = Tokenize(inner);
        StringBuilder sb = new(prefix);

        foreach (var token in tokens)
        {
            if (token.Type == TokenType.CellReference)
            {
                try
                {
                    if (isOdf)
                    {
                        string raw = token.Value;
                        if (raw.StartsWith("[") && raw.EndsWith("]"))
                        {
                            string content = raw.Substring(1, raw.Length - 2);
                            if (content.Contains(':'))
                            {
                                var range = OdfCellRange.ParseOdf(content);
                                var shifted = ShiftRelativeRange(range, rowOffset, colOffset);
                                sb.Append('[').Append(shifted.ToOdfString(false)).Append(']');
                            }
                            else
                            {
                                var addr = OdfCellAddress.ParseOdf(content);
                                var shifted = ShiftRelativeAddress(addr, rowOffset, colOffset);
                                sb.Append('[').Append(shifted.ToOdfString(false)).Append(']');
                            }
                        }
                        else
                            sb.Append(token.Value);
                    }
                    else
                    {
                        string raw = token.Value;
                        if (raw.Contains(':'))
                        {
                            var range = OdfCellRange.ParseExcel(raw);
                            var shifted = ShiftRelativeRange(range, rowOffset, colOffset);
                            sb.Append(shifted.ToExcelString());
                        }
                        else
                        {
                            var addr = OdfCellAddress.ParseExcel(raw);
                            var shifted = ShiftRelativeAddress(addr, rowOffset, colOffset);
                            sb.Append(shifted.ToExcelString());
                        }
                    }
                }
                catch (ArgumentOutOfRangeException)
                {
                    sb.Append(isOdf ? "[.#REF!]" : "#REF!");
                }
                catch
                {
                    sb.Append(token.Value);
                }
            }
            else
            {
                sb.Append(token.Value);
            }
        }
        return sb.ToString();
    }

    private static OdfCellAddress ShiftRelativeAddress(OdfCellAddress addr, int rowOffset, int colOffset)
    {
        int newRow = addr.IsRowAbsolute ? addr.Row : addr.Row + rowOffset;
        int newCol = addr.IsColumnAbsolute ? addr.Column : addr.Column + colOffset;

        if (newRow < 0 || newCol < 0)
            throw new ArgumentOutOfRangeException(nameof(addr), OdfLocalizer.GetMessage("Err_OdfFormulaTranslator_IndexOffsetResultsOut"));

        return new OdfCellAddress(newRow, newCol, addr.SheetName,
            addr.IsRowAbsolute, addr.IsColumnAbsolute, addr.IsSheetAbsolute);
    }

    private static OdfCellRange ShiftRelativeRange(OdfCellRange range, int rowOffset, int colOffset)
    {
        var start = ShiftRelativeAddress(range.StartAddress, rowOffset, colOffset);
        var end = ShiftRelativeAddress(range.EndAddress, rowOffset, colOffset);
        return new OdfCellRange(start, end);
    }

    #endregion
}

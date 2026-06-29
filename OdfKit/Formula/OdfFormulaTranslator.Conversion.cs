using System;
using System.Text;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

/// <summary>
/// Converts formulas between ODF and Excel formats.
/// 提供 ODF 與 Excel 公式格式之間的轉換工具。
/// </summary>
public static partial class OdfFormulaTranslator
{
    #region Formula Conversion

    /// <summary>
    /// Converts an Excel-style formula to an ODF-style formula.
    /// 將 Excel 樣式的公式轉換為 ODF 樣式的公式。
    /// </summary>
    /// <param name="excelFormula">The Excel-style formula string. / Excel 樣式的公式字串。</param>
    /// <returns>The ODF-style formula string. / ODF 樣式的公式字串。</returns>
    public static string ExcelToOdfFormula(string excelFormula)
    {
        if (string.IsNullOrEmpty(excelFormula))
            return excelFormula;

        string inner = excelFormula;
        if (excelFormula.StartsWith("="))
            inner = excelFormula.Substring(1);

        var tokens = Tokenize(inner);
        StringBuilder sb = new("oooc:=");

        for (int idx = 0; idx < tokens.Count; idx++)
        {
            var token = tokens[idx];
            switch (token.Type)
            {
                case TokenType.Identifier:
                    // 當識別碼後方緊接 '(' 時，將其標準化為大寫的函式名稱
                    if (idx + 1 < tokens.Count && tokens[idx + 1].Type == TokenType.OpenParenthesis)
                        sb.Append(token.Value.ToUpperInvariant());
                    else
                        sb.Append(token.Value);
                    break;

                case TokenType.CellReference:
                    try
                    {
                        if (token.Value.Contains(':'))
                        {
                            var range = OdfCellRange.ParseExcel(token.Value);
                            sb.Append('[').Append(range.ToOdfString(false)).Append(']');
                        }
                        else
                        {
                            var addr = OdfCellAddress.ParseExcel(token.Value);
                            sb.Append('[').Append(addr.ToOdfString(false)).Append(']');
                        }
                    }
                    catch
                    {
                        sb.Append(token.Value); // 備用方案
                    }
                    break;

                case TokenType.Separator:
                    // 將參數逗號轉換為分號，保留大括號陣列的規則
                    sb.Append(token.Value == "," ? ";" : token.Value);
                    break;

                default:
                    sb.Append(token.Value);
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Converts an ODF-style formula to an Excel-style formula.
    /// 將 ODF 樣式的公式轉換為 Excel 樣式的公式。
    /// </summary>
    /// <param name="odfFormula">The ODF-style formula string. / ODF 樣式的公式字串。</param>
    /// <returns>The Excel-style formula string. / Excel 樣式的公式字串。</returns>
    public static string OdfToExcelFormula(string odfFormula)
    {
        if (string.IsNullOrEmpty(odfFormula))
            return odfFormula;

        string inner = odfFormula;
        if (odfFormula.StartsWith("oooc:=", StringComparison.OrdinalIgnoreCase))
            inner = odfFormula.Substring(6);
        else if (odfFormula.StartsWith("of:=", StringComparison.OrdinalIgnoreCase))
            inner = odfFormula.Substring(4);
        else if (odfFormula.StartsWith("="))
            inner = odfFormula.Substring(1);

        var tokens = Tokenize(inner);
        StringBuilder sb = new("=");

        for (int idx = 0; idx < tokens.Count; idx++)
        {
            var token = tokens[idx];
            switch (token.Type)
            {
                case TokenType.Identifier:
                    if (idx + 1 < tokens.Count && tokens[idx + 1].Type == TokenType.OpenParenthesis)
                        sb.Append(token.Value.ToUpperInvariant());
                    else
                        sb.Append(token.Value);
                    break;

                case TokenType.CellReference:
                    try
                    {
                        string raw = token.Value;
                        if (raw.StartsWith("[") && raw.EndsWith("]"))
                        {
                            string content = raw.Substring(1, raw.Length - 2);
                            if (content.Contains(':'))
                            {
                                var range = OdfCellRange.ParseOdf(content);
                                sb.Append(range.ToExcelString());
                            }
                            else
                            {
                                var addr = OdfCellAddress.ParseOdf(content);
                                sb.Append(addr.ToExcelString());
                            }
                        }
                        else
                            sb.Append(raw);
                    }
                    catch
                    {
                        sb.Append(token.Value);
                    }
                    break;

                case TokenType.Separator:
                    sb.Append(token.Value == ";" ? "," : token.Value);
                    break;

                default:
                    sb.Append(token.Value);
                    break;
            }
        }
        return sb.ToString();
    }

    #endregion
}

#pragma warning disable 1591 // Suppress CS1591 (missing XML comments) for legacy hand-written APIs to maintain zero-warning compilation under TreatWarningsAsErrors while package XML documentation is generated.
using System;
using System.Collections.Generic;
using System.Text;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula
{
    public enum TokenType
    {
        Identifier,
        CellReference,
        StringLiteral,
        Number,
        Operator,
        Separator, // ',' or ';'
        OpenParenthesis,
        CloseParenthesis,
        OpenBrace,
        CloseBrace,
        Whitespace,
        Unknown
    }

    public class FormulaToken
    {
        public TokenType Type { get; }
        public string Value { get; }
        public int StartIndex { get; }

        public FormulaToken(TokenType type, string value, int startIndex)
        {
            Type = type;
            Value = value;
            StartIndex = startIndex;
        }
    }

    public static class OdfFormulaTranslator
    {
        public static List<FormulaToken> Tokenize(string formula)
        {
            var tokens = new List<FormulaToken>();
            int i = 0;
            int length = formula.Length;

            while (i < length)
            {
                char c = formula[i];

                if (char.IsWhiteSpace(c))
                {
                    int start = i;
                    while (i < length && char.IsWhiteSpace(formula[i])) i++;
                    tokens.Add(new FormulaToken(TokenType.Whitespace, formula.Substring(start, i - start), start));
                    continue;
                }

                if (c == '"')
                {
                    int start = i++;
                    while (i < length)
                    {
                        if (formula[i] == '"')
                        {
                            if (i + 1 < length && formula[i + 1] == '"') i += 2; // Escaped ""
                            else { i++; break; }
                        }
                        else i++;
                    }
                    tokens.Add(new FormulaToken(TokenType.StringLiteral, formula.Substring(start, i - start), start));
                    continue;
                }

                // ODF Bracketed Cell Reference
                if (c == '[')
                {
                    int start = i++;
                    int bracketCount = 1;
                    while (i < length && bracketCount > 0)
                    {
                        if (formula[i] == '[') bracketCount++;
                        else if (formula[i] == ']') bracketCount--;
                        i++;
                    }
                    tokens.Add(new FormulaToken(TokenType.CellReference, formula.Substring(start, i - start), start));
                    continue;
                }

                if (c == ',' || c == ';')
                {
                    tokens.Add(new FormulaToken(TokenType.Separator, c.ToString(), i++));
                    continue;
                }

                if (c == '(') { tokens.Add(new FormulaToken(TokenType.OpenParenthesis, "(", i++)); continue; }
                if (c == ')') { tokens.Add(new FormulaToken(TokenType.CloseParenthesis, ")", i++)); continue; }
                if (c == '{') { tokens.Add(new FormulaToken(TokenType.OpenBrace, "{", i++)); continue; }
                if (c == '}') { tokens.Add(new FormulaToken(TokenType.CloseBrace, "}", i++)); continue; }

                if (c == '+' || c == '-' || c == '*' || c == '/' || c == '^' || c == '&' || c == '=')
                {
                    tokens.Add(new FormulaToken(TokenType.Operator, c.ToString(), i++));
                    continue;
                }

                if (c == '<' || c == '>')
                {
                    int start = i++;
                    if (i < length)
                    {
                        char next = formula[i];
                        if ((c == '<' && (next == '>' || next == '=')) || (c == '>' && next == '=')) i++;
                    }
                    tokens.Add(new FormulaToken(TokenType.Operator, formula.Substring(start, i - start), start));
                    continue;
                }

                if (char.IsDigit(c) || (c == '.' && i + 1 < length && char.IsDigit(formula[i + 1])))
                {
                    int start = i++;
                    bool hasDecimal = (c == '.');
                    while (i < length)
                    {
                        char next = formula[i];
                        if (char.IsDigit(next)) i++;
                        else if (next == '.' && !hasDecimal) { hasDecimal = true; i++; }
                        else break;
                    }
                    tokens.Add(new FormulaToken(TokenType.Number, formula.Substring(start, i - start), start));
                    continue;
                }

                if (char.IsLetter(c) || c == '_' || c == '\'' || c == '$')
                {
                    int start = i;
                    if (TryScanExcelReference(formula, start, out int consumed))
                    {
                        tokens.Add(new FormulaToken(TokenType.CellReference, formula.Substring(start, consumed), start));
                        i += consumed;
                    }
                    else
                    {
                        while (i < length && (char.IsLetterOrDigit(formula[i]) || formula[i] == '_')) i++;
                        tokens.Add(new FormulaToken(TokenType.Identifier, formula.Substring(start, i - start), start));
                    }
                    continue;
                }

                tokens.Add(new FormulaToken(TokenType.Unknown, c.ToString(), i++));
            }
            return tokens;
        }

        private static bool TryScanExcelReference(string formula, int start, out int consumed)
        {
            consumed = 0;
            int i = start;
            int length = formula.Length;

            // Optional Sheet name
            int sheetEnd = -1;
            if (i < length && formula[i] == '\'')
            {
                int temp = i + 1;
                while (temp < length)
                {
                    if (formula[temp] == '\'')
                    {
                        if (temp + 1 < length && formula[temp + 1] == '\'') temp += 2;
                        else { temp++; break; }
                    }
                    else temp++;
                }
                if (temp < length && formula[temp] == '!') sheetEnd = temp + 1;
            }
            else
            {
                int temp = i;
                while (temp < length && (char.IsLetterOrDigit(formula[temp]) || formula[temp] == '_' || formula[temp] == '$')) temp++;
                if (temp < length && formula[temp] == '!') sheetEnd = temp + 1;
            }

            if (sheetEnd != -1) i = sheetEnd;

            // Handle Column ranges (A:B) or Row ranges (1:10)
            int rangeStart = i;
            if (ScanColumnRange(formula, ref i) || ScanRowRange(formula, ref i))
            {
                consumed = i - start;
                return true;
            }
            i = rangeStart; // rollback

            // Handle standard Cell (A1) or Cell Range (A1:B10)
            if (!ScanCellCoordinate(formula, ref i)) return false;

            if (i < length && formula[i] == ':')
            {
                int sepIndex = i++;
                
                // Optional second sheet prefix
                int sheetEnd2 = -1;
                if (i < length && formula[i] == '\'')
                {
                    int temp = i + 1;
                    while (temp < length)
                    {
                        if (formula[temp] == '\'')
                        {
                            if (temp + 1 < length && formula[temp + 1] == '\'') temp += 2;
                            else { temp++; break; }
                        }
                        else temp++;
                    }
                    if (temp < length && formula[temp] == '!') sheetEnd2 = temp + 1;
                }
                else
                {
                    int temp = i;
                    while (temp < length && (char.IsLetterOrDigit(formula[temp]) || formula[temp] == '_' || formula[temp] == '$')) temp++;
                    if (temp < length && formula[temp] == '!') sheetEnd2 = temp + 1;
                }

                if (sheetEnd2 != -1) i = sheetEnd2;

                if (!ScanCellCoordinate(formula, ref i))
                {
                    i = sepIndex; // rollback to first coordinate
                }
            }

            consumed = i - start;
            return true;
        }

        private static bool ScanColumnRange(string formula, ref int i)
        {
            int start = i;
            int length = formula.Length;
            if (i < length && formula[i] == '$') i++;
            int colStart = i;
            while (i < length && char.IsLetter(formula[i])) i++;
            if (i == colStart || i >= length || formula[i] != ':') { i = start; return false; }
            i++; // skip ':'
            if (i < length && formula[i] == '$') i++;
            colStart = i;
            while (i < length && char.IsLetter(formula[i])) i++;
            if (i == colStart) { i = start; return false; }
            return true;
        }

        private static bool ScanRowRange(string formula, ref int i)
        {
            int start = i;
            int length = formula.Length;
            if (i < length && formula[i] == '$') i++;
            int rowStart = i;
            while (i < length && char.IsDigit(formula[i])) i++;
            if (i == rowStart || i >= length || formula[i] != ':') { i = start; return false; }
            i++; // skip ':'
            if (i < length && formula[i] == '$') i++;
            rowStart = i;
            while (i < length && char.IsDigit(formula[i])) i++;
            if (i == rowStart) { i = start; return false; }
            return true;
        }

        private static bool ScanCellCoordinate(string formula, ref int i)
        {
            int start = i;
            int length = formula.Length;
            if (i < length && formula[i] == '$') i++;
            int colStart = i;
            while (i < length && char.IsLetter(formula[i])) i++;
            if (i == colStart) { i = start; return false; }
            int colLen = i - colStart;
            if (colLen > 3) { i = start; return false; }

            if (i < length && formula[i] == '$') i++;
            int rowStart = i;
            while (i < length && char.IsDigit(formula[i])) i++;
            if (i == rowStart) { i = start; return false; }

            // Ensure the character immediately following the coordinate is not an identifier character
            if (i < length && (char.IsLetterOrDigit(formula[i]) || formula[i] == '_'))
            {
                i = start;
                return false;
            }

            return true;
        }

        public static string ExcelToOdfFormula(string excelFormula)
        {
            if (string.IsNullOrEmpty(excelFormula)) return excelFormula;

            string inner = excelFormula;
            if (excelFormula.StartsWith("=")) inner = excelFormula.Substring(1);

            var tokens = Tokenize(inner);
            var sb = new StringBuilder("oooc:=");

            for (int idx = 0; idx < tokens.Count; idx++)
            {
                var token = tokens[idx];
                switch (token.Type)
                {
                    case TokenType.Identifier:
                        // Normalize function names to uppercase when followed by '('
                        if (idx + 1 < tokens.Count && tokens[idx + 1].Type == TokenType.OpenParenthesis)
                            sb.Append(token.Value.ToUpperInvariant());
                        else
                            sb.Append(token.Value);
                        break;

                    case TokenType.CellReference:
                        try
                        {
                            if (token.Value.Contains(":"))
                            {
                                var range = OdfCellRange.ParseExcel(token.Value);
                                sb.Append("[").Append(range.ToOdfString(false)).Append("]");
                            }
                            else
                            {
                                var addr = OdfCellAddress.ParseExcel(token.Value);
                                sb.Append("[").Append(addr.ToOdfString(false)).Append("]");
                            }
                        }
                        catch
                        {
                            sb.Append(token.Value); // Fallback
                        }
                        break;

                    case TokenType.Separator:
                        // Convert parameter commas to semicolons, preserving braces array rules
                        sb.Append(token.Value == "," ? ";" : token.Value);
                        break;

                    default:
                        sb.Append(token.Value);
                        break;
                }
            }
            return sb.ToString();
        }

        public static string OdfToExcelFormula(string odfFormula)
        {
            if (string.IsNullOrEmpty(odfFormula)) return odfFormula;

            string inner = odfFormula;
            if (odfFormula.StartsWith("oooc:=")) inner = odfFormula.Substring(6);
            else if (odfFormula.StartsWith("of:=")) inner = odfFormula.Substring(4);
            else if (odfFormula.StartsWith("=")) inner = odfFormula.Substring(1);

            var tokens = Tokenize(inner);
            var sb = new StringBuilder("=");

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
                                if (content.Contains(":"))
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
                            else sb.Append(raw);
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

        public static string TranslateFormulaOffset(string formula, int rowOffset, int colOffset)
        {
            if (string.IsNullOrEmpty(formula) || (rowOffset == 0 && colOffset == 0))
                return formula;

            bool isOdf = false;
            string prefix = "";
            string inner = formula;

            if (formula.StartsWith("oooc:=")) { isOdf = true; prefix = "oooc:="; inner = formula.Substring(6); }
            else if (formula.StartsWith("of:=")) { isOdf = true; prefix = "of:="; inner = formula.Substring(4); }
            else if (formula.StartsWith("=")) { prefix = "="; inner = formula.Substring(1); }

            var tokens = Tokenize(inner);
            var sb = new StringBuilder(prefix);

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
                                if (content.Contains(":"))
                                {
                                    var range = OdfCellRange.ParseOdf(content);
                                    var shifted = ShiftRelativeRange(range, rowOffset, colOffset);
                                    sb.Append("[").Append(shifted.ToOdfString(false)).Append("]");
                                }
                                else
                                {
                                    var addr = OdfCellAddress.ParseOdf(content);
                                    var shifted = ShiftRelativeAddress(addr, rowOffset, colOffset);
                                    sb.Append("[").Append(shifted.ToOdfString(false)).Append("]");
                                }
                            }
                            else sb.Append(token.Value);
                        }
                        else
                        {
                            string raw = token.Value;
                            if (raw.Contains(":"))
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
                throw new ArgumentOutOfRangeException(nameof(addr), "Index offset results in out of bounds coordinate.");

            return new OdfCellAddress(newRow, newCol, addr.SheetName, 
                addr.IsRowAbsolute, addr.IsColumnAbsolute, addr.IsSheetAbsolute);
        }

        private static OdfCellRange ShiftRelativeRange(OdfCellRange range, int rowOffset, int colOffset)
        {
            var start = ShiftRelativeAddress(range.StartAddress, rowOffset, colOffset);
            var end = ShiftRelativeAddress(range.EndAddress, rowOffset, colOffset);
            return new OdfCellRange(start, end);
        }
    }
}

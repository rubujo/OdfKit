using System;
using System.Collections.Generic;

namespace OdfKit.Formula;

/// <summary>
/// 提供 ODF 與 Excel 公式格式之間的轉換與偏移工具。
/// </summary>
public static partial class OdfFormulaTranslator
{
    #region Tokenization

    /// <summary>
    /// 將公式字串分割為語彙基元清單。
    /// </summary>
    /// <param name="formula">公式字串</param>
    /// <returns>語彙基元清單</returns>
    public static List<FormulaToken> Tokenize(string formula)
    {
        List<FormulaToken> tokens = [];
        int i = 0;
        int length = formula.Length;

        while (i < length)
        {
            char c = formula[i];

            if (char.IsWhiteSpace(c))
            {
                int start = i;
                while (i < length && char.IsWhiteSpace(formula[i]))
                    i++;
                tokens.Add(new(TokenType.Whitespace, formula.Substring(start, i - start), start));
                continue;
            }

            if (c == '"')
            {
                int start = i++;
                while (i < length)
                {
                    if (formula[i] == '"')
                    {
                        if (i + 1 < length && formula[i + 1] == '"')
                            i += 2; // 逸出引號 ""
                        else
                        { i++; break; }
                    }
                    else
                        i++;
                }
                tokens.Add(new(TokenType.StringLiteral, formula.Substring(start, i - start), start));
                continue;
            }

            // ODF 括號儲存格參照
            if (c == '[')
            {
                int start = i++;
                int bracketCount = 1;
                while (i < length && bracketCount > 0)
                {
                    if (formula[i] == '[')
                        bracketCount++;
                    else if (formula[i] == ']')
                        bracketCount--;
                    i++;
                }
                tokens.Add(new(TokenType.CellReference, formula.Substring(start, i - start), start));
                continue;
            }

            if (c == ',' || c == ';')
            {
                tokens.Add(new(TokenType.Separator, c.ToString(), i++));
                continue;
            }

            if (c == '(')
            { tokens.Add(new(TokenType.OpenParenthesis, "(", i++)); continue; }
            if (c == ')')
            { tokens.Add(new(TokenType.CloseParenthesis, ")", i++)); continue; }
            if (c == '{')
            { tokens.Add(new(TokenType.OpenBrace, "{", i++)); continue; }
            if (c == '}')
            { tokens.Add(new(TokenType.CloseBrace, "}", i++)); continue; }

            if (c == '+' || c == '-' || c == '*' || c == '/' || c == '^' || c == '&' || c == '=')
            {
                tokens.Add(new(TokenType.Operator, c.ToString(), i++));
                continue;
            }

            if (c == '<' || c == '>')
            {
                int start = i++;
                if (i < length)
                {
                    char next = formula[i];
                    if ((c == '<' && (next == '>' || next == '=')) || (c == '>' && next == '='))
                        i++;
                }
                tokens.Add(new(TokenType.Operator, formula.Substring(start, i - start), start));
                continue;
            }

            if (char.IsDigit(c) || (c == '.' && i + 1 < length && char.IsDigit(formula[i + 1])))
            {
                int start = i++;
                bool hasDecimal = (c == '.');
                while (i < length)
                {
                    char next = formula[i];
                    if (char.IsDigit(next))
                        i++;
                    else if (next == '.' && !hasDecimal)
                    { hasDecimal = true; i++; }
                    else
                        break;
                }
                tokens.Add(new(TokenType.Number, formula.Substring(start, i - start), start));
                continue;
            }

            if (char.IsLetter(c) || c == '_' || c == '\'' || c == '$')
            {
                int start = i;
                if (TryScanExcelReference(formula, start, out int consumed))
                {
                    tokens.Add(new(TokenType.CellReference, formula.Substring(start, consumed), start));
                    i += consumed;
                }
                else
                {
                    while (i < length && (char.IsLetterOrDigit(formula[i]) || formula[i] == '_'))
                        i++;
                    tokens.Add(new(TokenType.Identifier, formula.Substring(start, i - start), start));
                }
                continue;
            }

            tokens.Add(new(TokenType.Unknown, c.ToString(), i++));
        }
        return tokens;
    }

    private static bool TryScanExcelReference(string formula, int start, out int consumed)
    {
        consumed = 0;
        int i = start;
        int length = formula.Length;

        // 選擇性工作表名稱
        int sheetEnd = -1;
        if (i < length && formula[i] == '\'')
        {
            int temp = i + 1;
            while (temp < length)
            {
                if (formula[temp] == '\'')
                {
                    if (temp + 1 < length && formula[temp + 1] == '\'')
                        temp += 2;
                    else
                    { temp++; break; }
                }
                else
                    temp++;
            }
            if (temp < length && formula[temp] == '!')
                sheetEnd = temp + 1;
        }
        else
        {
            int temp = i;
            while (temp < length && (char.IsLetterOrDigit(formula[temp]) || formula[temp] == '_' || formula[temp] == '$'))
                temp++;
            if (temp < length && formula[temp] == '!')
                sheetEnd = temp + 1;
        }

        if (sheetEnd != -1)
            i = sheetEnd;

        // 處理欄範圍 (A:B) 或列範圍 (1:10)
        int rangeStart = i;
        if (ScanColumnRange(formula, ref i) || ScanRowRange(formula, ref i))
        {
            consumed = i - start;
            return true;
        }
        i = rangeStart; // 回滾

        // 處理標準儲存格 (A1) 或儲存格範圍 (A1:B10)
        if (!ScanCellCoordinate(formula, ref i))
            return false;

        if (i < length && formula[i] == ':')
        {
            int sepIndex = i++;

            // 選擇性第二個工作表前綴
            int sheetEnd2 = -1;
            if (i < length && formula[i] == '\'')
            {
                int temp = i + 1;
                while (temp < length)
                {
                    if (formula[temp] == '\'')
                    {
                        if (temp + 1 < length && formula[temp + 1] == '\'')
                            temp += 2;
                        else
                        { temp++; break; }
                    }
                    else
                        temp++;
                }
                if (temp < length && formula[temp] == '!')
                    sheetEnd2 = temp + 1;
            }
            else
            {
                int temp = i;
                while (temp < length && (char.IsLetterOrDigit(formula[temp]) || formula[temp] == '_' || formula[temp] == '$'))
                    temp++;
                if (temp < length && formula[temp] == '!')
                    sheetEnd2 = temp + 1;
            }

            if (sheetEnd2 != -1)
                i = sheetEnd2;

            if (!ScanCellCoordinate(formula, ref i))
            {
                i = sepIndex; // 回滾至第一個座標
            }
        }

        consumed = i - start;
        return true;
    }

    private static bool ScanColumnRange(string formula, ref int i)
    {
        int start = i;
        int length = formula.Length;
        if (i < length && formula[i] == '$')
            i++;
        int colStart = i;
        while (i < length && char.IsLetter(formula[i]))
            i++;
        if (i == colStart || i >= length || formula[i] != ':')
        { i = start; return false; }
        i++; // 跳過 ':'
        if (i < length && formula[i] == '$')
            i++;
        colStart = i;
        while (i < length && char.IsLetter(formula[i]))
            i++;
        if (i == colStart)
        { i = start; return false; }
        return true;
    }

    private static bool ScanRowRange(string formula, ref int i)
    {
        int start = i;
        int length = formula.Length;
        if (i < length && formula[i] == '$')
            i++;
        int rowStart = i;
        while (i < length && char.IsDigit(formula[i]))
            i++;
        if (i == rowStart || i >= length || formula[i] != ':')
        { i = start; return false; }
        i++; // 跳過 ':'
        if (i < length && formula[i] == '$')
            i++;
        rowStart = i;
        while (i < length && char.IsDigit(formula[i]))
            i++;
        if (i == rowStart)
        { i = start; return false; }
        return true;
    }

    private static bool ScanCellCoordinate(string formula, ref int i)
    {
        int start = i;
        int length = formula.Length;
        if (i < length && formula[i] == '$')
            i++;
        int colStart = i;
        while (i < length && char.IsLetter(formula[i]))
            i++;
        if (i == colStart)
        { i = start; return false; }
        int colLen = i - colStart;
        if (colLen > 3)
        { i = start; return false; }

        if (i < length && formula[i] == '$')
            i++;
        int rowStart = i;
        while (i < length && char.IsDigit(formula[i]))
            i++;
        if (i == rowStart)
        { i = start; return false; }

        // 確保座標後的下一個字元不是識別碼字元
        if (i < length && (char.IsLetterOrDigit(formula[i]) || formula[i] == '_'))
        {
            i = start;
            return false;
        }

        return true;
    }

    #endregion
}

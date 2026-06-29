using System.Globalization;
using System;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

/// <summary>
/// Tokenizes formula strings.
/// 公式字串的分詞器。
/// </summary>
/// <param name="formula">The formula character span to tokenize. / 要分詞的公式字元範圍。</param>
public ref struct Tokenizer(ReadOnlySpan<char> formula)
{
    private readonly ReadOnlySpan<char> _formula = formula;
    private int _index = 0;

    /// <summary>
    /// Gets the next token.
    /// 取得下一個語彙基元。
    /// </summary>
    /// <returns>The next token. / 下一個語彙基元。</returns>
    public FormulaParserToken NextToken()
    {
        SkipWhitespace();

        if (_index >= _formula.Length)
            return new(FormulaTokenType.EndOfFormula, ReadOnlySpan<char>.Empty);

        char current = _formula[_index];

        // 1. 括號與分隔符號
        if (current == '(')
        { _index++; return new(FormulaTokenType.OpenParen, _formula.Slice(_index - 1, 1)); }
        if (current == ')')
        { _index++; return new(FormulaTokenType.CloseParen, _formula.Slice(_index - 1, 1)); }
        if (current == ',' || current == ';')
        { _index++; return new(FormulaTokenType.Separator, _formula.Slice(_index - 1, 1)); }
        if (current == ':')
        { _index++; return new(FormulaTokenType.Colon, _formula.Slice(_index - 1, 1)); }

        // 2. 字串常值
        if (current == '"')
        {
            int start = _index;
            _index++; // 跳過起始引號
            while (_index < _formula.Length)
            {
                if (_formula[_index] == '"')
                {
                    if (_index + 1 < _formula.Length && _formula[_index + 1] == '"')
                    {
                        _index += 2; // 跳過逸出引號 ""
                    }
                    else
                    {
                        _index++; // 跳過結束引號
                        break;
                    }
                }
                else
                {
                    _index++;
                }
            }
            return new(FormulaTokenType.String, _formula.Slice(start, _index - start));
        }

        // 3. 運算子
        if (current == '<' || current == '>' || current == '=')
        {
            int start = _index;
            _index++;
            if (_index < _formula.Length)
            {
                char next = _formula[_index];
                if ((current == '<' && (next == '>' || next == '=')) ||
                    (current == '>' && next == '='))
                {
                    _index++;
                }
            }
            return new(FormulaTokenType.Operator, _formula.Slice(start, _index - start));
        }

        if (current == '+' || current == '-' || current == '*' || current == '/' || current == '^' || current == '&' || current == '%' || current == '~' || current == '!')
        {
            _index++;
            return new(FormulaTokenType.Operator, _formula.Slice(_index - 1, 1));
        }

        // 4. 數字常值
        if (char.IsDigit(current) || current == '.')
        {
            int start = _index;
            bool hasDot = current == '.';
            _index++;
            while (_index < _formula.Length)
            {
                char c = _formula[_index];
                if (char.IsDigit(c))
                {
                    _index++;
                }
                else if (c == '.' && !hasDot)
                {
                    hasDot = true;
                    _index++;
                }
                else
                {
                    break;
                }
            }
            var numSpan = _formula.Slice(start, _index - start);
            double val = ParseDouble(numSpan);
            return new(FormulaTokenType.Number, numSpan, val);
        }

        // 5. 識別碼、函式、座標
        if (char.IsLetter(current) || current == '$' || current == '_' || current == '\'')
        {
            int start = _index;
            if (current == '\'')
            {
                // 處理引號工作表名稱：'Sheet Name'.A1 或 'Sheet Name'!A1
                _index++;
                while (_index < _formula.Length && _formula[_index] != '\'')
                {
                    _index++;
                }
                if (_index < _formula.Length)
                    _index++; // 跳過結束引號
            }

            while (_index < _formula.Length)
            {
                char c = _formula[_index];
                if (c == '!')
                {
                    // 檢查目前掃描的內容是否為有效的儲存格位址。
                    // 如果是，則 '!' 為交集運算子，而非工作表分隔符號。
                    string prefix = _formula.Slice(start, _index - start).ToString();
                    if (OdfCellAddress.TryParse(prefix, out _))
                    {
                        break;
                    }
                }

                if (char.IsLetterOrDigit(c) || c == '$' || c == '_' || c == '.' || c == '!')
                {
                    _index++;
                }
                else
                {
                    break;
                }
            }

            var identSpan = _formula.Slice(start, _index - start);

            // 快速檢查是否為邏輯常值
            bool isFunctionCall = false;
            int peekIdx = _index;
            while (peekIdx < _formula.Length && char.IsWhiteSpace(_formula[peekIdx]))
            {
                peekIdx++;
            }
            if (peekIdx < _formula.Length && _formula[peekIdx] == '(')
            {
                isFunctionCall = true;
            }

            if (!isFunctionCall)
            {
                if (identSpan.Equals("TRUE", StringComparison.OrdinalIgnoreCase))
                    return new(FormulaTokenType.Bool, identSpan, boolValue: true);
                if (identSpan.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
                    return new(FormulaTokenType.Bool, identSpan, boolValue: false);
            }

            return new(FormulaTokenType.Identifier, identSpan);
        }

        // 未知字元的備用方案
        int errStart = _index;
        _index++;
        return new(FormulaTokenType.Operator, _formula.Slice(errStart, 1));
    }

    private void SkipWhitespace()
    {
        while (_index < _formula.Length && char.IsWhiteSpace(_formula[_index]))
        {
            _index++;
        }
    }

    private static double ParseDouble(ReadOnlySpan<char> span)
    {
#if NET10_0_OR_GREATER
        return double.Parse(span, NumberStyles.Any, CultureInfo.InvariantCulture);
#else
        return double.Parse(span.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture);
#endif
    }
}

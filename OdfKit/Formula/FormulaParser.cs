using System;
using System.Collections.Generic;
using OdfKit.Spreadsheet;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

/// <summary>
/// 代表公式剖析器的語彙基元型別。
/// </summary>
public enum FormulaTokenType
{
    /// <summary>
    /// 數字。
    /// </summary>
    Number,

    /// <summary>
    /// 字串。
    /// </summary>
    String,

    /// <summary>
    /// 布林值。
    /// </summary>
    Bool,

    /// <summary>
    /// 識別碼 (例如函式名稱、儲存格或範圍名稱)。
    /// </summary>
    Identifier,

    /// <summary>
    /// 運算子，例如 +, -, *, /, ^, &amp;, =, &lt;, &gt;, &lt;=, &gt;=, &lt;&gt; 等。
    /// </summary>
    Operator,

    /// <summary>
    /// 左括號 (。
    /// </summary>
    OpenParen,

    /// <summary>
    /// 右括號 )。
    /// </summary>
    CloseParen,

    /// <summary>
    /// 分隔符號，即 「,」 或 「;」。
    /// </summary>
    Separator,

    /// <summary>
    /// 冒號 :。
    /// </summary>
    Colon,

    /// <summary>
    /// 公式結尾。
    /// </summary>
    EndOfFormula
}

/// <summary>
/// 代表公式剖析器的語彙基元。
/// </summary>
/// <param name="type">語彙基元型別</param>
/// <param name="span">語彙基元的字元範圍</param>
/// <param name="numValue">數值</param>
/// <param name="boolValue">布林值</param>
public readonly ref struct FormulaParserToken(FormulaTokenType type, ReadOnlySpan<char> span, double numValue = 0, bool boolValue = false)
{
    /// <summary>
    /// 取得語彙基元的型別。
    /// </summary>
    public FormulaTokenType Type { get; } = type;

    /// <summary>
    /// 取得語彙基元的字元範圍。
    /// </summary>
    public ReadOnlySpan<char> Span { get; } = span;

    /// <summary>
    /// 取得語彙基元的雙倍精確度浮點數數值。
    /// </summary>
    public double NumberValue { get; } = numValue;

    /// <summary>
    /// 取得語彙基元的布林值。
    /// </summary>
    public bool BoolValue { get; } = boolValue;
}

/// <summary>
/// 公式字串的分詞器。
/// </summary>
/// <param name="formula">要分詞的公式字元範圍</param>
public ref struct Tokenizer(ReadOnlySpan<char> formula)
{
    private readonly ReadOnlySpan<char> _formula = formula;
    private int _index = 0;

    /// <summary>
    /// 取得下一個語彙基元。
    /// </summary>
    /// <returns>下一個語彙基元</returns>
    public FormulaParserToken NextToken()
    {
        SkipWhitespace();

        if (_index >= _formula.Length)
            return new(FormulaTokenType.EndOfFormula, ReadOnlySpan<char>.Empty);

        char current = _formula[_index];

        // 1. 括號與分隔符號
        if (current == '(') { _index++; return new(FormulaTokenType.OpenParen, _formula.Slice(_index - 1, 1)); }
        if (current == ')') { _index++; return new(FormulaTokenType.CloseParen, _formula.Slice(_index - 1, 1)); }
        if (current == ',' || current == ';') { _index++; return new(FormulaTokenType.Separator, _formula.Slice(_index - 1, 1)); }
        if (current == ':') { _index++; return new(FormulaTokenType.Colon, _formula.Slice(_index - 1, 1)); }

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
                if (_index < _formula.Length) _index++; // 跳過結束引號
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
        return double.Parse(span, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture);
#else
        return double.Parse(span.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture);
#endif
    }
}

/// <summary>
/// 公式剖析器，用於將公式字串剖析為抽象語法樹 (AST)。
/// </summary>
public ref struct FormulaParser
{
    private Tokenizer _tokenizer;
    private FormulaParserToken _currentToken;

    /// <summary>
    /// 使用指定的公式字串初始化 <see cref="FormulaParser"/> 結構的新執行個體。
    /// </summary>
    /// <param name="formula">公式字串</param>
    public FormulaParser(string formula)
    {
        _tokenizer = new Tokenizer(formula.AsSpan());
        _currentToken = _tokenizer.NextToken();
    }

    private void Consume()
    {
        _currentToken = _tokenizer.NextToken();
    }

    /// <summary>
    /// 開始剖析公式。
    /// </summary>
    /// <returns>剖析後的 AST 根節點</returns>
    /// <exception cref="InvalidOperationException">當公式結尾有未預期的語彙基元時擲出</exception>
    public AstNode Parse()
    {
        var node = ParseExpression();
        if (_currentToken.Type != FormulaTokenType.EndOfFormula)
        {
            throw new InvalidOperationException($"Unexpected token at the end of formula: {_currentToken.Span.ToString()}");
        }
        return node;
    }

    // 優先權 1：邏輯運算 (比較)
    private AstNode ParseExpression()
    {
        var node = ParseConcat();
        while (_currentToken.Type == FormulaTokenType.Operator && IsComparisonOperator(_currentToken.Span))
        {
            string op = _currentToken.Span.ToString();
            Consume();
            var right = ParseConcat();
            node = new BinaryNode(op, node, right);
        }
        return node;
    }

    private static bool IsComparisonOperator(ReadOnlySpan<char> op)
    {
        return op.Equals("=", StringComparison.Ordinal) ||
               op.Equals("<", StringComparison.Ordinal) ||
               op.Equals(">", StringComparison.Ordinal) ||
               op.Equals("<=", StringComparison.Ordinal) ||
               op.Equals(">=", StringComparison.Ordinal) ||
               op.Equals("<>", StringComparison.Ordinal);
    }

    // 優先權 2：字串連接 (&amp;)
    private AstNode ParseConcat()
    {
        var node = ParseTerm();
        while (_currentToken.Type == FormulaTokenType.Operator && _currentToken.Span.Equals("&", StringComparison.Ordinal))
        {
            Consume();
            var right = ParseTerm();
            node = new BinaryNode("&", node, right);
        }
        return node;
    }

    // 優先權 3：項運算 (+, -)
    private AstNode ParseTerm()
    {
        var node = ParseFactor();
        while (_currentToken.Type == FormulaTokenType.Operator && 
              (_currentToken.Span.Equals("+", StringComparison.Ordinal) || _currentToken.Span.Equals("-", StringComparison.Ordinal)))
        {
            string op = _currentToken.Span.ToString();
            Consume();
            var right = ParseFactor();
            node = new BinaryNode(op, node, right);
        }
        return node;
    }

    // 優先權 4：因數運算 (*, /)
    private AstNode ParseFactor()
    {
        var node = ParsePower();
        while (_currentToken.Type == FormulaTokenType.Operator && 
              (_currentToken.Span.Equals("*", StringComparison.Ordinal) || _currentToken.Span.Equals("/", StringComparison.Ordinal)))
        {
            string op = _currentToken.Span.ToString();
            Consume();
            var right = ParsePower();
            node = new BinaryNode(op, node, right);
        }
        return node;
    }

    // 優先權 5：乘方運算 (^)
    private AstNode ParsePower()
    {
        var node = ParseUnary();
        while (_currentToken.Type == FormulaTokenType.Operator && _currentToken.Span.Equals("^", StringComparison.Ordinal))
        {
            Consume();
            var right = ParseUnary();
            node = new BinaryNode("^", node, right);
        }
        return node;
    }

    // 優先權 6：單元運算 (+, -, %)
    private AstNode ParseUnary()
    {
        if (_currentToken.Type == FormulaTokenType.Operator && 
           (_currentToken.Span.Equals("+", StringComparison.Ordinal) || _currentToken.Span.Equals("-", StringComparison.Ordinal)))
        {
            char op = _currentToken.Span[0];
            Consume();
            var child = ParseUnary();
            return new UnaryNode(op, child);
        }

        var node = ParseReferenceExpression();

        while (_currentToken.Type == FormulaTokenType.Operator && _currentToken.Span.Equals("%", StringComparison.Ordinal))
        {
            Consume();
            node = new UnaryNode('%', node);
        }

        return node;
    }

    private AstNode ParseReferenceExpression()
    {
        var node = ParseIntersectionExpression();
        while (_currentToken.Type == FormulaTokenType.Operator && _currentToken.Span.Equals("~", StringComparison.Ordinal))
        {
            Consume();
            var right = ParseIntersectionExpression();
            node = new ReferenceUnionNode(node, right);
        }
        return node;
    }

    private AstNode ParseIntersectionExpression()
    {
        var node = ParsePrimary();
        while (_currentToken.Type == FormulaTokenType.Operator && _currentToken.Span.Equals("!", StringComparison.Ordinal))
        {
            Consume();
            var right = ParsePrimary();
            node = new ReferenceIntersectionNode(node, right);
        }
        return node;
    }

    // 優先權 7：主要運算式 (常值、括號、函式、儲存格/範圍)
    private AstNode ParsePrimary()
    {
        if (_currentToken.Type == FormulaTokenType.Number)
        {
            double val = _currentToken.NumberValue;
            Consume();
            return new LiteralNode(val);
        }

        if (_currentToken.Type == FormulaTokenType.String)
        {
            string raw = _currentToken.Span.ToString();
            string strVal = raw.Substring(1, raw.Length - 2).Replace("\"\"", "\"");
            Consume();
            return new LiteralNode(strVal);
        }

        if (_currentToken.Type == FormulaTokenType.Bool)
        {
            bool val = _currentToken.BoolValue;
            Consume();
            return new LiteralNode(val);
        }

        if (_currentToken.Type == FormulaTokenType.OpenParen)
        {
            Consume();
            var node = ParseExpression();
            if (_currentToken.Type != FormulaTokenType.CloseParen)
            {
                throw new InvalidOperationException("Mismatched parentheses: expected CloseParen.");
            }
            Consume();
            return new ParenthesizedNode(node);
        }

        if (_currentToken.Type == FormulaTokenType.Identifier)
        {
            string ident = _currentToken.Span.ToString();
            Consume();

            // 1. 檢查是否為函式呼叫
            if (_currentToken.Type == FormulaTokenType.OpenParen)
            {
                Consume(); // 消耗 '('
                List<AstNode> args = [];
                if (_currentToken.Type != FormulaTokenType.CloseParen)
                {
                    args.Add(ParseExpression());
                    while (_currentToken.Type == FormulaTokenType.Separator)
                    {
                        Consume();
                        args.Add(ParseExpression());
                    }
                }
                if (_currentToken.Type != FormulaTokenType.CloseParen)
                {
                    throw new InvalidOperationException("Mismatched parentheses in function call.");
                }
                Consume(); // 消耗 ')'
                return new FunctionNode(ident, args);
            }

            // 2. 檢查是否為範圍或儲存格參照
            if (_currentToken.Type == FormulaTokenType.Colon)
            {
                // 範圍情況：A1:B10
                Consume(); // 消耗 ':'
                if (_currentToken.Type != FormulaTokenType.Identifier)
                {
                    throw new InvalidOperationException("Invalid range reference layout: missing end coordinate.");
                }
                string endIdent = _currentToken.Span.ToString();
                Consume();

                string fullRangeStr = $"{ident}:{endIdent}";
                if (OdfCellRange.TryParse(fullRangeStr, out var range))
                {
                    return new RangeReferenceNode(range);
                }
                throw new InvalidOperationException($"Failed to parse range string '{fullRangeStr}'.");
            }

            // 單一儲存格或單一工作表限定之儲存格 A1 或 Sheet1.A1
            if (OdfCellRange.TryParse(ident, out var cellRange))
            {
                if (cellRange.StartAddress == cellRange.EndAddress)
                {
                    return new CellAddressNode(cellRange.StartAddress);
                }
                return new RangeReferenceNode(cellRange);
            }

            return new NamedRangeNode(ident);
        }

        throw new InvalidOperationException($"Unexpected token type {_currentToken.Type} during parsing.");
    }
}

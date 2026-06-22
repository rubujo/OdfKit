using System;
using System.Collections.Generic;
using OdfKit.Formula.AST;
using OdfKit.Spreadsheet;

using OdfKit.Compliance;
namespace OdfKit.Formula;

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
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_FormulaParser_UnexpectedTokenEndFormula", _currentToken.Span.ToString()));
        }
        return node;
    }

    // 優先權 1：邏輯運算（比較）
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

    // 優先權 7：主要運算式（常值、括號、函式、儲存格／範圍）
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
                throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_FormulaParser_MismatchedParenthesesExpectedCloseparen"));
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
                    throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_FormulaParser_MismatchedParenthesesFunctionCall"));
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
                    throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_FormulaParser_InvalidNotFound"));
                }
                string endIdent = _currentToken.Span.ToString();
                Consume();

                string fullRangeStr = $"{ident}:{endIdent}";
                if (OdfCellRange.TryParse(fullRangeStr, out var range))
                {
                    return new RangeReferenceNode(range);
                }
                throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_FormulaParser_FailedToParseRangeString", fullRangeStr));
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

        throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_FormulaParser_UnexpectedTokenTypeDuring", _currentToken.Type));
    }
}

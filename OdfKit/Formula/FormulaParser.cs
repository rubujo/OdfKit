using System;
using System.Collections.Generic;
using OdfKit.Formula.AST;
using OdfKit.Spreadsheet;

using OdfKit.Compliance;
namespace OdfKit.Formula;

/// <summary>
/// Parses formula strings into abstract syntax tree (AST) nodes.
/// 公式剖析器，用於將公式字串剖析為抽象語法樹 (AST)。
/// </summary>
public ref struct FormulaParser
{
    /// <summary>
    /// 括號、函式引數與內嵌陣列的最大巢狀深度，也是連續前置運算子的最大長度。
    /// 遞迴下降剖析與後續求值都以遞迴實作，過深的輸入會造成無法攔截的堆疊溢位而使整個處理程序崩潰。
    /// </summary>
    internal const int MaxNestingDepth = 256;

    /// <summary>
    /// 單一公式允許的運算子節點總數（二元、前置、百分比、聯集、交集與範圍運算子）。
    /// 左結合的運算子鏈（<c>1+1+…</c>、<c>1%%%…</c>）會產生與運算子個數等高的 AST，
    /// 而求值、序列化與相依性分析都以遞迴走訪；數千層即可讓堆疊 ≤1 MB 的執行緒溢位而使整個處理程序崩潰。
    /// 4,096 個運算子對應 LibreOffice／Excel 自身約 8,192 個記號的公式上限。
    /// </summary>
    internal const int MaxOperatorNodes = 4096;

    private Tokenizer _tokenizer;
    private FormulaParserToken _currentToken;
    private int _nestingDepth;
    private int _operatorNodeCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="FormulaParser"/> struct with the specified formula string.
    /// 使用指定的公式字串初始化 <see cref="FormulaParser"/> 結構的新執行個體。
    /// </summary>
    /// <param name="formula">The formula string. / 公式字串。</param>
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
    /// Starts parsing the formula.
    /// 開始剖析公式。
    /// </summary>
    /// <returns>The parsed AST root node. / 剖析後的 AST 根節點。</returns>
    /// <exception cref="InvalidOperationException">When an unexpected token remains at the end of the formula. / 當公式結尾有未預期的語彙基元時擲出。</exception>
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
            node = CountOperator(new BinaryNode(op, node, right));
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
            node = CountOperator(new BinaryNode("&", node, right));
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
            node = CountOperator(new BinaryNode(op, node, right));
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
            node = CountOperator(new BinaryNode(op, node, right));
        }
        return node;
    }

    // 優先權 5：單元運算 (+, -)
    // OpenFormula Note 1：前置一元運算子的優先權高於乘方（^）。
    // 例如 -2^2 應為 (-2)^2 = 4。
    // OpenFormula Note 1: Prefix unary operators have higher precedence than ^.
    // For example, -2^2 should be parsed as (-2)^2 = 4.
    private AstNode ParseUnary()
    {
        // 以迴圈收集連續的前置運算子（而非遞迴），並限制長度，避免超長的 '-' 鏈造成堆疊溢位。
        List<char>? operators = null;
        while (_currentToken.Type == FormulaTokenType.Operator &&
           (_currentToken.Span.Equals("+", StringComparison.Ordinal) || _currentToken.Span.Equals("-", StringComparison.Ordinal)))
        {
            if (operators is { Count: >= MaxNestingDepth })
            {
                throw CreateNestingTooDeepException();
            }

            (operators ??= []).Add(_currentToken.Span[0]);
            Consume();
        }

        AstNode node = ParsePercent();
        if (operators is not null)
        {
            // 第一個運算子在最外層。
            for (int index = operators.Count - 1; index >= 0; index--)
            {
                node = CountOperator(new UnaryNode(operators[index], node));
            }
        }

        return node;
    }

    private AstNode CountOperator(AstNode node)
    {
        if (++_operatorNodeCount > MaxOperatorNodes)
        {
            throw CreateNestingTooDeepException();
        }

        return node;
    }

    private static InvalidOperationException CreateNestingTooDeepException() =>
        new(OdfLocalizer.GetMessage("Err_OdfFormulaEvaluation_ResourceLimitExceeded", "MaxAstDepth"));

    private AstNode ParseNestedExpression()
    {
        if (++_nestingDepth > MaxNestingDepth)
        {
            throw CreateNestingTooDeepException();
        }

        AstNode node = ParseExpression();
        _nestingDepth--;
        return node;
    }

    // 優先權 6：乘方運算 (^)（左結合）
    private AstNode ParsePower()
    {
        var node = ParseUnary();
        while (_currentToken.Type == FormulaTokenType.Operator && _currentToken.Span.Equals("^", StringComparison.Ordinal))
        {
            Consume();
            var right = ParseUnary();
            node = CountOperator(new BinaryNode("^", node, right));
        }
        return node;
    }

    // 優先權 7：百分比後綴運算 (%)
    private AstNode ParsePercent()
    {
        var node = ParseReferenceExpression();

        while (_currentToken.Type == FormulaTokenType.Operator && _currentToken.Span.Equals("%", StringComparison.Ordinal))
        {
            Consume();
            node = CountOperator(new UnaryNode('%', node));
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
            node = CountOperator(new ReferenceUnionNode(node, right));
        }
        return node;
    }

    private AstNode ParseIntersectionExpression()
    {
        var node = ParseRangeExpression();
        while (_currentToken.Type == FormulaTokenType.Operator &&
            (_currentToken.Span.Equals("!", StringComparison.Ordinal) ||
                _currentToken.Span.Equals("!!", StringComparison.Ordinal)))
        {
            bool automatic = _currentToken.Span.Equals(
                "!!",
                StringComparison.Ordinal);
            Consume();
            var right = ParseRangeExpression();
            AstNode intersection = automatic
                ? new AutomaticIntersectionNode(node, right)
                : new ReferenceIntersectionNode(node, right);
            node = CountOperator(intersection);
        }
        return node;
    }

    private AstNode ParseRangeExpression()
    {
        var node = ParsePrimary();
        while (_currentToken.Type == FormulaTokenType.Colon)
        {
            Consume();
            AstNode right = ParsePrimary();
            node = CountOperator(CreateRangeNode(node, right));
        }

        return node;
    }

    private static AstNode CreateRangeNode(AstNode left, AstNode right)
    {
        if (left is not CellAddressNode leftCell ||
            right is not CellAddressNode rightCell)
        {
            return new ReferenceRangeNode(left, right);
        }

        string? leftSheet = leftCell.Address.SheetName;
        string? rightSheet = rightCell.Address.SheetName;
        if (!string.IsNullOrEmpty(leftSheet) &&
            !string.IsNullOrEmpty(rightSheet) &&
            !string.Equals(
                leftSheet,
                rightSheet,
                StringComparison.OrdinalIgnoreCase))
        {
            return new ReferenceRangeNode(left, right);
        }

        string? sheetName = leftSheet ?? rightSheet;
        OdfCellAddress start = WithSheetName(leftCell.Address, sheetName);
        OdfCellAddress end = WithSheetName(rightCell.Address, sheetName);
        return new RangeReferenceNode(new OdfCellRange(start, end));
    }

    private static OdfCellAddress WithSheetName(
        OdfCellAddress address,
        string? sheetName) =>
        new(
            address.Row,
            address.Column,
            sheetName,
            address.IsRowAbsolute,
            address.IsColumnAbsolute,
            address.IsSheetAbsolute);

    // 優先權 8：主要運算式（常值、括號、函式、儲存格／範圍）
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
            var node = ParseNestedExpression();
            if (_currentToken.Type != FormulaTokenType.CloseParen)
            {
                throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_FormulaParser_MismatchedParenthesesExpectedCloseparen"));
            }
            Consume();
            return new ParenthesizedNode(node);
        }

        if (_currentToken.Type == FormulaTokenType.OpenBrace)
        {
            return ParseInlineArray();
        }

        if (_currentToken.Type == FormulaTokenType.Identifier)
        {
            string ident = _currentToken.Span.ToString();
            Consume();

            if (TryParseError(ident, out OdfFormulaError? error))
                return new LiteralNode(error!);

            // 1. 檢查是否為函式呼叫
            if (_currentToken.Type == FormulaTokenType.OpenParen)
            {
                Consume(); // 消耗 '('
                List<AstNode> args = [];
                if (_currentToken.Type != FormulaTokenType.CloseParen)
                {
                    args.Add(ParseNestedExpression());
                    while (_currentToken.Type == FormulaTokenType.Separator)
                    {
                        Consume();
                        args.Add(ParseNestedExpression());
                    }
                }
                if (_currentToken.Type != FormulaTokenType.CloseParen)
                {
                    throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_FormulaParser_MismatchedParenthesesFunctionCall"));
                }
                Consume(); // 消耗 ')'
                return new FunctionNode(ident, args);
            }

            // 2. 檢查是否為儲存格參照
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

    private static bool TryParseError(string text, out OdfFormulaError? error)
    {
        error = text.ToUpperInvariant() switch
        {
            "#NULL!" => OdfFormulaError.Null,
            "#DIV/0!" => OdfFormulaError.Div0,
            "#VALUE!" => OdfFormulaError.Value,
            "#REF!" => OdfFormulaError.Ref,
            "#NAME?" => OdfFormulaError.Name,
            "#NUM!" => OdfFormulaError.Num,
            "#N/A" => OdfFormulaError.NA,
            _ => null
        };
        return error is not null;
    }

    private InlineArrayNode ParseInlineArray()
    {
        Consume();
        List<IReadOnlyList<AstNode>> rows = [];
        List<AstNode> currentRow = [];

        while (_currentToken.Type != FormulaTokenType.CloseBrace)
        {
            if (_currentToken.Type == FormulaTokenType.EndOfFormula)
            {
                throw new InvalidOperationException(
                    OdfLocalizer.GetMessage(
                        "Err_FormulaParser_UnexpectedTokenTypeDuring",
                        _currentToken.Type));
            }

            currentRow.Add(ParseNestedExpression());
            if (_currentToken.Type == FormulaTokenType.Separator)
            {
                Consume();
                continue;
            }

            rows.Add(currentRow);
            if (_currentToken.Type == FormulaTokenType.RowSeparator)
            {
                Consume();
                currentRow = [];
                continue;
            }

            if (_currentToken.Type != FormulaTokenType.CloseBrace)
            {
                throw new InvalidOperationException(
                    OdfLocalizer.GetMessage(
                        "Err_FormulaParser_UnexpectedTokenTypeDuring",
                        _currentToken.Type));
            }
        }

        Consume();
        return new InlineArrayNode(rows);
    }
}

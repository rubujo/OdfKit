using System;
using System.Collections.Generic;
using OdfKit.Spreadsheet;
using OdfKit.Formula.AST;

namespace OdfKit.Formula
{
    public enum FormulaTokenType
    {
        Number,
        String,
        Bool,
        Identifier, // Functions, coordinate names
        Operator,   // +, -, *, /, ^, &, =, <, >, <=, >=, <>
        OpenParen,  // (
        CloseParen, // )
        Separator,  // , or ;
        Colon,      // :
        EndOfFormula
    }

    public readonly ref struct FormulaParserToken
    {
        public FormulaTokenType Type { get; }
        public ReadOnlySpan<char> Span { get; }
        public double NumberValue { get; }
        public bool BoolValue { get; }

        public FormulaParserToken(FormulaTokenType type, ReadOnlySpan<char> span, double numValue = 0, bool boolValue = false)
        {
            Type = type;
            Span = span;
            NumberValue = numValue;
            BoolValue = boolValue;
        }
    }

    public ref struct Tokenizer
    {
        private readonly ReadOnlySpan<char> _formula;
        private int _index;

        public Tokenizer(ReadOnlySpan<char> formula)
        {
            _formula = formula;
            _index = 0;
        }

        public FormulaParserToken NextToken()
        {
            SkipWhitespace();

            if (_index >= _formula.Length)
                return new FormulaParserToken(FormulaTokenType.EndOfFormula, ReadOnlySpan<char>.Empty);

            char current = _formula[_index];

            // 1. Parentheses and separators
            if (current == '(') { _index++; return new FormulaParserToken(FormulaTokenType.OpenParen, _formula.Slice(_index - 1, 1)); }
            if (current == ')') { _index++; return new FormulaParserToken(FormulaTokenType.CloseParen, _formula.Slice(_index - 1, 1)); }
            if (current == ',' || current == ';') { _index++; return new FormulaParserToken(FormulaTokenType.Separator, _formula.Slice(_index - 1, 1)); }
            if (current == ':') { _index++; return new FormulaParserToken(FormulaTokenType.Colon, _formula.Slice(_index - 1, 1)); }

            // 2. String literal
            if (current == '"')
            {
                int start = _index;
                _index++; // skip opening quote
                while (_index < _formula.Length)
                {
                    if (_formula[_index] == '"')
                    {
                        if (_index + 1 < _formula.Length && _formula[_index + 1] == '"')
                        {
                            _index += 2; // skip escaped quote ""
                        }
                        else
                        {
                            _index++; // skip closing quote
                            break;
                        }
                    }
                    else
                    {
                        _index++;
                    }
                }
                return new FormulaParserToken(FormulaTokenType.String, _formula.Slice(start, _index - start));
            }

            // 3. Operators
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
                return new FormulaParserToken(FormulaTokenType.Operator, _formula.Slice(start, _index - start));
            }

            if (current == '+' || current == '-' || current == '*' || current == '/' || current == '^' || current == '&' || current == '%' || current == '~' || current == '!')
            {
                _index++;
                return new FormulaParserToken(FormulaTokenType.Operator, _formula.Slice(_index - 1, 1));
            }

            // 4. Number literals
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
                return new FormulaParserToken(FormulaTokenType.Number, numSpan, val);
            }

            // 5. Identifiers, functions, coordinates
            if (char.IsLetter(current) || current == '$' || current == '_' || current == '\'')
            {
                int start = _index;
                if (current == '\'')
                {
                    // Quoted sheet name handling: 'Sheet Name'.A1 or 'Sheet Name'!A1
                    _index++;
                    while (_index < _formula.Length && _formula[_index] != '\'')
                    {
                        _index++;
                    }
                    if (_index < _formula.Length) _index++; // skip closing quote
                }

                while (_index < _formula.Length)
                {
                    char c = _formula[_index];
                    if (c == '!')
                    {
                        // Check if what we have scanned so far is a valid cell address.
                        // If it is, then '!' is the intersection operator, not a sheet separator.
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
                
                // Fast-check for logical literals
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
                        return new FormulaParserToken(FormulaTokenType.Bool, identSpan, boolValue: true);
                    if (identSpan.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
                        return new FormulaParserToken(FormulaTokenType.Bool, identSpan, boolValue: false);
                }

                return new FormulaParserToken(FormulaTokenType.Identifier, identSpan);
            }

            // Fallback for unknown character
            int errStart = _index;
            _index++;
            return new FormulaParserToken(FormulaTokenType.Operator, _formula.Slice(errStart, 1));
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

    public ref struct FormulaParser
    {
        private Tokenizer _tokenizer;
        private FormulaParserToken _currentToken;

        public FormulaParser(string formula)
        {
            _tokenizer = new Tokenizer(formula.AsSpan());
            _currentToken = _tokenizer.NextToken();
        }

        private void Consume()
        {
            _currentToken = _tokenizer.NextToken();
        }

        public AstNode Parse()
        {
            var node = ParseExpression();
            if (_currentToken.Type != FormulaTokenType.EndOfFormula)
            {
                throw new InvalidOperationException($"Unexpected token at the end of formula: {_currentToken.Span.ToString()}");
            }
            return node;
        }

        // Precedence 1: Logical operations (Comparisons)
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

        // Precedence 2: Concat (&)
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

        // Precedence 3: Term (+, -)
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

        // Precedence 4: Factor (*, /)
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

        // Precedence 5: Power (^)
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

        // Precedence 6: Unary (+, -, %)
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

        // Precedence 7: Primary (Literals, Parens, Functions, Cells/Ranges)
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

                // 1. Check if it is a function call
                if (_currentToken.Type == FormulaTokenType.OpenParen)
                {
                    Consume(); // consume '('
                    var args = new List<AstNode>();
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
                    Consume(); // consume ')'
                    return new FunctionNode(ident, args);
                }

                // 2. Check if it is a range or cell reference
                if (_currentToken.Type == FormulaTokenType.Colon)
                {
                    // Range case: A1:B10
                    Consume(); // consume ':'
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

                // Single cell or single sheet-qualified cell A1 or Sheet1.A1
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
}

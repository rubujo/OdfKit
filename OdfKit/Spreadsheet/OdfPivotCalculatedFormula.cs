using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OdfKit.Compliance;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 編譯並執行樞紐計算欄位的有界純數值公式子集。
/// </summary>
internal sealed class OdfPivotCalculatedFormula
{
    private readonly Node _root;

    private OdfPivotCalculatedFormula(Node root, IReadOnlyCollection<int> references)
    {
        _root = root;
        References = references;
    }

    internal IReadOnlyCollection<int> References { get; }

    internal static OdfPivotCalculatedFormula Compile(
        string formula,
        IReadOnlyDictionary<string, int> fields,
        int maximumNodes,
        int maximumDepth)
    {
        var parser = new Parser(
            Normalize(formula),
            name => fields.TryGetValue(name, out int index) ? index : -1,
            maximumNodes,
            maximumDepth);
        return parser.Parse();
    }

    internal static void ValidateSyntax(string formula, int maximumNodes, int maximumDepth)
    {
        var parser = new Parser(
            Normalize(formula),
            _ => 0,
            maximumNodes,
            maximumDepth);
        _ = parser.Parse();
    }

    internal double Evaluate(object?[] values)
    {
        double value = _root.Evaluate(values);
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new OverflowException();
        return value;
    }

    private static bool TryConvertNumber(object value, out double number)
    {
        switch (value)
        {
            case double doubleValue:
                number = doubleValue;
                break;
            case float floatValue:
                number = floatValue;
                break;
            case decimal decimalValue:
                number = (double)decimalValue;
                break;
            case long longValue:
                number = longValue;
                break;
            case ulong ulongValue:
                number = ulongValue;
                break;
            case int intValue:
                number = intValue;
                break;
            case uint uintValue:
                number = uintValue;
                break;
            case short shortValue:
                number = shortValue;
                break;
            case ushort ushortValue:
                number = ushortValue;
                break;
            case byte byteValue:
                number = byteValue;
                break;
            case sbyte sbyteValue:
                number = sbyteValue;
                break;
            case string text when double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double parsed):
                number = parsed;
                break;
            default:
                number = 0d;
                return false;
        }
        return !double.IsNaN(number) && !double.IsInfinity(number);
    }

    private static string Normalize(string formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_FormulaParser_UnexpectedTokenEndFormula", string.Empty),
                nameof(formula));
        }
        string result = formula.Trim();
        if (result.StartsWith("of:", StringComparison.OrdinalIgnoreCase))
            result = result.Substring(3);
        else if (result.StartsWith("oooc:", StringComparison.OrdinalIgnoreCase))
            result = result.Substring(5);
        if (result.Length > 0 && result[0] == '=')
            result = result.Substring(1);
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_FormulaParser_UnexpectedTokenEndFormula", string.Empty),
                nameof(formula));
        }
        return result;
    }

    private abstract class Node
    {
        internal abstract double Evaluate(object?[] values);
    }

    private sealed class ConstantNode(double value) : Node
    {
        internal override double Evaluate(object?[] values) => value;
    }

    private sealed class FieldNode(int index) : Node
    {
        internal override double Evaluate(object?[] values)
        {
            object? value = values[index];
            if (value is null || value is string { Length: 0 })
                return 0d;
            if (!TryConvertNumber(value, out double number))
                throw new InvalidDataException();
            return number;
        }
    }

    private sealed class UnaryNode(char operation, Node operand) : Node
    {
        internal override double Evaluate(object?[] values)
        {
            double value = operand.Evaluate(values);
            return operation switch
            {
                '+' => value,
                '-' => -value,
                '%' => value / 100d,
                _ => throw new InvalidDataException(),
            };
        }
    }

    private sealed class BinaryNode(string operation, Node left, Node right) : Node
    {
        internal override double Evaluate(object?[] values)
        {
            double first = left.Evaluate(values);
            double second = right.Evaluate(values);
            return operation switch
            {
                "+" => first + second,
                "-" => first - second,
                "*" => first * second,
                "/" => second == 0d ? throw new DivideByZeroException() : first / second,
                "^" => Math.Pow(first, second),
                "=" => first == second ? 1d : 0d,
                "<>" => first != second ? 1d : 0d,
                "<" => first < second ? 1d : 0d,
                "<=" => first <= second ? 1d : 0d,
                ">" => first > second ? 1d : 0d,
                ">=" => first >= second ? 1d : 0d,
                _ => throw new InvalidDataException(),
            };
        }
    }

    private sealed class FunctionNode(string name, Node[] arguments) : Node
    {
        internal override double Evaluate(object?[] values)
        {
            switch (name)
            {
                case "ABS":
                    RequireCount(1);
                    return Math.Abs(arguments[0].Evaluate(values));
                case "SQRT":
                    RequireCount(1);
                    return Math.Sqrt(arguments[0].Evaluate(values));
                case "POWER":
                    RequireCount(2);
                    return Math.Pow(arguments[0].Evaluate(values), arguments[1].Evaluate(values));
                case "ROUND":
                    if (arguments.Length is < 1 or > 2)
                        throw new InvalidDataException();
                    int digits = arguments.Length == 1
                        ? 0
                        : checked((int)arguments[1].Evaluate(values));
                    if (digits is < 0 or > 15)
                        throw new InvalidDataException();
                    return Math.Round(arguments[0].Evaluate(values), digits, MidpointRounding.AwayFromZero);
                case "IF":
                    RequireCount(3);
                    return arguments[0].Evaluate(values) != 0d
                        ? arguments[1].Evaluate(values)
                        : arguments[2].Evaluate(values);
                case "NOT":
                    RequireCount(1);
                    return arguments[0].Evaluate(values) == 0d ? 1d : 0d;
                case "AND":
                    RequireMinimumCount(1);
                    foreach (Node argument in arguments)
                    {
                        if (argument.Evaluate(values) == 0d)
                            return 0d;
                    }
                    return 1d;
                case "OR":
                    RequireMinimumCount(1);
                    foreach (Node argument in arguments)
                    {
                        if (argument.Evaluate(values) != 0d)
                            return 1d;
                    }
                    return 0d;
                case "SUM":
                case "MIN":
                case "MAX":
                    RequireMinimumCount(1);
                    double result = arguments[0].Evaluate(values);
                    for (int index = 1; index < arguments.Length; index++)
                    {
                        double current = arguments[index].Evaluate(values);
                        result = name switch
                        {
                            "SUM" => result + current,
                            "MIN" => Math.Min(result, current),
                            _ => Math.Max(result, current),
                        };
                    }
                    return result;
                default:
                    throw new InvalidDataException();
            }
        }

        private void RequireCount(int count)
        {
            if (arguments.Length != count)
                throw new InvalidDataException();
        }

        private void RequireMinimumCount(int count)
        {
            if (arguments.Length < count)
                throw new InvalidDataException();
        }
    }

    private ref struct Parser
    {
        private readonly ReadOnlySpan<char> _formula;
        private readonly Func<string, int> _resolveField;
        private readonly int _maximumNodes;
        private readonly int _maximumDepth;
        private readonly HashSet<int> _references;
        private int _index;
        private int _nodes;

        internal Parser(
            string formula,
            Func<string, int> resolveField,
            int maximumNodes,
            int maximumDepth)
        {
            if (maximumNodes < 1 || maximumDepth < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumNodes));
            _formula = formula.AsSpan();
            _resolveField = resolveField;
            _maximumNodes = maximumNodes;
            _maximumDepth = maximumDepth;
            _references = [];
            _index = 0;
            _nodes = 0;
        }

        internal OdfPivotCalculatedFormula Parse()
        {
            Node root = ParseComparison(1);
            SkipWhitespace();
            if (_index != _formula.Length)
                throw new InvalidDataException();
            return new OdfPivotCalculatedFormula(root, _references);
        }

        private Node ParseComparison(int depth)
        {
            Node left = ParseAdditive(depth);
            SkipWhitespace();
            string? operation = ReadComparison();
            if (operation is null)
                return left;
            return AddNode(new BinaryNode(operation, left, ParseAdditive(depth)));
        }

        private Node ParseAdditive(int depth)
        {
            Node node = ParseMultiplicative(depth);
            while (true)
            {
                SkipWhitespace();
                if (!TryRead('+') && !TryRead('-'))
                    return node;
                char operation = _formula[_index - 1];
                node = AddNode(new BinaryNode(
                    operation.ToString(),
                    node,
                    ParseMultiplicative(depth)));
            }
        }

        private Node ParseMultiplicative(int depth)
        {
            Node node = ParsePower(depth);
            while (true)
            {
                SkipWhitespace();
                if (!TryRead('*') && !TryRead('/'))
                    return node;
                char operation = _formula[_index - 1];
                node = AddNode(new BinaryNode(
                    operation.ToString(),
                    node,
                    ParsePower(depth)));
            }
        }

        private Node ParsePower(int depth)
        {
            Node node = ParseUnary(depth);
            SkipWhitespace();
            if (TryRead('^'))
                node = AddNode(new BinaryNode("^", node, ParsePower(depth + 1)));
            return node;
        }

        private Node ParseUnary(int depth)
        {
            EnsureDepth(depth);
            SkipWhitespace();
            if (TryRead('+') || TryRead('-'))
            {
                char operation = _formula[_index - 1];
                return AddNode(new UnaryNode(operation, ParseUnary(depth + 1)));
            }
            Node node = ParsePrimary(depth);
            SkipWhitespace();
            while (TryRead('%'))
            {
                node = AddNode(new UnaryNode('%', node));
                SkipWhitespace();
            }
            return node;
        }

        private Node ParsePrimary(int depth)
        {
            EnsureDepth(depth);
            SkipWhitespace();
            if (TryRead('('))
            {
                Node nested = ParseComparison(depth + 1);
                SkipWhitespace();
                Require(')');
                return nested;
            }
            if (TryRead('['))
            {
                SkipWhitespace();
                TryRead('.');
                int start = _index;
                while (_index < _formula.Length && _formula[_index] != ']')
                    _index++;
                if (_index == _formula.Length)
                    throw new InvalidDataException();
                string name = _formula.Slice(start, _index - start).ToString().Trim();
                Require(']');
                int fieldIndex = _resolveField(name);
                if (fieldIndex < 0)
                    throw new InvalidDataException();
                _references.Add(fieldIndex);
                return AddNode(new FieldNode(fieldIndex));
            }
            if (_index < _formula.Length &&
                (char.IsDigit(_formula[_index]) || _formula[_index] == '.'))
            {
                return ParseNumber();
            }
            string identifier = ReadIdentifier();
            if (identifier.Length == 0)
                throw new InvalidDataException();
            SkipWhitespace();
            if (!TryRead('('))
                throw new InvalidDataException();
            var arguments = new List<Node>();
            SkipWhitespace();
            if (!TryRead(')'))
            {
                while (true)
                {
                    arguments.Add(ParseComparison(depth + 1));
                    SkipWhitespace();
                    if (TryRead(')'))
                        break;
                    if (!TryRead(';') && !TryRead(','))
                        throw new InvalidDataException();
                }
            }
            string functionName = identifier.ToUpperInvariant();
            ValidateFunction(functionName, arguments.Count);
            return AddNode(new FunctionNode(functionName, arguments.ToArray()));
        }

        private ConstantNode ParseNumber()
        {
            int start = _index;
            bool exponent = false;
            while (_index < _formula.Length)
            {
                char current = _formula[_index];
                if (char.IsDigit(current) || current == '.')
                {
                    _index++;
                    continue;
                }
                if (!exponent && current is 'e' or 'E')
                {
                    exponent = true;
                    _index++;
                    if (_index < _formula.Length && _formula[_index] is '+' or '-')
                        _index++;
                    continue;
                }
                break;
            }
            if (!double.TryParse(
                    _formula.Slice(start, _index - start).ToString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double value) ||
                double.IsNaN(value) ||
                double.IsInfinity(value))
            {
                throw new InvalidDataException();
            }
            return AddNode(new ConstantNode(value));
        }

        private string ReadIdentifier()
        {
            SkipWhitespace();
            int start = _index;
            while (_index < _formula.Length &&
                (char.IsLetterOrDigit(_formula[_index]) || _formula[_index] == '_'))
            {
                _index++;
            }
            return _formula.Slice(start, _index - start).ToString();
        }

        private static void ValidateFunction(string name, int argumentCount)
        {
            bool valid = name switch
            {
                "ABS" or "SQRT" or "NOT" => argumentCount == 1,
                "POWER" => argumentCount == 2,
                "ROUND" => argumentCount is 1 or 2,
                "IF" => argumentCount == 3,
                "AND" or "OR" or "SUM" or "MIN" or "MAX" => argumentCount >= 1,
                _ => false,
            };
            if (!valid)
                throw new InvalidDataException();
        }

        private string? ReadComparison()
        {
            foreach (string operation in new[] { "<=", ">=", "<>", "=", "<", ">" })
            {
                if (_formula.Slice(_index).StartsWith(operation.AsSpan(), StringComparison.Ordinal))
                {
                    _index += operation.Length;
                    return operation;
                }
            }
            return null;
        }

        private T AddNode<T>(T node) where T : Node
        {
            if (++_nodes > _maximumNodes)
                throw new InvalidDataException();
            return node;
        }

        private void EnsureDepth(int depth)
        {
            if (depth > _maximumDepth)
                throw new InvalidDataException();
        }

        private bool TryRead(char expected)
        {
            if (_index >= _formula.Length || _formula[_index] != expected)
                return false;
            _index++;
            return true;
        }

        private void Require(char expected)
        {
            if (!TryRead(expected))
                throw new InvalidDataException();
        }

        private void SkipWhitespace()
        {
            while (_index < _formula.Length && char.IsWhiteSpace(_formula[_index]))
                _index++;
        }
    }
}

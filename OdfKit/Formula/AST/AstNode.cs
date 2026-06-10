using System;
using System.Collections.Generic;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula.AST
{
    public abstract class AstNode
    {
        public abstract object Evaluate(IEvaluationContext context);
    }

    public class LiteralNode : AstNode
    {
        private readonly object _value;
        public LiteralNode(object value) => _value = value;
        public override object Evaluate(IEvaluationContext context) => _value;
    }

    public class CellAddressNode : AstNode
    {
        public OdfCellAddress Address { get; }
        public CellAddressNode(OdfCellAddress address) => Address = address;
        public override object Evaluate(IEvaluationContext context) => context.GetCellValue(Address);
    }

    public class RangeReferenceNode : AstNode
    {
        public OdfCellRange Range { get; }
        public RangeReferenceNode(OdfCellRange range) => Range = range;
        public override object Evaluate(IEvaluationContext context) => context.GetRangeValues(Range);
    }

    public class UnaryNode : AstNode
    {
        private readonly char _op;
        private readonly AstNode _child;

        public UnaryNode(char op, AstNode child)
        {
            _op = op;
            _child = child;
        }

        public override object Evaluate(IEvaluationContext context)
        {
            var val = _child.Evaluate(context);
            if (val is OdfFormulaError err) return err;

            if (_op == '%')
            {
                if (val is double d) return d / 100.0;
                if (val is string s && double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsed))
                    return parsed / 100.0;
                return OdfFormulaError.Value;
            }

            if (val is double num)
                return _op == '-' ? -num : num;

            if (val is bool b)
                return _op == '-' ? -(b ? 1.0 : 0.0) : (b ? 1.0 : 0.0);

            if (val is string str && double.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedNum))
                return _op == '-' ? -parsedNum : parsedNum;

            return OdfFormulaError.Value;
        }
    }

    public class BinaryNode : AstNode
    {
        private readonly string _op;
        private readonly AstNode _left;
        private readonly AstNode _right;

        public BinaryNode(string op, AstNode left, AstNode right)
        {
            _op = op;
            _left = left;
            _right = right;
        }

        public override object Evaluate(IEvaluationContext context)
        {
            var leftVal = _left.Evaluate(context);
            if (leftVal is OdfFormulaError) return leftVal;

            var rightVal = _right.Evaluate(context);
            if (rightVal is OdfFormulaError) return rightVal;

            if (_op == "&")
            {
                return string.Concat(
                    FormatValue(leftVal),
                    FormatValue(rightVal)
                );
            }

            // Math operators
            if (IsMathOp(_op))
            {
                if (!TryCoerceDouble(leftVal, out double leftNum) || !TryCoerceDouble(rightVal, out double rightNum))
                    return OdfFormulaError.Value;

                return _op switch
                {
                    "+" => leftNum + rightNum,
                    "-" => leftNum - rightNum,
                    "*" => leftNum * rightNum,
                    "/" => rightNum == 0 ? OdfFormulaError.Div0 : leftNum / rightNum,
                    "^" => Math.Pow(leftNum, rightNum),
                    _ => OdfFormulaError.Value
                };
            }

            // Comparison operators
            return EvaluateComparison(leftVal, _op, rightVal);
        }

        private static bool IsMathOp(string op) => op == "+" || op == "-" || op == "*" || op == "/" || op == "^";

        private static string FormatValue(object val)
        {
            if (val is bool b) return b ? "TRUE" : "FALSE";
            return val.ToString() ?? "";
        }

        private static bool TryCoerceDouble(object val, out double result)
        {
            if (val is double d) { result = d; return true; }
            if (val is bool b) { result = b ? 1.0 : 0.0; return true; }
            if (val is string s) return double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out result);
            result = 0;
            return false;
        }

        private static object EvaluateComparison(object left, string op, object right)
        {
            // Coerce types to compare them
            int comp;
            if (left is double d1 && right is double d2)
            {
                comp = d1.CompareTo(d2);
            }
            else if (left is bool b1 && right is bool b2)
            {
                comp = b1.CompareTo(b2);
            }
            else if (TryCoerceDouble(left, out double nd1) && TryCoerceDouble(right, out double nd2))
            {
                comp = nd1.CompareTo(nd2);
            }
            else
            {
                // String comparison (case-insensitive)
                comp = string.Compare(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
            }

            return op switch
            {
                "=" => comp == 0,
                "<" => comp < 0,
                ">" => comp > 0,
                "<=" => comp <= 0,
                ">=" => comp >= 0,
                "<>" => comp != 0,
                _ => OdfFormulaError.Value
            };
        }
    }

    public class FunctionNode : AstNode
    {
        public string Name { get; }
        public List<AstNode> Arguments { get; }

        public FunctionNode(string name, List<AstNode> arguments)
        {
            Name = name;
            Arguments = arguments;
        }

        public override object Evaluate(IEvaluationContext context)
        {
            return DefaultFormulaEvaluator.EvaluateFunction(Name, Arguments, context);
        }
    }
}

using System.Globalization;
using System;
using OdfKit.Formula;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula.AST;

/// <summary>
/// Represents an AST node for a unary operator.
/// 代表單元運算子 (Unary Operator) 的 AST 節點。
/// </summary>
/// <param name="op">The unary operator character. / 單元運算子字元。</param>
/// <param name="child">The child AST node. / 子 AST 節點。</param>
public class UnaryNode(char op, AstNode child) : AstNode
{
    /// <inheritdoc />
    public override List<OdfCellRange> GetRanges(IEvaluationContext context) => child.GetRanges(context);

    /// <inheritdoc />
    public override object Evaluate(IEvaluationContext context)
    {
        var val = child.Evaluate(context);
        if (val is OdfFormulaError err)
            return err;

        if (op == '%')
        {
            if (val is double d)
                return d / 100.0;
            if (val is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
                return parsed / 100.0;
            return OdfFormulaError.Value;
        }

        if (val is double num)
            return op == '-' ? -num : num;

        if (val is bool b)
            return op == '-' ? -(b ? 1.0 : 0.0) : (b ? 1.0 : 0.0);

        if (val is string str && double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedNum))
            return op == '-' ? -parsedNum : parsedNum;

        return OdfFormulaError.Value;
    }

    /// <inheritdoc />
    public override string Serialize()
    {
        if (op == '%')
            return $"{child.Serialize()}%";
        return $"{op}{child.Serialize()}";
    }
}

/// <summary>
/// Represents an AST node for a binary operator.
/// 代表二元運算子 (Binary Operator) 的 AST 節點。
/// </summary>
/// <param name="op">The binary operator string. / 二元運算子字串。</param>
/// <param name="left">The left AST node. / 左側 AST 節點。</param>
/// <param name="right">The right AST node. / 右側 AST 節點。</param>
public class BinaryNode(string op, AstNode left, AstNode right) : AstNode
{
    /// <inheritdoc />
    public override List<OdfCellRange> GetRanges(IEvaluationContext context)
    {
        var list = new List<OdfCellRange>();
        list.AddRange(left.GetRanges(context));
        list.AddRange(right.GetRanges(context));
        return list;
    }

    /// <inheritdoc />
    public override object Evaluate(IEvaluationContext context)
    {
        var leftVal = left.Evaluate(context);
        if (leftVal is OdfFormulaError)
            return leftVal;

        var rightVal = right.Evaluate(context);
        if (rightVal is OdfFormulaError)
            return rightVal;

        if (op == "&")
        {
            return string.Concat(
                FormatValue(leftVal),
                FormatValue(rightVal)
            );
        }

        // 數學運算子
        if (IsMathOp(op))
        {
            if (!TryCoerceDouble(leftVal, out double leftNum) || !TryCoerceDouble(rightVal, out double rightNum))
                return OdfFormulaError.Value;

            return op switch
            {
                "+" => leftNum + rightNum,
                "-" => leftNum - rightNum,
                "*" => leftNum * rightNum,
                "/" => rightNum == 0 ? OdfFormulaError.Div0 : leftNum / rightNum,
                "^" => Math.Pow(leftNum, rightNum),
                _ => OdfFormulaError.Value
            };
        }

        // 比較運算子
        return EvaluateComparison(leftVal, op, rightVal);
    }

    private static bool IsMathOp(string op) => op == "+" || op == "-" || op == "*" || op == "/" || op == "^";

    private static string FormatValue(object val)
    {
        if (val is bool b)
            return b ? "TRUE" : "FALSE";
        return val.ToString() ?? "";
    }

    private static bool TryCoerceDouble(object val, out double result)
    {
        if (val is double d)
        { result = d; return true; }
        if (val is bool b)
        { result = b ? 1.0 : 0.0; return true; }
        if (val is string s)
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        result = 0;
        return false;
    }

    private static object EvaluateComparison(object left, string op, object right)
    {
        // 強制轉換型別以進行比較
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
            // 字串比較（不區分大小寫）
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

    /// <inheritdoc />
    public override string Serialize() => $"{left.Serialize()}{op}{right.Serialize()}";
}

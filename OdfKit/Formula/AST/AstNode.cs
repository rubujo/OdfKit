using System;
using System.Collections.Generic;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula.AST;

/// <summary>
/// 代表 ODF 參照清單。
/// </summary>
public class OdfReferenceList
{
    /// <summary>
    /// 取得參照的物件清單。
    /// </summary>
    public List<object> References { get; } = [];
}

/// <summary>
/// 抽象的抽象語法樹 (AST) 節點基底類別。
/// </summary>
public abstract class AstNode
{
    /// <summary>
    /// 評估此節點的值。
    /// </summary>
    /// <param name="context">評估內容模型</param>
    /// <returns>評估後的結果物件</returns>
    public abstract object Evaluate(IEvaluationContext context);

    /// <summary>
    /// 取得此節點包含的儲存格範圍。
    /// </summary>
    /// <param name="context">評估內容模型</param>
    /// <returns>儲存格範圍清單</returns>
    public virtual List<OdfCellRange> GetRanges(IEvaluationContext context) => [];

    /// <summary>
    /// 將此節點序列化為公式字串。
    /// </summary>
    /// <returns>序列化後的公式字串</returns>
    public abstract string Serialize();
}

/// <summary>
/// 代表常值 (Literal) 的 AST 節點。
/// </summary>
/// <param name="value">常值內容</param>
public class LiteralNode(object value) : AstNode
{
    public override object Evaluate(IEvaluationContext context) => value;

    public override string Serialize()
    {
        if (value is string s)
            return $"\"{s.Replace("\"", "\"\"")}\"";
        if (value is bool b)
            return b ? "TRUE" : "FALSE";
        if (value is double d)
            return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return value?.ToString() ?? string.Empty;
    }
}

/// <summary>
/// 代表儲存格位址的 AST 節點。
/// </summary>
/// <param name="address">儲存格位址</param>
public class CellAddressNode(OdfCellAddress address) : AstNode
{
    /// <summary>
    /// 取得儲存格位址。
    /// </summary>
    public OdfCellAddress Address { get; } = address;

    public override object Evaluate(IEvaluationContext context) => context.GetCellValue(Address);

    public override List<OdfCellRange> GetRanges(IEvaluationContext context) => [new OdfCellRange(Address, Address)];

    public override string Serialize() => Address.ToString();
}

/// <summary>
/// 代表儲存格範圍參照的 AST 節點。
/// </summary>
/// <param name="range">儲存格範圍</param>
public class RangeReferenceNode(OdfCellRange range) : AstNode
{
    /// <summary>
    /// 取得儲存格範圍。
    /// </summary>
    public OdfCellRange Range { get; } = range;

    public override object Evaluate(IEvaluationContext context) => context.GetRangeValues(Range);

    public override List<OdfCellRange> GetRanges(IEvaluationContext context) => [Range];

    public override string Serialize() => Range.ToString();
}

/// <summary>
/// 代表聯集參照 (Union) 的 AST 節點。
/// </summary>
/// <param name="left">左側 AST 節點</param>
/// <param name="right">右側 AST 節點</param>
public class ReferenceUnionNode(AstNode left, AstNode right) : AstNode
{
    public override List<OdfCellRange> GetRanges(IEvaluationContext context)
    {
        var list = new List<OdfCellRange>();
        list.AddRange(left.GetRanges(context));
        list.AddRange(right.GetRanges(context));
        return list;
    }

    public override object Evaluate(IEvaluationContext context)
    {
        var ranges = GetRanges(context);
        var list = new OdfReferenceList();
        foreach (var r in ranges)
        {
            list.References.Add(context.GetRangeValues(r));
        }
        return list;
    }

    public override string Serialize() => $"{left.Serialize()}~{right.Serialize()}";
}

/// <summary>
/// 代表交集參照 (Intersection) 的 AST 節點。
/// </summary>
/// <param name="left">左側 AST 節點</param>
/// <param name="right">右側 AST 節點</param>
public class ReferenceIntersectionNode(AstNode left, AstNode right) : AstNode
{
    public override List<OdfCellRange> GetRanges(IEvaluationContext context)
    {
        var leftRanges = left.GetRanges(context);
        var rightRanges = right.GetRanges(context);
        var list = new List<OdfCellRange>();
        foreach (var r1 in leftRanges)
        {
            foreach (var r2 in rightRanges)
            {
                var intersect = r1.Intersect(r2);
                if (intersect.HasValue)
                {
                    list.Add(intersect.Value);
                }
            }
        }
        return list;
    }

    public override object Evaluate(IEvaluationContext context)
    {
        var ranges = GetRanges(context);
        if (ranges.Count == 0)
        {
            return OdfFormulaError.Null; // 無交集傳回 #NULL!
        }
        if (ranges.Count == 1)
        {
            return context.GetRangeValues(ranges[0]);
        }
        var list = new OdfReferenceList();
        foreach (var r in ranges)
        {
            list.References.Add(context.GetRangeValues(r));
        }
        return list;
    }

    public override string Serialize() => $"{left.Serialize()}!{right.Serialize()}";
}

/// <summary>
/// 代表單元運算子 (Unary Operator) 的 AST 節點。
/// </summary>
/// <param name="op">單元運算子字元</param>
/// <param name="child">子 AST 節點</param>
public class UnaryNode(char op, AstNode child) : AstNode
{
    public override object Evaluate(IEvaluationContext context)
    {
        var val = child.Evaluate(context);
        if (val is OdfFormulaError err) return err;

        if (op == '%')
        {
            if (val is double d) return d / 100.0;
            if (val is string s && double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsed))
                return parsed / 100.0;
            return OdfFormulaError.Value;
        }

        if (val is double num)
            return op == '-' ? -num : num;

        if (val is bool b)
            return op == '-' ? -(b ? 1.0 : 0.0) : (b ? 1.0 : 0.0);

        if (val is string str && double.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedNum))
            return op == '-' ? -parsedNum : parsedNum;

        return OdfFormulaError.Value;
    }

    public override string Serialize()
    {
        if (op == '%')
            return $"{child.Serialize()}%";
        return $"{op}{child.Serialize()}";
    }
}

/// <summary>
/// 代表二元運算子 (Binary Operator) 的 AST 節點。
/// </summary>
/// <param name="op">二元運算子字串</param>
/// <param name="left">左側 AST 節點</param>
/// <param name="right">右側 AST 節點</param>
public class BinaryNode(string op, AstNode left, AstNode right) : AstNode
{
    public override object Evaluate(IEvaluationContext context)
    {
        var leftVal = left.Evaluate(context);
        if (leftVal is OdfFormulaError) return leftVal;

        var rightVal = right.Evaluate(context);
        if (rightVal is OdfFormulaError) return rightVal;

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
            // 字串比較 (不區分大小寫)
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

    public override string Serialize() => $"{left.Serialize()}{op}{right.Serialize()}";
}

/// <summary>
/// 代表函式呼叫的 AST 節點。
/// </summary>
/// <param name="name">函式名稱</param>
/// <param name="arguments">引數 AST 節點清單</param>
public class FunctionNode(string name, List<AstNode> arguments) : AstNode
{
    /// <summary>
    /// 取得函式名稱。
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// 取得引數 AST 節點清單。
    /// </summary>
    public List<AstNode> Arguments { get; } = arguments;

    public override object Evaluate(IEvaluationContext context)
    {
        return DefaultFormulaEvaluator.EvaluateFunction(Name, Arguments, context);
    }

    public override string Serialize()
    {
        var args = new List<string>();
        foreach (var arg in Arguments)
        {
            args.Add(arg.Serialize());
        }
        return $"{Name}({string.Join(",", args)})";
    }
}

/// <summary>
/// 代表括號運算式的 AST 節點。
/// </summary>
/// <param name="inner">括號內部的 AST 節點</param>
public class ParenthesizedNode(AstNode inner) : AstNode
{
    /// <summary>
    /// 取得括號內部的 AST 節點。
    /// </summary>
    public AstNode Inner => inner;

    public override object Evaluate(IEvaluationContext context) => inner.Evaluate(context);

    public override List<OdfCellRange> GetRanges(IEvaluationContext context) => inner.GetRanges(context);

    public override string Serialize() => $"({inner.Serialize()})";
}

/// <summary>
/// 代表具名範圍的 AST 節點。
/// </summary>
/// <param name="name">具名範圍或具名運算式的名稱</param>
public class NamedRangeNode(string name) : AstNode
{
    /// <summary>
    /// 取得具名範圍或具名運算式的名稱。
    /// </summary>
    public string Name { get; } = name;

    public override object Evaluate(IEvaluationContext context) => context.GetNamedRangeOrExpressionValue(Name);

    public override List<OdfCellRange> GetRanges(IEvaluationContext context)
    {
        var val = context.GetNamedRangeOrExpressionValue(Name);
        if (val is OdfCellRange r) return [r];
        if (val is string s && OdfCellRange.TryParse(s, out var range)) return [range];
        return [];
    }

    public override string Serialize() => Name;
}

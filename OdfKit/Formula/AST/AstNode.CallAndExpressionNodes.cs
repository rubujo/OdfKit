using System;
using System.Collections.Generic;
using OdfKit.Formula;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula.AST;

/// <summary>
/// Represents an AST node for a function call.
/// 代表函式呼叫的 AST 節點。
/// </summary>
/// <param name="name">The function name. / 函式名稱。</param>
/// <param name="arguments">The argument AST node list. / 引數 AST 節點清單。</param>
public class FunctionNode(string name, List<AstNode> arguments) : AstNode
{
    /// <summary>
    /// Gets the function name.
    /// 取得函式名稱。
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the argument AST node list.
    /// 取得引數 AST 節點清單。
    /// </summary>
    public List<AstNode> Arguments { get; } = arguments;

    /// <inheritdoc />
    public override List<OdfCellRange> GetRanges(IEvaluationContext context)
    {
        var list = new List<OdfCellRange>();
        foreach (var arg in Arguments)
        {
            list.AddRange(arg.GetRanges(context));
        }
        return list;
    }

    /// <inheritdoc />
    public override object Evaluate(IEvaluationContext context)
    {
        return DefaultFormulaEvaluator.EvaluateFunction(Name, Arguments, context);
    }

    /// <inheritdoc />
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
/// Represents an AST node for a parenthesized expression.
/// 代表括號運算式的 AST 節點。
/// </summary>
/// <param name="inner">The AST node inside the parentheses. / 括號內部的 AST 節點。</param>
public class ParenthesizedNode(AstNode inner) : AstNode
{
    /// <summary>
    /// Gets the AST node inside the parentheses.
    /// 取得括號內部的 AST 節點。
    /// </summary>
    public AstNode Inner => inner;

    /// <inheritdoc />
    public override object Evaluate(IEvaluationContext context) => inner.Evaluate(context);

    /// <inheritdoc />
    public override List<OdfCellRange> GetRanges(IEvaluationContext context) => inner.GetRanges(context);

    /// <inheritdoc />
    public override string Serialize() => $"({inner.Serialize()})";
}

/// <summary>
/// Represents an AST node for a named range.
/// 代表具名範圍的 AST 節點。
/// </summary>
/// <param name="name">The named range or named expression name. / 具名範圍或具名運算式的名稱。</param>
public class NamedRangeNode(string name) : AstNode
{
    /// <summary>
    /// Gets the named range or named expression name.
    /// 取得具名範圍或具名運算式的名稱。
    /// </summary>
    public string Name { get; } = name;

    /// <inheritdoc />
    public override object Evaluate(IEvaluationContext context) => context.GetNamedRangeOrExpressionValue(Name);

    /// <inheritdoc />
    public override List<OdfCellRange> GetRanges(IEvaluationContext context)
    {
        var val = context.GetNamedRangeOrExpressionValue(Name);
        if (val is OdfCellRange r)
            return [r];
        if (val is string s && OdfCellRange.TryParse(s, out var range))
            return [range];
        return [];
    }

    /// <inheritdoc />
    public override string Serialize() => Name;
}

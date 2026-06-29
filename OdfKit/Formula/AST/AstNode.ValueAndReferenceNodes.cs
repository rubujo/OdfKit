using System.Globalization;
using System;
using System.Collections.Generic;
using OdfKit.Formula;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula.AST;

/// <summary>
/// Represents an AST node for a literal value.
/// 代表常值 (Literal) 的 AST 節點。
/// </summary>
/// <param name="value">The literal value. / 常值內容。</param>
public class LiteralNode(object value) : AstNode
{
    /// <inheritdoc />
    public override object Evaluate(IEvaluationContext context) => value;

    /// <inheritdoc />
    public override string Serialize()
    {
        if (value is string s)
            return $"\"{s.Replace("\"", "\"\"")}\"";
        if (value is bool b)
            return b ? "TRUE" : "FALSE";
        if (value is double d)
            return d.ToString(CultureInfo.InvariantCulture);
        return value?.ToString() ?? string.Empty;
    }
}

/// <summary>
/// Represents an AST node for a cell address.
/// 代表儲存格位址的 AST 節點。
/// </summary>
/// <param name="address">The cell address. / 儲存格位址。</param>
public class CellAddressNode(OdfCellAddress address) : AstNode
{
    /// <summary>
    /// Gets the cell address.
    /// 取得儲存格位址。
    /// </summary>
    public OdfCellAddress Address { get; } = address;

    /// <inheritdoc />
    public override object Evaluate(IEvaluationContext context) => context.GetCellValue(Address);

    /// <inheritdoc />
    public override List<OdfCellRange> GetRanges(IEvaluationContext context) => [new OdfCellRange(Address, Address)];

    /// <inheritdoc />
    public override string Serialize() => Address.ToString();
}

/// <summary>
/// Represents an AST node for a cell range reference.
/// 代表儲存格範圍參照的 AST 節點。
/// </summary>
/// <param name="range">The cell range. / 儲存格範圍。</param>
public class RangeReferenceNode(OdfCellRange range) : AstNode
{
    /// <summary>
    /// Gets the cell range.
    /// 取得儲存格範圍。
    /// </summary>
    public OdfCellRange Range { get; } = range;

    /// <inheritdoc />
    public override object Evaluate(IEvaluationContext context) => context.GetRangeValues(Range);

    /// <inheritdoc />
    public override List<OdfCellRange> GetRanges(IEvaluationContext context) => [Range];

    /// <inheritdoc />
    public override string Serialize() => Range.ToString();
}

/// <summary>
/// Represents an AST node for a reference union.
/// 代表聯集參照 (Union) 的 AST 節點。
/// </summary>
/// <param name="left">The left AST node. / 左側 AST 節點。</param>
/// <param name="right">The right AST node. / 右側 AST 節點。</param>
public class ReferenceUnionNode(AstNode left, AstNode right) : AstNode
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
        var ranges = GetRanges(context);
        var list = new OdfReferenceList();
        foreach (var r in ranges)
        {
            list.References.Add(context.GetRangeValues(r));
        }
        return list;
    }

    /// <inheritdoc />
    public override string Serialize() => $"{left.Serialize()}~{right.Serialize()}";
}

/// <summary>
/// Represents an AST node for a reference intersection.
/// 代表交集參照 (Intersection) 的 AST 節點。
/// </summary>
/// <param name="left">The left AST node. / 左側 AST 節點。</param>
/// <param name="right">The right AST node. / 右側 AST 節點。</param>
public class ReferenceIntersectionNode(AstNode left, AstNode right) : AstNode
{
    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public override string Serialize() => $"{left.Serialize()}!{right.Serialize()}";
}

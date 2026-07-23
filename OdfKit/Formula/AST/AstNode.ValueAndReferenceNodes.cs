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
    /// <summary>
    /// Performs evaluate.
    /// 執行 Evaluate。
    /// </summary>
    /// <inheritdoc />
    public override object Evaluate(IEvaluationContext context) => value;

    /// <summary>
    /// Performs serialize.
    /// 執行 Serialize。
    /// </summary>
    /// <inheritdoc />
    public override string Serialize()
    {
        if (value is string s)
            return $"\"{s.Replace("\"", "\"\"")}\"";
        if (value is bool b)
            return b ? "TRUE" : "FALSE";
        if (value is double d)
            return d.ToString(CultureInfo.InvariantCulture);
        if (value is OdfFormulaError error)
            return error.ToErrorString();
        return value?.ToString() ?? string.Empty;
    }
}

/// <summary>
/// Represents an OpenFormula inline array.
/// 代表 OpenFormula 內嵌陣列。
/// </summary>
/// <param name="rows">The array rows. / 陣列資料列。</param>
public sealed class InlineArrayNode(IReadOnlyList<IReadOnlyList<AstNode>> rows) : AstNode
{
    /// <summary>
    /// Evaluates the inline array.
    /// 評估內嵌陣列。
    /// </summary>
    /// <param name="context">The evaluation context. / 評估內容模型。</param>
    /// <returns>The rectangular array value. / 矩形陣列值。</returns>
    public override object Evaluate(IEvaluationContext context)
    {
        if (rows.Count == 0 || rows[0].Count == 0)
        {
            return OdfFormulaError.Value;
        }

        int columnCount = rows[0].Count;
        var values = new object[rows.Count, columnCount];
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            if (rows[rowIndex].Count != columnCount)
            {
                return OdfFormulaError.Value;
            }

            for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                values[rowIndex, columnIndex] = rows[rowIndex][columnIndex].Evaluate(context);
            }
        }

        return values;
    }

    /// <summary>
    /// Serializes the inline array.
    /// 序列化內嵌陣列。
    /// </summary>
    /// <returns>The OpenFormula inline-array text. / OpenFormula 內嵌陣列文字。</returns>
    public override string Serialize()
    {
        var serializedRows = new string[rows.Count];
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var serializedColumns = new string[rows[rowIndex].Count];
            for (int columnIndex = 0; columnIndex < rows[rowIndex].Count; columnIndex++)
            {
                serializedColumns[columnIndex] = rows[rowIndex][columnIndex].Serialize();
            }

            serializedRows[rowIndex] = string.Join(";", serializedColumns);
        }

        return "{" + string.Join("|", serializedRows) + "}";
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

    /// <summary>
    /// Performs evaluate.
    /// 執行 Evaluate。
    /// </summary>
    /// <inheritdoc />
    public override object Evaluate(IEvaluationContext context) => context.GetCellValue(Address);

    /// <summary>
    /// Gets ranges.
    /// 取得 Ranges。
    /// </summary>
    /// <inheritdoc />
    public override List<OdfCellRange> GetRanges(IEvaluationContext context) => [new OdfCellRange(Address, Address)];

    /// <summary>
    /// Performs serialize.
    /// 執行 Serialize。
    /// </summary>
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

    /// <summary>
    /// Performs evaluate.
    /// 執行 Evaluate。
    /// </summary>
    /// <inheritdoc />
    public override object Evaluate(IEvaluationContext context) => context.GetRangeValues(Range);

    /// <summary>
    /// Gets ranges.
    /// 取得 Ranges。
    /// </summary>
    /// <inheritdoc />
    public override List<OdfCellRange> GetRanges(IEvaluationContext context) => [Range];

    /// <summary>
    /// Performs serialize.
    /// 執行 Serialize。
    /// </summary>
    /// <inheritdoc />
    public override string Serialize() => Range.ToString();
}

/// <summary>
/// Represents the OpenFormula reference-range operator.
/// 代表 OpenFormula 參照範圍運算子。
/// </summary>
/// <param name="left">The left AST node. / 左側 AST 節點。</param>
/// <param name="right">The right AST node. / 右側 AST 節點。</param>
public sealed class ReferenceRangeNode(AstNode left, AstNode right) : AstNode
{
    /// <summary>
    /// Gets the ranges produced by the reference-range operator.
    /// 取得參照範圍運算子產生的範圍。
    /// </summary>
    /// <param name="context">The evaluation context. / 求值內容。</param>
    /// <returns>The resolved cell ranges. / 已解析的儲存格範圍。</returns>
    public override List<OdfCellRange> GetRanges(IEvaluationContext context)
    {
        List<OdfCellRange> leftRanges = left.GetRanges(context);
        List<OdfCellRange> rightRanges = right.GetRanges(context);
        var result = new List<OdfCellRange>();
        foreach (OdfCellRange leftRange in leftRanges)
        {
            foreach (OdfCellRange rightRange in rightRanges)
            {
                string? leftSheet = leftRange.StartAddress.SheetName ??
                    context.CurrentCell.SheetName;
                string? rightSheet = rightRange.EndAddress.SheetName ??
                    context.CurrentCell.SheetName;
                if (!string.Equals(
                    leftSheet,
                    rightSheet,
                    StringComparison.OrdinalIgnoreCase))
                {
                    AddSheetRanges(
                        context,
                        result,
                        leftRange,
                        rightRange,
                        leftSheet,
                        rightSheet);
                    continue;
                }

                int minRow = Math.Min(
                    Math.Min(leftRange.StartAddress.Row, leftRange.EndAddress.Row),
                    Math.Min(rightRange.StartAddress.Row, rightRange.EndAddress.Row));
                int maxRow = Math.Max(
                    Math.Max(leftRange.StartAddress.Row, leftRange.EndAddress.Row),
                    Math.Max(rightRange.StartAddress.Row, rightRange.EndAddress.Row));
                int minColumn = Math.Min(
                    Math.Min(leftRange.StartAddress.Column, leftRange.EndAddress.Column),
                    Math.Min(rightRange.StartAddress.Column, rightRange.EndAddress.Column));
                int maxColumn = Math.Max(
                    Math.Max(leftRange.StartAddress.Column, leftRange.EndAddress.Column),
                    Math.Max(rightRange.StartAddress.Column, rightRange.EndAddress.Column));
                result.Add(new OdfCellRange(
                    minRow,
                    minColumn,
                    maxRow,
                    maxColumn,
                    leftSheet));
            }
        }

        return result;
    }

    private static void AddSheetRanges(
        IEvaluationContext context,
        List<OdfCellRange> result,
        OdfCellRange leftRange,
        OdfCellRange rightRange,
        string? leftSheet,
        string? rightSheet)
    {
        if (context is not IOdfFormulaWorkbookContext workbook ||
            string.IsNullOrEmpty(leftSheet) ||
            string.IsNullOrEmpty(rightSheet))
        {
            return;
        }

        int leftIndex = IndexOfSheet(workbook.SheetNames, leftSheet!);
        int rightIndex = IndexOfSheet(workbook.SheetNames, rightSheet!);
        if (leftIndex < 0 || rightIndex < 0)
            return;

        int minRow = Math.Min(
            Math.Min(leftRange.StartAddress.Row, leftRange.EndAddress.Row),
            Math.Min(rightRange.StartAddress.Row, rightRange.EndAddress.Row));
        int maxRow = Math.Max(
            Math.Max(leftRange.StartAddress.Row, leftRange.EndAddress.Row),
            Math.Max(rightRange.StartAddress.Row, rightRange.EndAddress.Row));
        int minColumn = Math.Min(
            Math.Min(leftRange.StartAddress.Column, leftRange.EndAddress.Column),
            Math.Min(rightRange.StartAddress.Column, rightRange.EndAddress.Column));
        int maxColumn = Math.Max(
            Math.Max(leftRange.StartAddress.Column, leftRange.EndAddress.Column),
            Math.Max(rightRange.StartAddress.Column, rightRange.EndAddress.Column));
        int step = leftIndex <= rightIndex ? 1 : -1;
        for (int index = leftIndex; ; index += step)
        {
            result.Add(new OdfCellRange(
                minRow,
                minColumn,
                maxRow,
                maxColumn,
                workbook.SheetNames[index]));
            if (index == rightIndex)
                break;
        }
    }

    private static int IndexOfSheet(
        IReadOnlyList<string> sheetNames,
        string name)
    {
        for (int index = 0; index < sheetNames.Count; index++)
        {
            if (string.Equals(
                sheetNames[index],
                name,
                StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Evaluates the reference-range expression.
    /// 求值參照範圍運算式。
    /// </summary>
    /// <param name="context">The evaluation context. / 求值內容。</param>
    /// <returns>The referenced values or a formula error. / 參照值或公式錯誤。</returns>
    public override object Evaluate(IEvaluationContext context)
    {
        List<OdfCellRange> ranges = GetRanges(context);
        if (ranges.Count == 0)
            return OdfFormulaError.Ref;
        if (ranges.Count == 1)
            return context.GetRangeValues(ranges[0]);
        var references = new OdfReferenceList();
        foreach (OdfCellRange range in ranges)
            references.References.Add(context.GetRangeValues(range));
        return references;
    }

    /// <summary>
    /// Serializes the reference-range expression.
    /// 序列化參照範圍運算式。
    /// </summary>
    /// <returns>The OpenFormula expression text. / OpenFormula 運算式文字。</returns>
    public override string Serialize() =>
        $"{left.Serialize()}:{right.Serialize()}";
}

/// <summary>
/// Represents an AST node for a reference union.
/// 代表聯集參照 (Union) 的 AST 節點。
/// </summary>
/// <param name="left">The left reference expression. / 左側參照運算式。</param>
/// <param name="right">The right reference expression. / 右側參照運算式。</param>
public class ReferenceUnionNode(AstNode left, AstNode right) : AstNode
{
    /// <summary>
    /// Gets the ranges produced by the reference-union operator.
    /// 取得參照聯集運算子產生的範圍。
    /// </summary>
    /// <param name="context">The evaluation context. / 求值內容。</param>
    /// <returns>The combined cell ranges. / 合併後的儲存格範圍。</returns>
    public override List<OdfCellRange> GetRanges(IEvaluationContext context)
    {
        var list = new List<OdfCellRange>();
        list.AddRange(left.GetRanges(context));
        list.AddRange(right.GetRanges(context));
        return list;
    }

    /// <summary>
    /// Evaluates the reference-union expression.
    /// 求值參照聯集運算式。
    /// </summary>
    /// <param name="context">The evaluation context. / 求值內容。</param>
    /// <returns>The referenced value collections. / 參照值集合。</returns>
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

    /// <summary>
    /// Serializes the reference-union expression.
    /// 序列化參照聯集運算式。
    /// </summary>
    /// <returns>The OpenFormula expression text. / OpenFormula 運算式文字。</returns>
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
    /// <summary>
    /// Gets ranges.
    /// 取得 Ranges。
    /// </summary>
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

    /// <summary>
    /// Performs evaluate.
    /// 執行 Evaluate。
    /// </summary>
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

    /// <summary>
    /// Performs serialize.
    /// 執行 Serialize。
    /// </summary>
    /// <inheritdoc />
    public override string Serialize() => $"{left.Serialize()}!{right.Serialize()}";
}

/// <summary>
/// Represents OpenFormula automatic intersection between two quoted labels.
/// 代表兩個 OpenFormula 引號標籤之間的自動交集。
/// </summary>
/// <param name="left">The left quoted-label expression. / 左側引號標籤運算式。</param>
/// <param name="right">The right quoted-label expression. / 右側引號標籤運算式。</param>
public sealed class AutomaticIntersectionNode(AstNode left, AstNode right) : AstNode
{
    /// <summary>
    /// Gets the single-cell intersections produced by the label expressions.
    /// 取得標籤運算式產生的單一儲存格交集。
    /// </summary>
    /// <param name="context">The evaluation context. / 求值內容。</param>
    /// <returns>The intersecting cell ranges. / 相交的儲存格範圍。</returns>
    public override List<OdfCellRange> GetRanges(IEvaluationContext context)
    {
        var intersections = new List<OdfCellRange>();
        foreach (OdfCellRange leftRange in left.GetRanges(context))
        {
            foreach (OdfCellRange rightRange in right.GetRanges(context))
            {
                OdfCellRange? intersection = leftRange.Intersect(rightRange);
                if (intersection.HasValue &&
                    intersection.Value.StartAddress ==
                    intersection.Value.EndAddress)
                {
                    intersections.Add(intersection.Value);
                }
            }
        }

        return intersections;
    }

    /// <summary>
    /// Evaluates the automatic-intersection expression.
    /// 求值自動交集運算式。
    /// </summary>
    /// <param name="context">The evaluation context. / 求值內容。</param>
    /// <returns>The intersecting cell value or a formula error. / 相交的儲存格值或公式錯誤。</returns>
    public override object Evaluate(IEvaluationContext context)
    {
        List<OdfCellRange> ranges = GetRanges(context);
        return ranges.Count == 1
            ? context.GetCellValue(ranges[0].StartAddress)
            : OdfFormulaError.Value;
    }

    /// <summary>
    /// Serializes the automatic-intersection expression.
    /// 序列化自動交集運算式。
    /// </summary>
    /// <returns>The OpenFormula expression text. / OpenFormula 運算式文字。</returns>
    public override string Serialize() =>
        $"{left.Serialize()}!!{right.Serialize()}";
}

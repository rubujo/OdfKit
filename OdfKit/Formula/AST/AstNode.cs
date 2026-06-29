using System;
using System.Collections.Generic;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula.AST;

/// <summary>
/// Represents an ODF reference list.
/// 代表 ODF 參照清單。
/// </summary>
public class OdfReferenceList
{
    /// <summary>
    /// Gets the object list of references.
    /// 取得參照的物件清單。
    /// </summary>
    public List<object> References { get; } = [];
}

/// <summary>
/// Represents the abstract base class for abstract syntax tree (AST) nodes.
/// 表示抽象語法樹 (AST) 節點的抽象基底類別。
/// </summary>
public abstract class AstNode
{
    /// <summary>
    /// Evaluates this node.
    /// 評估此節點的值。
    /// </summary>
    /// <param name="context">The evaluation context. / 評估內容模型。</param>
    /// <returns>The evaluated result object. / 評估後的結果物件。</returns>
    public abstract object Evaluate(IEvaluationContext context);

    /// <summary>
    /// Gets the cell ranges contained by this node.
    /// 取得此節點包含的儲存格範圍。
    /// </summary>
    /// <param name="context">The evaluation context. / 評估內容模型。</param>
    /// <returns>The cell range list. / 儲存格範圍清單。</returns>
    public virtual List<OdfCellRange> GetRanges(IEvaluationContext context) => [];

    /// <summary>
    /// Serializes this node to a formula string.
    /// 將此節點序列化為公式字串。
    /// </summary>
    /// <returns>The serialized formula string. / 序列化後的公式字串。</returns>
    public abstract string Serialize();
}

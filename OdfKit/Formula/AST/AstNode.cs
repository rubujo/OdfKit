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

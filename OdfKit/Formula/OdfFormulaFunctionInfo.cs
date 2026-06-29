using System;

namespace OdfKit.Formula;

/// <summary>
/// Describes a function supported by the default formula evaluator.
/// 描述一個預設公式評估器支援的函式。
/// </summary>
public sealed class OdfFormulaFunctionInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OdfFormulaFunctionInfo"/> class.
    /// 初始化 <see cref="OdfFormulaFunctionInfo"/> 類別的新執行個體。
    /// </summary>
    /// <param name="name">The function name. / 函式名稱。</param>
    /// <param name="category">The function category. / 函式分類。</param>
    /// <param name="supportLevel">The support level. / 支援層級。</param>
    public OdfFormulaFunctionInfo(string name, string category, OdfFormulaSupportLevel supportLevel)
    {
        Name = name;
        Category = category;
        SupportLevel = supportLevel;
    }

    /// <summary>
    /// Gets the function name.
    /// 取得函式名稱。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the function category.
    /// 取得函式分類。
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// Gets the support level.
    /// 取得支援層級。
    /// </summary>
    public OdfFormulaSupportLevel SupportLevel { get; }
}

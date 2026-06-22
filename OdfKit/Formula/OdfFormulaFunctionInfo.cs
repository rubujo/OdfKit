using System;

namespace OdfKit.Formula;

/// <summary>
/// 描述一個預設公式評估器支援的函式。
/// </summary>
public sealed class OdfFormulaFunctionInfo
{
    /// <summary>
    /// 初始化 <see cref="OdfFormulaFunctionInfo"/> 類別的新執行個體。
    /// </summary>
    /// <param name="name">函式名稱</param>
    /// <param name="category">函式分類</param>
    /// <param name="supportLevel">支援層級</param>
    public OdfFormulaFunctionInfo(string name, string category, OdfFormulaSupportLevel supportLevel)
    {
        Name = name;
        Category = category;
        SupportLevel = supportLevel;
    }

    /// <summary>
    /// 取得函式名稱。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 取得函式分類。
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// 取得支援層級。
    /// </summary>
    public OdfFormulaSupportLevel SupportLevel { get; }
}

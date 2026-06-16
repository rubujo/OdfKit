using System;

namespace OdfKit.Formula;

/// <summary>
/// 表示公式函式支援層級。
/// </summary>
public enum OdfFormulaSupportLevel
{
    /// <summary>
    /// 可由預設評估器計算。
    /// </summary>
    Evaluated,

    /// <summary>
    /// 可在文件中保真保存，但預設評估器不計算。
    /// </summary>
    PreservedOnly
}

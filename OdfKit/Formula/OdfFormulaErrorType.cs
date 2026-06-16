using System;

namespace OdfKit.Formula;

/// <summary>
/// 代表 ODF 公式錯誤的型別。
/// </summary>
public enum OdfFormulaErrorType
{
    /// <summary>
    /// 無交集錯誤 (#NULL!)。
    /// </summary>
    Null,

    /// <summary>
    /// 除以零錯誤 (#DIV/0!)。
    /// </summary>
    Div0,

    /// <summary>
    /// 值錯誤 (#VALUE!)。
    /// </summary>
    Value,

    /// <summary>
    /// 參照無效錯誤 (#REF!)。
    /// </summary>
    Ref,

    /// <summary>
    /// 名稱未識別錯誤 (#NAME?)。
    /// </summary>
    Name,

    /// <summary>
    /// 數字錯誤 (#NUM!)。
    /// </summary>
    Num,

    /// <summary>
    /// 值無法使用錯誤 (#N/A)。
    /// </summary>
    NA
}

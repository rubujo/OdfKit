using System;

namespace OdfKit.Formula;

/// <summary>
/// Represents ODF formula error types.
/// 代表 ODF 公式錯誤的型別。
/// </summary>
public enum OdfFormulaErrorType
{
    /// <summary>
    /// No-intersection error (#NULL!).
    /// 無交集錯誤 (#NULL!)。
    /// </summary>
    Null,

    /// <summary>
    /// Division-by-zero error (#DIV/0!).
    /// 除以零錯誤 (#DIV/0!)。
    /// </summary>
    Div0,

    /// <summary>
    /// Value error (#VALUE!).
    /// 值錯誤 (#VALUE!)。
    /// </summary>
    Value,

    /// <summary>
    /// Invalid reference error (#REF!).
    /// 參照無效錯誤 (#REF!)。
    /// </summary>
    Ref,

    /// <summary>
    /// Unrecognized name error (#NAME?).
    /// 名稱未識別錯誤 (#NAME?)。
    /// </summary>
    Name,

    /// <summary>
    /// Numeric error (#NUM!).
    /// 數字錯誤 (#NUM!)。
    /// </summary>
    Num,

    /// <summary>
    /// Not available error (#N/A).
    /// 值無法使用錯誤 (#N/A)。
    /// </summary>
    NA
}

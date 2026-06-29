using System;

namespace OdfKit.Formula;

/// <summary>
/// Represents an ODF formula evaluation error.
/// 代表 ODF 公式評估錯誤。
/// </summary>
/// <param name="errorType">The formula error type. / 公式錯誤型別。</param>
public class OdfFormulaError(OdfFormulaErrorType errorType)
{
    /// <summary>
    /// Gets the formula error type.
    /// 取得公式錯誤的型別。
    /// </summary>
    public OdfFormulaErrorType ErrorType { get; } = errorType;

    /// <summary>
    /// Converts the formula error to its corresponding error string.
    /// 將公式錯誤轉換為對應的錯誤字串。
    /// </summary>
    /// <returns>The formula error string. / 公式錯誤字串。</returns>
    public string ToErrorString() => ErrorType switch
    {
        OdfFormulaErrorType.Null => "#NULL!",
        OdfFormulaErrorType.Div0 => "#DIV/0!",
        OdfFormulaErrorType.Value => "#VALUE!",
        OdfFormulaErrorType.Ref => "#REF!",
        OdfFormulaErrorType.Name => "#NAME?",
        OdfFormulaErrorType.Num => "#NUM!",
        OdfFormulaErrorType.NA => "#N/A",
        _ => "#VALUE!"
    };

    /// <summary>
    /// A static instance representing a no-intersection error.
    /// 代表無交集錯誤的靜態執行個體。
    /// </summary>
    public static readonly OdfFormulaError Null = new(OdfFormulaErrorType.Null);

    /// <summary>
    /// A static instance representing a division-by-zero error.
    /// 代表除以零錯誤的靜態執行個體。
    /// </summary>
    public static readonly OdfFormulaError Div0 = new(OdfFormulaErrorType.Div0);

    /// <summary>
    /// A static instance representing a value error.
    /// 代表值錯誤的靜態執行個體。
    /// </summary>
    public static readonly OdfFormulaError Value = new(OdfFormulaErrorType.Value);

    /// <summary>
    /// A static instance representing an invalid reference error.
    /// 代表參照無效錯誤的靜態執行個體。
    /// </summary>
    public static readonly OdfFormulaError Ref = new(OdfFormulaErrorType.Ref);

    /// <summary>
    /// A static instance representing an unrecognized name error.
    /// 代表名稱未識別錯誤的靜態執行個體。
    /// </summary>
    public static readonly OdfFormulaError Name = new(OdfFormulaErrorType.Name);

    /// <summary>
    /// A static instance representing a numeric error.
    /// 代表數字錯誤的靜態執行個體。
    /// </summary>
    public static readonly OdfFormulaError Num = new(OdfFormulaErrorType.Num);

    /// <summary>
    /// A static instance representing a not available error.
    /// 代表值無法使用錯誤的靜態執行個體。
    /// </summary>
    public static readonly OdfFormulaError NA = new(OdfFormulaErrorType.NA);
}

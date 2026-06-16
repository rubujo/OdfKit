using System;

namespace OdfKit.Formula;

/// <summary>
/// 代表 ODF 公式評估錯誤。
/// </summary>
/// <param name="errorType">公式錯誤型別</param>
public class OdfFormulaError(OdfFormulaErrorType errorType)
{
    /// <summary>
    /// 取得公式錯誤的型別。
    /// </summary>
    public OdfFormulaErrorType ErrorType { get; } = errorType;

    /// <summary>
    /// 將公式錯誤轉換為對應的錯誤字串。
    /// </summary>
    /// <returns>公式錯誤字串</returns>
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
    /// 代表無交集錯誤的靜態執行個體。
    /// </summary>
    public static readonly OdfFormulaError Null = new(OdfFormulaErrorType.Null);

    /// <summary>
    /// 代表除以零錯誤的靜態執行個體。
    /// </summary>
    public static readonly OdfFormulaError Div0 = new(OdfFormulaErrorType.Div0);

    /// <summary>
    /// 代表值錯誤的靜態執行個體。
    /// </summary>
    public static readonly OdfFormulaError Value = new(OdfFormulaErrorType.Value);

    /// <summary>
    /// 代表參照無效錯誤的靜態執行個體。
    /// </summary>
    public static readonly OdfFormulaError Ref = new(OdfFormulaErrorType.Ref);

    /// <summary>
    /// 代表名稱未識別錯誤的靜態執行個體。
    /// </summary>
    public static readonly OdfFormulaError Name = new(OdfFormulaErrorType.Name);

    /// <summary>
    /// 代表數字錯誤的靜態執行個體。
    /// </summary>
    public static readonly OdfFormulaError Num = new(OdfFormulaErrorType.Num);

    /// <summary>
    /// 代表值無法使用錯誤的靜態執行個體。
    /// </summary>
    public static readonly OdfFormulaError NA = new(OdfFormulaErrorType.NA);
}

using System;

using OdfKit.Compliance;

namespace OdfKit.Core;

internal static partial class OdfSignatureVerifier
{
    /// <summary>
    /// 記錄簽章驗證失敗結果並回傳 <see langword="false"/>：設定錯誤碼與在地化錯誤訊息，
    /// 並透過 <paramref name="markInvalid"/> 更新對應的驗證階段旗標（如 <c>IsSignatureValid</c>）。
    /// </summary>
    private static bool Fail(
        Action markInvalid,
        OdfSingleSignatureValidationResult result,
        string errorCode,
        string messageKey,
        params object?[] args)
    {
        markInvalid();
        result.ErrorCode = errorCode;
        result.ErrorMessage = OdfLocalizer.GetMessage(messageKey, args);
        return false;
    }

    /// <summary>
    /// 與 <see cref="Fail"/> 相同，另外將 <paramref name="warningDetail"/>（通常是原始例外訊息）加入 Warnings。
    /// </summary>
    private static bool FailWithWarning(
        Action markInvalid,
        OdfSingleSignatureValidationResult result,
        string errorCode,
        string messageKey,
        string warningDetail,
        params object?[] args)
    {
        markInvalid();
        result.ErrorCode = errorCode;
        result.ErrorMessage = OdfLocalizer.GetMessage(messageKey, args);
        result.Warnings.Add(warningDetail);
        return false;
    }
}

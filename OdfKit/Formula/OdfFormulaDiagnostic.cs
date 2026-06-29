using System;

namespace OdfKit.Formula;

/// <summary>
/// Represents a formula analysis diagnostic.
/// 表示公式分析診斷。
/// </summary>
public sealed class OdfFormulaDiagnostic
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OdfFormulaDiagnostic"/> class.
    /// 初始化 <see cref="OdfFormulaDiagnostic"/> 類別的新執行個體。
    /// </summary>
    /// <param name="code">The diagnostic code. / 診斷代碼。</param>
    /// <param name="message">The diagnostic message. / 診斷訊息。</param>
    /// <param name="severity">The diagnostic severity. / 診斷嚴重性。</param>
    /// <param name="position">The character position in the formula, or null when unavailable. / 公式中的字元位置，若無位置資訊則為 null。</param>
    public OdfFormulaDiagnostic(string code, string message, OdfFormulaDiagnosticSeverity severity, int? position = null)
    {
        Code = code;
        Message = message;
        Severity = severity;
        Position = position;
    }

    /// <summary>
    /// Gets the diagnostic code.
    /// 取得診斷代碼。
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the diagnostic message.
    /// 取得診斷訊息。
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the diagnostic severity.
    /// 取得診斷嚴重性。
    /// </summary>
    public OdfFormulaDiagnosticSeverity Severity { get; }

    /// <summary>
    /// Gets the character position in the formula, or null when unavailable.
    /// 取得公式中的字元位置，若無位置資訊則為 null。
    /// </summary>
    public int? Position { get; }
}

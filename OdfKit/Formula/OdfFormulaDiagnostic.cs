using System;

namespace OdfKit.Formula;

/// <summary>
/// 表示公式分析診斷。
/// </summary>
public sealed class OdfFormulaDiagnostic
{
    /// <summary>
    /// 初始化 <see cref="OdfFormulaDiagnostic"/> 類別的新執行個體。
    /// </summary>
    /// <param name="code">診斷代碼</param>
    /// <param name="message">診斷訊息</param>
    /// <param name="severity">嚴重性</param>
    /// <param name="position">公式中的字元位置，若無位置資訊則為 null</param>
    public OdfFormulaDiagnostic(string code, string message, OdfFormulaDiagnosticSeverity severity, int? position = null)
    {
        Code = code;
        Message = message;
        Severity = severity;
        Position = position;
    }

    /// <summary>
    /// 取得診斷代碼。
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// 取得診斷訊息。
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 取得嚴重性。
    /// </summary>
    public OdfFormulaDiagnosticSeverity Severity { get; }

    /// <summary>
    /// 取得公式中的字元位置，若無位置資訊則為 null。
    /// </summary>
    public int? Position { get; }
}

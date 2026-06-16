using System;

namespace OdfKit.Formula;

/// <summary>
/// 表示公式診斷的嚴重性。
/// </summary>
public enum OdfFormulaDiagnosticSeverity
{
    /// <summary>
    /// 資訊。
    /// </summary>
    Info,

    /// <summary>
    /// 警告。
    /// </summary>
    Warning,

    /// <summary>
    /// 錯誤。
    /// </summary>
    Error
}

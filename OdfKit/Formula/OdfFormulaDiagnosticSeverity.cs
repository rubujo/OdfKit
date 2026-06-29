using System;

namespace OdfKit.Formula;

/// <summary>
/// Represents the severity of a formula diagnostic.
/// 表示公式診斷的嚴重性。
/// </summary>
public enum OdfFormulaDiagnosticSeverity
{
    /// <summary>
    /// Informational diagnostic.
    /// 資訊。
    /// </summary>
    Info,

    /// <summary>
    /// Warning diagnostic.
    /// 警告。
    /// </summary>
    Warning,

    /// <summary>
    /// Error diagnostic.
    /// 錯誤。
    /// </summary>
    Error
}

using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("OdfKit.Extensions.Rendering")]
[assembly: InternalsVisibleTo("OdfKit.Tests")]
[assembly: InternalsVisibleTo("OdfKit.Extensions.Pdf")]
[assembly: InternalsVisibleTo("OdfKit.Extensions.Imaging")]
[assembly: InternalsVisibleTo("OdfKit.Extensions.Ooxml")]
[assembly: InternalsVisibleTo("OdfKit.Extensions.Html")]
[assembly: InternalsVisibleTo("OdfKit.TrimSmoke")]

namespace OdfKit.Core;

/// <summary>
/// Defines values for OdfDiagnosticsLevel.
/// 表示 OdfKit 診斷日誌的嚴重性等級。
/// </summary>
public enum OdfDiagnosticsLevel
{
    /// <summary>
    /// 資訊性日誌等級。
    /// </summary>
    Info,

    /// <summary>
    /// 警告性日誌等級。
    /// </summary>
    Warning,

    /// <summary>
    /// 錯誤性日誌等級。
    /// </summary>
    Error
}

/// <summary>
/// Provides the OdfDiagnosticsEventArgs API.
/// 提供 OdfKit 診斷日誌事件的資料。
/// </summary>
/// <param name="level">診斷日誌等級</param>
/// <param name="message">診斷訊息內容</param>
/// <param name="exception">相關聯的例外狀況，若無則為 null</param>
public class OdfDiagnosticsEventArgs(OdfDiagnosticsLevel level, string message, Exception? exception = null) : EventArgs
{
    /// <summary>
    /// Gets the Level value.
    /// 取得診斷日誌等級。
    /// </summary>
    public OdfDiagnosticsLevel Level { get; } = level;

    /// <summary>
    /// Gets the Message value.
    /// 取得診斷訊息內容。
    /// </summary>
    public string Message { get; } = message;

    /// <summary>
    /// Gets the Exception value.
    /// 取得診斷相關聯的例外狀況。
    /// </summary>
    public Exception? Exception { get; } = exception;

    /// <summary>
    /// Gets the Timestamp value.
    /// 取得診斷日誌記錄的 UTC 時間戳記。
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Provides the OdfKitDiagnostics API.
/// 提供 OdfKit 的全域診斷與日誌功能。
/// </summary>
public static class OdfKitDiagnostics
{
    /// <summary>
    /// Occurs when OdfKit emits a diagnostic log event.
    /// 當 OdfKit 發出診斷日誌事件時觸發。
    /// </summary>
    public static event EventHandler<OdfDiagnosticsEventArgs>? Log;

    internal static void Send(OdfDiagnosticsLevel level, string message, Exception? exception = null)
    {
        Log?.Invoke(null, new OdfDiagnosticsEventArgs(level, message, exception));
    }

    internal static void Info(string message) => Send(OdfDiagnosticsLevel.Info, message);
    internal static void Warn(string message, Exception? exception = null) => Send(OdfDiagnosticsLevel.Warning, message, exception);
    internal static void Error(string message, Exception? exception = null) => Send(OdfDiagnosticsLevel.Error, message, exception);
}

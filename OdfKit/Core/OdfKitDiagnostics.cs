using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("OdfKit.Extensions.Rendering")]
[assembly: InternalsVisibleTo("OdfKit.Tests")]

namespace OdfKit.Core;

/// <summary>
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
/// 提供 OdfKit 診斷日誌事件的資料。
/// </summary>
/// <param name="level">診斷日誌等級</param>
/// <param name="message">診斷訊息內容</param>
/// <param name="exception">相關聯的例外狀況，若無則為 null</param>
public class OdfDiagnosticsEventArgs(OdfDiagnosticsLevel level, string message, Exception? exception = null) : EventArgs
{
    /// <summary>
    /// 取得診斷日誌等級。
    /// </summary>
    public OdfDiagnosticsLevel Level { get; } = level;

    /// <summary>
    /// 取得診斷訊息內容。
    /// </summary>
    public string Message { get; } = message;

    /// <summary>
    /// 取得診斷相關聯的例外狀況。
    /// </summary>
    public Exception? Exception { get; } = exception;

    /// <summary>
    /// 取得診斷日誌記錄的 UTC 時間戳記。
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// 提供 OdfKit 的全域診斷與日誌功能。
/// </summary>
public static class OdfKitDiagnostics
{
    /// <summary>
    /// 全域靜態診斷日誌事件。開發者可訂閱此事件以將診斷資訊導向自訂的日誌系統。
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

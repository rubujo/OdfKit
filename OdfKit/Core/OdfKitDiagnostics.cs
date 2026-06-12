#pragma warning disable 1591 // Suppress CS1591 (missing XML comments) for legacy hand-written APIs to maintain zero-warning compilation under TreatWarningsAsErrors while package XML documentation is generated.
using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("OdfKit.Extensions.Rendering")]
[assembly: InternalsVisibleTo("OdfKit.Tests")]

namespace OdfKit.Core
{
    public enum OdfDiagnosticsLevel
    {
        Info,
        Warning,
        Error
    }

    public class OdfDiagnosticsEventArgs : EventArgs
    {
        public OdfDiagnosticsLevel Level { get; }
        public string Message { get; }
        public Exception? Exception { get; }
        public DateTime Timestamp { get; }

        public OdfDiagnosticsEventArgs(OdfDiagnosticsLevel level, string message, Exception? exception = null)
        {
            Level = level;
            Message = message;
            Exception = exception;
            Timestamp = DateTime.UtcNow;
        }
    }

    public static class OdfKitDiagnostics
    {
        /// <summary>
        /// 全域靜態診斷日誌事件，開發者可訂閱以導向自訂的日誌系統。
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
}

using System.Collections.Generic;

using OdfKit.Compliance;

namespace OdfKit.Collaboration;

/// <summary>
/// Reports the outcome of importing ODT operation logs.
/// 描述 ODT JSON operations 匯入時的重播與相容性診斷結果。
/// </summary>
public sealed class OdtOperationImportReport
{
    private readonly List<string> _diagnostics = [];
    private readonly List<OdtOperationReportEntry> _entries = [];

    /// <summary>
    /// Gets the number of operations replayed successfully.
    /// 取得已實際重播的 operation 數量。
    /// </summary>
    public int ReplayedCount { get; private set; }

    /// <summary>
    /// Gets the number of operations ignored during import.
    /// 取得因為相容性邊界或 metadata-only 語意而被略過的 operation 數量。
    /// </summary>
    public int IgnoredCount { get; private set; }

    /// <summary>
    /// Gets the number of unsupported operations encountered during import.
    /// 取得目前 compatibility subset 尚未支援的 operation 數量。
    /// </summary>
    public int UnsupportedCount { get; private set; }

    /// <summary>
    /// Gets diagnostic messages produced during import.
    /// 取得匯入過程產生的診斷訊息。
    /// </summary>
    public IReadOnlyList<string> Diagnostics => _diagnostics;

    /// <summary>
    /// Gets per-operation replay entries.
    /// 取得逐筆 operation 重播項目。
    /// </summary>
    public IReadOnlyList<OdtOperationReportEntry> Entries => _entries;

    /// <summary>
    /// Gets the number of skipped operations.
    /// 取得被略過的 operation 數量。
    /// </summary>
    public int SkippedCount => IgnoredCount;

    /// <summary>
    /// Gets the last safety limit hit reason, if any.
    /// 取得最後一次觸發安全限制的原因；若無則為 null。
    /// </summary>
    public string? SafetyLimitHitReason { get; private set; }

    internal void RecordReplayed(OdtOperation? operation = null)
    {
        ReplayedCount++;
        _entries.Add(new OdtOperationReportEntry(
            operation?.SourceIndex ?? -1,
            operation?.Name,
            OdtOperationReplayStatus.Replayed,
            null,
            null,
            null));
    }

    internal void RecordIgnored(string? operationName, OdtOperationCompatibilityOptions options, OdtOperation? operation = null)
    {
        IgnoredCount++;
        const string key = "Diag_OdtOperationImportReport_MetadataOnlyIgnored";
        string? message = null;
        if (options.EmitDiagnostics && options.UnsupportedOperationPolicy == OdtUnsupportedOperationPolicy.RecordDiagnostic)
        {
            message = OdfLocalizer.GetMessage(key, operationName ?? string.Empty);
            _diagnostics.Add(message);
        }

        _entries.Add(new OdtOperationReportEntry(
            operation?.SourceIndex ?? -1,
            operationName,
            OdtOperationReplayStatus.Ignored,
            key,
            message,
            null));
    }

    internal void RecordUnsupported(string? operationName, OdtOperationCompatibilityOptions options, OdtOperation? operation = null, string? reason = null)
    {
        UnsupportedCount++;

        if (options.UnsupportedOperationPolicy == OdtUnsupportedOperationPolicy.Throw)
        {
            throw new NotSupportedException(OdfLocalizer.GetMessage("Err_OdtOperationImportReport_UnsupportedOperation", operationName ?? string.Empty));
        }

        IgnoredCount++;
        const string key = "Diag_OdtOperationImportReport_UnsupportedOperation";
        string? message = null;
        if (options.EmitDiagnostics && options.UnsupportedOperationPolicy == OdtUnsupportedOperationPolicy.RecordDiagnostic)
        {
            message = string.IsNullOrEmpty(reason)
                ? OdfLocalizer.GetMessage(key, operationName ?? string.Empty)
                : OdfLocalizer.GetMessage(key, operationName ?? string.Empty) + " " + reason;
            _diagnostics.Add(message);
        }

        _entries.Add(new OdtOperationReportEntry(
            operation?.SourceIndex ?? -1,
            operationName,
            OdtOperationReplayStatus.Unsupported,
            key,
            message,
            reason));
    }

    internal void RecordSafetyLimit(string reason)
    {
        SafetyLimitHitReason = reason;
    }
}

using System.Collections.Generic;

using OdfKit.Compliance;

namespace OdfKit.Collaboration;

/// <summary>
/// Provides odt operation import report.
/// 描述 ODT JSON operations 匯入時的重播與相容性診斷結果。
/// </summary>
public sealed class OdtOperationImportReport
{
    private readonly List<string> _diagnostics = [];

    /// <summary>
    /// Gets replayed count.
    /// 取得已實際重播的 operation 數量。
    /// </summary>
    public int ReplayedCount { get; private set; }

    /// <summary>
    /// Gets ignored count.
    /// 取得因為相容性邊界或 metadata-only 語意而被略過的 operation 數量。
    /// </summary>
    public int IgnoredCount { get; private set; }

    /// <summary>
    /// Gets unsupported count.
    /// 取得目前 compatibility subset 尚未支援的 operation 數量。
    /// </summary>
    public int UnsupportedCount { get; private set; }

    /// <summary>
    /// Gets diagnostics.
    /// 取得匯入過程產生的診斷訊息。
    /// </summary>
    public IReadOnlyList<string> Diagnostics => _diagnostics;

    internal void RecordReplayed() => ReplayedCount++;

    internal void RecordIgnored(string? operationName, OdtOperationCompatibilityOptions options)
    {
        IgnoredCount++;
        if (options.EmitDiagnostics && options.UnsupportedOperationPolicy == OdtUnsupportedOperationPolicy.RecordDiagnostic)
        {
            _diagnostics.Add(OdfLocalizer.GetMessage("Diag_OdtOperationImportReport_MetadataOnlyIgnored", operationName ?? string.Empty));
        }
    }

    internal void RecordUnsupported(string? operationName, OdtOperationCompatibilityOptions options)
    {
        UnsupportedCount++;

        if (options.UnsupportedOperationPolicy == OdtUnsupportedOperationPolicy.Throw)
        {
            throw new NotSupportedException(OdfLocalizer.GetMessage("Err_OdtOperationImportReport_UnsupportedOperation", operationName ?? string.Empty));
        }

        IgnoredCount++;
        if (options.EmitDiagnostics && options.UnsupportedOperationPolicy == OdtUnsupportedOperationPolicy.RecordDiagnostic)
        {
            _diagnostics.Add(OdfLocalizer.GetMessage("Diag_OdtOperationImportReport_UnsupportedOperation", operationName ?? string.Empty));
        }
    }
}

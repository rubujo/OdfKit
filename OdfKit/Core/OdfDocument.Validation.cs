using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using OdfKit.Compliance;
using OdfKit.DOM;

namespace OdfKit.Core;
/// <summary>
/// Adds validation helpers for ODF documents.
/// 提供 ODF 文件驗證輔助方法。
/// </summary>

public abstract partial class OdfDocument
{
    /// <summary>
    /// Short overload of Validate that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：Validate 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfValidationReport Validate() => Validate(null);

    /// <summary>
    /// Performs the Validate operation.
    /// 驗證目前文件（反映記憶體中尚未儲存的編輯內容）是否符合 ODF 規格。
    /// </summary>
    /// <param name="profile">相容性設定檔；若為 <see langword="null"/> 則使用預設設定檔</param>
    /// <returns>結構化驗證結果報告</returns>
    /// <remarks>
    /// 此方法會先將目前 DOM 狀態序列化為記憶體中的暫存封裝（不影響原始 <see cref="Package"/>
    /// 或來源檔案），再交由 <see cref="OdfValidator"/> 驗證，因此可反映呼叫前所做的任何編輯。
    /// </remarks>
    public OdfValidationReport Validate(OdfComplianceProfile? profile)
    {
        using MemoryStream snapshot = new();
        SaveToStream(snapshot);
        OdfValidationReport packageReport = OdfValidator.Validate(snapshot, ValidationFileNameHint(), profile);
        return MergeTopologyReport(packageReport);
    }
    /// <summary>
    /// Short overload of ValidateAsync that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：ValidateAsync 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public Task<OdfValidationReport> ValidateAsync() => ValidateAsync(null, default);

    /// <summary>
    /// Short overload of ValidateAsync that accepts profile; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 profile；其餘可選參數使用預設值並轉呼叫最長 ValidateAsync 多載。
    /// </summary>
    public Task<OdfValidationReport> ValidateAsync(OdfComplianceProfile? profile) => ValidateAsync(profile, default);



    /// <summary>
    /// Validates async.
    /// 非同步驗證目前文件（反映記憶體中尚未儲存的編輯內容）是否符合 ODF 規格。
    /// </summary>
    /// <param name="profile">相容性設定檔；若為 <see langword="null"/> 則使用預設設定檔</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步驗證作業的工作，其結果為結構化驗證結果報告</returns>
    public async Task<OdfValidationReport> ValidateAsync(OdfComplianceProfile? profile, CancellationToken cancellationToken)
    {
        using MemoryStream snapshot = new();
        await SaveToStreamAsync(snapshot, options: null, cancellationToken).ConfigureAwait(false);
        OdfValidationReport packageReport = OdfValidator.Validate(snapshot, ValidationFileNameHint(), profile);
        return MergeTopologyReport(packageReport);
    }


    private OdfValidationReport MergeTopologyReport(OdfValidationReport packageReport)
    {
        OdfValidationReport topologyReport = OdfDocumentValidator.Validate(this);
        if (topologyReport.Issues.Count == 0)
        {
            return packageReport;
        }

        var issues = new List<OdfValidationIssue>(packageReport.Issues.Count + topologyReport.Issues.Count);
        issues.AddRange(packageReport.Issues);
        issues.AddRange(topologyReport.Issues);
        return new OdfValidationReport(packageReport.DetectedVersion, packageReport.DocumentKind, issues);
    }

    private string? ValidationFileNameHint()
    {
        OdfFormatInfo? format = Format;
        if (format is null)
        {
            return null;
        }

        return "document." + format.Extension;
    }
}

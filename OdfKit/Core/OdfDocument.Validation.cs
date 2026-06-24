using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;

namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    /// <summary>
    /// 驗證目前文件（反映記憶體中尚未儲存的編輯內容）是否符合 ODF 規格。
    /// </summary>
    /// <param name="profile">相容性設定檔；若為 <see langword="null"/> 則使用預設設定檔</param>
    /// <returns>結構化驗證結果報告</returns>
    /// <remarks>
    /// 此方法會先將目前 DOM 狀態序列化為記憶體中的暫存封裝（不影響原始 <see cref="Package"/>
    /// 或來源檔案），再交由 <see cref="OdfValidator"/> 驗證，因此可反映呼叫前所做的任何編輯。
    /// </remarks>
    public OdfValidationReport Validate(OdfComplianceProfile? profile = null)
    {
        using MemoryStream snapshot = new();
        SaveToStream(snapshot);
        return OdfValidator.Validate(snapshot, ValidationFileNameHint(), profile);
    }

    /// <summary>
    /// 非同步驗證目前文件（反映記憶體中尚未儲存的編輯內容）是否符合 ODF 規格。
    /// </summary>
    /// <param name="profile">相容性設定檔；若為 <see langword="null"/> 則使用預設設定檔</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步驗證作業的工作，其結果為結構化驗證結果報告</returns>
    public async Task<OdfValidationReport> ValidateAsync(OdfComplianceProfile? profile = null, CancellationToken cancellationToken = default)
    {
        using MemoryStream snapshot = new();
        await SaveToStreamAsync(snapshot, cancellationToken: cancellationToken).ConfigureAwait(false);
        return OdfValidator.Validate(snapshot, ValidationFileNameHint(), profile);
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

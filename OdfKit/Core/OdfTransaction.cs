using System;

namespace OdfKit.Core;

/// <summary>
/// Provides the OdfTransaction API.
/// 提供 OdfPackage 的低階操作沙盒交易防護 (Low-level Sandbox Transaction)。
/// </summary>
public sealed class OdfTransaction : IDisposable
{
    private readonly OdfPackage _package;
    private bool _committed;
    private bool _disposed;

    private OdfTransaction(OdfPackage package)
    {
        _package = package ?? throw new ArgumentNullException(nameof(package));
        _package.BeginTransaction();
    }

    /// <summary>
    /// Performs begin.
    /// 開始一個新的沙盒交易。
    /// </summary>
    /// <remarks>
    /// File-backed packages create a durable journal and hold a cooperative cross-process lock until commit or rollback.
    /// 檔案型封裝容器會建立耐久交易日誌，並持有跨程序協作鎖直到提交或回滾完成。
    /// </remarks>
    /// <param name="package">The package to protect. / 要保護的 ODF 封裝容器。</param>
    /// <returns>The transaction instance. / 代表交易的執行個體。</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="package"/> is <see langword="null"/>. / 當 <paramref name="package"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="IOException">Thrown when the durable journal or cooperative lock cannot be prepared. / 當無法準備耐久交易日誌或協作鎖時擲出。</exception>
    public static OdfTransaction Begin(OdfPackage package)
    {
        return new OdfTransaction(package);
    }

    /// <summary>
    /// Performs commit.
    /// 提交交易，確認所有修改。
    /// </summary>
    /// <remarks>
    /// This method does not save pending package changes; call the package or document save API before committing when disk persistence is required. For file-backed packages, commit flushes the main file before marking the journal as committed.
    /// 此方法不會儲存尚未寫出的封裝容器變更；需要持久化至磁碟時，請先呼叫封裝容器或文件的儲存 API。檔案型封裝容器會先耐久刷寫主檔，再將交易日誌標記為已提交。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when the transaction has already been disposed. / 當交易已處置時擲出。</exception>
    /// <exception cref="IOException">Thrown when the main file cannot be durably flushed or the journal cannot be marked as committed. / 當主檔無法耐久刷寫或交易日誌無法標記為已提交時擲出。</exception>
    public void Commit()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(OdfTransaction));

        _package.CommitTransaction();
        _committed = true;
    }

    /// <summary>
    /// Releases unmanaged resources.
    /// 釋放並結束交易。如果未呼叫 Commit，將自動進行 Rollback。
    /// </summary>
    /// <remarks>
    /// Rollback failures are reported through diagnostics; an uncommitted journal remains available for recovery on the next file open.
    /// 回滾失敗會透過診斷管道回報；未提交日誌會保留，供下次開檔時恢復。
    /// </remarks>
    public void Dispose()
    {
        if (!_disposed)
        {
            if (!_committed)
            {
                Rollback();
            }
            _disposed = true;
        }
    }

    private void Rollback()
    {
        try
        {
            OdfKitDiagnostics.Warn("OdfTransaction 未被 Commit。正在自動進行 Rollback 回滾記憶體虛擬 VFS 變更集...");
            _package.RollbackTransaction();
        }
        catch (Exception ex)
        {
            OdfKitDiagnostics.Warn("回滾過程中發生錯誤。", ex);
        }
    }
}

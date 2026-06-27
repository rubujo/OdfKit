using System;

namespace OdfKit.Core;

/// <summary>
/// 提供 OdfPackage 的低階操作沙盒事務防護 (Low-level Sandbox Transaction)。
/// 基於虛擬 VFS 變更集實作，其開銷為 O(1)，完全避免了傳統的大快照 Save 與 Restore 的磁碟與記憶體消耗。
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
    /// 開始一個新的沙盒事務。
    /// </summary>
    /// <param name="package">要保護的 OdfPackage</param>
    /// <returns>代表事務的 OdfTransaction 執行個體</returns>
    public static OdfTransaction Begin(OdfPackage package)
    {
        return new OdfTransaction(package);
    }

    /// <summary>
    /// 提交事務，確認所有修改。
    /// </summary>
    public void Commit()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(OdfTransaction));

        _package.CommitTransaction();
        _committed = true;
    }

    /// <summary>
    /// 釋放並結束事務。如果未呼叫 Commit，將自動進行 Rollback。
    /// </summary>
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

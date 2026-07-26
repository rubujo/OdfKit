using System.Text;

using OdfKit.Core;
using OdfKit.Spreadsheet;

using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Verifies durable transaction-journal state transitions and recovery behavior.
/// 驗證耐久交易日誌的狀態轉換與恢復行為。
/// </summary>
public sealed class OdfTransactionJournalTests
{
    /// <summary>
    /// Verifies that a failed durable flush keeps the transaction active and preserves the rollback journal.
    /// 驗證耐久刷寫失敗時會維持交易狀態並保留回滾日誌。
    /// </summary>
    [Fact]
    public void CommitFlushFailurePreservesRollbackJournal()
    {
        string path = CreatePackagePath();
        string journalPath = OdfTransactionJournal.GetJournalPath(path);
        try
        {
            CreatePackage(path);
            var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            using var package = OdfPackage.Open(
                stream,
                leaveOpen: true,
                new OdfLoadOptions { AllowLazyLoading = false });
            using var transaction = OdfTransaction.Begin(package);

            package.WriteEntry("transaction-change.txt", Encoding.UTF8.GetBytes("changed"));
            package.Save();
            stream.Dispose();

            IOException exception = Assert.Throws<IOException>(transaction.Commit);
            Assert.Equal(
                OdfKit.Compliance.OdfLocalizer.GetMessage("Err_OdfPackage_TransactionJournalFailed"),
                exception.Message);
            Assert.True(package.InTransaction);
            Assert.True(File.Exists(journalPath));
            Assert.False(File.Exists(OdfTransactionJournal.GetCommittedPath(path)));
        }
        finally
        {
            DeleteTransactionFiles(path);
        }
    }

    /// <summary>
    /// Verifies that a successful commit cannot be undone by a committed journal left behind during cleanup.
    /// 驗證成功提交不會被清理階段遺留的已提交日誌撤銷。
    /// </summary>
    [Fact]
    public void CommittedJournalIsIgnoredDuringNextOpen()
    {
        string path = CreatePackagePath();
        string originalSnapshot = path + ".original";
        string committedPath = OdfTransactionJournal.GetCommittedPath(path);
        try
        {
            CreatePackage(path);
            File.Copy(path, originalSnapshot);

            using (OdfPackage package = OdfPackage.Open(
                       path,
                       new OdfLoadOptions { AllowLazyLoading = false }))
            {
                package.WriteEntry("committed-change.txt", Encoding.UTF8.GetBytes("committed"));
                package.Save();
            }

            File.Copy(originalSnapshot, committedPath);

            using OdfPackage reopened = OdfPackage.Open(
                path,
                new OdfLoadOptions { AllowLazyLoading = false });
            Assert.True(reopened.HasEntry("committed-change.txt"));
            Assert.False(File.Exists(committedPath));
        }
        finally
        {
            TryDelete(originalSnapshot);
            DeleteTransactionFiles(path);
        }
    }

    /// <summary>
    /// Verifies that a normal commit removes rollback state and preserves the saved mutation.
    /// 驗證一般提交會移除回滾狀態並保留已儲存的變更。
    /// </summary>
    [Fact]
    public void SuccessfulCommitPublishesSavedMutation()
    {
        string path = CreatePackagePath();
        try
        {
            CreatePackage(path);
            using (OdfPackage package = OdfPackage.Open(
                       path,
                       new OdfLoadOptions { AllowLazyLoading = false }))
            using (OdfTransaction transaction = OdfTransaction.Begin(package))
            {
                package.WriteEntry("committed-change.txt", Encoding.UTF8.GetBytes("committed"));
                package.Save();
                transaction.Commit();
            }

            Assert.False(File.Exists(OdfTransactionJournal.GetJournalPath(path)));
            Assert.False(File.Exists(OdfTransactionJournal.GetCommittedPath(path)));
            Assert.False(File.Exists(OdfTransactionJournal.GetLockPath(path)));

            using OdfPackage reopened = OdfPackage.Open(
                path,
                new OdfLoadOptions { AllowLazyLoading = false });
            Assert.True(reopened.HasEntry("committed-change.txt"));
        }
        finally
        {
            DeleteTransactionFiles(path);
        }
    }

    /// <summary>
    /// Verifies that removing an active rollback journal prevents commit instead of silently weakening recovery.
    /// 驗證移除使用中的回滾日誌時會阻止提交，而不是靜默降低恢復保證。
    /// </summary>
    [Fact]
    public void MissingActiveJournalPreventsCommit()
    {
        string path = CreatePackagePath();
        try
        {
            CreatePackage(path);
            using OdfPackage package = OdfPackage.Open(
                path,
                new OdfLoadOptions { AllowLazyLoading = false });
            using OdfTransaction transaction = OdfTransaction.Begin(package);
            File.Delete(OdfTransactionJournal.GetJournalPath(path));

            IOException exception = Assert.Throws<IOException>(transaction.Commit);
            Assert.Equal(
                OdfKit.Compliance.OdfLocalizer.GetMessage("Err_OdfPackage_TransactionJournalFailed"),
                exception.Message);
            Assert.True(package.InTransaction);
        }
        finally
        {
            DeleteTransactionFiles(path);
        }
    }

    /// <summary>
    /// Verifies that asynchronous path opening uses the same crash-recovery journal semantics.
    /// 驗證非同步路徑開檔使用相同的崩潰恢復日誌語意。
    /// </summary>
    [Fact]
    public async Task OpenAsyncRecoversUncommittedJournal()
    {
        string path = CreatePackagePath();
        string journalPath = OdfTransactionJournal.GetJournalPath(path);
        try
        {
            CreatePackage(path);
            File.Copy(path, journalPath);
            File.WriteAllBytes(path, []);

            await using OdfPackage recovered = await OdfPackage.OpenAsync(
                path,
                new OdfLoadOptions { AllowLazyLoading = false },
                TestContext.Current.CancellationToken);
            Assert.True(recovered.HasEntry("content.xml"));
            Assert.False(File.Exists(journalPath));
        }
        finally
        {
            DeleteTransactionFiles(path);
        }
    }

    /// <summary>
    /// Verifies that two file-backed transactions cannot hold the cooperative lock concurrently.
    /// 驗證兩個檔案型交易無法同時持有協作鎖。
    /// </summary>
    [Fact]
    public void ConcurrentTransactionsAreRejectedByCooperativeLock()
    {
        string path = CreatePackagePath();
        try
        {
            CreatePackage(path);
            var options = new OdfLoadOptions { AllowLazyLoading = false };
            using OdfPackage firstPackage = OdfPackage.Open(path, options);
            using OdfPackage secondPackage = OdfPackage.Open(path, options);
            using OdfTransaction firstTransaction = OdfTransaction.Begin(firstPackage);

            IOException exception = Assert.Throws<IOException>(() => OdfTransaction.Begin(secondPackage));
            Assert.Equal(
                OdfKit.Compliance.OdfLocalizer.GetMessage("Err_OdfPackage_TransactionJournalFailed"),
                exception.Message);
            Assert.True(File.Exists(OdfTransactionJournal.GetJournalPath(path)));
        }
        finally
        {
            DeleteTransactionFiles(path);
        }
    }

    private static string CreatePackagePath() =>
        Path.Combine(Path.GetTempPath(), "odfkit-journal-" + Guid.NewGuid().ToString("N") + ".ods");

    private static void CreatePackage(string path)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        document.Save(path);
    }

    private static void DeleteTransactionFiles(string path)
    {
        TryDelete(OdfTransactionJournal.GetJournalPath(path));
        TryDelete(OdfTransactionJournal.GetCommittedPath(path));
        TryDelete(OdfTransactionJournal.GetLockPath(path));
        TryDelete(path);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }
}

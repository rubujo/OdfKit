using System;
using System.IO;

using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// 管理檔案型封裝的交易日誌、提交標記與崩潰恢復。
/// </summary>
internal static class OdfTransactionJournal
{
    private const string JournalSuffix = ".journal";
    private const string CommittedSuffix = ".journal.committed";
    private const string LockSuffix = ".journal.lock";

    /// <summary>
    /// 取得未提交交易日誌路徑。
    /// </summary>
    internal static string GetJournalPath(string filePath) => filePath + JournalSuffix;

    /// <summary>
    /// 取得已提交但尚未清理的交易日誌路徑。
    /// </summary>
    internal static string GetCommittedPath(string filePath) => filePath + CommittedSuffix;

    /// <summary>
    /// 取得交易協作鎖路徑。
    /// </summary>
    internal static string GetLockPath(string filePath) => filePath + LockSuffix;

    /// <summary>
    /// 取得檔案型交易的跨程序協作鎖。
    /// </summary>
    internal static FileStream AcquireLock(string filePath)
    {
        try
        {
            return new FileStream(
                GetLockPath(filePath),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
        }
        catch (Exception ex)
        {
            throw CreateJournalException(ex);
        }
    }

    /// <summary>
    /// 建立經耐久刷寫後才發布的未提交交易日誌。
    /// </summary>
    internal static void Prepare(string filePath)
    {
        string journalPath = GetJournalPath(filePath);
        string committedPath = GetCommittedPath(filePath);
        string temporaryPath = journalPath + ".tmp." + Guid.NewGuid().ToString("N");

        try
        {
            DeleteCommittedMarker(committedPath, throwOnFailure: true);
            if (File.Exists(journalPath) || Directory.Exists(journalPath))
                throw new IOException(OdfLocalizer.GetMessage("Err_OdfPackage_TransactionJournalFailed"));

            File.Copy(filePath, temporaryPath, overwrite: false);
            FlushFile(temporaryPath);
            File.Move(temporaryPath, journalPath);
        }
        catch (Exception ex)
        {
            throw CreateJournalException(ex);
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

    /// <summary>
    /// 將未提交日誌轉成不會觸發回滾的已提交標記。
    /// </summary>
    internal static void Commit(string filePath, Stream? underlyingStream)
    {
        string journalPath = GetJournalPath(filePath);
        if (!File.Exists(journalPath))
            throw CreateJournalException(new FileNotFoundException(null, journalPath));

        string committedPath = GetCommittedPath(filePath);
        try
        {
            FlushUnderlyingFile(filePath, underlyingStream);
            DeleteCommittedMarker(committedPath, throwOnFailure: true);
            File.Move(journalPath, committedPath);
        }
        catch (Exception ex)
        {
            throw CreateJournalException(ex);
        }

        DeleteCommittedMarker(committedPath, throwOnFailure: false);
    }

    /// <summary>
    /// 在開檔前以同卷暫存檔及原子取代恢復未提交交易。
    /// </summary>
    internal static void RecoverBeforeOpen(string filePath)
    {
        string journalPath = GetJournalPath(filePath);
        string committedPath = GetCommittedPath(filePath);
        if (!File.Exists(journalPath) &&
            !File.Exists(committedPath) &&
            !Directory.Exists(committedPath))
        {
            return;
        }

        FileStream? transactionLock = null;
        string temporaryPath = filePath + ".journal.restore." + Guid.NewGuid().ToString("N");
        try
        {
            transactionLock = AcquireLock(filePath);
            CleanupCommittedMarker(filePath);

            if (!File.Exists(journalPath))
                return;

            File.Copy(journalPath, temporaryPath, overwrite: false);
            FlushFile(temporaryPath);

            if (File.Exists(filePath))
                File.Replace(temporaryPath, filePath, destinationBackupFileName: null);
            else
                File.Move(temporaryPath, filePath);

            FlushFile(filePath);
            File.Delete(journalPath);
        }
        catch (Exception ex)
        {
            throw CreateJournalException(ex);
        }
        finally
        {
            TryDeleteFile(temporaryPath);
            if (transactionLock is not null)
            {
                transactionLock.Dispose();
                ReleaseLockFile(filePath);
            }
        }
    }

    /// <summary>
    /// 對已開啟的資料流恢復未提交交易。
    /// </summary>
    internal static void RecoverIntoOpenStream(
        string filePath,
        Stream underlyingStream,
        bool transactionLockAlreadyHeld = false,
        bool requireJournal = false)
    {
        string journalPath = GetJournalPath(filePath);
        string committedPath = GetCommittedPath(filePath);
        if (!File.Exists(journalPath) &&
            !File.Exists(committedPath) &&
            !Directory.Exists(committedPath))
        {
            if (requireJournal)
                throw CreateJournalException(new FileNotFoundException(null, journalPath));
            return;
        }

        FileStream? transactionLock = null;
        try
        {
            if (!transactionLockAlreadyHeld)
                transactionLock = AcquireLock(filePath);

            CleanupCommittedMarker(filePath);
            if (!File.Exists(journalPath))
            {
                if (requireJournal)
                    throw CreateJournalException(new FileNotFoundException(null, journalPath));
                return;
            }

            using (var journalStream = new FileStream(journalPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                underlyingStream.Position = 0;
                underlyingStream.SetLength(0);
                journalStream.CopyTo(underlyingStream);
            }
            underlyingStream.Flush();
            if (underlyingStream is FileStream fileStream)
                fileStream.Flush(flushToDisk: true);
            File.Delete(journalPath);
        }
        catch (Exception ex)
        {
            throw CreateJournalException(ex);
        }
        finally
        {
            if (transactionLock is not null)
            {
                transactionLock.Dispose();
                ReleaseLockFile(filePath);
            }
        }
    }

    /// <summary>
    /// 清理由成功提交留下且不應再觸發回滾的日誌。
    /// </summary>
    internal static void CleanupCommittedMarker(string filePath)
    {
        DeleteCommittedMarker(GetCommittedPath(filePath), throwOnFailure: false);
    }

    /// <summary>
    /// 釋放交易鎖後盡力移除 sidecar 檔案。
    /// </summary>
    internal static void ReleaseLockFile(string filePath)
    {
        TryDeleteFile(GetLockPath(filePath));
    }

    private static void FlushUnderlyingFile(string filePath, Stream? underlyingStream)
    {
        if (underlyingStream is FileStream fileStream)
        {
            fileStream.Flush(flushToDisk: true);
            return;
        }

        FlushFile(filePath);
    }

    private static void FlushFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        stream.Flush(flushToDisk: true);
    }

    private static void DeleteCommittedMarker(string committedPath, bool throwOnFailure)
    {
        try
        {
            if (Directory.Exists(committedPath))
                throw new IOException(OdfLocalizer.GetMessage("Err_OdfPackage_TransactionJournalFailed"));
            if (File.Exists(committedPath))
                File.Delete(committedPath);
        }
        catch (Exception ex)
        {
            if (throwOnFailure)
                throw CreateJournalException(ex);
            OdfKitDiagnostics.Warn($"[OdfTransactionJournal] 無法清理已提交交易日誌 '{committedPath}': {ex.Message}");
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            OdfKitDiagnostics.Warn($"[OdfTransactionJournal] 無法清理暫存檔案 '{path}': {ex.Message}");
        }
    }

    private static IOException CreateJournalException(Exception innerException) =>
        new(OdfLocalizer.GetMessage("Err_OdfPackage_TransactionJournalFailed"), innerException);
}

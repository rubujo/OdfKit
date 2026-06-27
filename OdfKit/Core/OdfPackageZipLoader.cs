using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using OdfKit.Compliance;
namespace OdfKit.Core;

/// <summary>
/// ODF ZIP 封裝專案載入器（內部協作者）。
/// </summary>
internal static class OdfPackageZipLoader
{
    internal static int LastMmfParallelPreloadEntryCountForTests;

    internal static int LastMmfParallelPreloadVisitedEntryCountForTests;

    internal static int LastMmfParallelPreloadMaxDegreeForTests;

    /// <summary>
    /// 自 ZIP 封存讀取所有專案至載入內容。
    /// </summary>
    internal static void LoadEntries(ZipArchive archive, OdfPackage.OdfPackageLoadCollaborators ctx)
    {
        OdfPackage package = ctx.Package;
        if (package.FilePath != null)
        {
            try
            {
                MemoryMappedFile mmf;
                if (ctx.UnderlyingStream is FileStream ufs)
                {
                    mmf = MemoryMappedFile.CreateFromFile(ufs, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
                }
                else
                {
                    mmf = MemoryMappedFile.CreateFromFile(package.FilePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
                }

                using (var fs = new FileStream(package.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var mmfEntries = OdfZipDirectoryParser.ParseCentralDirectory(fs);
                    if (mmfEntries != null)
                    {
                        package.Mmf = mmf;
                        package.MmfEntries = mmfEntries;
                        LoadEntriesFromMmf(ctx);
                        return;
                    }
                }
                mmf.Dispose();
            }
            catch (Exception ex)
            {
                OdfKitDiagnostics.Warn($"[OdfPackage] 無法使用 MMF 唯讀映射，將退回 BCL ZipArchive 讀取模式。原因: {ex.Message}");
            }
        }

        if (archive.Entries.Count > ctx.LoadOptions.MaxZipEntries)
        {
            throw new SecurityException(
                OdfLocalizer.GetMessage("Err_OdfPackage_ZipEntryCountLimitExceeded", archive.Entries.Count, ctx.LoadOptions.MaxZipEntries));
        }

        long totalUncompressedSize = 0;
        List<OdfPackageEntry> entriesToPreload = new();

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string name;
            try
            {
                name = OdfPackage.SanitizeEntryName(entry.FullName);
            }
            catch (SecurityException)
            {
                name = entry.FullName;
            }

            if (entry.Length > ctx.LoadOptions.MaxEntrySize)
            {
                throw new SecurityException(
                    OdfLocalizer.GetMessage("Err_OdfPackage_ZipEntrySizeLimitExceeded", name, entry.Length, ctx.LoadOptions.MaxEntrySize));
            }

            totalUncompressedSize += entry.Length;
            if (totalUncompressedSize > ctx.LoadOptions.MaxTotalUncompressedSize)
            {
                throw new SecurityException(
                    OdfLocalizer.GetMessage("Err_OdfPackage_ZipTotalUncompressedSizeLimitExceeded", totalUncompressedSize, ctx.LoadOptions.MaxTotalUncompressedSize));
            }

            OdfPackageEntry pkgEntry;
            if (ctx.LoadOptions.AllowLazyLoading)
            {
                pkgEntry = new OdfPackageEntry(name, entry);
                if (name == "content.xml" || name == "styles.xml" || name == "meta.xml" || name == "settings.xml")
                {
                    entriesToPreload.Add(pkgEntry);
                }
            }
            else
            {
                byte[] entryBytes;
                using (Stream entryStream = entry.Open())
                {
                    entryBytes = ReadEntryBytes(entryStream, entry.Length);
                }
                pkgEntry = new OdfPackageEntry(name, entryBytes);
            }

            bool wasStored = TryDetectStoredCompression(entry);
            pkgEntry.WasStoredInZip = wasStored;
            pkgEntry.IsCompressed = !wasStored;
            if (ctx.Entries.ContainsKey(name))
                ctx.DuplicateEntryNames.Add(name);

            ctx.Entries[name] = pkgEntry;
            if (!ctx.EntryOrder.Contains(name))
                ctx.EntryOrder.Add(name);
        }

        if (ctx.LoadOptions.AllowLazyLoading && entriesToPreload.Count > 0)
        {
            package.PreloadTask = Task.Run(() =>
            {
                lock (archive)
                {
                    foreach (OdfPackageEntry entry in entriesToPreload)
                    {
                        try
                        {
                            using Stream stream = entry.OpenReader();
                        }
                        catch
                        {
                            // 忽略預讀異常，待主線程存取時處理
                        }
                    }
                }
            });
        }
    }

    /// <summary>
    /// 非同步自 ZIP 封存讀取所有專案至載入內容，支援協作式取消。
    /// </summary>
    internal static async Task LoadEntriesAsync(
        ZipArchive archive,
        OdfPackage.OdfPackageLoadCollaborators ctx,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        OdfPackage package = ctx.Package;
        if (package.FilePath != null)
        {
            try
            {
                MemoryMappedFile mmf;
                if (ctx.UnderlyingStream is FileStream ufs)
                {
                    mmf = MemoryMappedFile.CreateFromFile(ufs, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
                }
                else
                {
                    mmf = MemoryMappedFile.CreateFromFile(package.FilePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
                }

                using (var fs = new FileStream(package.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true))
                {
                    var mmfEntries = OdfZipDirectoryParser.ParseCentralDirectory(fs);
                    if (mmfEntries != null)
                    {
                        package.Mmf = mmf;
                        package.MmfEntries = mmfEntries;
                        LoadEntriesFromMmf(ctx);
                        return;
                    }
                }
                mmf.Dispose();
            }
            catch (Exception ex)
            {
                OdfKitDiagnostics.Warn($"[OdfPackage] 非同步作業無法使用 MMF 唯讀映射，將退回 BCL ZipArchive 讀取模式。原因: {ex.Message}");
            }
        }

        if (archive.Entries.Count > ctx.LoadOptions.MaxZipEntries)
        {
            throw new SecurityException(
                OdfLocalizer.GetMessage("Err_OdfPackage_ZipEntryCountLimitExceeded", archive.Entries.Count, ctx.LoadOptions.MaxZipEntries));
        }

        long totalUncompressedSize = 0;
        List<OdfPackageEntry> entriesToPreload = new();

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string name;
            try
            {
                name = OdfPackage.SanitizeEntryName(entry.FullName);
            }
            catch (SecurityException)
            {
                name = entry.FullName;
            }

            if (entry.Length > ctx.LoadOptions.MaxEntrySize)
            {
                throw new SecurityException(
                    OdfLocalizer.GetMessage("Err_OdfPackage_ZipEntrySizeLimitExceeded", name, entry.Length, ctx.LoadOptions.MaxEntrySize));
            }

            totalUncompressedSize += entry.Length;
            if (totalUncompressedSize > ctx.LoadOptions.MaxTotalUncompressedSize)
            {
                throw new SecurityException(
                    OdfLocalizer.GetMessage("Err_OdfPackage_ZipTotalUncompressedSizeLimitExceeded", totalUncompressedSize, ctx.LoadOptions.MaxTotalUncompressedSize));
            }

            OdfPackageEntry pkgEntry;
            if (ctx.LoadOptions.AllowLazyLoading)
            {
                pkgEntry = new OdfPackageEntry(name, entry);
                if (name == "content.xml" || name == "styles.xml" || name == "meta.xml" || name == "settings.xml")
                {
                    entriesToPreload.Add(pkgEntry);
                }
            }
            else
            {
                byte[] entryBytes;
                using (Stream entryStream = entry.Open())
                {
                    entryBytes = await ReadEntryBytesAsync(entryStream, entry.Length, cancellationToken).ConfigureAwait(false);
                }
                pkgEntry = new OdfPackageEntry(name, entryBytes);
            }

            bool wasStored = TryDetectStoredCompression(entry);
            pkgEntry.WasStoredInZip = wasStored;
            pkgEntry.IsCompressed = !wasStored;
            if (ctx.Entries.ContainsKey(name))
                ctx.DuplicateEntryNames.Add(name);

            ctx.Entries[name] = pkgEntry;
            if (!ctx.EntryOrder.Contains(name))
                ctx.EntryOrder.Add(name);
        }

        if (ctx.LoadOptions.AllowLazyLoading && entriesToPreload.Count > 0)
        {
            package.PreloadTask = Task.Run(() =>
            {
                lock (archive)
                {
                    foreach (OdfPackageEntry entry in entriesToPreload)
                    {
                        try
                        {
                            using Stream stream = entry.OpenReader();
                        }
                        catch
                        {
                        }
                    }
                }
            });
        }
    }

    /// <summary>
    /// 確保底層串流可搜尋，供 ZipArchive 讀取中央目錄。
    /// </summary>
    internal static void EnsureSeekableStream(
        OdfPackage.OdfPackageLoadCollaborators ctx,
        byte[] signature,
        int bytesRead)
    {
        Stream? underlying = ctx.UnderlyingStream;
        if (underlying is null || underlying.CanSeek)
            return;

        var ms = new MemoryStream();
        ms.Write(signature, 0, bytesRead);
        underlying.CopyTo(ms);
        ms.Position = 0;
        if (!ctx.LeaveOpen)
            underlying.Dispose();

        ctx.UnderlyingStream = ms;
    }

    /// <summary>
    /// 非同步確保底層串流可搜尋，供 ZipArchive 讀取中央目錄。
    /// </summary>
    internal static async Task EnsureSeekableStreamAsync(
        OdfPackage.OdfPackageLoadCollaborators ctx,
        byte[] signature,
        int bytesRead,
        CancellationToken cancellationToken = default)
    {
        Stream? underlying = ctx.UnderlyingStream;
        if (underlying is null || underlying.CanSeek)
            return;

        var ms = new MemoryStream();
        ms.Write(signature, 0, bytesRead);
        await underlying.CopyToAsync(ms, 81920, cancellationToken).ConfigureAwait(false);
        ms.Position = 0;
        if (!ctx.LeaveOpen)
            underlying.Dispose();

        ctx.UnderlyingStream = ms;
    }

    /// <summary>
    /// 註冊 ZIP 檔名編碼（.NET Standard 2.0）。
    /// </summary>
    internal static void RegisterCodePagesIfNeeded()
    {
#if NETSTANDARD2_0
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        catch (Exception ex)
        {
            OdfKitDiagnostics.Warn($"註冊 ZIP 檔名編碼提供者失敗，將使用預設編碼：{ex.Message}", ex);
        }
#endif
    }

    private const int ReadBufferSize = 81920;

    private static byte[] ReadEntryBytes(Stream entryStream, long entryLength)
    {
        if (entryLength <= 0)
        {
            return ReadEntryBytesGrowable(entryStream, ReadEntryBytesCore);
        }

        if (entryLength > int.MaxValue)
        {
            throw new SecurityException(OdfLocalizer.GetMessage("Err_OdfPackageZipLoader_ZipEntrySizeExceeds_2", entryLength));
        }

        return ReadEntryBytesWithPool(
            entryStream,
            (int)entryLength,
            static (stream, buffer, offset, count) => stream.Read(buffer, offset, count));
    }

    private static async Task<byte[]> ReadEntryBytesAsync(
        Stream entryStream,
        long entryLength,
        CancellationToken cancellationToken)
    {
        if (entryLength <= 0)
        {
            return await ReadEntryBytesGrowableAsync(entryStream, cancellationToken).ConfigureAwait(false);
        }

        if (entryLength > int.MaxValue)
        {
            throw new SecurityException(OdfLocalizer.GetMessage("Err_OdfPackageZipLoader_ZipEntrySizeExceeds_2", entryLength));
        }

        return await ReadEntryBytesWithPoolAsync(
            entryStream,
            (int)entryLength,
            cancellationToken).ConfigureAwait(false);
    }

    private static byte[] ReadEntryBytesWithPool(
        Stream entryStream,
        int capacity,
        Func<Stream, byte[], int, int, int> read)
    {
        byte[] rented = ArrayPool<byte>.Shared.Rent(capacity);
        try
        {
            int offset = 0;
            while (offset < capacity)
            {
                int bytesRead = read(entryStream, rented, offset, capacity - offset);
                if (bytesRead == 0)
                {
                    break;
                }

                offset += bytesRead;
            }

            return CopyToOwnedArray(rented, offset);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static async Task<byte[]> ReadEntryBytesWithPoolAsync(
        Stream entryStream,
        int capacity,
        CancellationToken cancellationToken)
    {
        byte[] rented = ArrayPool<byte>.Shared.Rent(capacity);
        try
        {
            int offset = 0;
            while (offset < capacity)
            {
                int bytesRead = await entryStream
                    .ReadAsync(rented, offset, capacity - offset, cancellationToken)
                    .ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                offset += bytesRead;
            }

            return CopyToOwnedArray(rented, offset);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static byte[] ReadEntryBytesGrowable(
        Stream entryStream,
        Func<Stream, byte[], int, int, int> read)
    {
        byte[] rented = ArrayPool<byte>.Shared.Rent(ReadBufferSize);
        try
        {
            int count = 0;
            int rentedLength = rented.Length;
            while (true)
            {
                if (count == rentedLength)
                {
                    byte[] larger = ArrayPool<byte>.Shared.Rent(rentedLength * 2);
                    Buffer.BlockCopy(rented, 0, larger, 0, count);
                    ArrayPool<byte>.Shared.Return(rented);
                    rented = larger;
                    rentedLength = larger.Length;
                }

                int bytesRead = read(entryStream, rented, count, rentedLength - count);
                if (bytesRead == 0)
                {
                    break;
                }

                count += bytesRead;
            }

            return CopyToOwnedArray(rented, count);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static async Task<byte[]> ReadEntryBytesGrowableAsync(Stream entryStream, CancellationToken cancellationToken)
    {
        byte[] rented = ArrayPool<byte>.Shared.Rent(ReadBufferSize);
        try
        {
            int count = 0;
            int rentedLength = rented.Length;
            while (true)
            {
                if (count == rentedLength)
                {
                    byte[] larger = ArrayPool<byte>.Shared.Rent(rentedLength * 2);
                    Buffer.BlockCopy(rented, 0, larger, 0, count);
                    ArrayPool<byte>.Shared.Return(rented);
                    rented = larger;
                    rentedLength = larger.Length;
                }

                int bytesRead = await entryStream
                    .ReadAsync(rented, count, rentedLength - count, cancellationToken)
                    .ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                count += bytesRead;
            }

            return CopyToOwnedArray(rented, count);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static int ReadEntryBytesCore(Stream stream, byte[] buffer, int offset, int count) =>
        stream.Read(buffer, offset, count);

    private static byte[] CopyToOwnedArray(byte[] source, int length)
    {
        if (length == 0)
        {
            return [];
        }

        var owned = new byte[length];
        Buffer.BlockCopy(source, 0, owned, 0, length);
        return owned;
    }

    private static bool TryDetectStoredCompression(ZipArchiveEntry entry)
    {
        try
        {
            var fieldInfo = typeof(ZipArchiveEntry).GetField(
                    "_compressionMethod",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                ?? typeof(ZipArchiveEntry).GetField(
                    "m_compressionMethod",
                    BindingFlags.NonPublic | BindingFlags.Instance);

            if (fieldInfo is null)
            {
                OdfKitDiagnostics.Warn(
                    $"[OdfPackage] 無法反射取得 ZipArchiveEntry 壓縮方式欄位 ( .NET {Environment.Version} )；讀取時將以 CompressedLength == Length 作為判斷基準。");
                return entry.CompressedLength == entry.Length;
            }

            object? val = fieldInfo.GetValue(entry);
            if (val is null)
                return entry.CompressedLength == entry.Length;

            int intVal = Convert.ToInt32(val);
            return intVal == 0;
        }
        catch (Exception ex)
        {
            OdfKitDiagnostics.Warn(
                $"反射讀取 ZipArchiveEntry 壓縮方式失敗，改用 CompressedLength == Length 判斷：{ex.Message}",
                ex);
            return entry.CompressedLength == entry.Length;
        }
    }

    private static void LoadEntriesFromMmf(OdfPackage.OdfPackageLoadCollaborators ctx)
    {
        OdfPackage package = ctx.Package;
        var mmfEntries = package.MmfEntries!;

        long totalUncompressedSize = 0;
        List<OdfPackageEntry> entriesToPreload = new();

        foreach (var mmfEntry in mmfEntries.Values)
        {
            string name = mmfEntry.Name;
            if (mmfEntry.UncompressedSize > ctx.LoadOptions.MaxEntrySize)
            {
                throw new SecurityException(
                    OdfLocalizer.GetMessage("Err_OdfPackage_ZipEntrySizeLimitExceeded", name, mmfEntry.UncompressedSize, ctx.LoadOptions.MaxEntrySize));
            }

            totalUncompressedSize += mmfEntry.UncompressedSize;
            if (totalUncompressedSize > ctx.LoadOptions.MaxTotalUncompressedSize)
            {
                throw new SecurityException(
                    OdfLocalizer.GetMessage("Err_OdfPackage_ZipTotalUncompressedSizeLimitExceeded", totalUncompressedSize, ctx.LoadOptions.MaxTotalUncompressedSize));
            }

            OdfPackageEntry pkgEntry;
            if (ctx.LoadOptions.AllowLazyLoading)
            {
                pkgEntry = new OdfPackageEntry(name, mmfEntry, package);
                if (name == "content.xml" || name == "styles.xml" || name == "meta.xml" || name == "settings.xml")
                {
                    entriesToPreload.Add(pkgEntry);
                }
            }
            else
            {
                byte[] entryBytes;
                using (Stream entryStream = mmfEntry.OpenStream(package.Mmf!))
                {
                    entryBytes = ReadEntryBytes(entryStream, mmfEntry.UncompressedSize);
                }
                pkgEntry = new OdfPackageEntry(name, entryBytes);
            }

            bool wasStored = mmfEntry.CompressionMethod == 0;
            pkgEntry.WasStoredInZip = wasStored;
            pkgEntry.IsCompressed = !wasStored;
            if (ctx.Entries.ContainsKey(name))
                ctx.DuplicateEntryNames.Add(name);

            ctx.Entries[name] = pkgEntry;
            if (!ctx.EntryOrder.Contains(name))
                ctx.EntryOrder.Add(name);
        }

        if (ctx.LoadOptions.AllowLazyLoading && entriesToPreload.Count > 0)
        {
            LastMmfParallelPreloadEntryCountForTests = entriesToPreload.Count;
            LastMmfParallelPreloadVisitedEntryCountForTests = 0;
            ParallelOptions preloadOptions = CreatePreloadParallelOptions();
            LastMmfParallelPreloadMaxDegreeForTests = preloadOptions.MaxDegreeOfParallelism;

            package.PreloadTask = Task.Run(() =>
            {
                Parallel.ForEach(entriesToPreload, preloadOptions, entry =>
                {
                    OdfParallelScheduler.RunWithConfiguredThreadPriority(() =>
                    {
                        try
                        {
                            using Stream stream = entry.OpenReader();
                            Interlocked.Increment(ref LastMmfParallelPreloadVisitedEntryCountForTests);
                        }
                        catch
                        {
                            // 忽略預讀異常，待主線程存取時處理
                        }
                    });
                });
            });
        }
    }

    internal static ParallelOptions CreatePreloadParallelOptions()
        => new()
        {
            MaxDegreeOfParallelism = OdfParallelScheduler.GetEffectiveConcurrency()
        };
}

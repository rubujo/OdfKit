using System;
using System.Buffers;
using System.Buffers.Binary;
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
/// ODF ZIP 封裝項目載入器（內部協作者）。
/// </summary>
internal static class OdfPackageZipLoader
{
    private static readonly AsyncLocal<Func<int, Exception?>?> MmfLoadFailureInjectorForTests = new();

    internal static Func<int, Exception?>? MmfLoadFailureInjectorForTestContext
    {
        get => MmfLoadFailureInjectorForTests.Value;
        set => MmfLoadFailureInjectorForTests.Value = value;
    }

    /// <summary>
    /// 自 ZIP 封存讀取所有專案至載入內容。
    /// </summary>
    internal static void LoadEntries(ZipArchive archive, OdfPackage.OdfPackageLoadCollaborators ctx)
    {
        OdfPackage package = ctx.Package;
        if (package.FilePath != null)
        {
            MemoryMappedFile? mmf = null;
            bool handedOffToPackage = false;
            try
            {
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
                    var mmfDirectory = OdfZipDirectoryParser.ParseCentralDirectory(fs);
                    if (mmfDirectory != null)
                    {
                        if (mmfDirectory.EntryCount > ctx.LoadOptions.MaxZipEntries)
                        {
                            throw new SecurityException(
                                OdfLocalizer.GetMessage("Err_OdfPackage_ZipEntryCountLimitExceeded", mmfDirectory.EntryCount, ctx.LoadOptions.MaxZipEntries));
                        }

                        package.Mmf = mmf;
                        package.MmfEntries = mmfDirectory.Entries;
                        handedOffToPackage = true;
                        ctx.DuplicateEntryNames.AddRange(mmfDirectory.DuplicateEntryNames);
                        LoadEntriesFromMmf(ctx);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                if (handedOffToPackage)
                {
                    ResetMmfLoadState(package, ctx);
                }
                OdfKitDiagnostics.Warn($"[OdfPackage] 無法使用 MMF 唯讀映射，將退回 BCL ZipArchive 讀取模式。原因: {ex.Message}");
            }
            finally
            {
                if (!handedOffToPackage)
                    mmf?.Dispose();
            }
        }

        if (archive.Entries.Count > ctx.LoadOptions.MaxZipEntries)
        {
            throw new SecurityException(
                OdfLocalizer.GetMessage("Err_OdfPackage_ZipEntryCountLimitExceeded", archive.Entries.Count, ctx.LoadOptions.MaxZipEntries));
        }

        var mimeTypeHeader = ReadFirstMimeTypeHeader(ctx.UnderlyingStream);
        long totalUncompressedSize = 0;
        List<OdfPackageEntry> entriesToPreload = new();
        HashSet<string> entryOrderSet = new(ctx.EntryOrder, StringComparer.Ordinal);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string name = OdfPackage.SanitizeEntryName(entry.FullName);

            if (entry.Length > ctx.LoadOptions.MaxEntrySize)
            {
                throw new SecurityException(
                    OdfLocalizer.GetMessage("Err_OdfPackage_ZipEntrySizeLimitExceeded", name, entry.Length, ctx.LoadOptions.MaxEntrySize));
            }

            totalUncompressedSize = OdfBoundedStreamReader.AddBytes(
                totalUncompressedSize,
                entry.Length,
                ctx.LoadOptions.MaxTotalUncompressedSize,
                "Err_OdfPackage_ZipTotalUncompressedSizeLimitExceeded");

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

            bool wasStored = TryDetectStoredCompression(entry, out ushort? compressionMethod);
            pkgEntry.ZipCompressionMethod = compressionMethod;
            pkgEntry.ZipCompressionMetadataMissing = compressionMethod is null;
            pkgEntry.WasStoredInZip = wasStored;
            if (name == "mimetype")
            {
                pkgEntry.LocalHeaderExtraFieldLength = ReadMimeTypeExtraFieldLength(ctx.UnderlyingStream, entry, mimeTypeHeader.ExtraLength);
                pkgEntry.WasFirstLocalEntryInZip = mimeTypeHeader.IsFirst;
            }
            pkgEntry.IsCompressed = !wasStored;
            if (ctx.Entries.ContainsKey(name))
                ctx.DuplicateEntryNames.Add(name);

            ctx.Entries[name] = pkgEntry;
            if (entryOrderSet.Add(name))
                ctx.EntryOrder.Add(name);
        }

        if (ctx.LoadOptions.AllowLazyLoading && entriesToPreload.Count > 0)
        {
            package.PreloadTask = Task.Run(() =>
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
            }, CancellationToken.None);
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
            MemoryMappedFile? mmf = null;
            bool handedOffToPackage = false;
            try
            {
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
                    var mmfDirectory = OdfZipDirectoryParser.ParseCentralDirectory(fs);
                    if (mmfDirectory != null)
                    {
                        if (mmfDirectory.EntryCount > ctx.LoadOptions.MaxZipEntries)
                        {
                            throw new SecurityException(
                                OdfLocalizer.GetMessage("Err_OdfPackage_ZipEntryCountLimitExceeded", mmfDirectory.EntryCount, ctx.LoadOptions.MaxZipEntries));
                        }

                        package.Mmf = mmf;
                        package.MmfEntries = mmfDirectory.Entries;
                        handedOffToPackage = true;
                        ctx.DuplicateEntryNames.AddRange(mmfDirectory.DuplicateEntryNames);
                        LoadEntriesFromMmf(ctx);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                if (handedOffToPackage)
                {
                    ResetMmfLoadState(package, ctx);
                }
                OdfKitDiagnostics.Warn($"[OdfPackage] 非同步作業無法使用 MMF 唯讀映射，將退回 BCL ZipArchive 讀取模式。原因: {ex.Message}");
            }
            finally
            {
                if (!handedOffToPackage)
                    mmf?.Dispose();
            }
        }

        if (archive.Entries.Count > ctx.LoadOptions.MaxZipEntries)
        {
            throw new SecurityException(
                OdfLocalizer.GetMessage("Err_OdfPackage_ZipEntryCountLimitExceeded", archive.Entries.Count, ctx.LoadOptions.MaxZipEntries));
        }

        var mimeTypeHeader = ReadFirstMimeTypeHeader(ctx.UnderlyingStream);
        long totalUncompressedSize = 0;
        List<OdfPackageEntry> entriesToPreload = new();
        HashSet<string> entryOrderSet = new(ctx.EntryOrder, StringComparer.Ordinal);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string name = OdfPackage.SanitizeEntryName(entry.FullName);

            if (entry.Length > ctx.LoadOptions.MaxEntrySize)
            {
                throw new SecurityException(
                    OdfLocalizer.GetMessage("Err_OdfPackage_ZipEntrySizeLimitExceeded", name, entry.Length, ctx.LoadOptions.MaxEntrySize));
            }

            totalUncompressedSize = OdfBoundedStreamReader.AddBytes(
                totalUncompressedSize,
                entry.Length,
                ctx.LoadOptions.MaxTotalUncompressedSize,
                "Err_OdfPackage_ZipTotalUncompressedSizeLimitExceeded");

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

            bool wasStored = TryDetectStoredCompression(entry, out ushort? compressionMethod);
            pkgEntry.ZipCompressionMethod = compressionMethod;
            pkgEntry.ZipCompressionMetadataMissing = compressionMethod is null;
            pkgEntry.WasStoredInZip = wasStored;
            if (name == "mimetype")
            {
                pkgEntry.LocalHeaderExtraFieldLength = ReadMimeTypeExtraFieldLength(ctx.UnderlyingStream, entry, mimeTypeHeader.ExtraLength);
                pkgEntry.WasFirstLocalEntryInZip = mimeTypeHeader.IsFirst;
            }
            pkgEntry.IsCompressed = !wasStored;
            if (ctx.Entries.ContainsKey(name))
                ctx.DuplicateEntryNames.Add(name);

            ctx.Entries[name] = pkgEntry;
            if (entryOrderSet.Add(name))
                ctx.EntryOrder.Add(name);
        }

        if (ctx.LoadOptions.AllowLazyLoading && entriesToPreload.Count > 0)
        {
            package.PreloadTask = Task.Run(() =>
            {
                foreach (OdfPackageEntry entry in entriesToPreload)
                {
                    using Stream stream = entry.OpenReader();
                }
            }, cancellationToken);
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
        OdfBoundedStreamReader.CopyTo(
            underlying,
            ms,
            ctx.LoadOptions.MaxPackageSize,
            "Err_OdfPackage_InputStreamSizeLimitExceeded",
            bytesRead);
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
        await OdfBoundedStreamReader.CopyToAsync(
            underlying,
            ms,
            ctx.LoadOptions.MaxPackageSize,
            "Err_OdfPackage_InputStreamSizeLimitExceeded",
            bytesRead,
            cancellationToken).ConfigureAwait(false);
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
                int bytesRead = await global::OdfKit.Internal.OdfStreamHelper.ReadAsync(entryStream, rented, offset, capacity - offset, cancellationToken)
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

                int bytesRead = await global::OdfKit.Internal.OdfStreamHelper.ReadAsync(entryStream, rented, count, rentedLength - count, cancellationToken)
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

    private static (bool? IsFirst, ushort? ExtraLength) ReadFirstMimeTypeHeader(Stream? stream)
    {
        if (stream is null || !stream.CanSeek || stream.Length < 38)
            return (null, null);

        long position = stream.Position;
        try
        {
            // 只讀取固定長度標頭；不依據不可信長度配置記憶體或掃描封裝內容。
            stream.Position = 0;
            byte[] header = new byte[38];
            int count = 0;
            while (count < header.Length)
            {
                int read = stream.Read(header, count, header.Length - count);
                if (read == 0)
                    return (null, null);
                count += read;
            }
            if (BinaryPrimitives.ReadUInt32LittleEndian(header) != 0x04034b50 ||
                BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(26)) != 8 ||
                !header.AsSpan(30).SequenceEqual("mimetype"u8))
                return (false, null);

            return (true, BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(28)));
        }
        finally
        {
            stream.Position = position;
        }
    }

    private static ushort? ReadMimeTypeExtraFieldLength(Stream? stream, ZipArchiveEntry entry, ushort? knownLength)
    {
        if (knownLength.HasValue || stream is null || !stream.CanSeek)
            return knownLength;

        long position = stream.Position;
        try
        {
            if (LocalHeaderOffsetField?.GetValue(entry) is not long offset || offset < 0 || offset > stream.Length - 30)
                return null;
            stream.Position = offset;
            byte[] header = new byte[30];
            int count = 0;
            while (count < header.Length)
            {
                int read = stream.Read(header, count, header.Length - count);
                if (read == 0)
                    return null;
                count += read;
            }
            return BinaryPrimitives.ReadUInt32LittleEndian(header) == 0x04034b50
                ? BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(28)) : null;
        }
        catch (Exception ex)
        {
            OdfKitDiagnostics.Warn("[OdfPackage] 無法讀取 mimetype local header 的 extra field 長度。", ex);
            return null;
        }
        finally
        {
            stream.Position = position;
        }
    }

    private static void ResetMmfLoadState(OdfPackage package, OdfPackage.OdfPackageLoadCollaborators ctx)
    {
        // MMF handoff 發生在任何高階 XML／manifest 物件化之前；失敗時只需回復 entry 註冊與
        // MMF 關聯狀態。先釋放已註冊 entry，可與其他清理路徑維持相同的 view／stream 釋放慣例。
        foreach (OdfPackageEntry existing in ctx.Entries.Values)
        {
            existing.Dispose();
        }

        package.Mmf?.Dispose();
        package.Mmf = null;
        package.MmfEntries = null;
        package.PreloadTask = null;
        ctx.Entries.Clear();
        ctx.EntryOrder.Clear();
        ctx.DuplicateEntryNames.Clear();
    }

    // 靜態快取 BCL 私有欄位的反射結果：只在型別初始化時探測一次，
    // 之後每個 entry 直接讀取；欄位在未來 .NET 版本消失或反射受限（如 AOT）時
    // 整體改採至長度比對啟發式，且不得讓探測例外升級為 TypeInitializationException。
    private static readonly FieldInfo? CompressionMethodField = ProbeEntryField("_storedCompressionMethod", "_compressionMethod", "m_compressionMethod");
    private static readonly FieldInfo? LocalHeaderOffsetField = ProbeEntryField("_offsetOfLocalHeader", "m_offsetOfLocalHeader");

    private static FieldInfo? ProbeEntryField(params string[] names)
    {
        try
        {
            foreach (string name in names)
            {
                FieldInfo? field = typeof(ZipArchiveEntry).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                if (field is not null)
                    return field;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryDetectStoredCompression(ZipArchiveEntry entry, out ushort? compressionMethod)
    {
        compressionMethod = null;
        try
        {
            FieldInfo? fieldInfo = CompressionMethodField;
            if (fieldInfo is null)
            {
                OdfKitDiagnostics.Warn(
                    $"[OdfPackage] 無法反射取得 ZipArchiveEntry 壓縮方式欄位 ( .NET {Environment.Version} )；讀取時將以 CompressedLength == Length 作為判斷基準。");
                return entry.CompressedLength == entry.Length;
            }

            object? val = fieldInfo.GetValue(entry);
            if (val is null)
                return entry.CompressedLength == entry.Length;

            compressionMethod = checked((ushort)Convert.ToInt32(val, System.Globalization.CultureInfo.InvariantCulture));
            return compressionMethod == 0;
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
        int loadedEntryCount = 0;
        List<OdfPackageEntry> entriesToPreload = new();
        HashSet<string> entryOrderSet = new(ctx.EntryOrder, StringComparer.Ordinal);

        foreach (var mmfEntry in mmfEntries.Values)
        {
            string name = mmfEntry.Name;
            if (mmfEntry.UncompressedSize > ctx.LoadOptions.MaxEntrySize)
            {
                throw new SecurityException(
                    OdfLocalizer.GetMessage("Err_OdfPackage_ZipEntrySizeLimitExceeded", name, mmfEntry.UncompressedSize, ctx.LoadOptions.MaxEntrySize));
            }

            totalUncompressedSize = OdfBoundedStreamReader.AddBytes(
                totalUncompressedSize,
                mmfEntry.UncompressedSize,
                ctx.LoadOptions.MaxTotalUncompressedSize,
                "Err_OdfPackage_ZipTotalUncompressedSizeLimitExceeded");

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
            pkgEntry.ZipCompressionMethod = mmfEntry.CompressionMethod;
            pkgEntry.LocalHeaderExtraFieldLength = mmfEntry.LocalHeaderExtraFieldLength;
            pkgEntry.WasFirstLocalEntryInZip = mmfEntry.LocalHeaderOffset == 0;
            pkgEntry.WasStoredInZip = wasStored;
            pkgEntry.IsCompressed = !wasStored;
            if (ctx.Entries.ContainsKey(name))
                ctx.DuplicateEntryNames.Add(name);

            ctx.Entries[name] = pkgEntry;
            if (entryOrderSet.Add(name))
                ctx.EntryOrder.Add(name);

            loadedEntryCount++;
            Exception? injectedFailure = MmfLoadFailureInjectorForTestContext?.Invoke(loadedEntryCount);
            if (injectedFailure is not null)
            {
                throw injectedFailure;
            }
        }

        if (ctx.LoadOptions.AllowLazyLoading && entriesToPreload.Count > 0)
        {
            package.LastMmfParallelPreloadEntryCountForTests = entriesToPreload.Count;
            int[] visitedHolder = [0];
            package.LastMmfParallelPreloadVisitedCountHolderForTests = visitedHolder;
            ParallelOptions preloadOptions = CreatePreloadParallelOptions();
            package.LastMmfParallelPreloadMaxDegreeForTests = preloadOptions.MaxDegreeOfParallelism;

            package.PreloadTask = Task.Run(() =>
            {
                Parallel.ForEach(entriesToPreload, preloadOptions, entry =>
                {
                    OdfParallelScheduler.RunWithConfiguredThreadPriority(() =>
                    {
                        try
                        {
                            using Stream stream = entry.OpenReader();
                            Interlocked.Increment(ref visitedHolder[0]);
                        }
                        catch
                        {
                            // 忽略預讀異常，待主線程存取時處理
                        }
                    });
                });
            }, CancellationToken.None);
        }
    }

    internal static ParallelOptions CreatePreloadParallelOptions()
        => new()
        {
            MaxDegreeOfParallelism = OdfParallelScheduler.GetEffectiveConcurrency()
        };
}

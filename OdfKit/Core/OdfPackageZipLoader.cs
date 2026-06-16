using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace OdfKit.Core;

/// <summary>
/// ODF ZIP 封裝項目載入器（內部協作者）。
/// </summary>
internal static class OdfPackageZipLoader
{
    /// <summary>
    /// 自 ZIP 封存讀取所有項目至載入內容。
    /// </summary>
    internal static void LoadEntries(ZipArchive archive, OdfPackage.OdfPackageLoadCollaborators ctx)
    {
        if (archive.Entries.Count > ctx.LoadOptions.MaxZipEntries)
        {
            throw new SecurityException(
                $"Zip archive contains too many entries ({archive.Entries.Count} > {ctx.LoadOptions.MaxZipEntries}). Potential Zip DoS attack.");
        }

        long totalUncompressedSize = 0;

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
                    $"Zip entry '{name}' exceeds size limit ({entry.Length} > {ctx.LoadOptions.MaxEntrySize} bytes).");
            }

            totalUncompressedSize += entry.Length;
            if (totalUncompressedSize > ctx.LoadOptions.MaxTotalUncompressedSize)
            {
                throw new SecurityException(
                    $"Zip archive total uncompressed size exceeds limit ({totalUncompressedSize} > {ctx.LoadOptions.MaxTotalUncompressedSize} bytes).");
            }

            byte[] entryBytes;
            using (Stream entryStream = entry.Open())
            using (var ms = new MemoryStream())
            {
                entryStream.CopyTo(ms);
                entryBytes = ms.ToArray();
            }

            var pkgEntry = new OdfPackageEntry(name, entryBytes);
            if (entry.CompressedLength == entry.Length && entry.Length > 0)
                pkgEntry.IsCompressed = false;

            pkgEntry.WasStoredInZip = TryDetectStoredCompression(entry);
            if (ctx.Entries.ContainsKey(name))
                ctx.DuplicateEntryNames.Add(name);

            ctx.Entries[name] = pkgEntry;
            if (!ctx.EntryOrder.Contains(name))
                ctx.EntryOrder.Add(name);
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
    /// 註冊 ZIP 檔名編碼（.NET Standard 2.0）。
    /// </summary>
    internal static void RegisterCodePagesIfNeeded()
    {
#if NETSTANDARD2_0
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        catch
        {
            // 若平台不支援或缺少參考則靜默略過
        }
#endif
    }

    private static bool TryDetectStoredCompression(ZipArchiveEntry entry)
    {
        try
        {
            var fieldInfo = typeof(ZipArchiveEntry).GetField(
                    "_compressionMethod",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?? typeof(ZipArchiveEntry).GetField(
                    "m_compressionMethod",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

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
        catch
        {
            return entry.CompressedLength == entry.Length;
        }
    }
}

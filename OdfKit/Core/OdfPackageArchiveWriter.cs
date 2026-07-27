using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Core;

/// <summary>
/// ODF 封裝 ZIP 與 Flat XML 寫入引擎（內部協作者）。
/// </summary>
internal static partial class OdfPackageArchiveWriter
{
    private static int _lastFastPathCancellationCheckCount;

    /// <summary>
    /// 將封裝項目寫入目標串流（ZIP 或 Flat XML）。
    /// </summary>
    internal static void WriteToArchive(OdfPackage.OdfPackageSaveCollaborators ctx, Stream targetStream)
    {
        if (ctx.IsFlatXml)
        {
            WriteFlatXmlToStream(ctx, targetStream);
            return;
        }

        if (TryWriteRawCopyArchive(ctx, targetStream))
        {
            return;
        }

        if (TryWriteParallelPreparedArchive(ctx, targetStream))
        {
            return;
        }

        // 對所有 entries 啟動背景 DMA 預載
        foreach (OdfPackageEntry entry in ctx.Entries.Values)
        {
            entry.Prefetch();
        }

        using var zip = new ZipArchive(targetStream, ZipArchiveMode.Create, true, Encoding.UTF8);

        if (ctx.Entries.TryGetValue("mimetype", out OdfPackageEntry? mimeEntry))
        {
            ZipArchiveEntry zipEntry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);

            if (ctx.SaveOptions.Deterministic)
                zipEntry.LastWriteTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

            using (Stream entryStream = zipEntry.Open())
            {
                byte[] arr = GetFallbackEntryBytes(mimeEntry);
                WriteEntryContentWithCrc32(entryStream, arr);
            }
        }

        foreach (KeyValuePair<string, OdfPackageEntry> kvp in ctx.Entries)
        {
            if (kvp.Key == "mimetype")
                continue;

            CompressionLevel compLevel = kvp.Value.IsCompressed
                ? ctx.SaveOptions.CompressionLevel
                : CompressionLevel.NoCompression;
            ZipArchiveEntry zipEntry = zip.CreateEntry(kvp.Key, compLevel);

            if (ctx.SaveOptions.Deterministic)
                zipEntry.LastWriteTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

            using (Stream entryStream = zipEntry.Open())
            {
                byte[] arr = GetFallbackEntryBytes(kvp.Value);
                WriteEntryContentWithCrc32(entryStream, arr);
            }
        }
    }

    internal static int LastParallelPreparedEntryCount { get; set; }

    internal static bool LastParallelCompressionUsedPooledBuffer { get; set; }

    internal static bool LastRawCopyArchiveUsed { get; set; }

    internal static bool LastAsyncFastPathArchiveUsed { get; set; }

    internal static int LastFallbackCrcWriteCount { get; set; }

    internal static int LastFallbackEntryByteCloneCount { get; set; }

    internal static int LastFastPathCancellationCheckCount
    {
        get => _lastFastPathCancellationCheckCount;
        set => _lastFastPathCancellationCheckCount = value;
    }

    /// <summary>
    /// 將封裝項目非同步寫入目標串流（ZIP 或 Flat XML），支援協作式取消。
    /// </summary>
    internal static async Task WriteToArchiveAsync(
        OdfPackage.OdfPackageSaveCollaborators ctx,
        Stream targetStream,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ctx.IsFlatXml)
        {
            await WriteFlatXmlToStreamAsync(ctx, targetStream, cancellationToken).ConfigureAwait(false);
            return;
        }

        LastAsyncFastPathArchiveUsed = false;
        LastFastPathCancellationCheckCount = 0;
        CheckCancellation(cancellationToken);
        if (TryWriteRawCopyArchive(ctx, targetStream, cancellationToken))
        {
            LastAsyncFastPathArchiveUsed = true;
            CheckCancellation(cancellationToken);
            return;
        }

        CheckCancellation(cancellationToken);
        if (TryWriteParallelPreparedArchive(ctx, targetStream, cancellationToken))
        {
            LastAsyncFastPathArchiveUsed = true;
            CheckCancellation(cancellationToken);
            return;
        }

        // 對所有 entries 啟動背景 DMA 預載
        foreach (OdfPackageEntry entry in ctx.Entries.Values)
        {
            entry.Prefetch();
        }

        using var bufferedTarget = new OdfPagedGatherWritableStream(
            targetStream,
            pageSize: 4096,
            pagesPerFlush: 16,
            leaveOpen: true);
        using var zip = new ZipArchive(bufferedTarget, ZipArchiveMode.Create, true, Encoding.UTF8);

        if (ctx.Entries.TryGetValue("mimetype", out OdfPackageEntry? mimeEntry))
        {
            await mimeEntry.PrefetchAsync(cancellationToken).ConfigureAwait(false);
            ZipArchiveEntry zipEntry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);

            if (ctx.SaveOptions.Deterministic)
                zipEntry.LastWriteTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

            using Stream entryStream = zipEntry.Open();
            await WriteEntryContentWithCrc32Async(entryStream, GetFallbackEntryBytes(mimeEntry), cancellationToken).ConfigureAwait(false);
        }

        foreach (KeyValuePair<string, OdfPackageEntry> kvp in ctx.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (kvp.Key == "mimetype")
                continue;

            await kvp.Value.PrefetchAsync(cancellationToken).ConfigureAwait(false);

            CompressionLevel compLevel = kvp.Value.IsCompressed
                ? ctx.SaveOptions.CompressionLevel
                : CompressionLevel.NoCompression;
            ZipArchiveEntry zipEntry = zip.CreateEntry(kvp.Key, compLevel);

            if (ctx.SaveOptions.Deterministic)
                zipEntry.LastWriteTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

            using Stream entryStream = zipEntry.Open();
            await WriteEntryContentWithCrc32Async(entryStream, GetFallbackEntryBytes(kvp.Value), cancellationToken).ConfigureAwait(false);
        }
    }

    private static uint WriteEntryContentWithCrc32(Stream entryStream, byte[] data)
    {
        LastFallbackCrcWriteCount++;
        using var crcStream = new OdfCrc32Stream(entryStream);
        crcStream.Write(data, 0, data.Length);
        crcStream.Flush();
        return crcStream.Crc32;
    }

    internal static uint WriteEntryContentWithCrc32ForTests(Stream entryStream, byte[] data)
    {
        return WriteEntryContentWithCrc32(entryStream, data);
    }

    private static async Task WriteEntryContentWithCrc32Async(Stream entryStream, byte[] data, CancellationToken cancellationToken)
    {
        LastFallbackCrcWriteCount++;
        using var crcStream = new OdfCrc32Stream(entryStream);
        await global::OdfKit.Internal.OdfStreamHelper.WriteAsync(crcStream, data, 0, data.Length, cancellationToken).ConfigureAwait(false);
        await crcStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool TryWriteParallelPreparedArchive(
        OdfPackage.OdfPackageSaveCollaborators ctx,
        Stream targetStream,
        CancellationToken cancellationToken = default)
    {
        LastParallelPreparedEntryCount = 0;
        LastParallelCompressionUsedPooledBuffer = false;
        CheckCancellation(cancellationToken);

        if (!targetStream.CanSeek || ctx.HasActiveEncryption || ctx.Entries.Count <= 1)
            return false;

        KeyValuePair<string, OdfPackageEntry>[] orderedEntries = GetOdfZipWriteOrder(ctx);
        PreparedZipEntry?[] preparedEntries = new PreparedZipEntry?[orderedEntries.Length];

        try
        {
            foreach (OdfPackageEntry entry in ctx.Entries.Values)
            {
                CheckCancellation(cancellationToken);
                entry.Prefetch();
            }

            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = OdfParallelScheduler.GetEffectiveConcurrency(),
                CancellationToken = cancellationToken
            };

            Parallel.For(
                0,
                orderedEntries.Length,
                options,
                index =>
                {
                    CheckCancellation(cancellationToken);
                    KeyValuePair<string, OdfPackageEntry> kvp = orderedEntries[index];
                    preparedEntries[index] = OdfParallelScheduler.RunWithConfiguredThreadPriority(
                        () => PrepareZipEntry(kvp.Key, kvp.Value, ctx, cancellationToken));
                });

            LastParallelPreparedEntryCount = preparedEntries.Length;
            using CountingWriteStream countingTarget = new(targetStream);
            using BinaryWriter writer = new(countingTarget, Encoding.UTF8, leaveOpen: true);
            var centralDirectory = new List<RawZipCentralDirectoryEntry>(preparedEntries.Length);

            foreach (PreparedZipEntry? prepared in preparedEntries)
            {
                CheckCancellation(cancellationToken);
                if (prepared is null)
                    return false;

                long localHeaderOffset = countingTarget.BytesWritten;
                WriteLocalHeader(
                    writer,
                    prepared.Method,
                    prepared.Crc32,
                    checked((uint)prepared.Payload.Length),
                    checked((uint)prepared.UncompressedSize),
                    prepared.NameBytes,
                    prepared.Flags,
                    prepared.TimeDate);
                writer.Write(prepared.Payload);

                centralDirectory.Add(new RawZipCentralDirectoryEntry(
                    prepared.Name,
                    prepared.Method,
                    prepared.Crc32,
                    checked((uint)prepared.Payload.Length),
                    checked((uint)prepared.UncompressedSize),
                    checked((uint)localHeaderOffset),
                    prepared.Flags,
                    prepared.TimeDate,
                    prepared.NameBytes));
            }

            long centralDirectoryOffset = countingTarget.BytesWritten;
            CheckCancellation(cancellationToken);
            foreach (RawZipCentralDirectoryEntry entry in centralDirectory)
            {
                CheckCancellation(cancellationToken);
                WriteCentralDirectoryEntry(writer, entry);
            }

            CheckCancellation(cancellationToken);
            long centralDirectorySize = countingTarget.BytesWritten - centralDirectoryOffset;
            WriteEndOfCentralDirectory(
                writer,
                checked((ushort)centralDirectory.Count),
                checked((uint)centralDirectorySize),
                checked((uint)centralDirectoryOffset));
            writer.Flush();
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or OverflowException)
        {
            OdfKitDiagnostics.Warn($"[OdfPackage] 並行 ZIP 封裝寫入失敗，將降級為 ZipArchive 寫出。原因: {ex.Message}", ex);
            LastParallelPreparedEntryCount = 0;
            LastParallelCompressionUsedPooledBuffer = false;
            if (targetStream.CanSeek)
            {
                targetStream.SetLength(0);
                targetStream.Position = 0;
            }
            return false;
        }
    }

    private static PreparedZipEntry PrepareZipEntry(
        string name,
        OdfPackageEntry entry,
        OdfPackage.OdfPackageSaveCollaborators ctx,
        CancellationToken cancellationToken)
    {
        CheckCancellation(cancellationToken);
        byte[] data = GetEntryBytes(entry);
        CheckCancellation(cancellationToken);
        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        uint crc = OdfCrc32.Compute(data);
        ushort method;
        byte[] payload;

        if (name == "mimetype" || !entry.IsCompressed)
        {
            method = 0;
            payload = data;
        }
        else
        {
            method = 8;
            using var compressed = new PooledZipPayloadStream(Math.Max(256, Math.Min(data.Length, 81920)));
            using (var deflate = new DeflateStream(compressed, ctx.SaveOptions.CompressionLevel, leaveOpen: true))
            {
                CheckCancellation(cancellationToken);
                deflate.Write(data, 0, data.Length);
            }
            payload = compressed.ToArray();
            LastParallelCompressionUsedPooledBuffer = true;
        }

        uint timeDate = ctx.SaveOptions.Deterministic
            ? GetDeterministicDosTimeDate()
            : GetCurrentDosTimeDate();

        return new PreparedZipEntry(
            name,
            method,
            crc,
            payload,
            data.Length,
            0x0800,
            timeDate,
            nameBytes);
    }

    private static KeyValuePair<string, OdfPackageEntry>[] GetOdfZipWriteOrder(
        OdfPackage.OdfPackageSaveCollaborators ctx)
    {
        if (!ctx.Entries.TryGetValue("mimetype", out OdfPackageEntry? mimeEntry))
            return [.. ctx.Entries];

        var orderedEntries = new KeyValuePair<string, OdfPackageEntry>[ctx.Entries.Count];
        orderedEntries[0] = new KeyValuePair<string, OdfPackageEntry>("mimetype", mimeEntry);
        int index = 1;
        foreach (KeyValuePair<string, OdfPackageEntry> entry in ctx.Entries)
        {
            if (entry.Key == "mimetype")
                continue;

            orderedEntries[index++] = entry;
        }

        return orderedEntries;
    }

    private static bool TryWriteRawCopyArchive(
        OdfPackage.OdfPackageSaveCollaborators ctx,
        Stream targetStream,
        CancellationToken cancellationToken = default)
    {
        LastRawCopyArchiveUsed = false;
        CheckCancellation(cancellationToken);
        if (!targetStream.CanSeek || ctx.Package.Mmf is null || ctx.Package.MmfEntries is null)
            return false;

        if (ctx.HasActiveEncryption)
            return false;

        bool hasRawCopyCandidate = false;
        foreach (OdfPackageEntry entry in ctx.Entries.Values)
        {
            CheckCancellation(cancellationToken);
            if (!entry.IsModified && entry.MmfEntry is not null)
            {
                if (entry.MmfEntry.CompressionMethod is not 0 and not 8)
                    return false;

                hasRawCopyCandidate = true;
            }
        }

        if (!hasRawCopyCandidate)
            return false;

        using CountingWriteStream countingTarget = new(targetStream);
        using BinaryWriter writer = new(countingTarget, Encoding.UTF8, leaveOpen: true);
        var centralDirectory = new List<RawZipCentralDirectoryEntry>();

        try
        {
            foreach (KeyValuePair<string, OdfPackageEntry> kvp in GetOdfZipWriteOrder(ctx))
            {
                CheckCancellation(cancellationToken);
                string name = kvp.Key;
                OdfPackageEntry entry = kvp.Value;
                byte[] nameBytes = Encoding.UTF8.GetBytes(name);
                long localHeaderOffset = countingTarget.BytesWritten;

                if (!entry.IsModified && entry.MmfEntry is OdfMmfEntryInfo mmfEntry)
                {
                    ushort flags = NormalizeZipFlags(mmfEntry.Flags);
                    uint timeDate = ctx.SaveOptions.Deterministic
                        ? GetDeterministicDosTimeDate()
                        : mmfEntry.TimeDate;

                    WriteLocalHeader(
                        writer,
                        mmfEntry.CompressionMethod,
                        mmfEntry.Crc32,
                        checked((uint)mmfEntry.CompressedSize),
                        checked((uint)mmfEntry.UncompressedSize),
                        nameBytes,
                        flags,
                        timeDate);

                    using Stream rawStream = ctx.Package.Mmf!.CreateViewStream(
                        mmfEntry.CompressedDataOffset,
                        mmfEntry.CompressedSize,
                        System.IO.MemoryMappedFiles.MemoryMappedFileAccess.Read);
                    CopyStreamWithCancellation(rawStream, countingTarget, cancellationToken);

                    centralDirectory.Add(new RawZipCentralDirectoryEntry(
                        name,
                        mmfEntry.CompressionMethod,
                        mmfEntry.Crc32,
                        checked((uint)mmfEntry.CompressedSize),
                        checked((uint)mmfEntry.UncompressedSize),
                        checked((uint)localHeaderOffset),
                        flags,
                        timeDate,
                        nameBytes));
                }
                else
                {
                    byte[] data = GetEntryBytes(entry);
                    uint crc = OdfCrc32.Compute(data);
                    ushort method;
                    byte[] payload;
                    if (entry.IsCompressed)
                    {
                        method = 8;
                        using var compressed = new PooledZipPayloadStream(Math.Max(256, Math.Min(data.Length, 81920)));
                        using (var deflate = new DeflateStream(compressed, ctx.SaveOptions.CompressionLevel, leaveOpen: true))
                        {
                            deflate.Write(data, 0, data.Length);
                        }
                        payload = compressed.ToArray();
                    }
                    else
                    {
                        method = 0;
                        payload = data;
                    }

                    ushort flags = 0x0800;
                    uint timeDate = ctx.SaveOptions.Deterministic
                        ? GetDeterministicDosTimeDate()
                        : GetCurrentDosTimeDate();

                    WriteLocalHeader(
                        writer,
                        method,
                        crc,
                        checked((uint)payload.Length),
                        checked((uint)data.Length),
                        nameBytes,
                        flags,
                        timeDate);
                    CheckCancellation(cancellationToken);
                    writer.Write(payload);

                    centralDirectory.Add(new RawZipCentralDirectoryEntry(
                        name,
                        method,
                        crc,
                        checked((uint)payload.Length),
                        checked((uint)data.Length),
                        checked((uint)localHeaderOffset),
                        flags,
                        timeDate,
                        nameBytes));
                }
            }

            long centralDirectoryOffset = countingTarget.BytesWritten;
            CheckCancellation(cancellationToken);
            foreach (RawZipCentralDirectoryEntry entry in centralDirectory)
            {
                CheckCancellation(cancellationToken);
                WriteCentralDirectoryEntry(writer, entry);
            }

            CheckCancellation(cancellationToken);
            long centralDirectorySize = countingTarget.BytesWritten - centralDirectoryOffset;
            WriteEndOfCentralDirectory(
                writer,
                checked((ushort)centralDirectory.Count),
                checked((uint)centralDirectorySize),
                checked((uint)centralDirectoryOffset));
            writer.Flush();
            LastRawCopyArchiveUsed = true;
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or OverflowException)
        {
            OdfKitDiagnostics.Warn($"[OdfPackage] Raw ZIP entry 直接輸出失敗，將降級為 ZipArchive 寫出。原因: {ex.Message}", ex);
            if (targetStream.CanSeek)
            {
                targetStream.SetLength(0);
                targetStream.Position = 0;
            }
            return false;
        }
    }

    private static byte[] GetEntryBytes(OdfPackageEntry entry)
    {
        entry.EnsureBytesLoaded();
        return entry.GetCachedBytes() ?? entry.GetBytesSpan().ToArray();
    }

    private static byte[] GetFallbackEntryBytes(OdfPackageEntry entry)
    {
        byte[]? cached = entry.GetCachedBytes();
        if (cached is not null)
            return cached;

        LastFallbackEntryByteCloneCount++;
        return entry.GetBytesSpan().ToArray();
    }

    private static void CopyStreamWithCancellation(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                CheckCancellation(cancellationToken);
                destination.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void CheckCancellation(CancellationToken cancellationToken)
    {
        if (cancellationToken.CanBeCanceled)
            Interlocked.Increment(ref _lastFastPathCancellationCheckCount);

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void WriteLocalHeader(
        BinaryWriter writer,
        ushort method,
        uint crc,
        uint compressedSize,
        uint uncompressedSize,
        byte[] nameBytes,
        ushort flags,
        uint timeDate)
    {
        writer.Write(0x04034b50u);
        writer.Write((ushort)20);
        writer.Write(flags);
        writer.Write(method);
        writer.Write(timeDate);
        writer.Write(crc);
        writer.Write(compressedSize);
        writer.Write(uncompressedSize);
        writer.Write((ushort)nameBytes.Length);
        writer.Write((ushort)0);
        writer.Write(nameBytes);
    }

    private static void WriteCentralDirectoryEntry(BinaryWriter writer, RawZipCentralDirectoryEntry entry)
    {
        writer.Write(0x02014b50u);
        writer.Write((ushort)20);
        writer.Write((ushort)20);
        writer.Write(entry.Flags);
        writer.Write(entry.Method);
        writer.Write(entry.TimeDate);
        writer.Write(entry.Crc32);
        writer.Write(entry.CompressedSize);
        writer.Write(entry.UncompressedSize);
        writer.Write((ushort)entry.NameBytes.Length);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((uint)0);
        writer.Write(entry.LocalHeaderOffset);
        writer.Write(entry.NameBytes);
    }

    private static void WriteEndOfCentralDirectory(BinaryWriter writer, ushort entryCount, uint centralDirectorySize, uint centralDirectoryOffset)
    {
        writer.Write(0x06054b50u);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(entryCount);
        writer.Write(entryCount);
        writer.Write(centralDirectorySize);
        writer.Write(centralDirectoryOffset);
        writer.Write((ushort)0);
    }

    private static ushort NormalizeZipFlags(ushort flags)
    {
        const ushort dataDescriptorFlag = 0x0008;
        const ushort utf8Flag = 0x0800;
        return (ushort)((flags & ~dataDescriptorFlag) | utf8Flag);
    }

    private static uint GetDeterministicDosTimeDate()
        => ToDosTimeDate(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    private static uint GetCurrentDosTimeDate()
        => ToDosTimeDate(DateTime.Now);

    private static uint ToDosTimeDate(DateTime value)
    {
        if (value.Year < 1980)
            value = new DateTime(1980, 1, 1, 0, 0, 0, value.Kind);

        uint dosTime =
            ((uint)value.Hour << 11) |
            ((uint)value.Minute << 5) |
            ((uint)value.Second / 2);
        uint dosDate =
            ((uint)(value.Year - 1980) << 9) |
            ((uint)value.Month << 5) |
            (uint)value.Day;
        return (dosDate << 16) | dosTime;
    }

}

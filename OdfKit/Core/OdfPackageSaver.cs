using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Core;

/// <summary>
/// ODF 封裝儲存管線（內部協作者），整合加密範圍、中繼資料與 ZIP 寫入。
/// </summary>
internal static class OdfPackageSaver
{
    private const long TempFileThresholdBytes = 50L * 1024 * 1024;

    /// <summary>
    /// 將封裝儲存至可寫入的底層串流（同步）。
    /// </summary>
    internal static void SaveToUnderlyingStream(OdfPackage package, bool includeRdfMetadata)
    {
        OdfPackage.OdfPackageSaveCollaborators ctx = package.SaveCollaborators;
        foreach (var entry in ctx.Entries.Values)
        {
            entry.EnsureBytesLoaded();
        }

        if (package.InTransaction && package.Mmf != null && ctx.UnderlyingStream is FileStream ufs && !ctx.IsFlatXml)
        {
            PrepareMetadata(ctx, includeRdfMetadata);
            if (OdfPackage.TryIncrementalZipAppend(package, ctx, ufs, includeRdfMetadata))
            {
                foreach (var entry in ctx.Entries.Values)
                {
                    entry.ReleaseMmfView();
                }
                package.Mmf.Dispose();
                package.Mmf = null;
                package.MmfEntries = null;
                return;
            }
        }

        if (package.Mmf != null)
        {
            foreach (var entry in ctx.Entries.Values)
            {
                entry.ReleaseMmfView();
            }
            package.Mmf.Dispose();
            package.Mmf = null;
            package.MmfEntries = null;
        }
        RunEncryptedPipeline(
            package,
            () =>
            {
                PrepareMetadata(ctx, includeRdfMetadata);
                Stream? underlying = ctx.UnderlyingStream;
                if (underlying is null || !underlying.CanWrite)
                    return;

                using Stream temp = CreateTempStream(ctx, ctx.EstimateArchiveSize());
                ctx.WriteToArchive(temp);
                ReplaceUnderlyingStream(underlying, temp);
            },
            () =>
            {
                PrepareMetadata(ctx, includeRdfMetadata);
                Stream? underlying = ctx.UnderlyingStream;
                if (underlying is null || !underlying.CanWrite)
                    return;

                using Stream temp = CreateTempStream(ctx, ctx.EstimateArchiveSize());
                OdfWholesomeEncryption.WritePackage(ctx, temp);
                ReplaceUnderlyingStream(underlying, temp);
            });
    }

    /// <summary>
    /// 將封裝儲存至可寫入的底層串流（非同步）。
    /// </summary>
    internal static async Task SaveToUnderlyingStreamAsync(
        OdfPackage package,
        bool includeRdfMetadata,
        CancellationToken cancellationToken = default)
    {
        OdfPackage.OdfPackageSaveCollaborators ctx = package.SaveCollaborators;
        foreach (var entry in ctx.Entries.Values)
        {
            entry.Prefetch();
        }
        foreach (var entry in ctx.Entries.Values)
        {
            await entry.PrefetchAsync(cancellationToken).ConfigureAwait(false);
        }

        if (package.InTransaction && package.Mmf != null && ctx.UnderlyingStream is FileStream ufs && !ctx.IsFlatXml)
        {
            PrepareMetadata(ctx, includeRdfMetadata);
            if (OdfPackage.TryIncrementalZipAppend(package, ctx, ufs, includeRdfMetadata))
            {
                foreach (var entry in ctx.Entries.Values)
                {
                    entry.ReleaseMmfView();
                }
                package.Mmf.Dispose();
                package.Mmf = null;
                package.MmfEntries = null;
                return;
            }
        }

        if (package.Mmf != null)
        {
            foreach (var entry in ctx.Entries.Values)
            {
                entry.ReleaseMmfView();
            }
            package.Mmf.Dispose();
            package.Mmf = null;
            package.MmfEntries = null;
        }
        await RunEncryptedPipelineAsync(
            package,
            async () =>
            {
                PrepareMetadata(ctx, includeRdfMetadata);
                Stream? underlying = ctx.UnderlyingStream;
                if (underlying is null || !underlying.CanWrite)
                    return;

                Stream temp = CreateTempStream(ctx, ctx.EstimateArchiveSize(), async: true);
                try
                {
                    await ctx.WriteToArchiveAsync(temp, cancellationToken).ConfigureAwait(false);
                    await ReplaceUnderlyingStreamAsync(underlying, temp, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    await DisposeStreamAsync(temp).ConfigureAwait(false);
                }
            },
            async () =>
            {
                PrepareMetadata(ctx, includeRdfMetadata);
                Stream? underlying = ctx.UnderlyingStream;
                if (underlying is null || !underlying.CanWrite)
                    return;

                Stream temp = CreateTempStream(ctx, ctx.EstimateArchiveSize(), async: true);
                try
                {
                    await OdfWholesomeEncryption.WritePackageAsync(ctx, temp, cancellationToken).ConfigureAwait(false);
                    await ReplaceUnderlyingStreamAsync(underlying, temp, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    await DisposeStreamAsync(temp).ConfigureAwait(false);
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 將封裝序列化至指定目的地串流（同步）。
    /// </summary>
    internal static void SaveToStream(OdfPackage package, Stream destination, bool includeRdfMetadata)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(destination, nameof(destination));

        OdfPackage.OdfPackageSaveCollaborators ctx = package.SaveCollaborators;
        RunEncryptedPipeline(
            package,
            () =>
            {
                PrepareMetadata(ctx, includeRdfMetadata);
                ctx.WriteToArchive(destination);
            },
            () =>
            {
                PrepareMetadata(ctx, includeRdfMetadata);
                OdfWholesomeEncryption.WritePackage(ctx, destination);
            });
    }

    /// <summary>
    /// 將封裝序列化至指定目的地串流（非同步）。
    /// </summary>
    internal static async Task SaveToStreamAsync(
        OdfPackage package,
        Stream destination,
        bool includeRdfMetadata,
        CancellationToken cancellationToken = default)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(destination, nameof(destination));

        OdfPackage.OdfPackageSaveCollaborators ctx = package.SaveCollaborators;
        await RunEncryptedPipelineAsync(
            package,
            async () =>
            {
                PrepareMetadata(ctx, includeRdfMetadata);
                await ctx.WriteToArchiveAsync(destination, cancellationToken).ConfigureAwait(false);
            },
            async () =>
            {
                PrepareMetadata(ctx, includeRdfMetadata);
                await OdfWholesomeEncryption.WritePackageAsync(ctx, destination, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static void PrepareMetadata(OdfPackage.OdfPackageSaveCollaborators ctx, bool includeRdfMetadata)
    {
        if (ctx.IsFlatXml)
            return;

        if (includeRdfMetadata)
            ctx.SaveRdfMetadata();
        ctx.SaveManifest();
    }

    private static void RunEncryptedPipeline(OdfPackage package, Action body, Action wholesomeBody)
    {
        OdfPackage.OdfPackageSaveCollaborators ctx = package.SaveCollaborators;
        ctx.ProcessSaveHooks(CancellationToken.None);
        if (UsesWholesomeEncryption(ctx))
        {
            wholesomeBody();
            return;
        }

        Dictionary<OdfPackageEntry, EntrySnapshot>? snapshots = null;
        bool encrypted = false;

        try
        {
            try
            {
                if (ctx.HasActiveEncryption)
                {
                    snapshots = CaptureEntrySnapshots(ctx);
                    OdfEncryption.Encrypt(package, ctx.SaveOptions.Password ?? string.Empty, ctx.SaveOptions.EncryptionAlgorithm);
                    encrypted = true;
                }

                body();
            }
            catch
            {
                if (snapshots is not null)
                {
                    RestoreEntrySnapshots(snapshots);
                    encrypted = false;
                }

                throw;
            }

            if (ctx.HasActiveEncryption && encrypted)
            {
                try
                {
                    OdfEncryption.Decrypt(package, ctx.SaveOptions.Password ?? string.Empty);
                }
                catch
                {
                    if (snapshots is not null)
                        RestoreEntrySnapshots(snapshots);
                    throw;
                }
            }
        }
        finally
        {
            DisposeEntrySnapshots(snapshots);
        }
    }

    private static async Task RunEncryptedPipelineAsync(
        OdfPackage package,
        Func<Task> body,
        Func<Task> wholesomeBody,
        CancellationToken cancellationToken)
    {
        OdfPackage.OdfPackageSaveCollaborators ctx = package.SaveCollaborators;
        ctx.ProcessSaveHooks(cancellationToken);
        if (UsesWholesomeEncryption(ctx))
        {
            await wholesomeBody().ConfigureAwait(false);
            return;
        }

        Dictionary<OdfPackageEntry, EntrySnapshot>? snapshots = null;
        bool encrypted = false;

        try
        {
            try
            {
                if (ctx.HasActiveEncryption)
                {
                    snapshots = CaptureEntrySnapshots(ctx);
                    OdfEncryption.Encrypt(package, ctx.SaveOptions.Password ?? string.Empty, ctx.SaveOptions.EncryptionAlgorithm);
                    encrypted = true;
                }

                await body().ConfigureAwait(false);
            }
            catch
            {
                if (snapshots is not null)
                {
                    RestoreEntrySnapshots(snapshots);
                    encrypted = false;
                }

                throw;
            }

            if (ctx.HasActiveEncryption && encrypted)
            {
                try
                {
                    OdfEncryption.Decrypt(package, ctx.SaveOptions.Password ?? string.Empty);
                }
                catch
                {
                    if (snapshots is not null)
                        RestoreEntrySnapshots(snapshots);
                    throw;
                }
            }
        }
        finally
        {
            DisposeEntrySnapshots(snapshots);
        }
    }

    private static Dictionary<OdfPackageEntry, EntrySnapshot> CaptureEntrySnapshots(
        OdfPackage.OdfPackageSaveCollaborators ctx)
    {
        Dictionary<string, OdfPackageEntry> entries = ctx.Entries;
        Dictionary<OdfPackageEntry, EntrySnapshot> snapshots = new(entries.Count);
        try
        {
            foreach (OdfPackageEntry entry in entries.Values)
            {
                if (entry.Name == "mimetype" || entry.Name.StartsWith("META-INF/", StringComparison.Ordinal))
                    continue;

                using Stream stream = entry.OpenReader();
                Stream snapshotStream = CreateTempStream(ctx, entry.GetEstimatedSize());
                try
                {
                    stream.CopyTo(snapshotStream);
                    snapshotStream.Position = 0;
                    snapshots[entry] = new EntrySnapshot(snapshotStream, CloneEncryptionInfo(entry.EncryptionInfo));
                }
                catch
                {
                    snapshotStream.Dispose();
                    throw;
                }
            }
        }
        catch
        {
            DisposeEntrySnapshots(snapshots);
            throw;
        }

        return snapshots;
    }

    private static void RestoreEntrySnapshots(Dictionary<OdfPackageEntry, EntrySnapshot> snapshots)
    {
        foreach (KeyValuePair<OdfPackageEntry, EntrySnapshot> kvp in snapshots)
        {
            if (kvp.Value.Content.CanSeek)
                kvp.Value.Content.Position = 0;
            kvp.Key.SetContent(kvp.Value.TakeContent());
            kvp.Key.EncryptionInfo = CloneEncryptionInfo(kvp.Value.EncryptionInfo);
        }
    }

    private static void DisposeEntrySnapshots(Dictionary<OdfPackageEntry, EntrySnapshot>? snapshots)
    {
        if (snapshots is null)
            return;

        foreach (EntrySnapshot snapshot in snapshots.Values)
            snapshot.Dispose();
    }

    private static OdfEncryptionInfo? CloneEncryptionInfo(OdfEncryptionInfo? source)
    {
        if (source is null)
            return null;

        OdfEncryptionInfo clone = new()
        {
            ChecksumType = source.ChecksumType,
            Checksum = [.. source.Checksum],
            AlgorithmName = source.AlgorithmName,
            InitialisationVector = [.. source.InitialisationVector],
            KeyDerivationName = source.KeyDerivationName,
            KeySize = source.KeySize,
            IterationCount = source.IterationCount,
            Salt = [.. source.Salt],
            StartKeyGenerationName = source.StartKeyGenerationName,
            StartKeySize = source.StartKeySize,
            PlaintextSize = source.PlaintextSize,
            HasChecksumType = source.HasChecksumType,
            HasChecksum = source.HasChecksum,
            HasAlgorithmName = source.HasAlgorithmName,
            HasInitialisationVector = source.HasInitialisationVector,
            HasKeyDerivationName = source.HasKeyDerivationName,
            HasIterationCount = source.HasIterationCount,
            HasSalt = source.HasSalt
        };

        foreach (KeyValuePair<string, string> prop in source.ExtensionProperties)
            clone.ExtensionProperties[prop.Key] = prop.Value;

        foreach (OdfOpenPgpEncryptedKeyInfo encryptedKey in source.OpenPgpEncryptedKeys)
        {
            OdfOpenPgpEncryptedKeyInfo keyClone = new()
            {
                KeyId = encryptedKey.KeyId,
                Recipient = encryptedKey.Recipient,
                AlgorithmName = encryptedKey.AlgorithmName,
                KeyPacket = [.. encryptedKey.KeyPacket]
            };
            foreach (KeyValuePair<string, string> prop in encryptedKey.ExtensionProperties)
                keyClone.ExtensionProperties[prop.Key] = prop.Value;
            clone.OpenPgpEncryptedKeys.Add(keyClone);
        }

        return clone;
    }

    private sealed class EntrySnapshot(Stream content, OdfEncryptionInfo? encryptionInfo) : IDisposable
    {
        public Stream Content { get; } = content;
        public OdfEncryptionInfo? EncryptionInfo { get; } = encryptionInfo;
        private bool _disposeContent = true;

        public Stream TakeContent()
        {
            _disposeContent = false;
            return Content;
        }

        public void Dispose()
        {
            if (_disposeContent)
                Content.Dispose();
        }
    }

    internal static Stream CreateTempStream(OdfPackage.OdfPackageSaveCollaborators ctx, long estimatedSize, bool async = false)
        => OdfTempStreamFactory.Create(estimatedSize, ctx.SaveOptions.TemporaryDirectory, async, TempFileThresholdBytes);

    private static bool UsesWholesomeEncryption(OdfPackage.OdfPackageSaveCollaborators ctx) =>
        ctx.SaveOptions.Password is not null
        && ctx.SaveOptions.CryptographyProvider is null
        && ctx.SaveOptions.EncryptionAlgorithm == OdfEncryptionAlgorithm.Aes256Gcm;

    /// <summary>
    /// 以完整封裝內容覆寫底層串流。
    /// </summary>
    /// <remarks>
    /// 先複製再截斷，順序不可對調：若先 <see cref="Stream.SetLength"/> 歸零才複製，複製途中的取消或
    /// I/O 失敗會留下一個已被清空、且原始內容無法復原的檔案。改為複製完成後才截斷到實際寫入長度，
    /// 失敗時底層檔案至少保有原有長度與尾端資料，仍可由交易日誌或使用者自行判讀。
    /// </remarks>
    private static void ReplaceUnderlyingStream(Stream underlying, Stream content)
    {
        underlying.Position = 0;
        content.Position = 0;
        content.CopyTo(underlying);
        underlying.SetLength(underlying.Position);
        underlying.Flush();
    }

    /// <inheritdoc cref="ReplaceUnderlyingStream"/>
    private static async Task ReplaceUnderlyingStreamAsync(
        Stream underlying,
        Stream content,
        CancellationToken cancellationToken)
    {
        underlying.Position = 0;
        content.Position = 0;
        await content.CopyToAsync(underlying, 81920, cancellationToken).ConfigureAwait(false);
        underlying.SetLength(underlying.Position);
        await underlying.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask DisposeStreamAsync(Stream stream)
    {
        if (stream is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        else
            stream.Dispose();
    }
}

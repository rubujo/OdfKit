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
        RunEncryptedPipeline(package, () =>
        {
            PrepareMetadata(ctx, includeRdfMetadata);
            Stream? underlying = ctx.UnderlyingStream;
            if (underlying is null || !underlying.CanWrite)
                return;

            using Stream temp = CreateTempStream(ctx, ctx.EstimateArchiveSize());
            ctx.WriteToArchive(temp);
            underlying.SetLength(0);
            temp.Position = 0;
            temp.CopyTo(underlying);
            underlying.Flush();
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
        await RunEncryptedPipelineAsync(package, async () =>
        {
            PrepareMetadata(ctx, includeRdfMetadata);
            Stream? underlying = ctx.UnderlyingStream;
            if (underlying is null || !underlying.CanWrite)
                return;

            Stream temp = CreateTempStream(ctx, ctx.EstimateArchiveSize(), async: true);
            try
            {
                await ctx.WriteToArchiveAsync(temp, cancellationToken).ConfigureAwait(false);
                underlying.SetLength(0);
                temp.Position = 0;
                await temp.CopyToAsync(underlying, 81920, cancellationToken).ConfigureAwait(false);
                await underlying.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (temp is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                else
                    temp.Dispose();
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// 將封裝序列化至指定目的地串流（同步）。
    /// </summary>
    internal static void SaveToStream(OdfPackage package, Stream destination, bool includeRdfMetadata)
    {
        if (destination is null)
            throw new ArgumentNullException(nameof(destination));

        OdfPackage.OdfPackageSaveCollaborators ctx = package.SaveCollaborators;
        RunEncryptedPipeline(package, () =>
        {
            PrepareMetadata(ctx, includeRdfMetadata);
            ctx.WriteToArchive(destination);
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
        if (destination is null)
            throw new ArgumentNullException(nameof(destination));

        OdfPackage.OdfPackageSaveCollaborators ctx = package.SaveCollaborators;
        await RunEncryptedPipelineAsync(package, async () =>
        {
            PrepareMetadata(ctx, includeRdfMetadata);
            await ctx.WriteToArchiveAsync(destination, cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private static void PrepareMetadata(OdfPackage.OdfPackageSaveCollaborators ctx, bool includeRdfMetadata)
    {
        if (ctx.IsFlatXml)
            return;

        if (includeRdfMetadata)
            ctx.SaveRdfMetadata();
        ctx.SaveManifest();
    }

    private static void RunEncryptedPipeline(OdfPackage package, Action body)
    {
        OdfPackage.OdfPackageSaveCollaborators ctx = package.SaveCollaborators;
        ctx.ProcessSaveHooks();
        Dictionary<OdfPackageEntry, EntrySnapshot>? snapshots = null;
        bool encrypted = false;

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
        finally
        {
            try
            {
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
    }

    private static async Task RunEncryptedPipelineAsync(OdfPackage package, Func<Task> body)
    {
        OdfPackage.OdfPackageSaveCollaborators ctx = package.SaveCollaborators;
        ctx.ProcessSaveHooks();
        Dictionary<OdfPackageEntry, EntrySnapshot>? snapshots = null;
        bool encrypted = false;

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
        finally
        {
            try
            {
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
}

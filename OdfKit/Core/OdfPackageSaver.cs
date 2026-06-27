using System;
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

        if (ctx.HasActiveEncryption)
            OdfEncryption.Encrypt(package, ctx.SaveOptions.Password ?? string.Empty, ctx.SaveOptions.EncryptionAlgorithm);

        try
        {
            body();
        }
        finally
        {
            if (ctx.HasActiveEncryption)
                OdfEncryption.Decrypt(package, ctx.SaveOptions.Password ?? string.Empty);
        }
    }

    private static async Task RunEncryptedPipelineAsync(OdfPackage package, Func<Task> body)
    {
        OdfPackage.OdfPackageSaveCollaborators ctx = package.SaveCollaborators;
        ctx.ProcessSaveHooks();

        if (ctx.HasActiveEncryption)
            OdfEncryption.Encrypt(package, ctx.SaveOptions.Password ?? string.Empty, ctx.SaveOptions.EncryptionAlgorithm);

        try
        {
            await body().ConfigureAwait(false);
        }
        finally
        {
            if (ctx.HasActiveEncryption)
                OdfEncryption.Decrypt(package, ctx.SaveOptions.Password ?? string.Empty);
        }
    }

    internal static Stream CreateTempStream(OdfPackage.OdfPackageSaveCollaborators ctx, long estimatedSize, bool async = false)
        => OdfTempStreamFactory.Create(estimatedSize, ctx.SaveOptions.TemporaryDirectory, async, TempFileThresholdBytes);
}

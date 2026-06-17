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

    private static Stream CreateTempStream(OdfPackage.OdfPackageSaveCollaborators ctx, long estimatedSize, bool async = false)
    {
        if (estimatedSize < TempFileThresholdBytes)
            return new MemoryStream();

        string tempDir = ctx.SaveOptions.TemporaryDirectory ?? Path.GetTempPath();
        if (!Directory.Exists(tempDir))
            Directory.CreateDirectory(tempDir);

        string tempFilePath = Path.Combine(tempDir, "odfkit_" + Path.GetRandomFileName());
        FileOptions options = FileOptions.DeleteOnClose;
        if (async)
            options |= FileOptions.Asynchronous;

        return new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, options);
    }
}

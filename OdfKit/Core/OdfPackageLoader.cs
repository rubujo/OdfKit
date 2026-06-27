using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using OdfKit.Compliance;
namespace OdfKit.Core;

/// <summary>
/// ODF 封裝載入管線（內部協作者）。
/// </summary>
internal static class OdfPackageLoader
{
    /// <summary>
    /// 執行完整載入流程：格式嗅探、ZIP／Flat XML、manifest、解密與 RDF。
    /// </summary>
    internal static void Initialize(OdfPackage package)
    {
        if (package.FilePath != null)
        {
            RecoverJournal(package.FilePath);
        }

        OdfPackage.OdfPackageLoadCollaborators ctx = package.LoadCollaborators;
        if (ctx.UnderlyingStream is null)
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfPackageLoader_NoInputStreamAvailable_2"));

        byte[] signature = new byte[4];
        int bytesRead = ReadSignaturePrefix(ctx, signature);

        if (!IsZipSignature(signature, bytesRead))
        {
            ctx.IsFlatXml = true;
            ctx.InitializeFlatXml(signature, bytesRead);
            return;
        }

        OdfPackageZipLoader.EnsureSeekableStream(ctx, signature, bytesRead);
        OdfPackageZipLoader.RegisterCodePagesIfNeeded();

        Stream underlying = ctx.UnderlyingStream!;
        ctx.Archive = new ZipArchive(underlying, ZipArchiveMode.Read, ctx.LeaveOpen, Encoding.UTF8);
        OdfPackageZipLoader.LoadEntries(ctx.Archive, ctx);
        LoadMimeType(ctx);

        ctx.LoadManifest();

        if (ctx.LoadOptions.Password != null || ctx.LoadOptions.CryptographyProvider != null)
            OdfEncryption.Decrypt(package, ctx.LoadOptions.Password ?? string.Empty);

        ctx.LoadRdfMetadata();
    }

    /// <summary>
    /// 非同步執行完整載入流程：格式嗅探、ZIP／Flat XML、manifest、解密與 RDF。
    /// </summary>
    internal static async Task InitializeAsync(OdfPackage package, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (package.FilePath != null)
        {
            RecoverJournal(package.FilePath);
        }

        OdfPackage.OdfPackageLoadCollaborators ctx = package.LoadCollaborators;
        if (ctx.UnderlyingStream is null)
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfPackageLoader_NoInputStreamAvailable_2"));

        byte[] signature = new byte[4];
        int bytesRead = ReadSignaturePrefix(ctx, signature);

        if (!IsZipSignature(signature, bytesRead))
        {
            ctx.IsFlatXml = true;
            await ctx.InitializeFlatXmlAsync(signature, bytesRead, cancellationToken).ConfigureAwait(false);
            return;
        }

        await OdfPackageZipLoader.EnsureSeekableStreamAsync(ctx, signature, bytesRead, cancellationToken)
            .ConfigureAwait(false);
        OdfPackageZipLoader.RegisterCodePagesIfNeeded();

        Stream underlying = ctx.UnderlyingStream!;
        ctx.Archive = new ZipArchive(underlying, ZipArchiveMode.Read, ctx.LeaveOpen, Encoding.UTF8);
        await OdfPackageZipLoader.LoadEntriesAsync(ctx.Archive, ctx, cancellationToken).ConfigureAwait(false);
        LoadMimeType(ctx);

        ctx.LoadManifest();

        if (ctx.LoadOptions.Password != null || ctx.LoadOptions.CryptographyProvider != null)
            OdfEncryption.Decrypt(package, ctx.LoadOptions.Password ?? string.Empty);

        ctx.LoadRdfMetadata();
    }

    private static int ReadSignaturePrefix(OdfPackage.OdfPackageLoadCollaborators ctx, byte[] signature)
    {
        Stream stream = ctx.UnderlyingStream!;
        if (stream.CanSeek)
        {
            long initialPosition = stream.Position;
            stream.Position = 0;
            int read = OdfPackage.OdfPackageLoadCollaborators.ReadStreamPrefix(stream, signature, 0, signature.Length);
            stream.Position = initialPosition;
            return read;
        }

        return OdfPackage.OdfPackageLoadCollaborators.ReadStreamPrefix(stream, signature, 0, signature.Length);
    }

    private static bool IsZipSignature(byte[] signature, int bytesRead)
    {
        return bytesRead == 4 &&
               signature[0] == 0x50 &&
               signature[1] == 0x4B &&
               signature[2] == 0x03 &&
               signature[3] == 0x04;
    }

    private static void LoadMimeType(OdfPackage.OdfPackageLoadCollaborators ctx)
    {
        if (ctx.Entries.TryGetValue("mimetype", out OdfPackageEntry? mimeEntry))
        {
            using var reader = new StreamReader(mimeEntry.OpenReader(), Encoding.UTF8);
            ctx.MimeType = reader.ReadToEnd().Trim();
        }
        else if (ctx.LoadOptions.ValidateMimeType)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfPackageLoader_InvalidNotFound"));
        }
    }

    private static void RecoverJournal(string filePath)
    {
        string journalPath = filePath + ".journal";
        if (File.Exists(journalPath))
        {
            try
            {
                OdfKitDiagnostics.Warn($"[OdfPackageLoader] 偵測到未完成交易日誌 '{journalPath}'。正在執行原子 Rollback...");

                File.Copy(journalPath, filePath, true);
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    fs.Flush(true);
                }

                File.Delete(journalPath);
                OdfKitDiagnostics.Info("[OdfPackageLoader] 原子 Rollback 成功完成，交易日誌已清除。");
            }
            catch (Exception ex)
            {
                throw new IOException(OdfLocalizer.GetMessage("Err_OdfPackage_JournalCreateFailed"), ex);
            }
        }
    }
}

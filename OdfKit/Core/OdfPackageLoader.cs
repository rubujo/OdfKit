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
        OdfPackage.OdfPackageLoadCollaborators ctx = package.LoadCollaborators;
        if (ctx.UnderlyingStream is null)
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfPackageLoader_NoInputStreamAvailable_2"));

        if (package.FilePath != null)
        {
            OdfTransactionJournal.RecoverIntoOpenStream(package.FilePath, ctx.UnderlyingStream);
        }

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
        {
            // 整包加密（LibreOffice wholesome）先展開內層封裝；展開後內層本身未加密，
            // 因此不再進入逐項目解密流程。
            if (!TryExpandWholesomePackage(package, ctx))
                OdfEncryption.Decrypt(package, ctx.LoadOptions.Password ?? string.Empty);
        }

        ctx.LoadRdfMetadata();
    }

    /// <summary>
    /// 非同步執行完整載入流程：格式嗅探、ZIP／Flat XML、manifest、解密與 RDF。
    /// </summary>
    internal static async Task InitializeAsync(OdfPackage package, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        OdfPackage.OdfPackageLoadCollaborators ctx = package.LoadCollaborators;
        if (ctx.UnderlyingStream is null)
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfPackageLoader_NoInputStreamAvailable_2"));

        if (package.FilePath != null)
        {
            OdfTransactionJournal.RecoverIntoOpenStream(package.FilePath, ctx.UnderlyingStream);
        }

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
        {
            // 整包加密（LibreOffice wholesome）先展開內層封裝；展開後內層本身未加密，
            // 因此不再進入逐項目解密流程。
            if (!TryExpandWholesomePackage(package, ctx))
                OdfEncryption.Decrypt(package, ctx.LoadOptions.Password ?? string.Empty);
        }

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

    /// <summary>
    /// 展開整包加密（LibreOffice wholesome）封裝：解密後以內層 ODF 封裝取代目前的 ZIP 內容。
    /// 非此形狀時回傳 <see langword="false"/>，由呼叫端改走逐項目解密。
    /// </summary>
    private static bool TryExpandWholesomePackage(OdfPackage package, OdfPackage.OdfPackageLoadCollaborators ctx)
    {
        if (!OdfWholesomeEncryption.IsWholesomePackage(package))
            return false;

        byte[]? inner = OdfWholesomeEncryption.TryDecryptInnerPackage(package, ctx.LoadOptions);
        if (inner is null)
            return false;

        // 以內層封裝取代外層：釋放外層 ZIP 與已註冊項目，改由內層位元組重新載入。
        ctx.Archive?.Dispose();
        ctx.Archive = null;

        foreach (OdfPackageEntry existing in ctx.Entries.Values)
        {
            existing.Dispose();
        }

        ctx.Entries.Clear();
        ctx.EntryOrder.Clear();
        ctx.DuplicateEntryNames.Clear();
        ctx.Manifest.Clear();

        // 外層資料流與記憶體映射不再對應目前內容，必須解除關聯。
        package.Mmf = null;
        package.MmfEntries = null;

        var innerStream = new MemoryStream(inner, writable: false);
        ctx.UnderlyingStream = innerStream;
        ctx.Archive = new ZipArchive(innerStream, ZipArchiveMode.Read, leaveOpen: true, Encoding.UTF8);

        // LoadEntries 會在 FilePath 非 null 時改以記憶體映射讀取「原始檔案」，那仍是外層封裝；
        // 展開期間暫時解除路徑關聯，強制它從內層資料流讀取，載入完成後再還原供存檔使用。
        string? originalPath = package.FilePath;
        package.FilePath = null;
        try
        {
            OdfPackageZipLoader.LoadEntries(ctx.Archive, ctx);
            LoadMimeType(ctx);
            ctx.LoadManifest();
        }
        finally
        {
            package.FilePath = originalPath;
        }

        return true;
    }

}

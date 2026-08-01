using System;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// Reads and writes packages that encrypt the whole ODF container into a single <c>encrypted-package</c> entry.
/// 讀寫將整份 ODF 容器加密為單一 <c>encrypted-package</c> 項目的封裝。
/// </summary>
/// <remarks>
/// <para>
/// LibreOffice 24.8 起對 ODF 1.4 預設採用此形狀（其文件稱為 wholesome encryption），
/// 與 ODF 規範定義的逐項目加密並存但結構不同：整份內層封裝先 deflate、再以 AES-256-GCM 加密，
/// 金鑰由 Argon2id 衍生，因此只需衍生一次金鑰，並以 AEAD tag 取代逐項目 checksum。
/// </para>
/// <para>
/// 封裝形狀（以 LibreOffice 26.2 實機產出驗證）：
/// <list type="bullet">
///   <item><description>ZIP 只含 <c>mimetype</c>、<c>encrypted-package</c> 與 <c>META-INF/manifest.xml</c>。</description></item>
///   <item><description><c>encrypted-package</c> 的位元組為 <c>IV(12) ‖ 密文 ‖ GCM tag(16)</c>；IV 內嵌於密文開頭，
///   `manifest:initialisation-vector` 為重複資訊。</description></item>
///   <item><description>解密後的明文是 deflate 後的內層 ZIP，inflate 後大小等於 <c>manifest:size</c>。</description></item>
///   <item><description>start key 為 <c>SHA-256(密碼)</c>，衍生金鑰為 <c>Argon2id(start key, salt)</c>。</description></item>
/// </list>
/// </para>
/// </remarks>
internal static class OdfWholesomeEncryption
{
    /// <summary>
    /// 整包加密項目的固定名稱。
    /// </summary>
    internal const string EncryptedPackageEntryName = "encrypted-package";

    private const int GcmNonceLength = 12;

    private const int GcmTagLengthBits = 128;

    private const int GcmTagLength = GcmTagLengthBits / 8;

    private const int Argon2Iterations = 3;

    private const int Argon2MemoryKib = 65536;

    private const int Argon2Lanes = 4;

    private const string Sha256StartKeyUri = "http://www.w3.org/2001/04/xmlenc#sha256";

    /// <summary>
    /// 判斷封裝是否為整包加密形狀。
    /// </summary>
    internal static bool IsWholesomePackage(OdfPackage package) =>
        package.Entries.TryGetValue(EncryptedPackageEntryName, out OdfPackageEntry? entry)
        && entry.EncryptionInfo is not null;

    /// <summary>
    /// 解密整包加密項目並回傳內層 ODF 封裝的位元組；非此形狀時回傳 <see langword="null"/>。
    /// </summary>
    internal static byte[]? TryDecryptInnerPackage(OdfPackage package, OdfLoadOptions loadOptions)
    {
        if (!package.Entries.TryGetValue(EncryptedPackageEntryName, out OdfPackageEntry? entry))
            return null;

        OdfEncryptionInfo? info = entry.EncryptionInfo;
        if (info is null)
            return null;

        if (!string.Equals(info.AlgorithmName, OdfEncryption.Aes256GcmAlgorithmUri, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                OdfLocalizer.GetMessage("Err_OdfEncryption_UnsupportedEncryptionAlgorithmOdfkit", info.AlgorithmName));
        }

        byte[] container;
        using (Stream reader = entry.OpenReader())
        using (var buffer = new MemoryStream())
        {
            reader.CopyTo(buffer);
            container = buffer.ToArray();
        }

        if (container.Length <= GcmNonceLength + GcmTagLength)
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfEncryption_GcmDecryptionFailed"));

        if (info.OpenPgpEncryptedKeys.Count > 0)
        {
            IOdfCryptographyProvider? provider = loadOptions.CryptographyProvider;
            if (provider is null || !provider.CanHandle(info))
            {
                throw new CryptographicException(
                    OdfLocalizer.GetMessage("Err_OdfOpenPgpCryptographyProvider_OpenpgpDecryptionFailedUnable"));
            }

            return provider.Decrypt(container, info, loadOptions);
        }

        byte[] derivedKey = DeriveKey(info, loadOptions.Password ?? string.Empty);
        byte[] deflated;
        try
        {
            deflated = DecryptGcm(container, derivedKey);
        }
        finally
        {
            Array.Clear(derivedKey, 0, derivedKey.Length);
        }

        return Inflate(deflated, package.LoadOptions);
    }

    /// <summary>
    /// 將目前封裝寫成 LibreOffice wholesome encryption 外層封裝。
    /// </summary>
    internal static void WritePackage(OdfPackage.OdfPackageSaveCollaborators ctx, Stream destination)
    {
        using Stream innerPackage = OdfPackageSaver.CreateTempStream(ctx, ctx.EstimateArchiveSize());
        ctx.WriteToArchive(innerPackage);
        innerPackage.Position = 0;

        using Stream deflatedPackage = OdfPackageSaver.CreateTempStream(ctx, innerPackage.Length);
        using (var deflater = new DeflateStream(
            deflatedPackage,
            ctx.SaveOptions.CompressionLevel,
            leaveOpen: true))
        {
            innerPackage.CopyTo(deflater);
        }

        deflatedPackage.Position = 0;
        byte[] deflated = ReadAllBytes(deflatedPackage);
        byte[] ciphertext = EncryptDeflatedPackage(
            deflated,
            ctx.SaveOptions.Password ?? string.Empty,
            out byte[] iv,
            out byte[] salt);
        try
        {
            WriteOuterArchive(ctx, destination, innerPackage.Length, ciphertext, iv, salt);
        }
        finally
        {
            Array.Clear(deflated, 0, deflated.Length);
            Array.Clear(ciphertext, 0, ciphertext.Length);
        }
    }

    /// <summary>
    /// 非同步將目前封裝寫成 LibreOffice wholesome encryption 外層封裝。
    /// </summary>
    internal static async Task WritePackageAsync(
        OdfPackage.OdfPackageSaveCollaborators ctx,
        Stream destination,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream innerPackage = OdfPackageSaver.CreateTempStream(ctx, ctx.EstimateArchiveSize(), async: true);
        try
        {
            await ctx.WriteToArchiveAsync(innerPackage, cancellationToken).ConfigureAwait(false);
            innerPackage.Position = 0;

            Stream deflatedPackage = OdfPackageSaver.CreateTempStream(ctx, innerPackage.Length, async: true);
            try
            {
                using (var deflater = new DeflateStream(
                    deflatedPackage,
                    ctx.SaveOptions.CompressionLevel,
                    leaveOpen: true))
                {
                    await innerPackage.CopyToAsync(deflater, 81920, cancellationToken).ConfigureAwait(false);
                }

                deflatedPackage.Position = 0;
                byte[] deflated = await ReadAllBytesAsync(deflatedPackage, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                byte[] ciphertext = EncryptDeflatedPackage(
                    deflated,
                    ctx.SaveOptions.Password ?? string.Empty,
                    out byte[] iv,
                    out byte[] salt);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    WriteOuterArchive(ctx, destination, innerPackage.Length, ciphertext, iv, salt);
                }
                finally
                {
                    Array.Clear(deflated, 0, deflated.Length);
                    Array.Clear(ciphertext, 0, ciphertext.Length);
                }
            }
            finally
            {
                await DisposeStreamAsync(deflatedPackage).ConfigureAwait(false);
            }
        }
        finally
        {
            await DisposeStreamAsync(innerPackage).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 以 start key 與 Argon2id 參數衍生 AES-256-GCM 的金鑰。
    /// </summary>
    private static byte[] DeriveKey(OdfEncryptionInfo info, string password)
    {
        byte[] startKey = ComputeStartKey(info.StartKeyGenerationName, password);

        int iterations = ReadArgon2Parameter(info, "argon2-iterations", "argon2-t", Argon2Iterations);
        int memoryKib = ReadArgon2Parameter(info, "argon2-memory", "argon2-m", Argon2MemoryKib);
        int lanes = ReadArgon2Parameter(info, "argon2-lanes", "argon2-p", Argon2Lanes);
        int keySize = info.KeySize > 0 ? info.KeySize : OdfEncryption.Aes256KeySizeBytes;

        OdfEncryption.ValidateArgon2Parameters(iterations, memoryKib, lanes);
        OdfEncryption.ValidateEncryptionKeySize(OdfEncryption.Aes256GcmAlgorithmUri, keySize);

        OdfEncryption.EnterArgon2Operation();
        try
        {
            var builder = new Argon2Parameters.Builder(Argon2Parameters.Argon2id)
                .WithVersion(Argon2Parameters.Version13)
                .WithIterations(iterations)
                .WithMemoryAsKB(memoryKib)
                .WithParallelism(lanes)
                .WithSalt(info.Salt);

            var generator = new Argon2BytesGenerator();
            generator.Init(builder.Build());

            byte[] derivedKey = new byte[keySize];
            generator.GenerateBytes(startKey, derivedKey, 0, derivedKey.Length);
            return derivedKey;
        }
        finally
        {
            Array.Clear(startKey, 0, startKey.Length);
            OdfEncryption.ExitArgon2Operation();
        }
    }

    /// <summary>
    /// 依 `start-key-generation-name` 將密碼雜湊為 start key；缺席時採規範預設的 SHA-1。
    /// </summary>
    private static byte[] ComputeStartKey(string? startKeyGenerationName, string password)
    {
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

        bool isSha256 = startKeyGenerationName is not null
            && (startKeyGenerationName.EndsWith("#sha256", StringComparison.OrdinalIgnoreCase)
                || string.Equals(startKeyGenerationName, "sha256", StringComparison.OrdinalIgnoreCase)
                || string.Equals(startKeyGenerationName, "sha-256", StringComparison.OrdinalIgnoreCase));

        if (isSha256)
        {
            var digest = new Sha256Digest();
            byte[] hash = new byte[digest.GetDigestSize()];
            digest.BlockUpdate(passwordBytes, 0, passwordBytes.Length);
            digest.DoFinal(hash, 0);
            return hash;
        }

        var sha1 = new Sha1Digest();
        byte[] sha1Hash = new byte[sha1.GetDigestSize()];
        sha1.BlockUpdate(passwordBytes, 0, passwordBytes.Length);
        sha1.DoFinal(sha1Hash, 0);
        return sha1Hash;
    }

    /// <summary>
    /// 讀取 Argon2 參數：先取對標屬性名，再退回早期縮寫屬性名。
    /// </summary>
    private static int ReadArgon2Parameter(OdfEncryptionInfo info, string preferredName, string legacyName, int defaultValue)
    {
        foreach (string name in new[] { preferredName, legacyName })
        {
            if (info.ExtensionProperties.TryGetValue(name, out string? raw)
                && int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value)
                && value > 0)
            {
                return value;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// 解開 <c>IV ‖ 密文 ‖ tag</c> 佈局的 AES-256-GCM 容器。
    /// </summary>
    internal static byte[] DecryptGcm(byte[] container, byte[] key)
    {
        byte[] nonce = new byte[GcmNonceLength];
        Buffer.BlockCopy(container, 0, nonce, 0, GcmNonceLength);

        int bodyLength = container.Length - GcmNonceLength;
        byte[] body = new byte[bodyLength];
        Buffer.BlockCopy(container, GcmNonceLength, body, 0, bodyLength);

        try
        {
            var cipher = new GcmBlockCipher(new AesEngine());
            cipher.Init(false, new AeadParameters(new KeyParameter(key), GcmTagLengthBits, nonce));

            byte[] output = new byte[cipher.GetOutputSize(body.Length)];
            int written = cipher.ProcessBytes(body, 0, body.Length, output, 0);
            written += cipher.DoFinal(output, written);

            if (written == output.Length)
                return output;

            byte[] trimmed = new byte[written];
            Buffer.BlockCopy(output, 0, trimmed, 0, written);
            return trimmed;
        }
        catch (Exception ex)
        {
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfEncryption_GcmDecryptionFailed"), ex);
        }
    }

    /// <summary>
    /// 加密已 deflate 的內層封裝並組成 <c>IV ‖ ciphertext ‖ tag</c>。
    /// </summary>
    private static byte[] EncryptDeflatedPackage(
        byte[] deflated,
        string password,
        out byte[] iv,
        out byte[] salt)
    {
        byte[] ciphertext = OdfEncryption.EncryptEntry(
            deflated,
            password,
            OdfEncryptionAlgorithm.Aes256Gcm,
            out iv,
            out salt,
            out _);

        byte[] container = new byte[iv.Length + ciphertext.Length];
        Buffer.BlockCopy(iv, 0, container, 0, iv.Length);
        Buffer.BlockCopy(ciphertext, 0, container, iv.Length, ciphertext.Length);
        Array.Clear(ciphertext, 0, ciphertext.Length);
        return container;
    }

    /// <summary>
    /// 寫出只包含 mimetype、encrypted-package 與 manifest.xml 的外層 ZIP。
    /// </summary>
    private static void WriteOuterArchive(
        OdfPackage.OdfPackageSaveCollaborators ctx,
        Stream destination,
        long innerPackageSize,
        byte[] ciphertext,
        byte[] iv,
        byte[] salt)
    {
        string mimeType = ctx.MimeType ?? "application/vnd.oasis.opendocument.text";
        byte[] manifest = CreateOuterManifest(ctx, mimeType, innerPackageSize, iv, salt);

        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8);
        WriteOuterEntry(archive, "mimetype", Encoding.UTF8.GetBytes(mimeType), CompressionLevel.NoCompression, ctx.SaveOptions.Deterministic);
        WriteOuterEntry(archive, EncryptedPackageEntryName, ciphertext, CompressionLevel.NoCompression, ctx.SaveOptions.Deterministic);
        WriteOuterEntry(archive, "META-INF/manifest.xml", manifest, ctx.SaveOptions.CompressionLevel, ctx.SaveOptions.Deterministic);
    }

    /// <summary>
    /// 建立 wholesome 外層 manifest；其根目錄由內嵌 package 取代，因此不輸出 `/` 項目。
    /// </summary>
    private static byte[] CreateOuterManifest(
        OdfPackage.OdfPackageSaveCollaborators ctx,
        string mimeType,
        long innerPackageSize,
        byte[] iv,
        byte[] salt)
    {
        using var output = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = ctx.SaveOptions.IndentXml
        };

        using (XmlWriter writer = XmlWriter.Create(output, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("manifest", "manifest", OdfNamespaces.Manifest);
            writer.WriteAttributeString("xmlns", "loext", null, OdfNamespaces.LoExt);
            writer.WriteAttributeString("manifest", "version", OdfNamespaces.Manifest, "1.4");

            writer.WriteStartElement("manifest", "file-entry", OdfNamespaces.Manifest);
            writer.WriteAttributeString("manifest", "full-path", OdfNamespaces.Manifest, EncryptedPackageEntryName);
            writer.WriteAttributeString("manifest", "media-type", OdfNamespaces.Manifest, mimeType);
            writer.WriteAttributeString(
                "manifest",
                "size",
                OdfNamespaces.Manifest,
                innerPackageSize.ToString(System.Globalization.CultureInfo.InvariantCulture));

            writer.WriteStartElement("manifest", "encryption-data", OdfNamespaces.Manifest);

            writer.WriteStartElement("manifest", "algorithm", OdfNamespaces.Manifest);
            writer.WriteAttributeString("manifest", "algorithm-name", OdfNamespaces.Manifest, OdfEncryption.Aes256GcmAlgorithmUri);
            writer.WriteAttributeString("manifest", "initialisation-vector", OdfNamespaces.Manifest, Convert.ToBase64String(iv));
            writer.WriteEndElement();

            writer.WriteStartElement("manifest", "start-key-generation", OdfNamespaces.Manifest);
            writer.WriteAttributeString("manifest", "start-key-generation-name", OdfNamespaces.Manifest, Sha256StartKeyUri);
            writer.WriteAttributeString("manifest", "key-size", OdfNamespaces.Manifest, OdfEncryption.Aes256KeySizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteEndElement();

            writer.WriteStartElement("manifest", "key-derivation", OdfNamespaces.Manifest);
            writer.WriteAttributeString("manifest", "key-derivation-name", OdfNamespaces.Manifest, OdfEncryption.Argon2idDerivationUri);
            writer.WriteAttributeString("loext", "argon2-iterations", OdfNamespaces.LoExt, Argon2Iterations.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteAttributeString("loext", "argon2-memory", OdfNamespaces.LoExt, Argon2MemoryKib.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteAttributeString("loext", "argon2-lanes", OdfNamespaces.LoExt, Argon2Lanes.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteAttributeString("manifest", "salt", OdfNamespaces.Manifest, Convert.ToBase64String(salt));
            writer.WriteAttributeString("manifest", "key-size", OdfNamespaces.Manifest, OdfEncryption.Aes256KeySizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteEndElement();

            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return output.ToArray();
    }

    private static void WriteOuterEntry(
        ZipArchive archive,
        string name,
        byte[] content,
        CompressionLevel compressionLevel,
        bool deterministic)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, compressionLevel);
        if (deterministic)
            entry.LastWriteTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        using Stream stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        await stream.CopyToAsync(output, 81920, cancellationToken).ConfigureAwait(false);
        return output.ToArray();
    }

    private static async ValueTask DisposeStreamAsync(Stream stream)
    {
        if (stream is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        else
            stream.Dispose();
    }

    /// <summary>
    /// 將解密後的 deflate 位元組還原為內層 ODF 封裝，並套用載入選項的資源預算。
    /// </summary>
    internal static byte[] Inflate(byte[] deflated, OdfLoadOptions loadOptions)
    {
        using var source = new MemoryStream(deflated);
        using var inflater = new DeflateStream(source, CompressionMode.Decompress);
        using var output = new MemoryStream();

        long maxEntrySize = loadOptions.MaxEntrySize;
        byte[] buffer = new byte[8192];
        long total = 0;
        int read;
        while ((read = inflater.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > maxEntrySize)
                throw new SecurityException(OdfLocalizer.GetMessage("Err_OdfEncryption_UnzippedItemSizeExceeds", maxEntrySize));

            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }
}

using System;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// Reads packages that encrypt the whole ODF container into a single <c>encrypted-package</c> entry.
/// 讀取將整份 ODF 容器加密為單一 <c>encrypted-package</c> 項目的封裝。
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
/// <para>
/// 目前只支援讀取；OdfKit 寫入時仍採規範定義的逐項目加密。
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

    /// <summary>
    /// 判斷封裝是否為整包加密形狀。
    /// </summary>
    internal static bool IsWholesomePackage(OdfPackage package) =>
        package.Entries.TryGetValue(EncryptedPackageEntryName, out OdfPackageEntry? entry)
        && entry.EncryptionInfo is not null;

    /// <summary>
    /// 解密整包加密項目並回傳內層 ODF 封裝的位元組；非此形狀時回傳 <see langword="null"/>。
    /// </summary>
    internal static byte[]? TryDecryptInnerPackage(OdfPackage package, string password)
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

        byte[] derivedKey = DeriveKey(info, password);
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
    /// 以 start key 與 Argon2id 參數衍生 AES-256-GCM 的金鑰。
    /// </summary>
    private static byte[] DeriveKey(OdfEncryptionInfo info, string password)
    {
        byte[] startKey = ComputeStartKey(info.StartKeyGenerationName, password);

        int iterations = ReadArgon2Parameter(info, "argon2-iterations", "argon2-t", 3);
        int memoryKib = ReadArgon2Parameter(info, "argon2-memory", "argon2-m", 65536);
        int lanes = ReadArgon2Parameter(info, "argon2-lanes", "argon2-p", 4);
        int keySize = info.KeySize > 0 ? info.KeySize : OdfEncryption.Aes256KeySizeBytes;

        var builder = new Argon2Parameters.Builder(Argon2Parameters.Argon2id)
            .WithVersion(Argon2Parameters.Version13)
            .WithIterations(iterations)
            .WithMemoryAsKB(memoryKib)
            .WithParallelism(lanes)
            .WithSalt(info.Salt);

        var generator = new Argon2BytesGenerator();
        generator.Init(builder.Build());

        byte[] derivedKey = new byte[keySize];
        try
        {
            generator.GenerateBytes(startKey, derivedKey, 0, derivedKey.Length);
        }
        finally
        {
            Array.Clear(startKey, 0, startKey.Length);
        }

        return derivedKey;
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
    private static byte[] DecryptGcm(byte[] container, byte[] key)
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
    /// 將解密後的 deflate 位元組還原為內層 ODF 封裝，並套用載入選項的資源預算。
    /// </summary>
    private static byte[] Inflate(byte[] deflated, OdfLoadOptions loadOptions)
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

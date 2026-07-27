using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Globalization;
using System.Security;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Org.BouncyCastle.Crypto.Generators;

using OdfKit.Compliance;
namespace OdfKit.Core;
/// <summary>
/// Provides the OdfEncryption API.
/// 提供 OdfEncryption API。
/// </summary>

public static partial class OdfEncryption
{
    #region Package Encryption & Decryption

    internal static int LastParallelEncryptedEntryCountForTests { get; private set; }

    internal static int LastParallelEncryptionMaxDegreeForTests { get; private set; }

    /// <summary>
    /// Performs the Decrypt operation.
    /// 解密指定 ODF 封裝中的所有加密專案。
    /// </summary>
    /// <param name="package">要解密的 ODF 封裝執行個體</param>
    /// <param name="password">解密密碼</param>
    public static void Decrypt(OdfPackage package, string password)
    {
        foreach (var entry in package.Entries.Values)
        {
            if (entry.EncryptionInfo is null)
                continue;

            byte[] ciphertext;
            using (var stream = entry.OpenReader())
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                ciphertext = ms.ToArray();
            }

            byte[] decryptedPlaintext;

            IOdfCryptographyProvider? cryptoProvider = null;
            if (package.LoadOptions.CryptographyProvider is not null &&
                package.LoadOptions.CryptographyProvider.CanHandle(entry.EncryptionInfo))
            {
                cryptoProvider = package.LoadOptions.CryptographyProvider;
            }
            else if (package.SaveOptions.CryptographyProvider is not null &&
                package.SaveOptions.CryptographyProvider.CanHandle(entry.EncryptionInfo))
            {
                cryptoProvider = package.SaveOptions.CryptographyProvider;
            }

            if (cryptoProvider is not null)
            {
                decryptedPlaintext = cryptoProvider.Decrypt(ciphertext, entry.EncryptionInfo, package.LoadOptions);
                if (cryptoProvider is not OdfOpenPgpCryptographyProvider)
                    ValidateChecksumForProviderDecryption(entry.EncryptionInfo, decryptedPlaintext);
            }
            else if (entry.EncryptionInfo.OpenPgpEncryptedKeys.Count > 0 ||
                string.Equals(entry.EncryptionInfo.AlgorithmName, OpenPgpAlgorithmUri, StringComparison.Ordinal))
            {
                throw new NotSupportedException(OdfLocalizer.GetMessage("Err_OdfEncryption_OpenpgpEncryptedItemsDecrypted"));
            }
            else
            {
                string? kdfName = null;
                if (entry.EncryptionInfo.ExtensionProperties.TryGetValue("kdf-name", out string? kn))
                {
                    kdfName = kn;
                }

                // 優先讀 LibreOffice／目前 OdfKit 的 loext:argon2-iterations／-memory／-lanes，
                // 找不到才退回早期 OdfKit 版本寫出的 argon2-t／-m／-p。
                int argon2T = ReadArgon2Parameter(entry.EncryptionInfo, "argon2-iterations", "argon2-t", 3);
                int argon2M = ReadArgon2Parameter(entry.EncryptionInfo, "argon2-memory", "argon2-m", 65536);
                int argon2P = ReadArgon2Parameter(entry.EncryptionInfo, "argon2-lanes", "argon2-p", 4);

                // 早期 OdfKit 版本誤將 PBKDF2 的 PRF 綁在 start-key 演算法上，對 SHA-256 start key
                // 使用 HMAC-SHA-256；規範固定為 HMAC-SHA-1。只有兩者會不同時才需要後備嘗試。
                bool prfMayDiffer = !isArgon2ForEntry(entry.EncryptionInfo, kdfName)
                    && IsSha256StartKey(entry.EncryptionInfo.StartKeyGenerationName);

                decryptedPlaintext =
                    TryDecryptEntryContent(package, entry.EncryptionInfo, ciphertext, password, kdfName, argon2T, argon2M, argon2P, legacyPrf: false)
                    ?? (prfMayDiffer
                        ? TryDecryptEntryContent(package, entry.EncryptionInfo, ciphertext, password, kdfName, argon2T, argon2M, argon2P, legacyPrf: true)
                        : null)
                    ?? throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfEncryption_InvalidDecryptionFailedSum_2"));
            }

            entry.SetContent(decryptedPlaintext);
            entry.EncryptionInfo = null;
        }
    }

    /// <summary>
    /// 判斷 `start-key-generation-name` 是否為 SHA-256。
    /// </summary>
    private static bool IsSha256StartKey(string? startKeyGenerationName) =>
        startKeyGenerationName is not null
        && (startKeyGenerationName.EndsWith("#sha256", StringComparison.OrdinalIgnoreCase)
            || string.Equals(startKeyGenerationName, "sha256", StringComparison.OrdinalIgnoreCase)
            || string.Equals(startKeyGenerationName, "sha-256", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 判斷項目是否使用 Argon2id 金鑰衍生。
    /// </summary>
    private static bool isArgon2ForEntry(OdfEncryptionInfo info, string? kdfName) =>
        string.Equals(kdfName, "argon2id", StringComparison.OrdinalIgnoreCase)
        || string.Equals(info.KeyDerivationName, Argon2idDerivationUri, StringComparison.OrdinalIgnoreCase)
        || string.Equals(info.KeyDerivationName, Argon2idOdf15DerivationUri, StringComparison.OrdinalIgnoreCase)
        || string.Equals(info.KeyDerivationName, Argon2idLegacyDerivationUri, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 以指定的虛擬亂數函式解密單一項目，並驗證 checksum；不符時回傳 <see langword="null"/>。
    /// </summary>
    private static byte[]? TryDecryptEntryContent(
        OdfPackage package,
        OdfEncryptionInfo info,
        byte[] ciphertext,
        string password,
        string? kdfName,
        int argon2T,
        int argon2M,
        int argon2P,
        bool legacyPrf)
    {
        byte[] decryptedBytes;
        try
        {
            decryptedBytes = DecryptEntryCore(
                ciphertext,
                password,
                info.AlgorithmName,
                info.KeyDerivationName,
                info.KeySize,
                info.IterationCount,
                info.Salt,
                info.InitialisationVector,
                info.StartKeyGenerationName,
                kdfName,
                argon2T,
                argon2M,
                argon2P,
                legacyPrf);
        }
        catch (CryptographicException)
        {
            // 金鑰不符時區塊解密會在填補檢查或 GCM 驗證階段失敗；視為本次嘗試不成立，
            // 讓呼叫端有機會改用後備的虛擬亂數函式再試一次。
            return null;
        }

        byte[]? decompressedBytes = null;
        try
        {
            using var ms = new MemoryStream(decryptedBytes);
            using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
            using var outMs = new MemoryStream();

            long maxEntrySize = package.LoadOptions.MaxEntrySize;
            byte[] buffer = new byte[8192];
            long cumulativeBytes = 0;
            int bytesRead;
            while ((bytesRead = deflate.Read(buffer, 0, buffer.Length)) > 0)
            {
                cumulativeBytes += bytesRead;
                if (cumulativeBytes > maxEntrySize)
                {
                    throw new SecurityException(OdfLocalizer.GetMessage("Err_OdfEncryption_UnzippedItemSizeExceeds", maxEntrySize));
                }
                outMs.Write(buffer, 0, bytesRead);
            }
            decompressedBytes = outMs.ToArray();
        }
        catch (SecurityException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // 非 deflate 或解壓失敗時改以原始解密位元組驗證 checksum。
            OdfKitDiagnostics.Warn($"加密項目 deflate 容錯驗證失敗，改以原始位元組驗證：{ex.Message}", ex);
        }

        if (decompressedBytes is not null)
        {
            // ODF 1.0～1.4 Part 2 §4.16.4 的 checksum 涵蓋「壓縮後、未加密」資料，也就是 decryptedBytes；
            // 先驗規範形狀，再退回早期 OdfKit 版本的解壓後形狀。
            if (ByteArrayEquals(ComputeHash(decryptedBytes, info.ChecksumType), info.Checksum)
                || ByteArrayEquals(ComputeHash(decompressedBytes, info.ChecksumType), info.Checksum))
            {
                return decompressedBytes;
            }

            return null;
        }

        return ByteArrayEquals(ComputeHash(decryptedBytes, info.ChecksumType), info.Checksum)
            ? decryptedBytes
            : null;
    }

    /// <summary>
    /// 讀取 Argon2 參數：先取規範對標的屬性名，再退回早期 OdfKit 版本的縮寫屬性名。
    /// </summary>
    private static int ReadArgon2Parameter(OdfEncryptionInfo info, string preferredName, string legacyName, int defaultValue)
    {
        if (info.ExtensionProperties.TryGetValue(preferredName, out string? preferred)
            && int.TryParse(preferred, NumberStyles.Integer, CultureInfo.InvariantCulture, out int preferredValue))
        {
            return preferredValue;
        }

        if (info.ExtensionProperties.TryGetValue(legacyName, out string? legacy)
            && int.TryParse(legacy, NumberStyles.Integer, CultureInfo.InvariantCulture, out int legacyValue))
        {
            return legacyValue;
        }

        return defaultValue;
    }

    private static void ValidateChecksumForProviderDecryption(OdfEncryptionInfo info, byte[] plaintext)
    {
        if (info.Checksum.Length == 0 || string.IsNullOrWhiteSpace(info.ChecksumType))
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfEncryption_InvalidDecryptionFailedSum_2"));

        byte[] calculatedChecksum = ComputeHash(plaintext, info.ChecksumType);
        if (!ByteArrayEquals(calculatedChecksum, info.Checksum))
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfEncryption_InvalidDecryptionFailedSum_2"));
    }
    /// <summary>
    /// Short overload of Encrypt that accepts package and password; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 package 與 password；其餘可選參數使用預設值並轉呼叫最長 Encrypt 多載。
    /// </summary>
    public static void Encrypt(OdfPackage package, string password) => Encrypt(package, password, OdfEncryptionAlgorithm.Aes256);


    /// <summary>
    /// Performs the Encrypt operation.
    /// 加密指定 ODF 封裝中的所有適用專案。
    /// </summary>
    /// <param name="package">要加密的 ODF 封裝執行個體</param>
    /// <param name="password">加密密碼</param>
    /// <param name="algorithm">加密演算法，預設為 AES-256</param>
    public static void Encrypt(OdfPackage package, string password, OdfEncryptionAlgorithm algorithm)
    {
        LastParallelEncryptedEntryCountForTests = 0;
        LastParallelEncryptionMaxDegreeForTests = 0;

        if (algorithm == OdfEncryptionAlgorithm.OpenPgp && package.SaveOptions.CryptographyProvider is null)
        {
            throw new NotSupportedException(OdfLocalizer.GetMessage("Err_OdfEncryption_OpenpgpEncryptionImplementedThrough"));
        }

        if (package.SaveOptions.CryptographyProvider is null)
        {
            EncryptBuiltInEntries(package, password, algorithm);
            return;
        }

        if (algorithm == OdfEncryptionAlgorithm.OpenPgp
            && package.SaveOptions.CryptographyProvider is OdfOpenPgpCryptographyProvider openPgpProvider)
        {
            openPgpProvider.EncryptPackage(package, package.SaveOptions);
            return;
        }

        foreach (var entry in package.Entries.Values)
        {
            string name = entry.Name;
            if (name == "mimetype" || name.StartsWith("META-INF/"))
            {
                continue;
            }

            byte[] plaintext;
            using (var stream = entry.OpenReader())
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                plaintext = ms.ToArray();
            }

            byte[] ciphertext;
            OdfEncryptionInfo info;

            ciphertext = package.SaveOptions.CryptographyProvider.Encrypt(plaintext, name, package.SaveOptions, out info);

            entry.SetContent(ciphertext);
            entry.EncryptionInfo = info;
        }
    }


    private static void EncryptBuiltInEntries(OdfPackage package, string password, OdfEncryptionAlgorithm algorithm)
    {
        List<EncryptionWorkItem> workItems = [];
        foreach (var entry in package.Entries.Values)
        {
            string name = entry.Name;
            if (name == "mimetype" || name.StartsWith("META-INF/"))
            {
                continue;
            }

            byte[] plaintext;
            using (var stream = entry.OpenReader())
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                plaintext = ms.ToArray();
            }

            workItems.Add(new EncryptionWorkItem(entry, plaintext));
        }

        if (workItems.Count == 0)
        {
            return;
        }

        if (workItems.Count == 1)
        {
            workItems[0].Encrypt(password, algorithm);
        }
        else
        {
            int maxDegree = OdfParallelScheduler.GetEffectiveConcurrency();
            LastParallelEncryptionMaxDegreeForTests = maxDegree;
            Parallel.For(
                0,
                workItems.Count,
                new ParallelOptions { MaxDegreeOfParallelism = maxDegree },
                i => OdfParallelScheduler.RunWithConfiguredThreadPriority(
                    () => workItems[i].Encrypt(password, algorithm)));
            LastParallelEncryptedEntryCountForTests = workItems.Count;
        }

        foreach (EncryptionWorkItem item in workItems)
        {
            item.Apply();
        }
    }

    private sealed class EncryptionWorkItem(OdfPackageEntry entry, byte[] plaintext)
    {
        private byte[]? _ciphertext;
        private OdfEncryptionInfo? _info;

        public void Encrypt(string password, OdfEncryptionAlgorithm algorithm)
        {
            byte[] compressedPlaintext;
            using (var ms = new MemoryStream())
            {
                using (var deflate = new DeflateStream(ms, CompressionMode.Compress, true))
                {
                    deflate.Write(plaintext, 0, plaintext.Length);
                }
                compressedPlaintext = ms.ToArray();
            }

            byte[] iv;
            byte[] salt;
            _ciphertext = EncryptEntry(compressedPlaintext, password, algorithm, out iv, out salt, out _);

            // 檢查碼型別跟著演算法世代：傳統 Blowfish 沿用 ODF 1.0／1.1 的 `SHA1/1K`（LibreOffice
            // 只接受這個形狀），AES 路徑採規範建議給新實作的 `#sha256-1k`。
            string checksumType = algorithm == OdfEncryptionAlgorithm.Blowfish
                ? Sha1OneKilobyteChecksumName
                : Sha256OneKilobyteChecksumUri;
            byte[] checksum = ComputeHash(compressedPlaintext, checksumType);

            _info = new OdfEncryptionInfo
            {
                ChecksumType = checksumType,
                Checksum = checksum,

                // ODF Part 2 §3.4.1：加密項目必須以 manifest:size 宣告原始未壓縮未加密大小。
                PlaintextSize = plaintext.Length,
                // Blowfish 使用規範的簡短名稱：等價的 URI 形式雖然合法，但 LibreOffice 的
                // 傳統加密讀取路徑只比對 `Blowfish CFB`。
                AlgorithmName = algorithm == OdfEncryptionAlgorithm.Aes256Gcm
                    ? Aes256GcmAlgorithmUri
                    : (algorithm == OdfEncryptionAlgorithm.Aes256 ? Aes256AlgorithmUri : BlowfishAlgorithmName),
                InitialisationVector = iv,
                KeyDerivationName = "PBKDF2",
                KeySize = (algorithm == OdfEncryptionAlgorithm.Aes256 || algorithm == OdfEncryptionAlgorithm.Aes256Gcm) ? Aes256KeySizeBytes : BlowfishKeySizeBytes,
                IterationCount = DefaultPbkdf2IterationCount,
                Salt = salt
            };

            if (algorithm == OdfEncryptionAlgorithm.Aes256Gcm)
            {
                _info.StartKeyGenerationName = "http://www.w3.org/2000/09/xmldsig#sha256";
                _info.StartKeySize = 32;

                // Argon2id 的 manifest 形狀對標 LibreOffice 的
                // OpenDocument-v1.4+libreoffice-manifest-schema.rng：key-derivation-name 使用 TDF
                // 實驗性 URI，參數以 loext:argon2-iterations／-memory／-lanes 表示。
                // manifest:iteration-count 仍會輸出：官方 ODF 1.4 manifest schema 對非 PGP 的
                // key-derivation 要求該屬性必填，LibreOffice 解析 Argon2 時則直接讀 loext 參數而忽略它。
                _info.KeyDerivationName = Argon2idDerivationUri;
                _info.ExtensionProperties["argon2-iterations"] = "3";
                _info.ExtensionProperties["argon2-memory"] = "65536";
                _info.ExtensionProperties["argon2-lanes"] = "4";
            }
            else if (algorithm == OdfEncryptionAlgorithm.Aes256)
            {
                _info.StartKeyGenerationName = "http://www.w3.org/2000/09/xmldsig#sha256";
                _info.StartKeySize = 32;
            }
            // Blowfish 不輸出 start-key-generation：SHA-1 本來就是規範預設，省略後與
            // LibreOffice 產生的傳統加密文件形狀一致。
        }

        public void Apply()
        {
            entry.SetContent(_ciphertext ?? throw new InvalidOperationException(
                OdfLocalizer.GetMessage("Err_OdfEncryptionPackage_MissingCiphertext")));
            entry.EncryptionInfo = _info ?? throw new InvalidOperationException(
                OdfLocalizer.GetMessage("Err_OdfEncryptionPackage_MissingEncryptionInfo"));

            // 加密項目一律以 ZIP STORED 寫出：內容在加密前就已 deflate，密文不可再壓縮，
            // 且消費端（LibreOffice `package/source/zippackage`）預期加密項目的 ZIP 位元組
            // 就是密文本身。多包一層 DEFLATE 會讓它拿到非預期的位元組。
            entry.IsCompressed = false;
        }
    }

    #endregion
}

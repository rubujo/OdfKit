using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

using OdfKit.Compliance;
namespace OdfKit.Core;

/// <summary>
/// Provides the OdfOpenPgpCryptographyProvider API.
/// 以 <see cref="IOdfOpenPgpKeyProvider"/> 為基礎，實作 ODF 1.3 OpenPGP 加密模式的
/// </summary>
public sealed class OdfOpenPgpCryptographyProvider : IOdfCryptographyProvider
{
    private readonly IOdfOpenPgpKeyProvider _keyProvider;

    /// <summary>
    /// Performs odf open pgp cryptography provider.
    /// 初始化 <see cref="OdfOpenPgpCryptographyProvider"/> 類別的新執行個體。
    /// </summary>
    /// <param name="keyProvider">負責 Session Key 加解密的 OpenPGP 金鑰提供者</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="keyProvider"/> 為 null 時擲出</exception>
    public OdfOpenPgpCryptographyProvider(IOdfOpenPgpKeyProvider keyProvider)
    {
        _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
    }

    /// <summary>
    /// Returns whether this instance is handle is allowed.
    /// 傳回是否可 Handle。
    /// </summary>
    /// <inheritdoc />
    public bool CanHandle(OdfEncryptionInfo info)
    {
        return string.Equals(info.AlgorithmName, OdfEncryption.OpenPgpAlgorithmUri, StringComparison.Ordinal)
            || info.OpenPgpEncryptedKeys.Count > 0;
    }

    /// <summary>
    /// Performs the Decrypt operation.
    /// 執行 Decrypt 作業。
    /// </summary>
    /// <inheritdoc />
    public byte[] Decrypt(byte[] ciphertext, OdfEncryptionInfo info, OdfLoadOptions loadOptions)
    {
        foreach (var encKey in info.OpenPgpEncryptedKeys)
        {
            byte[] encryptedSessionKey = encKey.CipherValue.Length > 0
                ? encKey.CipherValue
                : encKey.KeyPacket;
            if (encryptedSessionKey.Length == 0)
                continue;
            byte[] sessionKey;
            try
            {
                sessionKey = _keyProvider.DecryptSessionKey(encryptedSessionKey, encKey.KeyId);
            }
            catch (Exception ex) when (ex is CryptographicException
                                           or InvalidOperationException
                                           or NotSupportedException)
            {
                continue;
            }

            try
            {
                if (string.Equals(
                        info.AlgorithmName,
                        OdfEncryption.Aes256GcmAlgorithmUri,
                        StringComparison.Ordinal))
                {
                    byte[] deflatedPackage = OdfWholesomeEncryption.DecryptGcm(ciphertext, sessionKey);
                    return OdfWholesomeEncryption.Inflate(deflatedPackage, loadOptions);
                }

                byte[] decryptedBytes = DecryptAes256Cbc(ciphertext, sessionKey, info.InitialisationVector);

                // 早期 OdfKit 版本直接加密未壓縮資料，並誤用 #openpgp 作為內容演算法名稱。
                // 此分支只保留讀取相容；新輸出一律使用 AES-256-CBC 與 deflate。
                if (string.Equals(info.AlgorithmName, OdfEncryption.OpenPgpAlgorithmUri, StringComparison.Ordinal))
                {
                    if (!ChecksumMatchesOrUnverifiable(decryptedBytes, info))
                        continue;
                    return decryptedBytes;
                }

                if (!ChecksumMatchesOrUnverifiable(decryptedBytes, info))
                    continue;
                return Inflate(decryptedBytes, loadOptions.MaxEntrySize);
            }
            catch (CryptographicException)
            {
                continue;
            }
            finally
            {
                Array.Clear(sessionKey, 0, sessionKey.Length);
            }
        }

        throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfOpenPgpCryptographyProvider_OpenpgpDecryptionFailedUnable"));
    }

    /// <summary>
    /// 驗證解密結果是否符合 <see cref="OdfEncryptionInfo"/> 宣告的總和檢查碼。錯誤的 session key
    /// 約有 1/256 機率仍湊出合法 PKCS7 padding，僅靠 unpadding 失敗不足以判定金鑰錯誤；
    /// 未宣告或不支援的檢查碼類型不在此層否決，交由封裝層驗證。
    /// </summary>
    private static bool ChecksumMatchesOrUnverifiable(byte[] plaintext, OdfEncryptionInfo info)
    {
        if (info.Checksum is null || info.Checksum.Length == 0 || string.IsNullOrWhiteSpace(info.ChecksumType))
            return true;

        try
        {
            return OdfEncryption.ByteArrayEquals(OdfEncryption.ComputeHash(plaintext, info.ChecksumType), info.Checksum);
        }
        catch (NotSupportedException)
        {
            return true;
        }
    }

    /// <summary>
    /// Performs the Encrypt operation.
    /// 執行 Encrypt 作業。
    /// </summary>
    /// <inheritdoc />
    public byte[] Encrypt(byte[] plaintext, string entryPath, OdfSaveOptions saveOptions, out OdfEncryptionInfo info)
    {
        if (saveOptions.OpenPgpRecipients.Count == 0)
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_RecipientCannotBeEmpty"));

        byte[] sessionKey = new byte[32];
        byte[] iv = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(sessionKey);
            rng.GetBytes(iv);
        }

        try
        {
            List<OdfOpenPgpEncryptedKeyInfo> encryptedKeys = EncryptSessionKeyForRecipients(sessionKey, saveOptions);
            return EncryptEntry(plaintext, sessionKey, iv, encryptedKeys, out info);
        }
        finally
        {
            Array.Clear(sessionKey, 0, sessionKey.Length);
        }
    }

    /// <summary>
    /// 以單一 package session key 加密全部項目，符合 ODF 的 package-wide PGP key transport。
    /// </summary>
    internal void EncryptPackage(OdfPackage package, OdfSaveOptions saveOptions)
    {
        if (saveOptions.OpenPgpRecipients.Count == 0)
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_RecipientCannotBeEmpty"));

        byte[] sessionKey = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(sessionKey);

        try
        {
            List<OdfOpenPgpEncryptedKeyInfo> encryptedKeys = EncryptSessionKeyForRecipients(sessionKey, saveOptions);
            foreach (OdfPackageEntry entry in package.Entries.Values)
            {
                if (entry.Name == "mimetype" || entry.Name.StartsWith("META-INF/", StringComparison.Ordinal))
                    continue;

                byte[] plaintext;
                using (Stream stream = entry.OpenReader())
                using (var buffer = new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    plaintext = buffer.ToArray();
                }

                byte[] iv = new byte[16];
                using (var rng = RandomNumberGenerator.Create())
                    rng.GetBytes(iv);

                byte[] ciphertext = EncryptEntry(plaintext, sessionKey, iv, encryptedKeys, out OdfEncryptionInfo info);
                entry.SetContent(ciphertext);
                entry.EncryptionInfo = info;
                entry.IsCompressed = false;
            }
        }
        finally
        {
            Array.Clear(sessionKey, 0, sessionKey.Length);
        }
    }

    private static byte[] EncryptEntry(
        byte[] plaintext,
        byte[] sessionKey,
        byte[] iv,
        IReadOnlyList<OdfOpenPgpEncryptedKeyInfo> encryptedKeys,
        out OdfEncryptionInfo info)
    {
        byte[] compressedPlaintext = Deflate(plaintext);
        byte[] ciphertext = EncryptAes256Cbc(compressedPlaintext, sessionKey, iv);

        info = new OdfEncryptionInfo
        {
            AlgorithmName = OdfEncryption.Aes256AlgorithmUri,
            InitialisationVector = iv,
            ChecksumType = OdfEncryption.Sha256OneKilobyteChecksumUri,
            Checksum = OdfEncryption.ComputeHash(
                compressedPlaintext,
                OdfEncryption.Sha256OneKilobyteChecksumUri),
            KeyDerivationName = "PGP",
            PlaintextSize = plaintext.Length
        };

        foreach (OdfOpenPgpEncryptedKeyInfo encryptedKey in encryptedKeys)
        {
            info.OpenPgpEncryptedKeys.Add(new OdfOpenPgpEncryptedKeyInfo
            {
                KeyId = encryptedKey.KeyId,
                Recipient = encryptedKey.Recipient,
                AlgorithmName = encryptedKey.AlgorithmName,
                KeyPacket = (byte[])encryptedKey.KeyPacket.Clone(),
                CipherValue = (byte[])encryptedKey.CipherValue.Clone()
            });
        }

        return ciphertext;
    }

    private List<OdfOpenPgpEncryptedKeyInfo> EncryptSessionKeyForRecipients(
        byte[] sessionKey,
        OdfSaveOptions saveOptions)
    {
        var encryptedKeys = new List<OdfOpenPgpEncryptedKeyInfo>(saveOptions.OpenPgpRecipients.Count);
        foreach (OdfOpenPgpRecipient recipient in saveOptions.OpenPgpRecipients)
        {
            byte[] cipherValue = (byte[])_keyProvider.EncryptSessionKey(sessionKey, recipient).Clone();
            encryptedKeys.Add(new OdfOpenPgpEncryptedKeyInfo
            {
                KeyId = recipient.KeyId,
                Recipient = recipient.Recipient,
                AlgorithmName = OdfEncryption.OpenPgpKeyTransportAlgorithmUri,
                CipherValue = cipherValue
            });
        }

        return encryptedKeys;
    }

    private static byte[] Deflate(byte[] plaintext)
    {
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionMode.Compress, leaveOpen: true))
            deflate.Write(plaintext, 0, plaintext.Length);
        return output.ToArray();
    }

    private static byte[] Inflate(byte[] compressedPlaintext, long maxEntrySize)
    {
        using var input = new MemoryStream(compressedPlaintext);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        byte[] buffer = new byte[8192];
        long total = 0;
        int read;
        while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > maxEntrySize)
            {
                throw new CryptographicException(
                    OdfLocalizer.GetMessage("Err_OdfEncryption_UnzippedItemSizeExceeds", maxEntrySize));
            }

            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }

    private static byte[] EncryptAes256Cbc(byte[] plaintext, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            cs.Write(plaintext, 0, plaintext.Length);
            cs.FlushFinalBlock();
        }
        return ms.ToArray();
    }

    private static byte[] DecryptAes256Cbc(byte[] ciphertext, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
        {
            cs.Write(ciphertext, 0, ciphertext.Length);
            cs.FlushFinalBlock();
        }
        return ms.ToArray();
    }
}

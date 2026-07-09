using System;
using System.IO;
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
            if (encKey.KeyPacket.Length == 0)
                continue;
            byte[] sessionKey;
            try
            {
                sessionKey = _keyProvider.DecryptSessionKey(encKey.KeyPacket, encKey.KeyId);
            }
            catch (Exception ex) when (ex is CryptographicException
                                           or InvalidOperationException
                                           or NotSupportedException)
            {
                continue;
            }

            try
            {
                return DecryptAes256Cbc(ciphertext, sessionKey, info.InitialisationVector);
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
            byte[] ciphertext = EncryptAes256Cbc(plaintext, sessionKey, iv);

            byte[] checksum;
            using (var sha = SHA256.Create())
            {
                checksum = sha.ComputeHash(plaintext);
            }

            info = new OdfEncryptionInfo
            {
                AlgorithmName = OdfEncryption.OpenPgpAlgorithmUri,
                InitialisationVector = iv,
                ChecksumType = "SHA256",
                Checksum = checksum,
                KeyDerivationName = string.Empty
            };

            foreach (var recipient in saveOptions.OpenPgpRecipients)
            {
                byte[] encryptedSessionKey = (byte[])_keyProvider.EncryptSessionKey(sessionKey, recipient).Clone();
                info.OpenPgpEncryptedKeys.Add(new OdfOpenPgpEncryptedKeyInfo
                {
                    KeyId = recipient.KeyId,
                    Recipient = recipient.Recipient,
                    AlgorithmName = OdfEncryption.OpenPgpAlgorithmUri,
                    KeyPacket = encryptedSessionKey
                });
            }

            return ciphertext;
        }
        finally
        {
            Array.Clear(sessionKey, 0, sessionKey.Length);
        }
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

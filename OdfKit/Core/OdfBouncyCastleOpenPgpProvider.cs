using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

using OdfKit.Compliance;
namespace OdfKit.Core;

/// <summary>
/// 以 BouncyCastle.Cryptography 為底層，實作 ODF 1.3 OpenPGP Session Key 加解密。
/// 支援 RSA（PKCS#1 v1.5 盲簽）、ElGamal 及 ECDH（X25519 / Curve25519 及傳統 EC 曲線）公開金鑰演算法。
/// </summary>
/// <remarks>
/// Native AOT：BouncyCastle 演算法實作依賴執行期組裝探索，裁剪時須保留完整 <c>Org.BouncyCastle</c> 組件或改用靜態註冊表。
/// </remarks>
#if !NETSTANDARD2_0
[RequiresUnreferencedCode("BouncyCastle OpenPGP 路徑尚未完成 trimming 相容；Native AOT 需保留 Org.BouncyCastle 組件。")]
[RequiresDynamicCode("BouncyCastle 密碼學實作依賴動態程式碼產生。")]
#endif
public sealed partial class OdfBouncyCastleOpenPgpProvider : IOdfOpenPgpKeyProvider
{
    private readonly byte[]? _secretKeyRingData;
    private readonly Func<long, char[]>? _passphraseProvider;

    private static readonly SecureRandom s_rng = new();

    // Curve25519 OID 1.3.6.1.4.1.3029.1.5.1 的 DER 內容位元組（不含 tag 0x06 與 length）
    private static readonly byte[] s_curve25519OidBytes =
        new byte[] { 0x2B, 0x06, 0x01, 0x04, 0x01, 0x97, 0x55, 0x01, 0x05, 0x01 };

    // RFC 6637 §8 KDF Param 中固定的 "Anonymous Sender    "（20 bytes，含尾部空格）
    private static readonly byte[] s_ecdhAnonSender =
        new byte[]
        {
            0x41, 0x6E, 0x6F, 0x6E, 0x79, 0x6D, 0x6F, 0x75,
            0x73, 0x20, 0x53, 0x65, 0x6E, 0x64, 0x65, 0x72,
            0x20, 0x20, 0x20, 0x20,
        };

    /// <summary>
    /// 建立僅支援加密（無法解密）的提供者實例。
    /// </summary>
    public OdfBouncyCastleOpenPgpProvider() { }

    /// <summary>
    /// 建立同時支援加密與解密的提供者實例。
    /// </summary>
    /// <param name="secretKeyRingData">
    /// OpenPGP 私鑰環的原始位元組，支援 ASCII Armor 與二進位格式。
    /// </param>
    /// <param name="passphraseProvider">
    /// 根據金鑰 ID（long）提供解鎖密語的委派函式；空陣列表示無密語保護。
    /// </param>
    /// <exception cref="ArgumentNullException">任一參數為 null 時擲出</exception>
    public OdfBouncyCastleOpenPgpProvider(byte[] secretKeyRingData, Func<long, char[]> passphraseProvider)
    {
        _secretKeyRingData = secretKeyRingData ?? throw new ArgumentNullException(nameof(secretKeyRingData));
        _passphraseProvider = passphraseProvider ?? throw new ArgumentNullException(nameof(passphraseProvider));
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="sessionKey"/> 或 <paramref name="recipient"/> 為 null</exception>
    /// <exception cref="ArgumentException">收件人未提供公鑰資料</exception>
    /// <exception cref="InvalidOperationException">公鑰資料中找不到可用於加密的子金鑰</exception>
    /// <exception cref="NotSupportedException">公鑰演算法不受支援（僅支援 RSA 及 ElGamal）</exception>
    public byte[] EncryptSessionKey(byte[] sessionKey, OdfOpenPgpRecipient recipient)
    {
        if (sessionKey is null)
            throw new ArgumentNullException(nameof(sessionKey));
        if (recipient is null)
            throw new ArgumentNullException(nameof(recipient));
        if (recipient.PublicKey is not { Length: > 0 })
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_RecipientCannotBeEmpty"), nameof(recipient));

        PgpPublicKey encKey = FindEncryptionSubkey(recipient.PublicKey);
        byte[] payload = BuildSessionKeyPayload(sessionKey);
        byte[][] encMpis = EncryptPayload(encKey, payload);

        var pkt = new PublicKeyEncSessionPacket(encKey.KeyId, encKey.Algorithm, encMpis);
        return pkt.GetEncoded();
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">此實例以純加密模式建立，無法執行解密</exception>
    /// <exception cref="ArgumentNullException"><paramref name="encryptedKeyPacket"/> 為 null</exception>
    /// <exception cref="CryptographicException">
    /// PKESK 封包格式有誤、私鑰解鎖失敗、演算法不受支援，或總和檢查碼驗證失敗時擲出。
    /// </exception>
    public byte[] DecryptSessionKey(byte[] encryptedKeyPacket, string keyId)
    {
        if (_secretKeyRingData is null || _passphraseProvider is null)
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_ProviderInstanceProvidePrivate"));
        if (encryptedKeyPacket is null)
            throw new ArgumentNullException(nameof(encryptedKeyPacket));

        (long pkeskKeyId, PublicKeyAlgorithmTag algorithm, byte[][] encMpis) = DecodePkeskPacket(encryptedKeyPacket);

        PgpSecretKey secretKey = FindSecretKey(pkeskKeyId);
        char[] passphrase = _passphraseProvider(pkeskKeyId)
            ?? throw new ArgumentException(
                "密語提供者回傳了 null；應回傳空陣列 (Array.Empty<char>()) 表示無密語保護。",
                nameof(_passphraseProvider));
        PgpPrivateKey privateKey;
        try
        {
            privateKey = secretKey.ExtractPrivateKey(passphrase);
        }
        catch (Exception ex) when (ex is not CryptographicException)
        {
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_OpenpgpPrivateKeyUnlocking"), ex);
        }
        finally
        {
            Array.Clear(passphrase, 0, passphrase.Length);
        }

        byte[] payload = DecryptPayload(privateKey, secretKey.PublicKey, algorithm, encMpis);
        return ExtractAndVerifySessionKey(payload);
    }

    #region Session Key Payload

    private static byte[] ExtractAndVerifySessionKey(byte[] payload)
    {
        // 承載資料 = [1 位元組演算法][N 位元組金鑰][2 位元組總和檢查碼]
        if (payload.Length < 4)
            throw new CryptographicException(
                $"解密後的 Session Key Payload 長度不足（{payload.Length} 位元組）。");

        int keyLen = payload.Length - 3;
        byte[] sessionKey = new byte[keyLen];
        Array.Copy(payload, 1, sessionKey, 0, keyLen);

        int expected = 0;
        foreach (byte b in sessionKey)
            expected += b;
        expected &= 0xFFFF;

        int actual = (payload[payload.Length - 2] << 8) | payload[payload.Length - 1];
        if (expected != actual)
            throw new CryptographicException(
                $"Session Key 總和檢查碼驗證失敗（預期 0x{expected:X4}，實際 0x{actual:X4}）。");

        return sessionKey;
    }

    #endregion

}

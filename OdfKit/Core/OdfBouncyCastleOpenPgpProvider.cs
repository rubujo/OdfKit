using System;
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
/// Provides the BouncyCastle-backed OpenPGP session key provider.
/// 以 BouncyCastle.Cryptography 為底層，實作 ODF 1.3 OpenPGP Session Key 加解密。
/// </summary>
/// <remarks>
/// Uses statically referenced cryptographic primitives and supports trimming and Native AOT.
/// 使用靜態參照的密碼學基元，支援 trimming 與 Native AOT。
/// </remarks>
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
    /// Performs odf bouncy castle open pgp provider.
    /// 建立僅支援加密（無法解密）的提供者實例。
    /// </summary>
    public OdfBouncyCastleOpenPgpProvider() { }

    /// <summary>
    /// Performs odf bouncy castle open pgp provider.
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

    /// <summary>
    /// Encrypts session key.
    /// 加密 Session Key。
    /// </summary>
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="sessionKey"/> 或 <paramref name="recipient"/> 為 null</exception>
    /// <exception cref="ArgumentException">收件人未提供公鑰資料</exception>
    /// <exception cref="InvalidOperationException">公鑰資料中找不到可用於加密的子金鑰</exception>
    /// <exception cref="NotSupportedException">公鑰演算法不受支援（僅支援 RSA 及 ElGamal）</exception>
    public byte[] EncryptSessionKey(byte[] sessionKey, OdfOpenPgpRecipient recipient)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(sessionKey, nameof(sessionKey));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(recipient, nameof(recipient));
        if (recipient.PublicKey is not { Length: > 0 })
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_RecipientCannotBeEmpty"), nameof(recipient));

        PgpPublicKey encKey = FindEncryptionSubkey(recipient.PublicKey);
        using var output = new MemoryStream();
        var encryptedGenerator = new PgpEncryptedDataGenerator(
            SymmetricKeyAlgorithmTag.Aes256,
            withIntegrityPacket: true,
            s_rng);
        encryptedGenerator.AddMethod(encKey);

        using (Stream encryptedStream = encryptedGenerator.Open(output, new byte[1 << 16]))
        {
            var literalGenerator = new PgpLiteralDataGenerator();
            using Stream literalStream = literalGenerator.Open(
                encryptedStream,
                PgpLiteralData.Binary,
                PgpLiteralDataGenerator.Console,
                sessionKey.Length,
                new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            literalStream.Write(sessionKey, 0, sessionKey.Length);
        }

        return output.ToArray();
    }

    /// <summary>
    /// Decrypts session key.
    /// 解密 Session Key。
    /// </summary>
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
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(encryptedKeyPacket, nameof(encryptedKeyPacket));

        try
        {
            byte[]? messageKey = TryDecryptOpenPgpMessage(encryptedKeyPacket);
            if (messageKey is not null)
                return messageKey;
        }
        catch (Exception ex) when (ex is IOException or PgpException)
        {
            // 早期 OdfKit 版本只儲存 PKESK packet；若輸入不是完整 OpenPGP message，
            // 退回既有的 RFC 4880 packet 解碼路徑。
        }

        (long pkeskKeyId, PublicKeyAlgorithmTag algorithm, byte[][] encMpis) =
            DecodePkeskPacket(encryptedKeyPacket);

        PgpSecretKey secretKey = FindSecretKey(pkeskKeyId);
        char[] passphrase = _passphraseProvider(pkeskKeyId)
            ?? throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_PassphraseProviderReturnedNull"));
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

    private byte[]? TryDecryptOpenPgpMessage(byte[] encryptedMessage)
    {
        byte[]? aeadMessageKey = TryDecryptAeadMessage(encryptedMessage);
        if (aeadMessageKey is not null)
            return aeadMessageKey;

        using var input = new MemoryStream(encryptedMessage, writable: false);
        using Stream decoder = PgpUtilities.GetDecoderStream(input);
        var factory = new PgpObjectFactory(decoder);
        object? first = factory.NextPgpObject();
        PgpEncryptedDataList? encryptedDataList = first as PgpEncryptedDataList
            ?? factory.NextPgpObject() as PgpEncryptedDataList;
        if (encryptedDataList is null)
            return null;

        foreach (PgpPublicKeyEncryptedData encryptedData in encryptedDataList.GetEncryptedDataObjects())
        {
            PgpSecretKey secretKey;
            try
            {
                secretKey = FindSecretKey(encryptedData.KeyId);
            }
            catch (CryptographicException)
            {
                continue;
            }

            char[] passphrase = _passphraseProvider!(encryptedData.KeyId)
                ?? throw new ArgumentException(
                    OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_PassphraseProviderReturnedNull"));
            PgpPrivateKey privateKey;
            try
            {
                privateKey = secretKey.ExtractPrivateKey(passphrase);
            }
            catch (Exception ex) when (ex is not CryptographicException)
            {
                throw new CryptographicException(
                    OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_OpenpgpPrivateKeyUnlocking"),
                    ex);
            }
            finally
            {
                Array.Clear(passphrase, 0, passphrase.Length);
            }

            using Stream clear = encryptedData.GetDataStream(privateKey);
            var clearFactory = new PgpObjectFactory(clear);
            object? message = clearFactory.NextPgpObject();
            if (message is PgpCompressedData compressedData)
            {
                using Stream compressedStream = compressedData.GetDataStream();
                message = new PgpObjectFactory(compressedStream).NextPgpObject();
            }

            if (message is not PgpLiteralData literalData)
                continue;

            using Stream literalStream = literalData.GetInputStream();
            using var output = new MemoryStream();
            literalStream.CopyTo(output);
            byte[] sessionKey = output.ToArray();
            if (sessionKey.Length != 32)
            {
                Array.Clear(sessionKey, 0, sessionKey.Length);
                continue;
            }

            if (encryptedData.IsIntegrityProtected() && !encryptedData.Verify())
            {
                Array.Clear(sessionKey, 0, sessionKey.Length);
                continue;
            }

            return sessionKey;
        }

        return null;
    }

    private byte[] DecryptPkeskSessionKey(byte[] encodedPkesk)
    {
        (long pkeskKeyId, PublicKeyAlgorithmTag algorithm, byte[][] encMpis) =
            DecodePkeskPacket(encodedPkesk);
        PgpSecretKey secretKey = FindSecretKey(pkeskKeyId);
        char[] passphrase = _passphraseProvider!(pkeskKeyId)
            ?? throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_PassphraseProviderReturnedNull"));
        try
        {
            PgpPrivateKey privateKey = secretKey.ExtractPrivateKey(passphrase);
            byte[] payload = DecryptPayload(privateKey, secretKey.PublicKey, algorithm, encMpis);
            return ExtractAndVerifySessionKey(payload);
        }
        finally
        {
            Array.Clear(passphrase, 0, passphrase.Length);
        }
    }

    #region Session Key Payload

    private static byte[] ExtractAndVerifySessionKey(byte[] payload)
    {
        try
        {
            // 承載資料 = [1 位元組演算法][N 位元組金鑰][2 位元組總和檢查碼]
            if (payload.Length < 4)
                throw new CryptographicException(
                    OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_SessionKeyPayloadTooShort", payload.Length));

            int keyLen = payload.Length - 3;
            byte[] sessionKey = new byte[keyLen];
            Array.Copy(payload, 1, sessionKey, 0, keyLen);

            int expected = 0;
            foreach (byte b in sessionKey)
                expected += b;
            expected &= 0xFFFF;

            int actual = (payload[payload.Length - 2] << 8) | payload[payload.Length - 1];
            if (expected != actual)
            {
                // 驗證失敗時先抹除已複製的金鑰位元組；
                // 錯誤訊息不得包含預期／實際總和檢查碼值，避免洩漏金鑰位元組總和資訊。
                Array.Clear(sessionKey, 0, sessionKey.Length);
                throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_SessionKeyChecksumMismatch"));
            }

            return sessionKey;
        }
        finally
        {
            // 承載資料含明文 Session Key 副本，離開前一律抹除。
            Array.Clear(payload, 0, payload.Length);
        }
    }

    #endregion

}

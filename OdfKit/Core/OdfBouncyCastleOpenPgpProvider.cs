using System;
using System.IO;
using System.Security.Cryptography;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace OdfKit.Core;

/// <summary>
/// 以 BouncyCastle.Cryptography 為底層，實作 ODF 1.3 OpenPGP Session Key 加解密。
/// 支援 RSA（PKCS#1 v1.5 盲簽）及 ElGamal 公開金鑰演算法。
/// ECDH（Ed25519 / X25519）計畫於後續版本支援。
/// </summary>
public sealed class OdfBouncyCastleOpenPgpProvider : IOdfOpenPgpKeyProvider
{
    private readonly byte[]? _secretKeyRingData;
    private readonly Func<long, char[]>? _passphraseProvider;

    private static readonly SecureRandom s_rng = new();

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
    /// <exception cref="ArgumentNullException">任一參數為 null 時擲出。</exception>
    public OdfBouncyCastleOpenPgpProvider(byte[] secretKeyRingData, Func<long, char[]> passphraseProvider)
    {
        _secretKeyRingData = secretKeyRingData ?? throw new ArgumentNullException(nameof(secretKeyRingData));
        _passphraseProvider = passphraseProvider ?? throw new ArgumentNullException(nameof(passphraseProvider));
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="sessionKey"/> 或 <paramref name="recipient"/> 為 null。</exception>
    /// <exception cref="ArgumentException">收件人未提供公鑰資料。</exception>
    /// <exception cref="InvalidOperationException">公鑰資料中找不到可用於加密的子金鑰。</exception>
    /// <exception cref="NotSupportedException">公鑰演算法不受支援（僅支援 RSA 及 ElGamal）。</exception>
    public byte[] EncryptSessionKey(byte[] sessionKey, OdfOpenPgpRecipient recipient)
    {
        if (sessionKey is null) throw new ArgumentNullException(nameof(sessionKey));
        if (recipient is null) throw new ArgumentNullException(nameof(recipient));
        if (recipient.PublicKey is not { Length: > 0 })
            throw new ArgumentException("收件人公鑰不可為空。", nameof(recipient));

        PgpPublicKey encKey = FindEncryptionSubkey(recipient.PublicKey);
        byte[] payload = BuildSessionKeyPayload(sessionKey);
        byte[][] encMpis = EncryptPayload(encKey, payload);

        var pkt = new PublicKeyEncSessionPacket(encKey.KeyId, encKey.Algorithm, encMpis);
        return pkt.GetEncoded();
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">此實例以純加密模式建立，無法執行解密。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="encryptedKeyPacket"/> 為 null。</exception>
    /// <exception cref="CryptographicException">
    /// PKESK 封包格式有誤、私鑰解鎖失敗、演算法不受支援，或總和檢查碼驗證失敗時擲出。
    /// </exception>
    public byte[] DecryptSessionKey(byte[] encryptedKeyPacket, string keyId)
    {
        if (_secretKeyRingData is null || _passphraseProvider is null)
            throw new InvalidOperationException("此提供者實例未提供私鑰資料，無法執行解密操作。");
        if (encryptedKeyPacket is null) throw new ArgumentNullException(nameof(encryptedKeyPacket));

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
            throw new CryptographicException("OpenPGP 私鑰解鎖失敗，請確認密語是否正確。", ex);
        }
        finally
        {
            Array.Clear(passphrase, 0, passphrase.Length);
        }

        byte[] payload = DecryptPayload(privateKey, algorithm, encMpis);
        return ExtractAndVerifySessionKey(payload);
    }

    // ── 私有輔助方法 ──────────────────────────────────────────────────────────

    private static PgpPublicKey FindEncryptionSubkey(byte[] publicKeyData)
    {
        using var stream = PgpUtilities.GetDecoderStream(new MemoryStream(publicKeyData));
        var factory = new PgpObjectFactory(stream);

        PgpObject? obj;
        while ((obj = factory.NextPgpObject()) is not null)
        {
            if (obj is PgpPublicKeyRing ring)
            {
                PgpPublicKey? key = FindEncryptionKey(ring);
                if (key is not null) return key;
            }
        }
        throw new InvalidOperationException("在公鑰資料中找不到可用於加密的子金鑰。");
    }

    private static PgpPublicKey? FindEncryptionKey(PgpPublicKeyRing ring)
    {
        PgpPublicKey? master = null;
        foreach (PgpPublicKey k in ring.GetPublicKeys())
        {
            if (!k.IsEncryptionKey) continue;
            if (!k.IsMasterKey) return k; // 優先選用專用加密子金鑰
            master ??= k;
        }
        return master;
    }

    private static byte[][] EncryptPayload(PgpPublicKey encKey, byte[] payload)
    {
        switch (encKey.Algorithm)
        {
            case PublicKeyAlgorithmTag.RsaEncrypt:
            case PublicKeyAlgorithmTag.RsaGeneral:
            {
                var cipher = new Pkcs1Encoding(new RsaBlindedEngine());
                cipher.Init(true, new ParametersWithRandom(encKey.GetKey(), s_rng));
                byte[] enc = cipher.ProcessBlock(payload, 0, payload.Length);
                return new byte[][] { enc };
            }
            case PublicKeyAlgorithmTag.ElGamalEncrypt:
            case PublicKeyAlgorithmTag.ElGamalGeneral:
            {
                // BC 2.6.x 將 byte[][] 序列化為相鄰的原始位元組，
                // ElGamal 輸出 c1+c2 是單一連續區塊，直接存為一個元素。
                var cipher = new Pkcs1Encoding(new ElGamalEngine());
                cipher.Init(true, new ParametersWithRandom(encKey.GetKey(), s_rng));
                byte[] enc = cipher.ProcessBlock(payload, 0, payload.Length);
                return new byte[][] { enc };
            }
            default:
                throw new NotSupportedException(
                    $"不支援的 OpenPGP 公鑰演算法：{encKey.Algorithm}。目前支援 RSA 及 ElGamal。");
        }
    }

    private static byte[] BuildSessionKeyPayload(byte[] sessionKey)
    {
        // RFC 4880 §5.1：[1 byte algo][key bytes][2 bytes checksum]
        // algo 9 = AES with 256-bit key（RFC 4880 §9.2）
        int checksum = 0;
        foreach (byte b in sessionKey)
            checksum += b;
        checksum &= 0xFFFF;

        byte[] payload = new byte[1 + sessionKey.Length + 2];
        payload[0] = 9; // RFC 4880 §9.2：9 = AES-256，與 OdfEncryption 的 session key 演算法一致
        sessionKey.CopyTo(payload, 1);
        payload[payload.Length - 2] = (byte)(checksum >> 8);
        payload[payload.Length - 1] = (byte)(checksum & 0xFF);
        return payload;
    }

    private static (long KeyId, PublicKeyAlgorithmTag Algorithm, byte[][] EncData) DecodePkeskPacket(byte[] bytes)
    {
        // BC 2.6.x 的 PublicKeyEncSessionPacket.GetEncoded() 輸出格式：
        // [packet header][version(1)][keyId(8)][algo(1)][raw encrypted bytes]
        // 原始加密資料直接寫入，不含 MPI bit count header。
        int pos = 0;
        if (bytes.Length < 12)
            throw new CryptographicException("PKESK 封包位元組太短。");

        int hdr = bytes[pos++];
        if ((hdr & 0x80) == 0)
            throw new CryptographicException("非有效的 PGP 封包標頭（bit 7 必須為 1）。");

        bool isNew = (hdr & 0x40) != 0;
        int tag, bodyLen;

        if (isNew)
        {
            tag = hdr & 0x3F;
            int b1 = bytes[pos++];
            if (b1 < 192)
            {
                bodyLen = b1;
            }
            else if (b1 < 224)
            {
                bodyLen = ((b1 - 192) << 8) + bytes[pos++] + 192;
            }
            else if (b1 == 255)
            {
                bodyLen = (bytes[pos] << 24) | (bytes[pos + 1] << 16) | (bytes[pos + 2] << 8) | bytes[pos + 3];
                pos += 4;
            }
            else
            {
                throw new CryptographicException("不支援 PGP partial body length 格式。");
            }
        }
        else
        {
            tag = (hdr >> 2) & 0x0F;
            int lenType = hdr & 0x03;
            switch (lenType)
            {
                case 0: bodyLen = bytes[pos++]; break;
                case 1: bodyLen = (bytes[pos++] << 8) | bytes[pos++]; break;
                case 2:
                    bodyLen = (bytes[pos] << 24) | (bytes[pos + 1] << 16) | (bytes[pos + 2] << 8) | bytes[pos + 3];
                    pos += 4;
                    break;
                default: bodyLen = bytes.Length - pos; break;
            }
        }

        if (tag != 1)
            throw new CryptographicException($"預期 PKESK 封包（標籤 1），但收到標籤 {tag}。");

        int bodyStart = pos;
        pos++; // version（跳過）

        long keyId = 0;
        for (int i = 0; i < 8; i++)
            keyId = (keyId << 8) | bytes[pos++];

        var algorithm = (PublicKeyAlgorithmTag)bytes[pos++];

        // 剩餘全部位元組即為加密後的 Session Key 資料（RSA 為單一區塊，ElGamal 為 c1+c2）
        int dataLen = bodyStart + bodyLen - pos;
        if (dataLen <= 0)
            throw new CryptographicException("PKESK 封包內無加密金鑰資料。");

        byte[] encData = new byte[dataLen];
        Array.Copy(bytes, pos, encData, 0, dataLen);

        return (keyId, algorithm, new byte[][] { encData });
    }

    private PgpSecretKey FindSecretKey(long pkeskKeyId)
    {
        using var stream = PgpUtilities.GetDecoderStream(new MemoryStream(_secretKeyRingData!));
        PgpSecretKeyRingBundle bundle;
        try
        {
            bundle = new PgpSecretKeyRingBundle(stream);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("無法解析 OpenPGP 私鑰環資料。", ex);
        }

        PgpSecretKey? key = bundle.GetSecretKey(pkeskKeyId);
        if (key is null)
            throw new InvalidOperationException(
                $"在私鑰環中找不到 Key ID 0x{pkeskKeyId:X16} 的私鑰。");
        return key;
    }

    private static byte[] DecryptPayload(PgpPrivateKey privateKey, PublicKeyAlgorithmTag algorithm, byte[][] encData)
    {
        // encData[0] 包含完整的加密資料：RSA 為單一密文區塊，ElGamal 為 c1+c2 相鄰位元組。
        byte[] enc = encData[0];
        switch (algorithm)
        {
            case PublicKeyAlgorithmTag.RsaEncrypt:
            case PublicKeyAlgorithmTag.RsaGeneral:
            {
                var cipher = new Pkcs1Encoding(new RsaBlindedEngine());
                cipher.Init(false, privateKey.Key);
                return cipher.ProcessBlock(enc, 0, enc.Length);
            }
            case PublicKeyAlgorithmTag.ElGamalEncrypt:
            case PublicKeyAlgorithmTag.ElGamalGeneral:
            {
                // ElGamalEngine 內部以 modulus 大小折半分割 c1 與 c2
                var cipher = new Pkcs1Encoding(new ElGamalEngine());
                cipher.Init(false, privateKey.Key);
                return cipher.ProcessBlock(enc, 0, enc.Length);
            }
            default:
                throw new NotSupportedException(
                    $"不支援的 OpenPGP 公鑰演算法：{algorithm}。目前支援 RSA 及 ElGamal。");
        }
    }

    private static byte[] ExtractAndVerifySessionKey(byte[] payload)
    {
        // payload = [1 byte algo][N bytes key][2 bytes checksum]
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
}

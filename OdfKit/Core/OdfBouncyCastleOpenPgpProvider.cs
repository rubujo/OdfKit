using System;
using System.IO;
using System.Security.Cryptography;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Generators;

namespace OdfKit.Core;

/// <summary>
/// 以 BouncyCastle.Cryptography 為底層，實作 ODF 1.3 OpenPGP Session Key 加解密。
/// 支援 RSA（PKCS#1 v1.5 盲簽）、ElGamal 及 ECDH（X25519 / Curve25519 及傳統 EC 曲線）公開金鑰演算法。
/// </summary>
public sealed class OdfBouncyCastleOpenPgpProvider : IOdfOpenPgpKeyProvider
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

        byte[] payload = DecryptPayload(privateKey, secretKey.PublicKey, algorithm, encMpis);
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
            case PublicKeyAlgorithmTag.ECDH:
            {
                AsymmetricKeyParameter rawKey = encKey.GetKey();
                byte[] fingerprint = encKey.GetFingerprint();

                if (rawKey is X25519PublicKeyParameters x25519Pub)
                {
                    // ── X25519 路徑 ──────────────────────────────────────────────────
                    var kpGen = new X25519KeyPairGenerator();
                    kpGen.Init(new X25519KeyGenerationParameters(s_rng));
                    AsymmetricCipherKeyPair ephKp = kpGen.GenerateKeyPair();
                    var ephPriv = (X25519PrivateKeyParameters)ephKp.Private;
                    var ephPub  = (X25519PublicKeyParameters)ephKp.Public;

                    byte[] sharedZ = new byte[32];
                    ephPriv.GenerateSecret(x25519Pub, sharedZ, 0);

                    byte[] kek     = ComputeEcdhKdf(sharedZ, s_curve25519OidBytes, fingerprint);
                    byte[] padded  = ApplyEcdhPkcs5Padding(payload);
                    byte[] wrapped = AesKeyWrap128(kek, padded);

                    // 封包格式：0x40 || ephPub(32 bytes) || wrappedLen(1 byte) || wrapped
                    byte[] ephPubRaw = ephPub.GetEncoded(); // 32 bytes
                    byte[] result = new byte[1 + 32 + 1 + wrapped.Length];
                    result[0] = 0x40;
                    Array.Copy(ephPubRaw, 0, result, 1, 32);
                    result[33] = (byte)wrapped.Length;
                    Array.Copy(wrapped, 0, result, 34, wrapped.Length);
                    return new byte[][] { result };
                }
                else if (rawKey is ECPublicKeyParameters ecPub)
                {
                    // ── 傳統 EC 曲線路徑（P-256 / P-384 / P-521）────────────────────
                    var kpGen2 = new ECKeyPairGenerator();
                    kpGen2.Init(new ECKeyGenerationParameters(ecPub.Parameters, s_rng));
                    AsymmetricCipherKeyPair ephKp2 = kpGen2.GenerateKeyPair();
                    var ephPriv2 = (ECPrivateKeyParameters)ephKp2.Private;
                    var ephPub2  = (ECPublicKeyParameters)ephKp2.Public;

                    var agreement = new ECDHCBasicAgreement();
                    agreement.Init(ephPriv2);
                    byte[] sharedZ2 = agreement.CalculateAgreement(ecPub).ToByteArrayUnsigned();

                    byte[] oidBytes2 = GetEcCurveOidBytes(ecPub);
                    byte[] kek2      = ComputeEcdhKdf(sharedZ2, oidBytes2, fingerprint);
                    byte[] padded2   = ApplyEcdhPkcs5Padding(payload);
                    byte[] wrapped2  = AesKeyWrap128(kek2, padded2);

                    // 封包格式：0x04 || X || Y（未壓縮點）|| wrappedLen(1 byte) || wrapped
                    byte[] point = ephPub2.Q.GetEncoded(false);
                    byte[] result2 = new byte[point.Length + 1 + wrapped2.Length];
                    Array.Copy(point, 0, result2, 0, point.Length);
                    result2[point.Length] = (byte)wrapped2.Length;
                    Array.Copy(wrapped2, 0, result2, point.Length + 1, wrapped2.Length);
                    return new byte[][] { result2 };
                }
                throw new NotSupportedException(
                    $"不支援的 ECDH 金鑰類型：{rawKey.GetType().Name}。目前支援 X25519 及傳統 EC 曲線。");
            }
            default:
                throw new NotSupportedException(
                    $"不支援的 OpenPGP 公鑰演算法：{encKey.Algorithm}。目前支援 RSA、ElGamal 及 ECDH。");
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

    private static byte[] DecryptPayload(PgpPrivateKey privateKey, PgpPublicKey publicKey, PublicKeyAlgorithmTag algorithm, byte[][] encData)
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
            case PublicKeyAlgorithmTag.ECDH:
            {
                byte[] fingerprint = publicKey.GetFingerprint(); // 真實 fingerprint，KDF 必須與加密端一致
                AsymmetricKeyParameter key = privateKey.Key;

                if (key is X25519PrivateKeyParameters x25519Priv)
                {
                    // ── X25519 路徑 ──────────────────────────────────────────────────
                    // 格式：0x40 || ephPub(32) || wrappedLen(1) || wrapped
                    if (enc.Length < 35)
                        throw new CryptographicException("ECDH X25519 封包資料長度不足。");
                    byte[] ephPubRaw = new byte[32];
                    Array.Copy(enc, 1, ephPubRaw, 0, 32); // 略過首位 0x40
                    var ephPub = new X25519PublicKeyParameters(ephPubRaw, 0);

                    byte[] sharedZ = new byte[32];
                    x25519Priv.GenerateSecret(ephPub, sharedZ, 0);

                    byte[] kek = ComputeEcdhKdf(sharedZ, s_curve25519OidBytes, fingerprint);
                    int wrapLen = enc[33];
                    byte[] wrapped = new byte[wrapLen];
                    Array.Copy(enc, 34, wrapped, 0, wrapLen);
                    return RemoveEcdhPkcs5Padding(AesKeyUnwrap128(kek, wrapped));
                }
                else if (key is ECPrivateKeyParameters ecPriv)
                {
                    // ── 傳統 EC 曲線路徑 ─────────────────────────────────────────────
                    // 格式：0x04 || X || Y（點長度由曲線 field size 決定）|| wrappedLen(1) || wrapped
                    int coordBytes = (ecPriv.Parameters.Curve.FieldSize + 7) / 8;
                    int pointLen   = 1 + 2 * coordBytes; // 0x04 + X + Y
                    if (enc.Length < pointLen + 1)
                        throw new CryptographicException("ECDH 傳統曲線封包資料長度不足。");

                    byte[] pointBytes = new byte[pointLen];
                    Array.Copy(enc, 0, pointBytes, 0, pointLen);
                    var ephPoint = ecPriv.Parameters.Curve.DecodePoint(pointBytes);
                    var ephPub   = new ECPublicKeyParameters(ephPoint, ecPriv.Parameters);

                    var agreement = new ECDHCBasicAgreement();
                    agreement.Init(ecPriv);
                    byte[] sharedZ = agreement.CalculateAgreement(ephPub).ToByteArrayUnsigned();

                    byte[] oidBytes = GetEcCurveOidBytes(ecPriv);
                    byte[] kek      = ComputeEcdhKdf(sharedZ, oidBytes, fingerprint);
                    int wrapLen     = enc[pointLen];
                    byte[] wrapped  = new byte[wrapLen];
                    Array.Copy(enc, pointLen + 1, wrapped, 0, wrapLen);
                    return RemoveEcdhPkcs5Padding(AesKeyUnwrap128(kek, wrapped));
                }
                throw new NotSupportedException($"不支援的 ECDH 金鑰類型：{key.GetType().Name}。");
            }
            default:
                throw new NotSupportedException(
                    $"不支援的 OpenPGP 公鑰演算法：{algorithm}。目前支援 RSA、ElGamal 及 ECDH。");
        }
    }

    /// <summary>
    /// RFC 6637 §8 KDF：SHA-256( 00 00 00 01 || Z || oidLen || oid || 0x12 || 03 || 08 || 07 || anonSender || fingerprint )，
    /// 取前 16 bytes 作為 AES-128 KEK。
    /// </summary>
    private static byte[] ComputeEcdhKdf(byte[] sharedZ, byte[] curveOidBytes, byte[] fingerprint)
    {
        // 總長度：4（counter）+ Z + 1（oidLen）+ oid + 4（alg/kdf/hash/sym）+ 20（anonSender）+ fingerprint
        byte[] input = new byte[4 + sharedZ.Length + 1 + curveOidBytes.Length + 4 + 20 + fingerprint.Length];
        int p = 0;
        input[p++] = 0x00; input[p++] = 0x00; input[p++] = 0x00; input[p++] = 0x01;
        Array.Copy(sharedZ, 0, input, p, sharedZ.Length);                 p += sharedZ.Length;
        input[p++] = (byte)curveOidBytes.Length;
        Array.Copy(curveOidBytes, 0, input, p, curveOidBytes.Length);     p += curveOidBytes.Length;
        input[p++] = 0x12; // ECDiffieHellman = 18
        input[p++] = 0x03; // KDF params count
        input[p++] = 0x08; // SHA-256
        input[p++] = 0x07; // AES-128
        Array.Copy(s_ecdhAnonSender, 0, input, p, 20);                    p += 20;
        Array.Copy(fingerprint, 0, input, p, fingerprint.Length);

        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(input);
        byte[] kek = new byte[16];
        Array.Copy(hash, 0, kek, 0, 16);
        return kek;
    }

    /// <summary>從 ECKeyParameters 取得具名曲線的 OID 內容位元組（不含 DER tag/length）。</summary>
    private static byte[] GetEcCurveOidBytes(ECKeyParameters ecKey)
    {
        if (ecKey.PublicKeyParamSet is { } oid)
        {
            byte[] der = oid.GetDerEncoded(); // 格式：0x06 || len || content
            byte[] content = new byte[der.Length - 2];
            Array.Copy(der, 2, content, 0, content.Length);
            return content;
        }
        throw new NotSupportedException("ECDH KDF 需要具名曲線，不支援匿名曲線參數。");
    }

    /// <summary>
    /// RFC 6637 AES key wrap 前置：以 PKCS#5（PKCS#7）填充至 8 bytes 的倍數。
    /// padLen ∈ [1, 8]，確保無論原始長度為何都加 padding。
    /// </summary>
    private static byte[] ApplyEcdhPkcs5Padding(byte[] data)
    {
        int padLen = 8 - (data.Length % 8); // 結果 ∈ [1, 8]
        byte[] padded = new byte[data.Length + padLen];
        Array.Copy(data, 0, padded, 0, data.Length);
        for (int i = data.Length; i < padded.Length; i++)
            padded[i] = (byte)padLen;
        return padded;
    }

    /// <summary>移除 PKCS#5 填充並驗證填充位元組。</summary>
    private static byte[] RemoveEcdhPkcs5Padding(byte[] data)
    {
        if (data.Length == 0)
            throw new CryptographicException("PKCS#5 解填充時資料為空。");
        int padLen = data[data.Length - 1];
        if (padLen < 1 || padLen > 8 || padLen > data.Length)
            throw new CryptographicException($"PKCS#5 填充長度無效：{padLen}。");
        for (int i = data.Length - padLen; i < data.Length; i++)
            if (data[i] != (byte)padLen)
                throw new CryptographicException("PKCS#5 填充位元組驗證失敗。");
        byte[] result = new byte[data.Length - padLen];
        Array.Copy(data, 0, result, 0, result.Length);
        return result;
    }

    private static byte[] AesKeyWrap128(byte[] kek, byte[] plaintext)
    {
        var engine = new Rfc3394WrapEngine(new AesEngine());
        engine.Init(true, new KeyParameter(kek));
        return engine.Wrap(plaintext, 0, plaintext.Length);
    }

    private static byte[] AesKeyUnwrap128(byte[] kek, byte[] ciphertext)
    {
        var engine = new Rfc3394WrapEngine(new AesEngine());
        engine.Init(false, new KeyParameter(kek));
        return engine.Unwrap(ciphertext, 0, ciphertext.Length);
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

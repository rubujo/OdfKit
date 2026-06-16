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

namespace OdfKit.Core;

public sealed partial class OdfBouncyCastleOpenPgpProvider
{
    #region OpenPGP Encryption

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
                if (key is not null)
                    return key;
            }
        }
        throw new InvalidOperationException("在公鑰資料中找不到可用於加密的子金鑰。");
    }

    private static PgpPublicKey? FindEncryptionKey(PgpPublicKeyRing ring)
    {
        PgpPublicKey? master = null;
        foreach (PgpPublicKey k in ring.GetPublicKeys())
        {
            if (!k.IsEncryptionKey)
                continue;
            if (!k.IsMasterKey)
                return k; // 優先選用專用加密子金鑰
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
                        var ephPub = (X25519PublicKeyParameters)ephKp.Public;

                        byte[] sharedZ = new byte[32];
                        ephPriv.GenerateSecret(x25519Pub, sharedZ, 0);

                        byte[] kek = ComputeEcdhKdf(sharedZ, s_curve25519OidBytes, fingerprint);
                        byte[] padded = ApplyEcdhPkcs5Padding(payload);
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
                        var ephPub2 = (ECPublicKeyParameters)ephKp2.Public;

                        var agreement = new ECDHCBasicAgreement();
                        agreement.Init(ephPriv2);
                        byte[] sharedZ2 = agreement.CalculateAgreement(ecPub).ToByteArrayUnsigned();

                        byte[] oidBytes2 = GetEcCurveOidBytes(ecPub);
                        byte[] kek2 = ComputeEcdhKdf(sharedZ2, oidBytes2, fingerprint);
                        byte[] padded2 = ApplyEcdhPkcs5Padding(payload);
                        byte[] wrapped2 = AesKeyWrap128(kek2, padded2);

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

    #endregion
}

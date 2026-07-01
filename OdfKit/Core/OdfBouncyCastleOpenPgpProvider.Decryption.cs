using System;
using System.IO;
using System.Security.Cryptography;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

using OdfKit.Compliance;
namespace OdfKit.Core;

public sealed partial class OdfBouncyCastleOpenPgpProvider
{
    #region OpenPGP Decryption

    private static (long KeyId, PublicKeyAlgorithmTag Algorithm, byte[][] EncData) DecodePkeskPacket(byte[] bytes)
    {
        try
        {
            return DecodePkeskPacketCore(bytes);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException or ArgumentException)
        {
            // 任何攔截失誤的越界存取（例如標頭宣告的長度前綴超出封包實際位元組數）一律轉換為
            // CryptographicException，維持本方法「格式有誤時擲出 CryptographicException」的例外契約，
            // 不讓未經驗證的輸入以非預期例外型別外洩。
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_PkeskPacketBytesToo"), ex);
        }
    }

    private static (long KeyId, PublicKeyAlgorithmTag Algorithm, byte[][] EncData) DecodePkeskPacketCore(byte[] bytes)
    {
        // BC 2.6.x 的 PublicKeyEncSessionPacket.GetEncoded() 輸出格式：
        // [封包標頭][版本 (1)][金鑰識別碼 (8)][演算法 (1)][原始加密位元組]
        // 原始加密資料直接寫入，不含 MPI bit count header。
        int pos = 0;
        if (bytes.Length < 12)
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_PkeskPacketBytesToo"));

        int hdr = bytes[pos++];
        if ((hdr & 0x80) == 0)
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_InvalidPgpPacketHeader"));

        bool isNew = (hdr & 0x40) != 0;
        int tag;
        long bodyLen;

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
                bodyLen = ((uint)bytes[pos] << 24) | ((uint)bytes[pos + 1] << 16) | ((uint)bytes[pos + 2] << 8) | bytes[pos + 3];
                pos += 4;
            }
            else
            {
                throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_PgpPartialBodyLength"));
            }
        }
        else
        {
            tag = (hdr >> 2) & 0x0F;
            int lenType = hdr & 0x03;
            switch (lenType)
            {
                case 0:
                    bodyLen = bytes[pos++];
                    break;
                case 1:
                    bodyLen = (bytes[pos++] << 8) | bytes[pos++];
                    break;
                case 2:
                    bodyLen = ((uint)bytes[pos] << 24) | ((uint)bytes[pos + 1] << 16) | ((uint)bytes[pos + 2] << 8) | bytes[pos + 3];
                    pos += 4;
                    break;
                default:
                    bodyLen = bytes.Length - pos;
                    break;
            }
        }

        if (tag != 1)
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_ExpectedPkeskPacketLabel", tag));

        int bodyStart = pos;

        // 表頭宣告的內文長度必須完整落在實際位元組陣列範圍內，且至少容納
        // version(1) + keyId(8) + algorithm(1) 共 10 位元組的固定尾端結構；
        // 以 long 運算避免 bodyStart + bodyLen 以 int 相加時發生溢位。
        if (bodyLen < 10 || (long)bodyStart + bodyLen > bytes.Length)
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_PkeskPacketBytesToo"));

        pos++; // version（跳過）

        long keyId = 0;
        for (int i = 0; i < 8; i++)
            keyId = (keyId << 8) | bytes[pos++];

        var algorithm = (PublicKeyAlgorithmTag)bytes[pos++];

        // 剩餘全部位元組即為加密後的 Session Key 資料（RSA 為單一區塊，ElGamal 為 c1+c2）
        int dataLen = (int)(bodyStart + bodyLen - pos);
        if (dataLen <= 0)
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_NoEncryptionKeyInformation"));

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
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_UnableParseOpenpgpPrivate"), ex);
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
                            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_EcdhX25519PacketData"));
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
                        int pointLen = 1 + 2 * coordBytes; // 0x04 + X + Y
                        if (enc.Length < pointLen + 1)
                            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_EcdhTraditionalCurvePacket"));

                        byte[] pointBytes = new byte[pointLen];
                        Array.Copy(enc, 0, pointBytes, 0, pointLen);
                        var ephPoint = ecPriv.Parameters.Curve.DecodePoint(pointBytes);
                        var ephPub = new ECPublicKeyParameters(ephPoint, ecPriv.Parameters);

                        var agreement = new ECDHCBasicAgreement();
                        agreement.Init(ecPriv);
                        byte[] sharedZ = agreement.CalculateAgreement(ephPub).ToByteArrayUnsigned();

                        byte[] oidBytes = GetEcCurveOidBytes(ecPriv);
                        byte[] kek = ComputeEcdhKdf(sharedZ, oidBytes, fingerprint);
                        int wrapLen = enc[pointLen];
                        byte[] wrapped = new byte[wrapLen];
                        Array.Copy(enc, pointLen + 1, wrapped, 0, wrapLen);
                        return RemoveEcdhPkcs5Padding(AesKeyUnwrap128(kek, wrapped));
                    }
                    throw new NotSupportedException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_UnsupportedEcdhKeyType", key.GetType().Name));
                }
            default:
                throw new NotSupportedException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_UnsupportedPublicKeyAlgorithm", algorithm));
        }
    }

    #endregion
}

using System;
using System.IO;
using System.Security.Cryptography;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

using OdfKit.Compliance;
namespace OdfKit.Core;

public sealed partial class OdfBouncyCastleOpenPgpProvider
{
    #region ECDH Primitives

    /// <summary>
    /// RFC 6637 §8 KDF：SHA-256( 00 00 00 01 || Z || oidLen || oid || 0x12 || 03 || 08 || 07 || anonSender || fingerprint )，
    /// 取前 16 bytes 作為 AES-128 KEK。
    /// </summary>
    private static byte[] ComputeEcdhKdf(byte[] sharedZ, byte[] curveOidBytes, byte[] fingerprint)
    {
        // 總長度：4（counter）+ Z + 1（oidLen）+ oid + 4（alg/kdf/hash/sym）+ 20（anonSender）+ fingerprint
        byte[] input = new byte[4 + sharedZ.Length + 1 + curveOidBytes.Length + 4 + 20 + fingerprint.Length];
        int p = 0;
        input[p++] = 0x00;
        input[p++] = 0x00;
        input[p++] = 0x00;
        input[p++] = 0x01;
        Array.Copy(sharedZ, 0, input, p, sharedZ.Length);
        p += sharedZ.Length;
        input[p++] = (byte)curveOidBytes.Length;
        Array.Copy(curveOidBytes, 0, input, p, curveOidBytes.Length);
        p += curveOidBytes.Length;
        input[p++] = 0x12; // ECDiffieHellman = 18
        input[p++] = 0x03; // KDF params count
        input[p++] = 0x08; // SHA-256
        input[p++] = 0x07; // AES-128
        Array.Copy(s_ecdhAnonSender, 0, input, p, 20);
        p += 20;
        Array.Copy(fingerprint, 0, input, p, fingerprint.Length);

        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(input);
        byte[] kek = new byte[16];
        Array.Copy(hash, 0, kek, 0, 16);
        return kek;
    }

    /// <summary>
    /// 從 ECKeyParameters 取得具名曲線的 OID 內容位元組（不含 DER tag/length）
    /// </summary>
    private static byte[] GetEcCurveOidBytes(ECKeyParameters ecKey)
    {
        if (ecKey.PublicKeyParamSet is { } oid)
        {
            byte[] der = oid.GetDerEncoded(); // 格式：0x06 || len || content
            byte[] content = new byte[der.Length - 2];
            Array.Copy(der, 2, content, 0, content.Length);
            return content;
        }
        throw new NotSupportedException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_EcdhKdfRequiresNamed"));
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

    /// <summary>
    /// 移除 PKCS#5 填充並驗證填充位元組
    /// </summary>
    private static byte[] RemoveEcdhPkcs5Padding(byte[] data)
    {
        if (data.Length == 0)
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_Pkcs5DataEmpty"));
        int padLen = data[data.Length - 1];
        if (padLen < 1 || padLen > 8 || padLen > data.Length)
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_InvalidPkcs5Padding", padLen));
        for (int i = data.Length - padLen; i < data.Length; i++)
            if (data[i] != (byte)padLen)
                throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfBouncyCastleOpenPgpProvider_Pkcs5PaddingByte"));
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

    #endregion
}

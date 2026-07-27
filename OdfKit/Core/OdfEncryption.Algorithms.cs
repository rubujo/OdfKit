using System;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

using OdfKit.Compliance;
namespace OdfKit.Core;
/// <summary>
/// Provides the OdfEncryption API.
/// 提供 OdfEncryption API。
/// </summary>

public static partial class OdfEncryption
{
    #region Hash & Cipher Primitives

    /// <summary>
    /// Computes hash.
    /// 計算資料的雜湊值；`SHA1/1K` 與 `#sha1-1k`／`#sha256-1k` 依 ODF 規範只涵蓋前 1024 個位元組。
    /// </summary>
    /// <param name="data">輸入資料的位元組陣列</param>
    /// <param name="checksumType">總和檢查碼的類型（例如 `#sha256-1k`、`SHA1/1K`、SHA256 或 SHA1）</param>
    /// <returns>雜湊值位元組陣列</returns>
    public static byte[] ComputeHash(byte[] data, string checksumType)
    {
        bool isSha256OneKilobyte = string.Equals(checksumType, Sha256OneKilobyteChecksumUri, StringComparison.Ordinal)
            || string.Equals(checksumType, "SHA256/1K", StringComparison.OrdinalIgnoreCase);

        bool isSha1OneKilobyte = string.Equals(checksumType, Sha1OneKilobyteChecksumUri, StringComparison.Ordinal)
            || string.Equals(checksumType, Sha1OneKilobyteChecksumName, StringComparison.OrdinalIgnoreCase);

        bool isSha256 = isSha256OneKilobyte
            || string.Equals(checksumType, "SHA256", StringComparison.OrdinalIgnoreCase)
            || string.Equals(checksumType, "sha-256", StringComparison.OrdinalIgnoreCase)
            || string.Equals(checksumType, "http://www.w3.org/2000/09/xmldsig#sha256", StringComparison.Ordinal)
            || string.Equals(checksumType, "http://www.w3.org/2001/04/xmlenc#sha256", StringComparison.Ordinal);

        bool isSha1 = isSha1OneKilobyte
            || string.Equals(checksumType, "SHA1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(checksumType, "sha-1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(checksumType, "http://www.w3.org/2000/09/xmldsig#sha1", StringComparison.Ordinal);

        byte[] digestInput = data;
        if ((isSha256OneKilobyte || isSha1OneKilobyte) && data.Length > OneKilobyteChecksumLength)
        {
            digestInput = new byte[OneKilobyteChecksumLength];
            Buffer.BlockCopy(data, 0, digestInput, 0, OneKilobyteChecksumLength);
        }

        if (isSha256)
        {
            return global::OdfKit.Internal.OdfHashHelper.Sha256(digestInput);
        }
        else if (isSha1)
        {
            return global::OdfKit.Internal.OdfHashHelper.Sha1(digestInput);
        }
        else
        {
            throw new NotSupportedException(OdfLocalizer.GetMessage("Err_OdfEncryption_UnsupportedChecksumType", checksumType));
        }
    }

    /// <summary>
    /// Performs byte array equals.
    /// 比較兩個位元組陣列是否相等。
    /// </summary>
    /// <param name="a">第一個位元組陣列</param>
    /// <param name="b">第二個位元組陣列</param>
    /// <returns>若兩者相等，則為 <see langword="true"/> ；否則為 <see langword="false"/></returns>
    public static bool ByteArrayEquals(byte[]? a, byte[]? b)
    {
        if (a is null || b is null)
            return a == b;
        if (a.Length != b.Length)
            return false;
#if NET5_0_OR_GREATER
        return CryptographicOperations.FixedTimeEquals(a, b);
#else
        // netstandard2.0：CryptographicOperations 不存在，以 XOR 累加模擬恆定時間比較
        int result = 0;
        for (int i = 0; i < a.Length; i++)
            result |= a[i] ^ b[i];
        return result == 0;
#endif
    }

    /// <summary>
    /// 判斷 `manifest:algorithm-name` 是否為 ODF 傳統的 `Blowfish CFB` 演算法。
    /// </summary>
    internal static bool IsBlowfishCfbAlgorithm(string? algorithmName) =>
        string.Equals(algorithmName, BlowfishAlgorithmUri, StringComparison.Ordinal)
        || string.Equals(algorithmName, BlowfishAlgorithmName, StringComparison.Ordinal);

    /// <summary>
    /// 判斷 `manifest:algorithm-name` 是否為早期 OdfKit 版本寫出的非規範 Blowfish CBC 宣告。
    /// </summary>
    internal static bool IsLegacyBlowfishCbcAlgorithm(string? algorithmName) =>
        string.Equals(algorithmName, BlowfishCbcLegacyAlgorithmUri, StringComparison.Ordinal);

    /// <summary>
    /// 依 W3C XML Encryption §5.2 的 padding 規則移除填充：最後一個位元組是填充長度，
    /// 其餘填充位元組的值未定義。PKCS#7 是本規則的特例，因此同一段程式可同時處理兩者。
    /// </summary>
    private static byte[] RemoveXmlEncryptionPadding(byte[] plaintext, int blockSize)
    {
        if (plaintext.Length == 0)
            return plaintext;

        int paddingLength = plaintext[plaintext.Length - 1];
        if (paddingLength <= 0 || paddingLength > blockSize || paddingLength > plaintext.Length)
        {
            // 正確金鑰解出的區塊，最後一個位元組必然落在 1..blockSize；超出範圍代表金鑰或密文有誤。
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfEncryption_InvalidDecryptionFailedSum"));
        }

        byte[] unpadded = new byte[plaintext.Length - paddingLength];
        Buffer.BlockCopy(plaintext, 0, unpadded, 0, unpadded.Length);
        return unpadded;
    }

    /// <summary>
    /// ODF 傳統 `Blowfish CFB` 的回饋寬度。
    /// </summary>
    /// <remarks>
    /// 規範文字（ODF 1.0～1.4 Part 2 §4.16.1）寫「8-bit CFB」，但被它標準化的 OpenOffice.org
    /// 實作用的是整個 64 位元區塊回饋：LibreOffice 的 `sal/rtl/cipher.cxx` 對
    /// `rtl_Cipher_ModeStream` 呼叫 OpenSSL `EVP_bf_cfb()`（即 `EVP_bf_cfb64()`），其自有 fallback
    /// `BF_updateCFB` 也是每 8 個位元組重新加密一次 IV、再逐位元組 XOR，同樣是 CFB-64。
    /// 既有檔案都是這個形狀，因此 OdfKit 依實作而非規範字面。
    /// </remarks>
    private const int BlowfishCipherFeedbackBits = 64;

    /// <summary>
    /// 以 ODF 傳統 `Blowfish CFB`（64 位元回饋）解密；CFB 是串流模式，不使用填充。
    /// </summary>
    private static byte[] DecryptBlowfishCfb(byte[] ciphertext, byte[] key, byte[] iv)
    {
        var cipher = new BufferedBlockCipher(new CfbBlockCipher(new BlowfishEngine(), BlowfishCipherFeedbackBits));
        cipher.Init(false, new ParametersWithIV(new KeyParameter(key), iv));
        return cipher.DoFinal(ciphertext);
    }

    /// <summary>
    /// 以 ODF 傳統 `Blowfish CFB`（64 位元回饋）加密。
    /// </summary>
    private static byte[] EncryptBlowfishCfb(byte[] plaintext, byte[] key, byte[] iv)
    {
        var cipher = new BufferedBlockCipher(new CfbBlockCipher(new BlowfishEngine(), BlowfishCipherFeedbackBits));
        cipher.Init(true, new ParametersWithIV(new KeyParameter(key), iv));
        return cipher.DoFinal(plaintext);
    }

    private static byte[] DecryptBlowfishCbc(byte[] ciphertext, byte[] key, byte[] iv)
    {
        var engine = new BlowfishEngine();
        var cipher = new CbcBlockCipher(engine);
        cipher.Init(false, new ParametersWithIV(new KeyParameter(key), iv));
        byte[] plaintext = new byte[ciphertext.Length];
        for (int i = 0; i < ciphertext.Length; i += 8)
        {
            cipher.ProcessBlock(ciphertext, i, plaintext, i);
        }

        if (plaintext.Length == 0)
            return plaintext;
        int paddingLen = plaintext[plaintext.Length - 1];
        if (paddingLen > 0 && paddingLen <= 8 && paddingLen <= plaintext.Length)
        {
            byte acc = 0;
            for (int i = plaintext.Length - paddingLen; i < plaintext.Length; i++)
            {
                acc |= (byte)(plaintext[i] ^ paddingLen);
            }
            bool valid = (acc == 0);
            if (valid)
            {
                byte[] unpadded = new byte[plaintext.Length - paddingLen];
                Buffer.BlockCopy(plaintext, 0, unpadded, 0, unpadded.Length);
                return unpadded;
            }
        }
        return plaintext;
    }

    #endregion
}

using System;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;

using OdfKit.Compliance;
namespace OdfKit.Core;

public static partial class OdfEncryption
{
    #region Hash & Cipher Primitives

    /// <summary>
    /// 計算資料的雜湊值。
    /// </summary>
    /// <param name="data">輸入資料的位元組陣列</param>
    /// <param name="checksumType">總和檢查碼的類型（例如 SHA256 或 SHA1）</param>
    /// <returns>雜湊值位元組陣列</returns>
    public static byte[] ComputeHash(byte[] data, string checksumType)
    {
        bool isSha256 = string.Equals(checksumType, "SHA256", StringComparison.OrdinalIgnoreCase)
            || string.Equals(checksumType, "sha-256", StringComparison.OrdinalIgnoreCase)
            || string.Equals(checksumType, "http://www.w3.org/2000/09/xmldsig#sha256", StringComparison.Ordinal)
            || string.Equals(checksumType, "http://www.w3.org/2001/04/xmlenc#sha256", StringComparison.Ordinal);

        bool isSha1 = string.Equals(checksumType, "SHA1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(checksumType, "sha-1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(checksumType, "http://www.w3.org/2000/09/xmldsig#sha1", StringComparison.Ordinal);

        if (isSha256)
        {
            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(data);
            }
        }
        else if (isSha1)
        {
            using (var sha = SHA1.Create())
            {
                return sha.ComputeHash(data);
            }
        }
        else
        {
            throw new NotSupportedException(OdfLocalizer.GetMessage("Err_OdfEncryption_UnsupportedChecksumType", checksumType));
        }
    }

    /// <summary>
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

    private static byte[] EncryptBlowfishCbc(byte[] plaintext, byte[] key, byte[] iv)
    {
        var engine = new BlowfishEngine();
        var cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(engine), new Pkcs7Padding());
        cipher.Init(true, new ParametersWithIV(new KeyParameter(key), iv));
        byte[] output = new byte[cipher.GetOutputSize(plaintext.Length)];
        int len = cipher.ProcessBytes(plaintext, 0, plaintext.Length, output, 0);
        int finalLen = cipher.DoFinal(output, len);
        byte[] result = new byte[len + finalLen];
        Buffer.BlockCopy(output, 0, result, 0, result.Length);
        return result;
    }

    #endregion
}

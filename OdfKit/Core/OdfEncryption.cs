using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;

namespace OdfKit.Core;

/// <summary>
/// 提供 ODF 封裝檔案加密與解密操作的實作。
/// </summary>
public static partial class OdfEncryption
{
    /// <summary>
    /// AES-256 加密演算法的識別 URI。
    /// </summary>
    public const string Aes256AlgorithmUri = "http://www.w3.org/2001/04/xmlenc#aes256-cbc";

    /// <summary>
    /// Blowfish 加密演算法的識別 URI。
    /// </summary>
    public const string BlowfishAlgorithmUri = "http://www.w3.org/2001/04/xmldsig-more#blowfish-cbc";

    /// <summary>
    /// OpenPGP 加密演算法的識別 URI。
    /// </summary>
    public const string OpenPgpAlgorithmUri = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0#openpgp";

    /// <summary>
    /// AES-256-GCM 加密演算法的識別 URI。
    /// </summary>
    public const string Aes256GcmAlgorithmUri = "http://www.w3.org/2009/xmlenc11#aes256-gcm";

    /// <summary>
    /// Argon2id 金鑰衍生函數的識別 URI。
    /// </summary>
    public const string Argon2idDerivationUri = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0#argon2id";

    /// <summary>
    /// 同時進行 Argon2id 衍生運算的上限，避免高併發解密耗盡 ThreadPool（PERF-4k）。
    /// </summary>
    private static readonly int Argon2MaxConcurrentOperations = Math.Max(1, Environment.ProcessorCount / 2);

    private static readonly SemaphoreSlim Argon2ConcurrencyGate = new(Argon2MaxConcurrentOperations, Argon2MaxConcurrentOperations);

    /// <summary>
    /// 自訂實作以金鑰為基礎的金鑰衍生函式 PBKDF2，支援 SHA-1 與 SHA-256，確保跨平台行為一致。
    /// </summary>
    /// <param name="password">密碼位元組陣列</param>
    /// <param name="salt">鹽值位元組陣列</param>
    /// <param name="iterations">反覆運算次數</param>
    /// <param name="keyLength">衍生的金鑰長度</param>
    /// <param name="hashName">雜湊演算法名稱</param>
    /// <returns>衍生的金鑰位元組陣列</returns>
    public static byte[] Pbkdf2(byte[] password, byte[] salt, int iterations, int keyLength, string hashName)
    {
        if (salt is null)
        {
            throw new NullReferenceException("Salt 不能為 null。");
        }
        if (iterations > 50000)
        {
            throw new CryptographicException($"PBKDF2 反覆運算次數 {iterations} 超過最大限制 50000。");
        }
        if (keyLength < 0)
        {
            throw new OverflowException("金鑰長度不能為負數。");
        }

        int effectiveIterations = iterations <= 0 ? 1 : iterations;

        string normalizedHashName = hashName.Trim().ToLowerInvariant();
        Org.BouncyCastle.Crypto.IDigest digest;
        if (normalizedHashName is "sha256" or "sha-256"
            or "http://www.w3.org/2000/09/xmldsig#sha256"
            or "http://www.w3.org/2001/04/xmlenc#sha256")
        {
            digest = new Org.BouncyCastle.Crypto.Digests.Sha256Digest();
        }
        else if (normalizedHashName is "sha1" or "sha-1" or "http://www.w3.org/2000/09/xmldsig#sha1")
        {
            digest = new Org.BouncyCastle.Crypto.Digests.Sha1Digest();
        }
        else
        {
            throw new NotSupportedException($"不支援的雜湊演算法：{hashName}");
        }

        var generator = new Pkcs5S2ParametersGenerator(digest);
        generator.Init(password, salt, effectiveIterations);
        var keyParam = (KeyParameter)generator.GenerateDerivedMacParameters(keyLength * 8);
        return keyParam.GetKey();
    }
}

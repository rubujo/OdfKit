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

using OdfKit.Compliance;
namespace OdfKit.Core;

/// <summary>
/// Provides the OdfEncryption API.
/// 提供 ODF 封裝檔案加密與解密操作的實作。
/// </summary>
public static partial class OdfEncryption
{
    /// <summary>
    /// AES-256 加密演算法的識別 URI。
    /// </summary>
    public const string Aes256AlgorithmUri = "http://www.w3.org/2001/04/xmlenc#aes256-cbc";

    /// <summary>
    /// ODF 傳統 Blowfish CFB 加密演算法的識別 URI（ODF 1.0～1.4 Part 2 §4.16.1）；
    /// 回饋寬度依 OpenOffice.org 以來的實作採 64 位元區塊，見 OdfEncryption.Algorithms.cs。
    /// </summary>
    public const string BlowfishAlgorithmUri = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0#blowfish";

    /// <summary>
    /// ODF 傳統 Blowfish CFB 的規範簡短名稱；`manifest:algorithm-name` 允許與 URI 等價使用。
    /// </summary>
    public const string BlowfishAlgorithmName = "Blowfish CFB";

    /// <summary>
    /// 早期 OdfKit 版本寫出的非規範 Blowfish CBC 識別 URI；僅供讀取既有檔案時相容比對。
    /// </summary>
    public const string BlowfishCbcLegacyAlgorithmUri = "http://www.w3.org/2001/04/xmldsig-more#blowfish-cbc";

    /// <summary>
    /// OpenPGP 加密演算法的識別 URI。
    /// </summary>
    public const string OpenPgpAlgorithmUri = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0#openpgp";

    /// <summary>
    /// AES-256-GCM 加密演算法的識別 URI。
    /// </summary>
    public const string Aes256GcmAlgorithmUri = "http://www.w3.org/2009/xmlenc11#aes256-gcm";

    /// <summary>
    /// Argon2id 金鑰衍生函數的識別 URI；與 LibreOffice 25.8+ 的
    /// `OpenDocument-v1.4+libreoffice-manifest-schema.rng` 一致。
    /// </summary>
    public const string Argon2idDerivationUri = "urn:org:documentfoundation:names:experimental:office:manifest:argon2id";

    /// <summary>
    /// ODF 1.5 草案預計採用的 Argon2id 識別 URI；目前只用於讀取相容。
    /// </summary>
    public const string Argon2idOdf15DerivationUri = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.5#argon2id";

    /// <summary>
    /// 早期 OdfKit 版本寫出的非標準 Argon2id 識別 URI；僅供讀取既有檔案時相容比對。
    /// </summary>
    public const string Argon2idLegacyDerivationUri = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0#argon2id";

    /// <summary>
    /// SHA-256／1K 檢查碼的識別 URI；ODF 1.2 起建議新實作採用。
    /// </summary>
    public const string Sha256OneKilobyteChecksumUri = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0#sha256-1k";

    /// <summary>
    /// SHA-1／1K 檢查碼的識別 URI；ODF 1.0～1.1 產出的檔案採用。
    /// </summary>
    public const string Sha1OneKilobyteChecksumUri = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0#sha1-1k";

    /// <summary>
    /// SHA-1／1K 檢查碼的 ODF 規範簡短名稱。
    /// </summary>
    public const string Sha1OneKilobyteChecksumName = "SHA1/1K";

    /// <summary>
    /// 1K 檢查碼所涵蓋的位元組數；ODF 規範定義為壓縮後未加密資料的前 1024 個位元組。
    /// </summary>
    public const int OneKilobyteChecksumLength = 1024;

    /// <summary>
    /// 讀取既有文件時允許的 PBKDF2 反覆運算次數上限，作為 DoS 防線。
    /// 實務值需高於現行實作：LibreOffice 26.x 寫入 100,000 次，OWASP 對 PBKDF2-HMAC-SHA1
    /// 的現行建議為 1,300,000 次，因此上限訂在 10,000,000。
    /// </summary>
    public const int MaxPbkdf2IterationCount = 10_000_000;

    /// <summary>
    /// 寫入新文件時採用的 PBKDF2 反覆運算次數。
    /// </summary>
    public const int DefaultPbkdf2IterationCount = 100_000;

    /// <summary>
    /// Blowfish 傳統加密的金鑰長度（位元組）；`manifest:key-size` 缺席時的規範預設。
    /// </summary>
    public const int BlowfishKeySizeBytes = 16;

    /// <summary>
    /// AES-256 的金鑰長度（位元組）；`manifest:key-size` 缺席時的預設。
    /// </summary>
    public const int Aes256KeySizeBytes = 32;

    /// <summary>
    /// 同時進行 Argon2id 衍生運算的上限，避免高併發解密耗盡 ThreadPool（PERF-4k）。
    /// </summary>
    private static readonly int Argon2MaxConcurrentOperations = Math.Max(1, Environment.ProcessorCount / 2);

    private static readonly SemaphoreSlim Argon2ConcurrencyGate = new(Argon2MaxConcurrentOperations, Argon2MaxConcurrentOperations);

    /// <summary>
    /// Performs pbkdf 2.
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
            throw new NullReferenceException(OdfLocalizer.GetMessage("Err_OdfEncryption_SaltCannotBeEmpty"));
        }
        if (iterations < 1 || iterations > MaxPbkdf2IterationCount)
        {
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfEncryption_NumberPbkdf2IterationsExceeds", iterations, MaxPbkdf2IterationCount));
        }
        if (keyLength < 0)
        {
            throw new OverflowException(OdfLocalizer.GetMessage("Err_OdfEncryption_KeyLengthCannotNegative"));
        }

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
            throw new NotSupportedException(OdfLocalizer.GetMessage("Err_OdfEncryption_UnsupportedHashAlgorithm", hashName));
        }

        var generator = new Pkcs5S2ParametersGenerator(digest);
        generator.Init(password, salt, iterations);
        var keyParam = (KeyParameter)generator.GenerateDerivedMacParameters(keyLength * 8);
        return keyParam.GetKey();
    }
}

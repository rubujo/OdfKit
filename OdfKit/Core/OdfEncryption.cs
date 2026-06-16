using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Paddings;

namespace OdfKit.Core;

/// <summary>
/// 提供 ODF 封裝檔案加密與解密操作的實作。
/// </summary>
public static class OdfEncryption
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

    /// <summary>
    /// 解密單一封裝項目。支援 PBKDF2 搭配 AES/Blowfish 以及 Argon2id 搭配 AES-GCM。
    /// </summary>
    /// <param name="ciphertext">加密的密文資料位元組陣列。</param>
    /// <param name="password">解密密碼。</param>
    /// <param name="algorithmUri">加密演算法的 XML 識別 URI。</param>
    /// <param name="derivationName">金鑰衍生演算法的 XML 識別 URI。</param>
    /// <param name="keySize">金鑰大小（以位元組為單位）。</param>
    /// <param name="iterationCount">金鑰衍生的反覆運算次數。</param>
    /// <param name="salt">金鑰衍生的鹽值（Salt）位元組陣列。</param>
    /// <param name="iv">加密的初始向量（IV）位元組陣列。</param>
    /// <param name="startKeyGenName">初始金鑰產生的演算法名稱（選填）。</param>
    /// <param name="kdfName">金鑰衍生函數的名稱（選填，例如 "argon2id"）。</param>
    /// <param name="argon2T">Argon2id 的時間複雜度/反覆運算次數（選填）。</param>
    /// <param name="argon2M">Argon2id 的記憶體複雜度（單位為 KB，選填）。</param>
    /// <param name="argon2P">Argon2id 的平行度/通道數（選填）。</param>
    /// <returns>解密後的純文字資料位元組陣列。</returns>
    public static byte[] DecryptEntry(
        byte[] ciphertext,
        string password,
        string algorithmUri,
        string derivationName,
        int keySize,
        int iterationCount,
        byte[] salt,
        byte[] iv,
        string? startKeyGenName = null,
        string? kdfName = null,
        int argon2T = 3,
        int argon2M = 65536,
        int argon2P = 4)
    {
        bool isArgon2 = string.Equals(kdfName, "argon2id", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(derivationName, Argon2idDerivationUri, StringComparison.OrdinalIgnoreCase);

        if (!isArgon2 && string.Equals(derivationName, "PBKDF2", StringComparison.OrdinalIgnoreCase) && iterationCount > 50000)
        {
            throw new CryptographicException($"PBKDF2 反覆運算次數 {iterationCount} 超過最大限制 50000。");
        }

        if (algorithmUri != Aes256AlgorithmUri && algorithmUri != BlowfishAlgorithmUri && algorithmUri != Aes256GcmAlgorithmUri)
        {
            throw new NotSupportedException($"不支援的加密演算法： {algorithmUri}。 OdfKit 僅支援標準 AES-256-CBC、Blowfish-CBC 與 AES-256-GCM。");
        }

        if (!isArgon2 && !string.Equals(derivationName, "PBKDF2", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"不支援的金鑰衍生函式： {derivationName}。");
        }

        // 若 startKeyGenName 存在，則衍生密碼位元組
        byte[] pwdBytes;
        if (startKeyGenName is not null)
        {
            byte[] rawPassBytes = Encoding.UTF8.GetBytes(password);
            if (startKeyGenName.EndsWith("#sha256", StringComparison.OrdinalIgnoreCase)
                || string.Equals(startKeyGenName, "sha256", StringComparison.OrdinalIgnoreCase)
                || string.Equals(startKeyGenName, "sha-256", StringComparison.OrdinalIgnoreCase))
            {
                using (var sha = SHA256.Create())
                    pwdBytes = sha.ComputeHash(rawPassBytes);
            }
            else if (startKeyGenName.EndsWith("#sha1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(startKeyGenName, "sha1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(startKeyGenName, "sha-1", StringComparison.OrdinalIgnoreCase))
            {
                using (var sha = SHA1.Create())
                    pwdBytes = sha.ComputeHash(rawPassBytes);
            }
            else
            {
                pwdBytes = rawPassBytes;
            }
        }
        else
        {
            pwdBytes = Encoding.UTF8.GetBytes(password);
        }

        // 決定 PBKDF2 的雜湊名稱
        string hashName = "sha256";
        if (startKeyGenName is not null
            && (startKeyGenName.EndsWith("#sha1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(startKeyGenName, "sha1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(startKeyGenName, "sha-1", StringComparison.OrdinalIgnoreCase)))
        {
            hashName = "sha1";
        }
        else if (algorithmUri == BlowfishAlgorithmUri)
        {
            hashName = "sha1";
        }

        byte[] derivedKey;
        if (isArgon2)
        {
            var builder = new Argon2Parameters.Builder(Argon2Parameters.Argon2id)
                .WithVersion(Argon2Parameters.Version13)
                .WithIterations(argon2T)
                .WithMemoryAsKB(argon2M)
                .WithParallelism(argon2P)
                .WithSalt(salt);

            var generator = new Argon2BytesGenerator();
            generator.Init(builder.Build());
            derivedKey = new byte[keySize];
            generator.GenerateBytes(pwdBytes, derivedKey, 0, derivedKey.Length);
        }
        else
        {
            derivedKey = Pbkdf2(pwdBytes, salt, iterationCount, keySize, hashName);
        }

        if (algorithmUri == Aes256GcmAlgorithmUri)
        {
            try
            {
                var cipher = new GcmBlockCipher(new AesEngine());
                var parameters = new AeadParameters(new KeyParameter(derivedKey), 128, iv);
                cipher.Init(false, parameters);

                byte[] output = new byte[cipher.GetOutputSize(ciphertext.Length)];
                int len = cipher.ProcessBytes(ciphertext, 0, ciphertext.Length, output, 0);
                int finalLen = cipher.DoFinal(output, len);

                byte[] decrypted = new byte[len + finalLen];
                Buffer.BlockCopy(output, 0, decrypted, 0, decrypted.Length);
                return decrypted;
            }
            catch (Exception ex)
            {
                throw new CryptographicException(
                    $"GCM 解密失敗。診斷資訊：isArgon2={isArgon2}, kdfName='{kdfName}', derivationName='{derivationName}', derivedKeyLen={derivedKey?.Length}, ivLen={iv?.Length}, saltLen={salt?.Length}, ciphertextLen={ciphertext?.Length}. 原始錯誤：{ex.Message}", ex);
            }
        }
        else if (algorithmUri == Aes256AlgorithmUri)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = derivedKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor())
                using (var msDecrypt = new MemoryStream())
                {
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Write))
                    {
                        csDecrypt.Write(ciphertext, 0, ciphertext.Length);
                        csDecrypt.FlushFinalBlock();
                    }
                    return msDecrypt.ToArray();
                }
            }
        }
        else
        {
            return DecryptBlowfishCbc(ciphertext, derivedKey, iv);
        }
    }

    /// <summary>
    /// 加密單一封裝項目。支援傳統加密與 AES-GCM 加密，並產生對應的 IV、鹽值與驗證碼。
    /// </summary>
    /// <param name="plaintext">待加密的純文字資料位元組陣列。</param>
    /// <param name="password">加密密碼。</param>
    /// <param name="algorithm">加密演算法類型。</param>
    /// <param name="iv">輸出參數，接收隨機產生的初始向量（IV）位元組陣列。</param>
    /// <param name="salt">輸出參數，接收隨機產生的鹽值（Salt）位元組陣列。</param>
    /// <param name="checksum">輸出參數，接收加密後計算出的驗證碼（Checksum）位元組陣列。</param>
    /// <param name="iterationCount">金鑰衍生的反覆運算次數（預設為 50,000 次）。</param>
    /// <returns>加密後的密文資料位元組陣列。</returns>
    public static byte[] EncryptEntry(
        byte[] plaintext,
        string password,
        OdfEncryptionAlgorithm algorithm,
        out byte[] iv,
        out byte[] salt,
        out byte[] checksum,
        int iterationCount = 50000)
    {
        salt = new byte[algorithm == OdfEncryptionAlgorithm.Aes256Gcm ? 32 : 16];
        iv = new byte[algorithm == OdfEncryptionAlgorithm.Aes256Gcm ? 12 : (algorithm == OdfEncryptionAlgorithm.Aes256 ? 16 : 8)];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
            rng.GetBytes(iv);
        }

        byte[] pwdBytes = Encoding.UTF8.GetBytes(password);
        int keySize = (algorithm == OdfEncryptionAlgorithm.Aes256 || algorithm == OdfEncryptionAlgorithm.Aes256Gcm) ? 32 : 16;

        byte[] derivedKey;
        if (algorithm == OdfEncryptionAlgorithm.Aes256Gcm)
        {
            // 使用 Argon2id 衍生金鑰，參數相容於 LibreOffice 25.8+ loext
            byte[] preHashedPwd;
            using (var sha = SHA256.Create())
                preHashedPwd = sha.ComputeHash(pwdBytes);

            var builder = new Argon2Parameters.Builder(Argon2Parameters.Argon2id)
                .WithVersion(Argon2Parameters.Version13)
                .WithIterations(3)
                .WithMemoryAsKB(65536)
                .WithParallelism(4)
                .WithSalt(salt);

            var generator = new Argon2BytesGenerator();
            generator.Init(builder.Build());
            derivedKey = new byte[keySize];
            generator.GenerateBytes(preHashedPwd, derivedKey, 0, derivedKey.Length);
        }
        else if (algorithm == OdfEncryptionAlgorithm.Aes256)
        {
            byte[] preHashedPwd;
            using (var sha = SHA256.Create())
                preHashedPwd = sha.ComputeHash(pwdBytes);
            derivedKey = Pbkdf2(preHashedPwd, salt, iterationCount, keySize, "sha256");
        }
        else
        {
            byte[] preHashedPwd;
            using (var sha = SHA1.Create())
                preHashedPwd = sha.ComputeHash(pwdBytes);
            derivedKey = Pbkdf2(preHashedPwd, salt, iterationCount, keySize, "sha1");
        }

        byte[] ciphertext;

        if (algorithm == OdfEncryptionAlgorithm.Aes256Gcm)
        {
            var cipher = new GcmBlockCipher(new AesEngine());
            var parameters = new AeadParameters(new KeyParameter(derivedKey), 128, iv);
            cipher.Init(true, parameters);

            byte[] output = new byte[cipher.GetOutputSize(plaintext.Length)];
            int len = cipher.ProcessBytes(plaintext, 0, plaintext.Length, output, 0);
            int finalLen = cipher.DoFinal(output, len);

            // BouncyCastle GcmBlockCipher 會自動將 tag 附在 output 尾端
            ciphertext = output;
        }
        else if (algorithm == OdfEncryptionAlgorithm.Aes256)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = derivedKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                using (var msEncrypt = new MemoryStream())
                {
                    using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        csEncrypt.Write(plaintext, 0, plaintext.Length);
                        csEncrypt.FlushFinalBlock();
                    }
                    ciphertext = msEncrypt.ToArray();
                }
            }
        }
        else
        {
            ciphertext = EncryptBlowfishCbc(plaintext, derivedKey, iv);
        }

        using (var sha = SHA256.Create())
        {
            checksum = sha.ComputeHash(plaintext);
        }

        return ciphertext;
    }

    /// <summary>
    /// 解密指定 ODF 封裝中的所有加密項目。
    /// </summary>
    /// <param name="package">要解密的 ODF 封裝執行個體</param>
    /// <param name="password">解密密碼</param>
    public static void Decrypt(OdfPackage package, string password)
    {
        foreach (var entry in package.Entries.Values)
        {
            if (entry.EncryptionInfo is null) continue;

            byte[] ciphertext;
            using (var stream = entry.OpenReader())
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                ciphertext = ms.ToArray();
            }

            byte[] decryptedPlaintext;

            IOdfCryptographyProvider? cryptoProvider = null;
            if (package.LoadOptions.CryptographyProvider is not null &&
                package.LoadOptions.CryptographyProvider.CanHandle(entry.EncryptionInfo))
            {
                cryptoProvider = package.LoadOptions.CryptographyProvider;
            }
            else if (package.SaveOptions.CryptographyProvider is not null &&
                package.SaveOptions.CryptographyProvider.CanHandle(entry.EncryptionInfo))
            {
                cryptoProvider = package.SaveOptions.CryptographyProvider;
            }

            if (cryptoProvider is not null)
            {
                decryptedPlaintext = cryptoProvider.Decrypt(ciphertext, entry.EncryptionInfo, package.LoadOptions);
            }
            else if (entry.EncryptionInfo.OpenPgpEncryptedKeys.Count > 0 ||
                string.Equals(entry.EncryptionInfo.AlgorithmName, OpenPgpAlgorithmUri, StringComparison.Ordinal))
            {
                throw new NotSupportedException("OpenPGP 加密項目必須透過 IOdfCryptographyProvider 解密。");
            }
            else
            {
                string? kdfName = null;
                if (entry.EncryptionInfo.ExtensionProperties.TryGetValue("kdf-name", out string? kn))
                {
                    kdfName = kn;
                }
                int argon2T = 3;
                if (entry.EncryptionInfo.ExtensionProperties.TryGetValue("argon2-t", out string? tStr) && int.TryParse(tStr, out int tVal))
                {
                    argon2T = tVal;
                }
                int argon2M = 65536;
                if (entry.EncryptionInfo.ExtensionProperties.TryGetValue("argon2-m", out string? mStr) && int.TryParse(mStr, out int mVal))
                {
                    argon2M = mVal;
                }
                int argon2P = 4;
                if (entry.EncryptionInfo.ExtensionProperties.TryGetValue("argon2-p", out string? pStr) && int.TryParse(pStr, out int pVal))
                {
                    argon2P = pVal;
                }

                byte[] decryptedBytes = DecryptEntry(
                    ciphertext,
                    password,
                    entry.EncryptionInfo.AlgorithmName,
                    entry.EncryptionInfo.KeyDerivationName,
                    entry.EncryptionInfo.KeySize,
                    entry.EncryptionInfo.IterationCount,
                    entry.EncryptionInfo.Salt,
                    entry.EncryptionInfo.InitialisationVector,
                    entry.EncryptionInfo.StartKeyGenerationName,
                    kdfName,
                    argon2T,
                    argon2M,
                    argon2P
                );

                bool decompressedSuccessfully = false;
                byte[]? decompressedBytes = null;
                try
                {
                    using (var ms = new MemoryStream(decryptedBytes))
                    using (var deflate = new DeflateStream(ms, CompressionMode.Decompress))
                    using (var outMs = new MemoryStream())
                    {
                        long maxEntrySize = package.LoadOptions.MaxEntrySize;
                        byte[] buffer = new byte[8192];
                        long cumulativeBytes = 0;
                        int bytesRead;
                        while ((bytesRead = deflate.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            cumulativeBytes += bytesRead;
                            if (cumulativeBytes > maxEntrySize)
                            {
                                throw new SecurityException($"解壓縮後的項目大小超過最大限制： {maxEntrySize} 位元組。");
                            }
                            outMs.Write(buffer, 0, bytesRead);
                        }
                        decompressedBytes = outMs.ToArray();
                        decompressedSuccessfully = true;
                    }
                }
                catch (SecurityException)
                {
                    throw;
                }
                catch
                {
                    // 容錯驗證
                }

                if (decompressedSuccessfully && decompressedBytes is not null)
                {
                    byte[] calculatedChecksum = ComputeHash(decompressedBytes, entry.EncryptionInfo.ChecksumType);
                    if (ByteArrayEquals(calculatedChecksum, entry.EncryptionInfo.Checksum))
                    {
                        decryptedPlaintext = decompressedBytes;
                    }
                    else
                    {
                        byte[] rawCalculatedChecksum = ComputeHash(decryptedBytes, entry.EncryptionInfo.ChecksumType);
                        if (ByteArrayEquals(rawCalculatedChecksum, entry.EncryptionInfo.Checksum))
                        {
                            decryptedPlaintext = decryptedBytes;
                        }
                        else
                        {
                            throw new CryptographicException("解密失敗：總和檢查碼不符或密碼無效。");
                        }
                    }
                }
                else
                {
                    byte[] rawCalculatedChecksum = ComputeHash(decryptedBytes, entry.EncryptionInfo.ChecksumType);
                    if (ByteArrayEquals(rawCalculatedChecksum, entry.EncryptionInfo.Checksum))
                    {
                        decryptedPlaintext = decryptedBytes;
                    }
                    else
                    {
                        throw new CryptographicException("解密失敗：總和檢查碼不符或密碼無效。");
                    }
                }
            }

            entry.SetContent(decryptedPlaintext);
            entry.EncryptionInfo = null;
        }
    }

    /// <summary>
    /// 加密指定 ODF 封裝中的所有適用項目。
    /// </summary>
    /// <param name="package">要加密的 ODF 封裝執行個體</param>
    /// <param name="password">加密密碼</param>
    /// <param name="algorithm">加密演算法，預設為 AES-256</param>
    public static void Encrypt(OdfPackage package, string password, OdfEncryptionAlgorithm algorithm = OdfEncryptionAlgorithm.Aes256)
    {
        if (algorithm == OdfEncryptionAlgorithm.OpenPgp && package.SaveOptions.CryptographyProvider is null)
        {
            throw new NotSupportedException("OpenPGP 加密必須透過 IOdfCryptographyProvider 實作。");
        }

        foreach (var entry in package.Entries.Values)
        {
            string name = entry.Name;
            if (name == "mimetype" || name.StartsWith("META-INF/"))
            {
                continue;
            }

            byte[] plaintext;
            using (var stream = entry.OpenReader())
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                plaintext = ms.ToArray();
            }

            byte[] ciphertext;
            OdfEncryptionInfo info;

            if (package.SaveOptions.CryptographyProvider is not null)
            {
                ciphertext = package.SaveOptions.CryptographyProvider.Encrypt(plaintext, name, package.SaveOptions, out info);
            }
            else
            {
                byte[] compressedPlaintext;
                using (var ms = new MemoryStream())
                {
                    using (var deflate = new DeflateStream(ms, CompressionMode.Compress, true))
                    {
                        deflate.Write(plaintext, 0, plaintext.Length);
                    }
                    compressedPlaintext = ms.ToArray();
                }

                byte[] iv;
                byte[] salt;
                ciphertext = EncryptEntry(compressedPlaintext, password, algorithm, out iv, out salt, out _);

                byte[] checksum = ComputeHash(plaintext, "SHA256");

                info = new OdfEncryptionInfo
                {
                    ChecksumType = "SHA256",
                    Checksum = checksum,
                    AlgorithmName = algorithm == OdfEncryptionAlgorithm.Aes256Gcm
                        ? Aes256GcmAlgorithmUri
                        : (algorithm == OdfEncryptionAlgorithm.Aes256 ? Aes256AlgorithmUri : BlowfishAlgorithmUri),
                    InitialisationVector = iv,
                    KeyDerivationName = "PBKDF2",
                    KeySize = (algorithm == OdfEncryptionAlgorithm.Aes256 || algorithm == OdfEncryptionAlgorithm.Aes256Gcm) ? 32 : 16,
                    IterationCount = 50000,
                    Salt = salt
                };

                if (algorithm == OdfEncryptionAlgorithm.Aes256Gcm)
                {
                    info.StartKeyGenerationName = "http://www.w3.org/2000/09/xmldsig#sha256";
                    info.StartKeySize = 32;
                    info.ExtensionProperties["kdf-name"] = "argon2id";
                    info.ExtensionProperties["argon2-t"] = "3";
                    info.ExtensionProperties["argon2-m"] = "65536";
                    info.ExtensionProperties["argon2-p"] = "4";
                }
                else if (algorithm == OdfEncryptionAlgorithm.Aes256)
                {
                    info.StartKeyGenerationName = "http://www.w3.org/2000/09/xmldsig#sha256";
                    info.StartKeySize = 32;
                }
                else
                {
                    info.StartKeyGenerationName = "http://www.w3.org/2000/09/xmldsig#sha1";
                    info.StartKeySize = 20;
                }
            }

            entry.SetContent(ciphertext);
            entry.EncryptionInfo = info;
        }
    }

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
            throw new NotSupportedException($"不支援的總和檢查碼類型： {checksumType}");
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
        if (a is null || b is null) return a == b;
        if (a.Length != b.Length) return false;
#if NET5_0_OR_GREATER
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b);
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

        if (plaintext.Length == 0) return plaintext;
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
}


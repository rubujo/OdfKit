using System;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Generators;

namespace OdfKit.Core;

public static partial class OdfEncryption
{
    #region Package Encryption & Decryption

    /// <summary>
    /// 解密指定 ODF 封裝中的所有加密項目。
    /// </summary>
    /// <param name="package">要解密的 ODF 封裝執行個體</param>
    /// <param name="password">解密密碼</param>
    public static void Decrypt(OdfPackage package, string password)
    {
        foreach (var entry in package.Entries.Values)
        {
            if (entry.EncryptionInfo is null)
                continue;

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

    #endregion
}

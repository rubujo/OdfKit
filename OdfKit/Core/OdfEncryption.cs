#pragma warning disable 1591 // Suppress CS1591 (missing XML comments) for legacy hand-written APIs to maintain zero-warning compilation under TreatWarningsAsErrors while package XML documentation is generated.
using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace OdfKit.Core
{
    public static class OdfEncryption
    {
        public const string Aes256AlgorithmUri = "http://www.w3.org/2001/04/xmlenc#aes256-cbc";
        public const string BlowfishAlgorithmUri = "http://www.w3.org/2001/04/xmldsig-more#blowfish-cbc";

        /// <summary>
        /// 自訂實作 PBKDF2，支援 SHA-1 與 SHA-256，以確保在所有平台上的行為完全一致。
        /// </summary>
        public static byte[] Pbkdf2(byte[] password, byte[] salt, int iterations, int keyLength, string hashName)
        {
            if (iterations > 50000)
            {
                throw new CryptographicException($"PBKDF2 iteration count {iterations} exceeds the maximum limit of 50000.");
            }
            if (hashName.IndexOf("sha256", StringComparison.OrdinalIgnoreCase) >= 0 || hashName.IndexOf("sha-256", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                using (var hmac = new HMACSHA256(password))
                {
                    return Pbkdf2Hmac(hmac, salt, iterations, keyLength);
                }
            }
            else
            {
                using (var hmac = new HMACSHA1(password))
                {
                    return Pbkdf2Hmac(hmac, salt, iterations, keyLength);
                }
            }
        }

        private static byte[] Pbkdf2Hmac(HMAC hmac, byte[] salt, int iterations, int keyLength)
        {
            byte[] key = new byte[keyLength];
            int hashLength = hmac.HashSize / 8;
            int numBlocks = (keyLength + hashLength - 1) / hashLength;
            byte[] blockBuf = new byte[salt.Length + 4];
            Buffer.BlockCopy(salt, 0, blockBuf, 0, salt.Length);

            for (int i = 1; i <= numBlocks; i++)
            {
                blockBuf[salt.Length] = (byte)(i >> 24);
                blockBuf[salt.Length + 1] = (byte)(i >> 16);
                blockBuf[salt.Length + 2] = (byte)(i >> 8);
                blockBuf[salt.Length + 3] = (byte)i;

                byte[] u = hmac.ComputeHash(blockBuf);
                byte[] f = new byte[hashLength];
                Buffer.BlockCopy(u, 0, f, 0, hashLength);

                for (int j = 1; j < iterations; j++)
                {
                    u = hmac.ComputeHash(u);
                    for (int k = 0; k < hashLength; k++)
                    {
                        f[k] ^= u[k];
                    }
                }

                int offset = (i - 1) * hashLength;
                int count = Math.Min(hashLength, keyLength - offset);
                Buffer.BlockCopy(f, 0, key, offset, count);
            }
            return key;
        }

        /// <summary>
        /// 解密單個 ODF 加密 Entry 的內容。
        /// </summary>
        public static byte[] DecryptEntry(byte[] ciphertext, string password, string algorithmUri, string derivationName, int keySize, int iterationCount, byte[] salt, byte[] iv, string? startKeyGenName = null)
        {
            if (iterationCount > 50000)
            {
                throw new CryptographicException($"PBKDF2 iteration count {iterationCount} exceeds the maximum limit of 50000.");
            }

            if (algorithmUri != Aes256AlgorithmUri && algorithmUri != BlowfishAlgorithmUri)
            {
                throw new NotSupportedException($"Unsupported encryption algorithm: {algorithmUri}. OdfKit supports standard AES-256-CBC and Blowfish-CBC.");
            }

            if (!string.Equals(derivationName, "PBKDF2", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException($"Unsupported key derivation function: {derivationName}.");
            }

            // Derive password bytes if startKeyGenName is present
            byte[] pwdBytes;
            if (startKeyGenName != null)
            {
                byte[] rawPassBytes = Encoding.UTF8.GetBytes(password);
                if (startKeyGenName.IndexOf("sha256", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    using (var sha = SHA256.Create())
                        pwdBytes = sha.ComputeHash(rawPassBytes);
                }
                else if (startKeyGenName.IndexOf("sha1", StringComparison.OrdinalIgnoreCase) >= 0)
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

            // Determine hash name for PBKDF2
            string hashName = "sha256";
            if (startKeyGenName != null && startKeyGenName.IndexOf("sha1", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                hashName = "sha1";
            }
            else if (algorithmUri == BlowfishAlgorithmUri)
            {
                hashName = "sha1";
            }

            byte[] derivedKey = Pbkdf2(pwdBytes, salt, iterationCount, keySize, hashName);

            if (algorithmUri == Aes256AlgorithmUri)
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
                var blowfish = new Blowfish();
                blowfish.Initialize(derivedKey);
                return blowfish.DecryptCbc(ciphertext, iv);
            }
        }

        /// <summary>
        /// 加密單個 ODF Entry 的內容。
        /// </summary>
        public static byte[] EncryptEntry(byte[] plaintext, string password, OdfEncryptionAlgorithm algorithm, out byte[] iv, out byte[] salt, out byte[] checksum)
        {
            salt = new byte[16];
            iv = new byte[algorithm == OdfEncryptionAlgorithm.Aes256 ? 16 : 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            byte[] pwdBytes = Encoding.UTF8.GetBytes(password);
            int keySize = algorithm == OdfEncryptionAlgorithm.Aes256 ? 32 : 16;
            
            byte[] preHashedPwd;
            byte[] derivedKey;
            if (algorithm == OdfEncryptionAlgorithm.Aes256)
            {
                using (var sha = SHA256.Create())
                    preHashedPwd = sha.ComputeHash(pwdBytes);
                derivedKey = Pbkdf2(preHashedPwd, salt, 1024, keySize, "sha256");
            }
            else
            {
                using (var sha = SHA1.Create())
                    preHashedPwd = sha.ComputeHash(pwdBytes);
                derivedKey = Pbkdf2(preHashedPwd, salt, 1024, keySize, "sha1");
            }

            byte[] ciphertext;

            if (algorithm == OdfEncryptionAlgorithm.Aes256)
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
                var blowfish = new Blowfish();
                blowfish.Initialize(derivedKey);
                ciphertext = blowfish.EncryptCbc(plaintext, iv);
            }

            using (var sha = SHA256.Create())
            {
                checksum = sha.ComputeHash(plaintext);
            }

            return ciphertext;
        }

        public static void Decrypt(OdfPackage package, string password)
        {
            foreach (var entry in package.Entries.Values)
            {
                if (entry.EncryptionInfo == null) continue;

                byte[] ciphertext;
                using (var stream = entry.OpenReader())
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    ciphertext = ms.ToArray();
                }

                byte[] decryptedPlaintext;

                IOdfCryptographyProvider? cryptoProvider = null;
                if (package.LoadOptions.CryptographyProvider != null &&
                    package.LoadOptions.CryptographyProvider.CanHandle(entry.EncryptionInfo))
                {
                    cryptoProvider = package.LoadOptions.CryptographyProvider;
                }
                else if (package.SaveOptions.CryptographyProvider != null &&
                         package.SaveOptions.CryptographyProvider.CanHandle(entry.EncryptionInfo))
                {
                    cryptoProvider = package.SaveOptions.CryptographyProvider;
                }

                if (cryptoProvider != null)
                {
                    decryptedPlaintext = cryptoProvider.Decrypt(ciphertext, entry.EncryptionInfo, package.LoadOptions);
                }
                else
                {
                    byte[] decryptedBytes = DecryptEntry(
                        ciphertext,
                        password,
                        entry.EncryptionInfo.AlgorithmName,
                        entry.EncryptionInfo.KeyDerivationName,
                        entry.EncryptionInfo.KeySize,
                        entry.EncryptionInfo.IterationCount,
                        entry.EncryptionInfo.Salt,
                        entry.EncryptionInfo.InitialisationVector,
                        entry.EncryptionInfo.StartKeyGenerationName
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
                                    throw new System.Security.SecurityException($"Decompressed size of entry exceeds the maximum entry size limit of {maxEntrySize} bytes.");
                                }
                                outMs.Write(buffer, 0, bytesRead);
                            }
                            decompressedBytes = outMs.ToArray();
                            decompressedSuccessfully = true;
                        }
                    }
                    catch (System.Security.SecurityException)
                    {
                        throw;
                    }
                    catch
                    {
                        // Fallback verification
                    }

                    if (decompressedSuccessfully && decompressedBytes != null)
                    {
                        byte[] calculatedChecksum = ComputeHash(decompressedBytes, entry.EncryptionInfo.ChecksumType);
                        if (ByteArrayEquals(calculatedChecksum, entry.EncryptionInfo.Checksum))
                        {
                            decryptedPlaintext = decompressedBytes;
                            goto AssignPlaintext;
                        }
                    }

                    byte[] rawCalculatedChecksum = ComputeHash(decryptedBytes, entry.EncryptionInfo.ChecksumType);
                    if (ByteArrayEquals(rawCalculatedChecksum, entry.EncryptionInfo.Checksum))
                    {
                        decryptedPlaintext = decryptedBytes;
                    }
                    else
                    {
                        throw new CryptographicException("Decryption failed: Checksum mismatch or invalid password.");
                    }
                }

            AssignPlaintext:
                entry.SetContent(decryptedPlaintext);
                entry.EncryptionInfo = null;
            }
        }

        public static void Encrypt(OdfPackage package, string password, OdfEncryptionAlgorithm algorithm = OdfEncryptionAlgorithm.Aes256)
        {
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

                if (package.SaveOptions.CryptographyProvider != null)
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
                    byte[] dummyChecksum;
                    ciphertext = EncryptEntry(compressedPlaintext, password, algorithm, out iv, out salt, out dummyChecksum);

                    byte[] checksum = ComputeHash(plaintext, "SHA256");

                    info = new OdfEncryptionInfo
                    {
                        ChecksumType = "SHA256",
                        Checksum = checksum,
                        AlgorithmName = algorithm == OdfEncryptionAlgorithm.Aes256 ? Aes256AlgorithmUri : BlowfishAlgorithmUri,
                        InitialisationVector = iv,
                        KeyDerivationName = "PBKDF2",
                        KeySize = algorithm == OdfEncryptionAlgorithm.Aes256 ? 32 : 16,
                        IterationCount = 1024,
                        Salt = salt
                    };

                    if (algorithm == OdfEncryptionAlgorithm.Aes256)
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

        public static byte[] ComputeHash(byte[] data, string checksumType)
        {
            if (checksumType.IndexOf("sha256", StringComparison.OrdinalIgnoreCase) >= 0 || checksumType.IndexOf("sha-256", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                using (var sha = SHA256.Create())
                {
                    return sha.ComputeHash(data);
                }
            }
            else if (checksumType.IndexOf("sha1", StringComparison.OrdinalIgnoreCase) >= 0 || checksumType.IndexOf("sha-1", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                using (var sha = SHA1.Create())
                {
                    return sha.ComputeHash(data);
                }
            }
            else
            {
                throw new NotSupportedException($"Unsupported checksum type: {checksumType}");
            }
        }

        public static bool ByteArrayEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }

    internal class Blowfish
    {
        private readonly uint[] _p = new uint[18];
        private readonly uint[][] _s = new uint[4][] { new uint[256], new uint[256], new uint[256], new uint[256] };

        private uint F(uint x)
        {
            uint a = x >> 24;
            uint b = (x >> 16) & 0xff;
            uint c = (x >> 8) & 0xff;
            uint d = x & 0xff;
            uint y = _s[0][a] + _s[1][b];
            y ^= _s[2][c];
            y += _s[3][d];
            return y;
        }

        private void Encrypt(ref uint xl, ref uint xr)
        {
            for (int i = 0; i < 16; i++)
            {
                xl ^= _p[i];
                xr ^= F(xl);
                uint temp = xl; xl = xr; xr = temp;
            }
            uint t = xl; xl = xr; xr = t;
            xr ^= _p[16];
            xl ^= _p[17];
        }

        private void Decrypt(ref uint xl, ref uint xr)
        {
            for (int i = 17; i > 1; i--)
            {
                xl ^= _p[i];
                xr ^= F(xl);
                uint temp = xl; xl = xr; xr = temp;
            }
            uint t = xl; xl = xr; xr = t;
            xr ^= _p[1];
            xl ^= _p[0];
        }

        public void Initialize(byte[] key)
        {
            if (key == null || key.Length == 0)
            {
                throw new ArgumentException("Blowfish key cannot be null or empty.", nameof(key));
            }
            Array.Copy(BlowfishConstants.P, _p, 18);
            for (int i = 0; i < 4; i++)
            {
                Array.Copy(BlowfishConstants.S[i], _s[i], 256);
            }

            int keyIndex = 0;
            for (int i = 0; i < 18; i++)
            {
                uint data = 0;
                for (int j = 0; j < 4; j++)
                {
                    data = (data << 8) | key[keyIndex];
                    keyIndex = (keyIndex + 1) % key.Length;
                }
                _p[i] ^= data;
            }

            uint xl = 0, xr = 0;
            for (int i = 0; i < 18; i += 2)
            {
                Encrypt(ref xl, ref xr);
                _p[i] = xl;
                _p[i + 1] = xr;
            }
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 256; j += 2)
                {
                    Encrypt(ref xl, ref xr);
                    _s[i][j] = xl;
                    _s[i][j + 1] = xr;
                }
            }
        }

        public byte[] DecryptCbc(byte[] ciphertext, byte[] iv)
        {
            byte[] plaintext = new byte[ciphertext.Length];
            uint ivL = ((uint)iv[0] << 24) | ((uint)iv[1] << 16) | ((uint)iv[2] << 8) | iv[3];
            uint ivR = ((uint)iv[4] << 24) | ((uint)iv[5] << 16) | ((uint)iv[6] << 8) | iv[7];

            for (int offset = 0; offset < ciphertext.Length; offset += 8)
            {
                uint cL = ((uint)ciphertext[offset] << 24) | ((uint)ciphertext[offset + 1] << 16) | ((uint)ciphertext[offset + 2] << 8) | ciphertext[offset + 3];
                uint cR = ((uint)ciphertext[offset + 4] << 24) | ((uint)ciphertext[offset + 5] << 16) | ((uint)ciphertext[offset + 6] << 8) | ciphertext[offset + 7];

                uint pL = cL, pR = cR;
                Decrypt(ref pL, ref pR);

                pL ^= ivL;
                pR ^= ivR;

                plaintext[offset] = (byte)(pL >> 24);
                plaintext[offset + 1] = (byte)(pL >> 16);
                plaintext[offset + 2] = (byte)(pL >> 8);
                plaintext[offset + 3] = (byte)pL;
                plaintext[offset + 4] = (byte)(pR >> 24);
                plaintext[offset + 5] = (byte)(pR >> 16);
                plaintext[offset + 6] = (byte)(pR >> 8);
                plaintext[offset + 7] = (byte)pR;

                ivL = cL;
                ivR = cR;
            }

            if (plaintext.Length == 0) return plaintext;
            int paddingLen = plaintext[plaintext.Length - 1];
            if (paddingLen > 0 && paddingLen <= 8)
            {
                bool valid = true;
                for (int i = plaintext.Length - paddingLen; i < plaintext.Length; i++)
                {
                    if (plaintext[i] != paddingLen) { valid = false; break; }
                }
                if (valid)
                {
                    byte[] unpadded = new byte[plaintext.Length - paddingLen];
                    Array.Copy(plaintext, 0, unpadded, 0, unpadded.Length);
                    return unpadded;
                }
            }
            return plaintext;
        }

        public byte[] EncryptCbc(byte[] plaintext, byte[] iv)
        {
            int paddingLen = 8 - (plaintext.Length % 8);
            byte[] padded = new byte[plaintext.Length + paddingLen];
            Array.Copy(plaintext, 0, padded, 0, plaintext.Length);
            for (int i = plaintext.Length; i < padded.Length; i++)
            {
                padded[i] = (byte)paddingLen;
            }

            byte[] ciphertext = new byte[padded.Length];
            uint ivL = ((uint)iv[0] << 24) | ((uint)iv[1] << 16) | ((uint)iv[2] << 8) | iv[3];
            uint ivR = ((uint)iv[4] << 24) | ((uint)iv[5] << 16) | ((uint)iv[6] << 8) | iv[7];

            for (int offset = 0; offset < padded.Length; offset += 8)
            {
                uint pL = ((uint)padded[offset] << 24) | ((uint)padded[offset + 1] << 16) | ((uint)padded[offset + 2] << 8) | padded[offset + 3];
                uint pR = ((uint)padded[offset + 4] << 24) | ((uint)padded[offset + 5] << 16) | ((uint)padded[offset + 6] << 8) | padded[offset + 7];

                pL ^= ivL;
                pR ^= ivR;

                Encrypt(ref pL, ref pR);

                ciphertext[offset] = (byte)(pL >> 24);
                ciphertext[offset + 1] = (byte)(pL >> 16);
                ciphertext[offset + 2] = (byte)(pL >> 8);
                ciphertext[offset + 3] = (byte)pL;
                ciphertext[offset + 4] = (byte)(pR >> 24);
                ciphertext[offset + 5] = (byte)(pR >> 16);
                ciphertext[offset + 6] = (byte)(pR >> 8);
                ciphertext[offset + 7] = (byte)pR;

                ivL = pL;
                ivR = pR;
            }
            return ciphertext;
        }
    }
}

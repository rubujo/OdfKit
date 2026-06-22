using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

using OdfKit.Compliance;
namespace OdfKit.Core;

public static partial class OdfEncryption
{
    #region Entry Encryption & Decryption

    /// <summary>
    /// 解密單一封裝專案。支援 PBKDF2 搭配 AES/Blowfish 以及 Argon2id 搭配 AES-GCM。
    /// </summary>
    /// <param name="ciphertext">加密的密文資料位元組陣列</param>
    /// <param name="password">解密密碼</param>
    /// <param name="algorithmUri">加密演算法的 XML 識別 URI</param>
    /// <param name="derivationName">金鑰衍生演算法的 XML 識別 URI</param>
    /// <param name="keySize">金鑰大小（以位元組為單位）</param>
    /// <param name="iterationCount">金鑰衍生的反覆運算次數</param>
    /// <param name="salt">金鑰衍生的鹽值（Salt）位元組陣列</param>
    /// <param name="iv">加密的初始向量（IV）位元組陣列</param>
    /// <param name="startKeyGenName">初始金鑰產生的演算法名稱（選填）</param>
    /// <param name="kdfName">金鑰衍生函數的名稱（選填，例如 "argon2id"）</param>
    /// <param name="argon2T">Argon2id 的時間複雜度/反覆運算次數（選填）</param>
    /// <param name="argon2M">Argon2id 的記憶體複雜度（單位為 KB，選填）</param>
    /// <param name="argon2P">Argon2id 的平行度/通道數（選填）</param>
    /// <returns>解密後的純文字資料位元組陣列</returns>
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
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfEncryption_NumberPbkdf2IterationsExceeds_2", iterationCount));
        }

        if (algorithmUri != Aes256AlgorithmUri && algorithmUri != BlowfishAlgorithmUri && algorithmUri != Aes256GcmAlgorithmUri)
        {
            throw new NotSupportedException(OdfLocalizer.GetMessage("Err_OdfEncryption_UnsupportedEncryptionAlgorithmOdfkit", algorithmUri));
        }

        if (!isArgon2 && !string.Equals(derivationName, "PBKDF2", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(OdfLocalizer.GetMessage("Err_OdfEncryption_UnsupportedKeyDerivationFunction", derivationName));
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
            int effectiveParallelism = Math.Max(1, Math.Min(argon2P, Environment.ProcessorCount));
            Argon2ConcurrencyGate.Wait();
            try
            {
                var builder = new Argon2Parameters.Builder(Argon2Parameters.Argon2id)
                    .WithVersion(Argon2Parameters.Version13)
                    .WithIterations(argon2T)
                    .WithMemoryAsKB(argon2M)
                    .WithParallelism(effectiveParallelism)
                    .WithSalt(salt);

                var generator = new Argon2BytesGenerator();
                generator.Init(builder.Build());
                derivedKey = new byte[keySize];
                generator.GenerateBytes(pwdBytes, derivedKey, 0, derivedKey.Length);
            }
            finally
            {
                Argon2ConcurrencyGate.Release();
            }
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

                int outputSize = cipher.GetOutputSize(ciphertext.Length);
                byte[] output = new byte[outputSize];
                int len = cipher.ProcessBytes(ciphertext, 0, ciphertext.Length, output, 0);
                int finalLen = cipher.DoFinal(output, len);
                int totalLen = len + finalLen;

                if (totalLen == output.Length)
                {
                    return output;
                }

                byte[] decrypted = new byte[totalLen];
                Buffer.BlockCopy(output, 0, decrypted, 0, totalLen);
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
    /// 加密單一封裝專案。支援傳統加密與 AES-GCM 加密，並產生對應的 IV、鹽值與驗證碼。
    /// </summary>
    /// <param name="plaintext">待加密的純文字資料位元組陣列</param>
    /// <param name="password">加密密碼</param>
    /// <param name="algorithm">加密演算法類型</param>
    /// <param name="iv">輸出參數，接收隨機產生的初始向量（IV）位元組陣列</param>
    /// <param name="salt">輸出參數，接收隨機產生的鹽值（Salt）位元組陣列</param>
    /// <param name="checksum">輸出參數，接收加密後計算出的驗證碼（Checksum）位元組陣列</param>
    /// <param name="iterationCount">金鑰衍生的反覆運算次數（預設為 50,000 次）</param>
    /// <returns>加密後的密文資料位元組陣列</returns>
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

    #endregion
}

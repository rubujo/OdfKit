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
/// <summary>
/// Provides the OdfEncryption API.
/// 提供 OdfEncryption API。
/// </summary>

public static partial class OdfEncryption
{
    #region Entry Encryption & Decryption
    /// <summary>
    /// Short overload of DecryptEntry that accepts ciphertext, password, algorithmUri, derivationName, keySize, iterationCount, salt, and iv; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 ciphertext、password、algorithmUri、derivationName、keySize、iterationCount、salt 與 iv；其餘可選參數使用預設值並轉呼叫最長 DecryptEntry 多載。
    /// </summary>
    public static byte[] DecryptEntry(byte[] ciphertext, string password, string algorithmUri, string derivationName, int keySize, int iterationCount, byte[] salt, byte[] iv) => DecryptEntry(ciphertext, password, algorithmUri, derivationName, keySize, iterationCount, salt, iv, null, null, 3, 65536, 4);

    /// <summary>
    /// Short overload of DecryptEntry that accepts ciphertext, password, algorithmUri, derivationName, keySize, iterationCount, salt, iv, and startKeyGenName; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 ciphertext、password、algorithmUri、derivationName、keySize、iterationCount、salt、iv 與 startKeyGenName；其餘可選參數使用預設值並轉呼叫最長 DecryptEntry 多載。
    /// </summary>
    public static byte[] DecryptEntry(byte[] ciphertext, string password, string algorithmUri, string derivationName, int keySize, int iterationCount, byte[] salt, byte[] iv, string? startKeyGenName) => DecryptEntry(ciphertext, password, algorithmUri, derivationName, keySize, iterationCount, salt, iv, startKeyGenName, null, 3, 65536, 4);

    /// <summary>
    /// Short overload of DecryptEntry that accepts ciphertext, password, algorithmUri, derivationName, keySize, iterationCount, salt, iv, startKeyGenName, and kdfName; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 ciphertext、password、algorithmUri、derivationName、keySize、iterationCount、salt、iv、startKeyGenName 與 kdfName；其餘可選參數使用預設值並轉呼叫最長 DecryptEntry 多載。
    /// </summary>
    public static byte[] DecryptEntry(byte[] ciphertext, string password, string algorithmUri, string derivationName, int keySize, int iterationCount, byte[] salt, byte[] iv, string? startKeyGenName, string? kdfName) => DecryptEntry(ciphertext, password, algorithmUri, derivationName, keySize, iterationCount, salt, iv, startKeyGenName, kdfName, 3, 65536, 4);

    /// <summary>
    /// Short overload of DecryptEntry that accepts ciphertext, password, algorithmUri, derivationName, keySize, iterationCount, salt, iv, startKeyGenName, kdfName, and argon2T; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 ciphertext、password、algorithmUri、derivationName、keySize、iterationCount、salt、iv、startKeyGenName、kdfName 與 argon2T；其餘可選參數使用預設值並轉呼叫最長 DecryptEntry 多載。
    /// </summary>
    public static byte[] DecryptEntry(byte[] ciphertext, string password, string algorithmUri, string derivationName, int keySize, int iterationCount, byte[] salt, byte[] iv, string? startKeyGenName, string? kdfName, int argon2T) => DecryptEntry(ciphertext, password, algorithmUri, derivationName, keySize, iterationCount, salt, iv, startKeyGenName, kdfName, argon2T, 65536, 4);

    /// <summary>
    /// Short overload of DecryptEntry that accepts ciphertext, password, algorithmUri, derivationName, keySize, iterationCount, salt, iv, startKeyGenName, kdfName, argon2T, and argon2M; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 ciphertext、password、algorithmUri、derivationName、keySize、iterationCount、salt、iv、startKeyGenName、kdfName、argon2T 與 argon2M；其餘可選參數使用預設值並轉呼叫最長 DecryptEntry 多載。
    /// </summary>
    public static byte[] DecryptEntry(byte[] ciphertext, string password, string algorithmUri, string derivationName, int keySize, int iterationCount, byte[] salt, byte[] iv, string? startKeyGenName, string? kdfName, int argon2T, int argon2M) => DecryptEntry(ciphertext, password, algorithmUri, derivationName, keySize, iterationCount, salt, iv, startKeyGenName, kdfName, argon2T, argon2M, 4);


    /// <summary>
    /// Decrypts entry.
    /// 解密單一封裝項目。支援 PBKDF2 搭配 AES/Blowfish 以及 Argon2id 搭配 AES-GCM。
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
    public static byte[] DecryptEntry(byte[] ciphertext, string password, string algorithmUri, string derivationName, int keySize, int iterationCount, byte[] salt, byte[] iv, string? startKeyGenName, string? kdfName, int argon2T, int argon2M, int argon2P) =>
        DecryptEntryCore(ciphertext, password, algorithmUri, derivationName, keySize, iterationCount, salt, iv, startKeyGenName, kdfName, argon2T, argon2M, argon2P, legacyPbkdf2WithSha256Prf: false);

    /// <summary>
    /// 解密單一封裝項目的核心實作。
    /// </summary>
    /// <remarks>
    /// <c>legacyPbkdf2WithSha256Prf</c> 為 <see langword="true"/> 時，改以早期 OdfKit 版本的
    /// HMAC-SHA-256 虛擬亂數函式衍生金鑰；僅供讀取既有檔案的後備路徑使用，新寫入一律採規範的 HMAC-SHA-1。
    /// </remarks>
    internal static byte[] DecryptEntryCore(byte[] ciphertext, string password, string algorithmUri, string derivationName, int keySize, int iterationCount, byte[] salt, byte[] iv, string? startKeyGenName, string? kdfName, int argon2T, int argon2M, int argon2P, bool legacyPbkdf2WithSha256Prf)
    {
        bool isArgon2 = string.Equals(kdfName, "argon2id", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(derivationName, Argon2idDerivationUri, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(derivationName, Argon2idOdf15DerivationUri, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(derivationName, Argon2idLegacyDerivationUri, StringComparison.OrdinalIgnoreCase);

        if (!isArgon2 && string.Equals(derivationName, "PBKDF2", StringComparison.OrdinalIgnoreCase) && iterationCount > MaxPbkdf2IterationCount)
        {
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfEncryption_NumberPbkdf2IterationsExceeds_2", iterationCount, MaxPbkdf2IterationCount));
        }

        bool isBlowfishCfb = IsBlowfishCfbAlgorithm(algorithmUri);
        bool isLegacyBlowfishCbc = IsLegacyBlowfishCbcAlgorithm(algorithmUri);
        if (algorithmUri != Aes256AlgorithmUri && algorithmUri != Aes256GcmAlgorithmUri && !isBlowfishCfb && !isLegacyBlowfishCbc)
        {
            throw new NotSupportedException(OdfLocalizer.GetMessage("Err_OdfEncryption_UnsupportedEncryptionAlgorithmOdfkit", algorithmUri));
        }

        if (!isArgon2 && !string.Equals(derivationName, "PBKDF2", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(OdfLocalizer.GetMessage("Err_OdfEncryption_UnsupportedKeyDerivationFunction", derivationName));
        }

        // ODF 1.0～1.4 Part 2 §4.16.7：`manifest:start-key-generation` 可省略，省略時的預設是 SHA1。
        // LibreOffice 產生的傳統加密文件即不輸出該元素，因此不能退回「直接使用原始密碼位元組」。
        bool startKeyIsSha256 = startKeyGenName is not null
            && (startKeyGenName.EndsWith("#sha256", StringComparison.OrdinalIgnoreCase)
                || string.Equals(startKeyGenName, "sha256", StringComparison.OrdinalIgnoreCase)
                || string.Equals(startKeyGenName, "sha-256", StringComparison.OrdinalIgnoreCase));

        bool startKeyIsSha1 = startKeyGenName is null
            || startKeyGenName.EndsWith("#sha1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(startKeyGenName, "sha1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(startKeyGenName, "sha-1", StringComparison.OrdinalIgnoreCase);

        byte[] pwdBytes;
        byte[] rawPasswordBytes = Encoding.UTF8.GetBytes(password);
        if (startKeyIsSha256)
        {
            pwdBytes = global::OdfKit.Internal.OdfHashHelper.Sha256(rawPasswordBytes);
        }
        else if (startKeyIsSha1)
        {
            pwdBytes = global::OdfKit.Internal.OdfHashHelper.Sha1(rawPasswordBytes);
        }
        else
        {
            pwdBytes = rawPasswordBytes;
        }

        // ODF 1.0～1.4 Part 2 §4.16.7：`PBKDF2` 的虛擬亂數函式固定為 HMAC-SHA-1，與
        // `start-key-generation-name` 無關（後者只決定密碼如何雜湊成 start key）。
        // 早期 OdfKit 版本誤把兩者綁在一起，對 AES 路徑用了 HMAC-SHA-256；解密端保留該形狀作為後備。
        string hashName = legacyPbkdf2WithSha256Prf ? "sha256" : "sha1";

        // `manifest:key-size` 為可選屬性；缺席時依演算法採用規範預設長度。
        if (keySize <= 0)
        {
            keySize = isBlowfishCfb || isLegacyBlowfishCbc ? BlowfishKeySizeBytes : Aes256KeySizeBytes;
        }

        ValidateEncryptionKeySize(algorithmUri, keySize);

        byte[] derivedKey;
        if (isArgon2)
        {
            ValidateArgon2Parameters(argon2T, argon2M, argon2P);
            int effectiveParallelism = Math.Max(1, Math.Min(argon2P, Environment.ProcessorCount));
            EnterArgon2Operation();
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
                ExitArgon2Operation();
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
                throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfEncryption_GcmDecryptionFailed"), ex);
            }
        }
        else if (algorithmUri == Aes256AlgorithmUri)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = derivedKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;

                // W3C XML Encryption §5.2 只定義「最後一個位元組是填充長度」，其餘填充位元組值未定義；
                // PKCS#7 是其特例。因此改以 PaddingMode.None 解出全部區塊，再手動移除填充，
                // 才能同時讀取 LibreOffice／OpenOffice 與早期 OdfKit 產生的密文。
                aes.Padding = PaddingMode.None;

                using (var decryptor = aes.CreateDecryptor())
                using (var msDecrypt = new MemoryStream())
                {
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Write))
                    {
                        csDecrypt.Write(ciphertext, 0, ciphertext.Length);
                        csDecrypt.FlushFinalBlock();
                    }
                    return RemoveXmlEncryptionPadding(msDecrypt.ToArray(), aes.BlockSize / 8);
                }
            }
        }
        else if (isBlowfishCfb)
        {
            return DecryptBlowfishCfb(ciphertext, derivedKey, iv);
        }
        else
        {
            return DecryptBlowfishCbc(ciphertext, derivedKey, iv);
        }
    }


    /// <summary>
    /// Encrypts entry.
    /// 加密單一封裝項目。支援傳統加密與 AES-GCM 加密，並產生對應的 IV、鹽值與驗證碼。
    /// </summary>
    /// <param name="plaintext">待加密的純文字資料位元組陣列</param>
    /// <param name="password">加密密碼</param>
    /// <param name="algorithm">加密演算法類型</param>
    /// <param name="iv">輸出參數，接收隨機產生的初始向量（IV）位元組陣列</param>
    /// <param name="salt">輸出參數，接收隨機產生的鹽值（Salt）位元組陣列</param>
    /// <param name="checksum">輸出參數，接收加密後計算出的驗證碼（Checksum）位元組陣列</param>
    /// <param name="iterationCount">金鑰衍生的反覆運算次數（預設為 <see cref="DefaultPbkdf2IterationCount"/>）</param>
    /// <returns>加密後的密文資料位元組陣列</returns>
    public static byte[] EncryptEntry(
        byte[] plaintext,
        string password,
        OdfEncryptionAlgorithm algorithm,
        out byte[] iv,
        out byte[] salt,
        out byte[] checksum,
        int iterationCount = DefaultPbkdf2IterationCount)
    {
        salt = new byte[algorithm == OdfEncryptionAlgorithm.Aes256Gcm ? 32 : 16];
        iv = new byte[algorithm == OdfEncryptionAlgorithm.Aes256Gcm ? 12 : (algorithm == OdfEncryptionAlgorithm.Aes256 ? 16 : 8)];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
            rng.GetBytes(iv);
        }

        byte[] pwdBytes = Encoding.UTF8.GetBytes(password);
        int keySize = (algorithm == OdfEncryptionAlgorithm.Aes256 || algorithm == OdfEncryptionAlgorithm.Aes256Gcm)
            ? Aes256KeySizeBytes
            : BlowfishKeySizeBytes;

        byte[] derivedKey;
        if (algorithm == OdfEncryptionAlgorithm.Aes256Gcm)
        {
            // 使用 Argon2id 衍生金鑰。t=3／m=64 MiB／p=4 屬 RFC 9106 建議區間；manifest 形狀
            // （key-derivation-name 與 loext:argon2-iterations／-memory／-lanes）對標 LibreOffice
            // 的 OpenDocument-v1.4+libreoffice-manifest-schema.rng。互通邊界見 docs/odf-format-support.md。
            byte[] preHashedPwd;
            preHashedPwd = global::OdfKit.Internal.OdfHashHelper.Sha256(pwdBytes);

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
            // start key 依 start-key-generation-name 取 SHA-256；PBKDF2 的 PRF 則是規範固定的
            // HMAC-SHA-1（Part 2 §4.16.7），兩者不可混為一談。
            byte[] preHashedPwd;
            preHashedPwd = global::OdfKit.Internal.OdfHashHelper.Sha256(pwdBytes);
            derivedKey = Pbkdf2(preHashedPwd, salt, iterationCount, keySize, "sha1");
        }
        else
        {
            byte[] preHashedPwd;
            preHashedPwd = global::OdfKit.Internal.OdfHashHelper.Sha1(pwdBytes);
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
            ciphertext = EncryptBlowfishCfb(plaintext, derivedKey, iv);
        }

        // ODF 1.0～1.4 Part 2 §4.16.4：checksum 是「壓縮後、未加密」資料前 1024 位元組的摘要。
        // 呼叫端傳入的 plaintext 已經是 deflate 後的位元組。檢查碼型別跟著演算法世代。
        checksum = ComputeHash(
            plaintext,
            algorithm == OdfEncryptionAlgorithm.Blowfish ? Sha1OneKilobyteChecksumName : Sha256OneKilobyteChecksumUri);

        return ciphertext;
    }

    #endregion
}

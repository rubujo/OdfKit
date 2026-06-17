using System;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Xunit;
using OdfKit.Core;
using OdfKit.Spreadsheet;
using OdfKit.Styles;

namespace OdfKit.Tests;

/// <summary>
/// Challenger 2 針對加密、雜湊比對與列高邊界條件、極端值及無效輸入的實證測試。
/// </summary>
public class Challenger2StressTests
{
    /// <summary>
    /// 測試 Pbkdf2 當輸入無效或極端的反覆運算次數時，是否能正確處理或擲出例外。
    /// </summary>
    [Fact]
    public void TestPbkdf2_BoundaryAndInvalidInputs()
    {
        byte[] pwd = Encoding.UTF8.GetBytes("Password");
        byte[] salt = new byte[8];

        // 1. 測試 iterations 大於 50000 時擲出 CryptographicException
        Assert.Throws<CryptographicException>(() =>
            OdfEncryption.Pbkdf2(pwd, salt, 50001, 16, "sha256"));

        // 2. 測試 iterations 等於 50000（剛好在邊界上）應成功執行
        byte[] key50000 = OdfEncryption.Pbkdf2(pwd, salt, 50000, 16, "sha256");
        Assert.Equal(16, key50000.Length);

        // 3. 測試 iterations 為 0 或負數時的行為
        // 當 iterations <= 0 時，Pbkdf2Hmac 迴圈不會執行，但仍會計算一次初始 HMAC 值並回傳 key。
        // 這不符合標準的 PBKDF2 行為（通常 iterations 應至少為 1），但此處驗證其不崩潰。
        byte[] key0 = OdfEncryption.Pbkdf2(pwd, salt, 0, 16, "sha256");
        Assert.Equal(16, key0.Length);

        byte[] keyNeg = OdfEncryption.Pbkdf2(pwd, salt, -5, 16, "sha256");
        Assert.Equal(16, keyNeg.Length);

        // 4. 測試不支援或空的雜湊演算法名稱應擲出 NotSupportedException
        Assert.Throws<NotSupportedException>(() =>
            OdfEncryption.Pbkdf2(pwd, salt, 1000, 16, ""));

        Assert.Throws<NotSupportedException>(() =>
            OdfEncryption.Pbkdf2(pwd, salt, 1000, 16, "sha3"));

        // 5. 測試輸入 null 參數應擲出 ArgumentNullException 或是 NullReferenceException
        Assert.Throws<ArgumentNullException>(() =>
            OdfEncryption.Pbkdf2(null!, salt, 1000, 16, "sha256"));

        Assert.Throws<NullReferenceException>(() =>
            OdfEncryption.Pbkdf2(pwd, null!, 1000, 16, "sha256"));

        Assert.Throws<NullReferenceException>(() =>
            OdfEncryption.Pbkdf2(pwd, salt, 1000, 16, null!));
    }

    /// <summary>
    /// 測試 DecryptEntry 當輸入無效的密碼學參數時，是否正確擲出對應的例外。
    /// </summary>
    [Fact]
    public void TestDecryptEntry_BoundaryAndInvalidInputs()
    {
        byte[] ciphertext = new byte[32];
        byte[] salt = new byte[16];
        byte[] iv = new byte[16];

        // 1. 測試不支援的演算法 URI 應擲出 NotSupportedException
        Assert.Throws<NotSupportedException>(() =>
            OdfEncryption.DecryptEntry(ciphertext, "pass", "invalid_uri", "PBKDF2", 32, 1000, salt, iv));

        // 2. 測試不支援的金鑰衍生函式應擲出 NotSupportedException
        Assert.Throws<NotSupportedException>(() =>
            OdfEncryption.DecryptEntry(ciphertext, "pass", OdfEncryption.Aes256AlgorithmUri, "scrypt", 32, 1000, salt, iv));

        // 3. 測試反覆運算次數超過 50000 應擲出 CryptographicException
        Assert.Throws<CryptographicException>(() =>
            OdfEncryption.DecryptEntry(ciphertext, "pass", OdfEncryption.Aes256AlgorithmUri, "PBKDF2", 32, 50001, salt, iv));

        // 4. 測試 null 參數的防禦性異常攔截
        Assert.Throws<NullReferenceException>(() =>
            OdfEncryption.DecryptEntry(null!, "pass", OdfEncryption.Aes256AlgorithmUri, "PBKDF2", 32, 1000, salt, iv));

        Assert.Throws<ArgumentNullException>(() =>
            OdfEncryption.DecryptEntry(ciphertext, null!, OdfEncryption.Aes256AlgorithmUri, "PBKDF2", 32, 1000, salt, iv));

        Assert.Throws<NotSupportedException>(() =>
            OdfEncryption.DecryptEntry(ciphertext, "pass", null!, "PBKDF2", 32, 1000, salt, iv));

        Assert.Throws<NotSupportedException>(() =>
            OdfEncryption.DecryptEntry(ciphertext, "pass", OdfEncryption.Aes256AlgorithmUri, null!, 32, 1000, salt, iv));

        Assert.Throws<NullReferenceException>(() =>
            OdfEncryption.DecryptEntry(ciphertext, "pass", OdfEncryption.Aes256AlgorithmUri, "PBKDF2", 32, 1000, null!, iv));

        Assert.Throws<ArgumentNullException>(() =>
            OdfEncryption.DecryptEntry(ciphertext, "pass", OdfEncryption.Aes256AlgorithmUri, "PBKDF2", 32, 1000, salt, null!));
    }

    /// <summary>
    /// 測試 ComputeHash 在處理未知雜湊別名與局部匹配時，是否能正確拒腳並擲出例外。
    /// </summary>
    [Fact]
    public void TestComputeHash_BoundaryAndInvalidInputs()
    {
        byte[] data = [1, 2, 3, 4];

        // 1. 測試無效雜湊名稱或局部匹配（例如 sha256extra）應擲出 NotSupportedException
        Assert.Throws<NotSupportedException>(() => OdfEncryption.ComputeHash(data, "sha256extra"));
        Assert.Throws<NotSupportedException>(() => OdfEncryption.ComputeHash(data, "sha1extra"));
        Assert.Throws<NotSupportedException>(() => OdfEncryption.ComputeHash(data, ""));
        Assert.Throws<NotSupportedException>(() => OdfEncryption.ComputeHash(data, "md5"));

        // 2. 測試正確的雜湊別名
        byte[] hashSha256 = OdfEncryption.ComputeHash(data, "SHA256");
        Assert.Equal(32, hashSha256.Length);

        byte[] hashSha1 = OdfEncryption.ComputeHash(data, "SHA1");
        Assert.Equal(20, hashSha1.Length);

        // 3. 測試 null 輸入
        Assert.Throws<ArgumentNullException>(() => OdfEncryption.ComputeHash(null!, "SHA256"));
        Assert.Throws<NotSupportedException>(() => OdfEncryption.ComputeHash(data, null!));
    }

    /// <summary>
    /// 測試列高的極端值與邊界條件，檢查寫入 NaN、無限大與負數時的行為。
    /// </summary>
    [Fact]
    public void TestWriteStartRow_RowHeightBoundaryConditions()
    {
        // 1. 測試負數高度 (例如 -15.0 pt)
        // 預期行為：OdfLength 會正常解析，並在 styles.xml 中寫入對應的負公分值 "-0.5292cm"
        using (var ms1 = new MemoryStream())
        {
            using (var writer = new OdsStreamWriter(ms1))
            {
                writer.WriteStartSheet("Sheet1");
                writer.WriteStartRow(height: -15.0);
                writer.WriteCell("Negative Height");
            }
            ms1.Position = 0;
            using var zip1 = new ZipArchive(ms1, ZipArchiveMode.Read);
            string stylesXml = ReadZipEntry(zip1, "styles.xml");
            Assert.Contains("style:row-height=\"-0.5292cm\"", stylesXml);
        }

        // 2. 測試高度為 0.0
        // 預期行為：應正確輸出 "0.0000cm"
        using (var ms2 = new MemoryStream())
        {
            using (var writer = new OdsStreamWriter(ms2))
            {
                writer.WriteStartSheet("Sheet1");
                writer.WriteStartRow(height: 0.0);
                writer.WriteCell("Zero Height");
            }
            ms2.Position = 0;
            using var zip2 = new ZipArchive(ms2, ZipArchiveMode.Read);
            string stylesXml = ReadZipEntry(zip2, "styles.xml");
            Assert.Contains("style:row-height=\"0.0000cm\"", stylesXml);
        }

        // 3. 測試極端值 NaN 以及無限大（Divergent Values）
        // 預期行為：寫入 NaN 或正負無限大時，ToString("F4") 會輸出 "NaN"、"Infinity" 或 "-Infinity"，
        // 導致 XML 出現非數值的 row-height 屬性（如 "NaNcm"、"Infinitycm"）。
        // 這裡進行實證檢測，確認此設計邊界不致於在程式碼層面崩潰（但此屬性在 ODF 規格上為 invalid 格式）。
        using (var ms3 = new MemoryStream())
        {
            using (var writer = new OdsStreamWriter(ms3))
            {
                writer.WriteStartSheet("Sheet1");
                writer.WriteStartRow(height: double.NaN);
                writer.WriteStartRow(height: double.PositiveInfinity);
            }
            ms3.Position = 0;
            using var zip3 = new ZipArchive(ms3, ZipArchiveMode.Read);
            string stylesXml = ReadZipEntry(zip3, "styles.xml");
            Assert.Contains("style:row-height=\"NaNcm\"", stylesXml);
            Assert.Contains("style:row-height=\"Infinitycm\"", stylesXml);
        }
    }

    private static string ReadZipEntry(ZipArchive zip, string entryName)
    {
        var entry = zip.GetEntry(entryName);
        if (entry is null)
            return string.Empty;
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}

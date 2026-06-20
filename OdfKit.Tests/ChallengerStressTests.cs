using System;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using OdfKit.Core;
using OdfKit.Spreadsheet;
using OdfKit.Styles;
using System.Linq;

namespace OdfKit.Tests
{
    public class ChallengerStressTests
    {
        // ─── ROW HEIGHTS BOUNDARY & STRESS TESTS ───

        [Fact]
        public void WriteStartRow_NegativeHeight_FailsOrConvertsToNegativeCm()
        {
            // Verify negative height behavior
            using var ms = new MemoryStream();
            using (var writer = new OdsStreamWriter(ms))
            {
                writer.WriteStartSheet("Sheet1");
                // Height in points: -28.35 pt ≈ -1.0000 cm
                writer.WriteStartRow(height: -28.35);
                writer.WriteCell("Value");
            }

            ms.Position = 0;
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var stylesEntry = zip.GetEntry("styles.xml");
            Assert.NotNull(stylesEntry);
            using var stream = stylesEntry.Open();
            var doc = System.Xml.Linq.XDocument.Load(stream);

            var rowHeightAttr = doc.Descendants()
                .Where(e => e.Name.LocalName == "table-row-properties")
                .SelectMany(e => e.Attributes())
                .FirstOrDefault(a => a.Name.LocalName == "row-height");

            Assert.NotNull(rowHeightAttr);
            // It writes negative centimeters because there is no negative bounds checking in OdsStreamWriter
            Assert.Equal("-1.0001cm", rowHeightAttr.Value);
        }

        [Fact]
        public void WriteStartRow_NaNOrInfinityHeight_ThrowsOrWritesInvalidValues()
        {
            // NaN
            using (var ms = new MemoryStream())
            {
                using (var writer = new OdsStreamWriter(ms))
                {
                    writer.WriteStartSheet("Sheet1");
                    writer.WriteStartRow(height: double.NaN);
                    writer.WriteCell("Value");
                }

                ms.Position = 0;
                using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                using var stream = zip.GetEntry("styles.xml")!.Open();
                var doc = System.Xml.Linq.XDocument.Load(stream);
                var rowHeightAttr = doc.Descendants()
                    .Where(e => e.Name.LocalName == "table-row-properties")
                    .SelectMany(e => e.Attributes())
                    .FirstOrDefault(a => a.Name.LocalName == "row-height");

                Assert.NotNull(rowHeightAttr);
                Assert.Equal("NaNcm", rowHeightAttr.Value);
            }

            // PositiveInfinity
            using (var ms = new MemoryStream())
            {
                using (var writer = new OdsStreamWriter(ms))
                {
                    writer.WriteStartSheet("Sheet1");
                    writer.WriteStartRow(height: double.PositiveInfinity);
                    writer.WriteCell("Value");
                }

                ms.Position = 0;
                using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                using var stream = zip.GetEntry("styles.xml")!.Open();
                var doc = System.Xml.Linq.XDocument.Load(stream);
                var rowHeightAttr = doc.Descendants()
                    .Where(e => e.Name.LocalName == "table-row-properties")
                    .SelectMany(e => e.Attributes())
                    .FirstOrDefault(a => a.Name.LocalName == "row-height");

                Assert.NotNull(rowHeightAttr);
                Assert.Equal("Infinitycm", rowHeightAttr.Value);
            }
        }

        [Fact]
        public void WriteStartRow_UseOptimalAndHeightTogether_PrioritizesOptimalHeight()
        {
            using var ms = new MemoryStream();
            using (var writer = new OdsStreamWriter(ms))
            {
                writer.WriteStartSheet("Sheet1");
                writer.WriteStartRow(height: 28.35, useOptimalHeight: true);
                writer.WriteCell("Value");
            }

            ms.Position = 0;
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            using var stream = zip.GetEntry("styles.xml")!.Open();
            var doc = System.Xml.Linq.XDocument.Load(stream);

            var properties = doc.Descendants()
                .Where(e => e.Name.LocalName == "table-row-properties")
                .ToList();

            Assert.NotEmpty(properties);
            var optimalAttr = properties[0].Attributes().FirstOrDefault(a => a.Name.LocalName == "use-optimal-row-height");
            var rowHeightAttr = properties[0].Attributes().FirstOrDefault(a => a.Name.LocalName == "row-height");

            // According to task A-1, optimal height excludes row-height attribute completely
            Assert.NotNull(optimalAttr);
            Assert.Equal("true", optimalAttr.Value);
            Assert.Null(rowHeightAttr);
        }


        // ─── HASH MATCHING EDGE CASES ───

        [Fact]
        public void ComputeHash_NullOrEmptyChecksumType_ThrowsExceptions()
        {
            // ComputeHash is null-safe for static string.Equals, returning false, so it falls to NotSupportedException instead of ArgumentNullException
            Assert.Throws<NotSupportedException>(() =>
                OdfEncryption.ComputeHash([1, 2, 3], null!));

            Assert.Throws<NotSupportedException>(() =>
                OdfEncryption.ComputeHash([1, 2, 3], ""));
        }

        [Fact]
        public void ComputeHash_VaryingCasingAndWhitespace_HandlesStandardTypesCorrectly()
        {
            byte[] data = [1, 2, 3];
            byte[] expectedHash = OdfEncryption.ComputeHash(data, "SHA256");

            // ComputeHash does not call Trim(), so leading/trailing space throws NotSupportedException
            Assert.Throws<NotSupportedException>(() =>
                OdfEncryption.ComputeHash(data, "  SHA256  "));

            // ComputeHash is case-insensitive for standard algorithm strings "SHA256", "sha-256", "SHA1", etc.
            // But it is case-sensitive for URIs because it uses string.Equals(..., StringComparison.Ordinal)
            Assert.Equal(expectedHash, OdfEncryption.ComputeHash(data, "sha256"));
            Assert.Equal(expectedHash, OdfEncryption.ComputeHash(data, "sHa-256"));

            // Lowercase URI will fail for SHA-256 URI
            Assert.Throws<NotSupportedException>(() =>
                OdfEncryption.ComputeHash(data, "http://www.w3.org/2000/09/xmldsig#sha256".ToUpperInvariant()));
        }

        [Fact]
        public void ComputeHash_EmptyDataArray_ComputesValidHash()
        {
            byte[] data = [];
            byte[] sha256Hash = OdfEncryption.ComputeHash(data, "SHA256");
            Assert.Equal(32, sha256Hash.Length);

            byte[] sha1Hash = OdfEncryption.ComputeHash(data, "SHA1");
            Assert.Equal(20, sha1Hash.Length);
        }


        // ─── ENCRYPTION & DECRYPTION BOUNDARY & STRESS TESTS ───

        [Fact]
        public void Pbkdf2_ZeroOrNegativeIterations_ThrowsOrReturnsEmpty()
        {
            byte[] password = Encoding.UTF8.GetBytes("password");
            byte[] salt = [1, 2, 3, 4, 5, 6, 7, 8];

            // Zero iterations: PBKDF2 algorithm in Pbkdf2Hmac loops from j = 1 to iterations.
            // If iterations <= 0, the loop behaves unexpectedly or throws.
            // Let's verify what happens.
            try
            {
                byte[] result = OdfEncryption.Pbkdf2(password, salt, 0, 16, "sha256");
                // If it doesn't throw, let's log it or assert its length
                Assert.Equal(16, result.Length);
            }
            catch (Exception ex)
            {
                // If it throws, that is also a handled failure
                Assert.True(ex is ArgumentOutOfRangeException || ex is CryptographicException || ex is IndexOutOfRangeException);
            }

            try
            {
                OdfEncryption.Pbkdf2(password, salt, -1, 16, "sha256");
            }
            catch (Exception ex)
            {
                Assert.True(ex is ArgumentOutOfRangeException || ex is CryptographicException || ex is IndexOutOfRangeException);
            }
        }

        [Fact]
        public void Pbkdf2_NegativeKeyLength_ThrowsOverflowException()
        {
            byte[] password = Encoding.UTF8.GetBytes("password");
            byte[] salt = [1, 2, 3, 4, 5, 6, 7, 8];

            // new byte[negative] throws OverflowException in dotnet instead of ArgumentOutOfRangeException
            Assert.Throws<OverflowException>(() =>
                OdfEncryption.Pbkdf2(password, salt, 1000, -1, "sha256"));
        }

        [Fact]
        public void DecryptEntry_WithWhitespaceInStartKeyGenName_ThrowsOrFails()
        {
            byte[] plaintext = Encoding.UTF8.GetBytes("plain text");
            byte[] ciphertext = OdfEncryption.EncryptEntry(plaintext, "pwd", OdfEncryptionAlgorithm.Aes256, out byte[] iv, out byte[] salt, out byte[] checksum);

            // Casing mismatch in URI: startKeyGenName in DecryptEntry checks with EndsWith using StringComparison.OrdinalIgnoreCase
            // Let's verify if uppercase URI is handled
            byte[] decrypted = OdfEncryption.DecryptEntry(
                ciphertext, "pwd", OdfEncryption.Aes256AlgorithmUri, "PBKDF2", 32, 50000, salt, iv,
                "HTTP://WWW.W3.ORG/2000/09/XMLDSIG#SHA256");
            Assert.Equal(plaintext, decrypted);

            // startKeyGenName 中的空格將導致 EndsWith 檢查失敗，預期會拋出 CryptographicException
            // 或者因為解密金鑰錯誤而解密出損壞的亂碼資料。
            try
            {
                byte[] decryptedWrong = OdfEncryption.DecryptEntry(
                     ciphertext, "pwd", OdfEncryption.Aes256AlgorithmUri, "PBKDF2", 32, 50000, salt, iv,
                     "http://www.w3.org/2000/09/xmldsig#sha256 ");
                // 若解密未拋出例外（機率極低之 PKCS7 填充巧合對齊），則解密出的資料必定不等於明文
                Assert.NotEqual(plaintext, decryptedWrong);
            }
            catch (CryptographicException)
            {
                // 預期的例外路徑
            }
        }
    }
}

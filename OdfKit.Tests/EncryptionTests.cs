using System.IO.Compression;
using System.Globalization;
using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using OdfKit.Core;
using OdfKit.Compliance;
using OdfKit.Text;

namespace OdfKit.Tests
{
    [Trait(TestCategories.Kind, TestCategories.Boundary)]
    public class EncryptionTests : IDisposable
    {
        private readonly CultureInfo? _originalDefaultCulture;

        public EncryptionTests()
        {
            _originalDefaultCulture = OdfLocalizer.DefaultCulture;
            OdfLocalizer.DefaultCulture = new CultureInfo("zh-TW");
        }

        public void Dispose()
        {
            OdfLocalizer.DefaultCulture = _originalDefaultCulture;
        }

        [Fact]
        public void TestPbkdf2IterationLimit()
        {
            int overLimit = OdfEncryption.MaxPbkdf2IterationCount + 1;

            // Verify direct Pbkdf2 throws CryptographicException
            Assert.Throws<CryptographicException>(() =>
            {
                OdfEncryption.Pbkdf2(new byte[16], new byte[8], overLimit, 16, "sha256");
            });

            // Verify direct DecryptEntry throws CryptographicException
            Assert.Throws<CryptographicException>(() =>
            {
                OdfEncryption.DecryptEntry(new byte[16], "password", OdfEncryption.Aes256AlgorithmUri, "PBKDF2", 32, overLimit, new byte[16], new byte[16]);
            });
        }

        /// <summary>
        /// LibreOffice 26.x 的傳統加密文件寫入 100,000 次 PBKDF2；上限必須高於實務值才能讀取。
        /// </summary>
        [Fact]
        public void Pbkdf2IterationLimit_AllowsRealWorldIterationCounts()
        {
            Assert.True(OdfEncryption.MaxPbkdf2IterationCount >= 1_300_000,
                "上限應涵蓋 OWASP 對 PBKDF2-HMAC-SHA1 的現行建議值。");

            byte[] key = OdfEncryption.Pbkdf2(new byte[20], new byte[16], 100_000, 16, "sha1");
            Assert.Equal(16, key.Length);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void TestPbkdf2IterationCountLessThanOneThrows(int iterationCount)
        {
            Assert.Throws<CryptographicException>(() =>
            {
                OdfEncryption.Pbkdf2(new byte[16], new byte[8], iterationCount, 16, "sha256");
            });
        }

        [Theory]
        [InlineData("sha256", "sha-256")]
        [InlineData("sha256", "http://www.w3.org/2000/09/xmldsig#sha256")]
        [InlineData("sha256", "http://www.w3.org/2001/04/xmlenc#sha256")]
        [InlineData("sha1", "sha-1")]
        [InlineData("sha1", "http://www.w3.org/2000/09/xmldsig#sha1")]
        public void Pbkdf2_HashAlgorithm_AcceptsKnownAliases(string canonicalName, string aliasName)
        {
            byte[] password = Encoding.UTF8.GetBytes("密碼");
            byte[] salt = Encoding.UTF8.GetBytes("salt");

            byte[] canonical = OdfEncryption.Pbkdf2(password, salt, 1024, 16, canonicalName);
            byte[] alias = OdfEncryption.Pbkdf2(password, salt, 1024, 16, aliasName);

            Assert.Equal(canonical, alias);
        }

        [Theory]
        [InlineData("sha2561")]
        [InlineData("http://www.w3.org/2000/09/xmldsig#sha2561")]
        [InlineData("sha512")]
        public void Pbkdf2_HashAlgorithm_RejectsUnsupportedNames(string hashName)
        {
            var exception = Assert.Throws<NotSupportedException>(() =>
                OdfEncryption.Pbkdf2(new byte[16], new byte[8], 1024, 16, hashName));

            Assert.Equal($"不支援的雜湊演算法：{hashName}", exception.Message);
        }



        [Fact]
        public void TestDecompressionBombDefense()
        {
            var ms = new MemoryStream();
            string originalContent = new string('A', 150); // 150 bytes
            string password = "DecompressPassword";

            // 1. Create and encrypt a package
            using (var package = OdfPackage.Create(ms, true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(originalContent), "text/xml");

                package.SaveOptions.Password = password;
                package.SaveOptions.EncryptionAlgorithm = OdfEncryptionAlgorithm.Aes256;
                package.Save();
            }

            // 2. Try to open the package with a MaxEntrySize limit smaller than 150 bytes
            ms.Position = 0;
            var loadOptions = new OdfLoadOptions
            {
                Password = password,
                MaxEntrySize = 100 // smaller than 150 bytes
            };

            Assert.Throws<SecurityException>(() =>
            {
                using (var package = OdfPackage.Open(ms, true, loadOptions))
                {
                    // Open triggers Decrypt, which decompresses content.xml
                }
            });
        }

        [Fact]
        public void TestAes256EncryptionDecryption_Roundtrip()
        {
            var ms = new MemoryStream();
            string originalContent = "<content>AES-256 Protected Data</content>";
            string password = "StrongPassword123";

            // 1. Create and encrypt
            using (var package = OdfPackage.Create(ms, true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(originalContent), "text/xml");

                package.SaveOptions.Password = password;
                package.SaveOptions.EncryptionAlgorithm = OdfEncryptionAlgorithm.Aes256;
                package.Save();
            }

            // 2. Open and decrypt with correct password
            ms.Position = 0;
            using (var package = OdfPackage.Open(ms, true, new OdfLoadOptions { Password = password }))
            {
                Assert.True(package.HasEntry("content.xml"));
                using (var stream = package.GetEntryStream("content.xml"))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string content = reader.ReadToEnd();
                    Assert.Equal(originalContent, content);
                }
            }

            // 3. Open with wrong password -> should throw CryptographicException
            ms.Position = 0;
            Assert.Throws<CryptographicException>(() =>
            {
                using (var package = OdfPackage.Open(ms, true, new OdfLoadOptions { Password = "WrongPassword" }))
                {
                    // Trigger evaluation
                }
            });
        }

        [Fact]
        public async Task SaveEncryptedAsyncAndLoadEncryptedAsync_DocumentRoundTrip()
        {
            const string password = "AsyncDocumentSecret";
            using var ms = new MemoryStream();
            using (TextDocument document = TextDocument.Create())
            {
                document.Body.Paragraphs.Add("非同步加密文件測試");

                await document.SaveEncryptedAsync(
                    ms,
                    password,
                    OdfEncryptionAlgorithm.Aes256Gcm,
                    TestContext.Current.CancellationToken);
            }

            ms.Position = 0;
            using OdfDocument loaded = await OdfDocument.LoadEncryptedAsync(
                ms,
                password,
                "async-encrypted.odt",
                TestContext.Current.CancellationToken);

            TextDocument textDocument = Assert.IsType<TextDocument>(loaded);
            Assert.Contains("非同步加密文件測試", textDocument.BodyTextRoot.TextContent);
        }

        [Fact]
        public async Task SaveEncryptedAsyncAndLoadEncryptedAsync_PackageRoundTrip()
        {
            const string password = "AsyncPackageSecret";
            using var ms = new MemoryStream();
            using (OdfPackage package = OdfPackage.Create(ms, leaveOpen: true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content>secret</content>"), "text/xml");
                package.WriteEntry("styles.xml", Encoding.UTF8.GetBytes("<styles/>"), "text/xml");
                package.WriteEntry("meta.xml", Encoding.UTF8.GetBytes("<meta/>"), "text/xml");
                package.WriteEntry("settings.xml", Encoding.UTF8.GetBytes("<settings/>"), "text/xml");

                await package.SaveEncryptedAsync(
                    password,
                    OdfEncryptionAlgorithm.Aes256Gcm,
                    TestContext.Current.CancellationToken);

                Assert.True(OdfEncryption.LastParallelEncryptedEntryCountForTests >= 4);
                Assert.True(OdfEncryption.LastParallelEncryptionMaxDegreeForTests >= 1);
            }

            ms.Position = 0;
            using OdfPackage loaded = await OdfPackage.LoadEncryptedAsync(
                ms,
                password,
                leaveOpen: true,
                TestContext.Current.CancellationToken);

            string content = Encoding.UTF8.GetString(loaded.ReadEntry("content.xml"));
            Assert.Contains("secret", content);
        }

        [Fact]
        public void TestBlowfishLegacyDecryption_Roundtrip()
        {
            var ms = new MemoryStream();
            string originalContent = "<content>Blowfish Legacy Protected Data</content>";
            string password = "BlowfishPassword";

            // 1. Create and encrypt with Blowfish
            using (var package = OdfPackage.Create(ms, true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(originalContent), "text/xml");

                package.SaveOptions.Password = password;
                package.SaveOptions.EncryptionAlgorithm = OdfEncryptionAlgorithm.Blowfish;
                package.Save();
            }

            // 2. Open and verify manifest contains Blowfish algorithm URI
            ms.Position = 0;
            using (var package = OdfPackage.Open(ms, true))
            {
                var info = package.FindEntryEncryptionInfo("content.xml");
                Assert.NotNull(info);

                // 寫入端採規範的簡短名稱（LibreOffice 的傳統加密讀取路徑只比對這個字面）。
                Assert.Equal(OdfEncryption.BlowfishAlgorithmName, info.AlgorithmName);
            }

            // 3. Reopen and decrypt with correct password
            ms.Position = 0;
            using (var package = OdfPackage.Open(ms, true, new OdfLoadOptions { Password = password }))
            {
                Assert.True(package.HasEntry("content.xml"));
                using (var stream = package.GetEntryStream("content.xml"))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string content = reader.ReadToEnd();
                    Assert.Equal(originalContent, content);
                }
            }
        }

        /// <summary>
        /// 驗證解密後的項目不再暴露記憶體映射指標。加密項目以 ZIP STORED 寫出，符合零拷貝路徑的
        /// 條件，但映射區留著的是密文；解密以 <c>SetContent</c> 寫入明文後若仍走零拷貝，
        /// <see cref="OdfDocument.Save(string)"/> 會把密文當成 DOM 寫出並擲出 XML 名稱錯誤。
        /// </summary>
        [Fact]
        public void EncryptedPackage_LoadThenSave_DoesNotReuseCiphertextFromMemoryMapping()
        {
            const string password = "mmf_roundtrip_password";
            string path = Path.Combine(Path.GetTempPath(), $"odfkit-mmf-{Guid.NewGuid():N}.odt");
            string resaved = Path.Combine(Path.GetTempPath(), $"odfkit-mmf-{Guid.NewGuid():N}-resaved.odt");

            try
            {
                using (TextDocument document = TextDocument.Create())
                {
                    document.AddParagraph("記憶體映射零拷貝與解密的互動測試。");
                    document.Save(path, new OdfSaveOptions { Password = password });
                }

                // 加密項目必須是 STORED（壓縮後長度等於原長度）；否則本測試無法涵蓋零拷貝路徑。
                using (var zip = ZipFile.OpenRead(path))
                {
                    ZipArchiveEntry contentEntry = zip.GetEntry("content.xml")!;
                    Assert.Equal(contentEntry.Length, contentEntry.CompressedLength);
                }

                // 以路徑載入才會啟用記憶體映射；用 Stream 載入不會走同一條路。
                using (OdfDocument loaded = OdfDocument.Load(path, new OdfLoadOptions { Password = password }))
                {
                    Assert.Contains("記憶體映射零拷貝", loaded.ExtractText(), StringComparison.Ordinal);
                    loaded.Save(resaved);
                }

                using (OdfDocument reopened = OdfDocument.Load(resaved))
                {
                    Assert.Contains("記憶體映射零拷貝", reopened.ExtractText(), StringComparison.Ordinal);
                }
            }
            finally
            {
                foreach (string temp in new[] { path, resaved })
                {
                    if (File.Exists(temp))
                        File.Delete(temp);
                }
            }
        }

        /// <summary>
        /// 驗證加密封裝的 manifest 符合 ODF 1.0～1.4 Part 2 §4.16：checksum-type 為 `#sha256-1k`、
        /// checksum 涵蓋壓縮後未加密資料的前 1024 位元組，且 encryption-data 的子元素依
        /// algorithm →〔start-key-generation〕→ key-derivation 排列。
        /// </summary>
        [Fact]
        public void EncryptedManifest_UsesSpecifiedChecksumAndElementOrder()
        {
            var ms = new MemoryStream();
            byte[] content = Encoding.UTF8.GetBytes("<content>checksum contract</content>");

            using (var package = OdfPackage.Create(ms, true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", content, "text/xml");
                package.SaveOptions.Password = "checksum_password";
                package.SaveOptions.EncryptionAlgorithm = OdfEncryptionAlgorithm.Aes256;
                package.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms, true))
            {
                OdfEncryptionInfo? info = package.FindEntryEncryptionInfo("content.xml");
                Assert.NotNull(info);
                Assert.Equal(OdfEncryption.Sha256OneKilobyteChecksumUri, info!.ChecksumType);

                byte[] deflated = Deflate(content);
                byte[] expected = OdfEncryption.ComputeHash(deflated, OdfEncryption.Sha256OneKilobyteChecksumUri);
                Assert.Equal(expected, info.Checksum);
            }

            ms.Position = 0;
            string manifest;
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Read, true))
            using (var reader = new StreamReader(zip.GetEntry("META-INF/manifest.xml")!.Open()))
            {
                manifest = reader.ReadToEnd();
            }

            int algorithmIndex = manifest.IndexOf("<manifest:algorithm", StringComparison.Ordinal);
            int startKeyIndex = manifest.IndexOf("<manifest:start-key-generation", StringComparison.Ordinal);
            int keyDerivationIndex = manifest.IndexOf("<manifest:key-derivation", StringComparison.Ordinal);
            Assert.True(algorithmIndex >= 0 && startKeyIndex > algorithmIndex && keyDerivationIndex > startKeyIndex);

            // encryption-data 只允許 checksum-type 與 checksum 兩個屬性，金鑰衍生擴充屬性不得外洩到此處。
            Assert.DoesNotContain("manifest:argon2", manifest, StringComparison.Ordinal);
            Assert.DoesNotContain("manifest:kdf-name", manifest, StringComparison.Ordinal);
        }

        /// <summary>
        /// 驗證 AES-256-CBC 解密相容 W3C XML Encryption §5.2 的填補（僅最後一個位元組表示長度），
        /// 這是 LibreOffice／OpenOffice 產生密碼保護文件時採用的形狀。
        /// </summary>
        [Fact]
        public void DecryptEntry_Aes256_AcceptsXmlEncryptionPadding()
        {
            byte[] plaintext = Encoding.UTF8.GetBytes("W3C padded ciphertext produced by another ODF implementation.");
            byte[] salt = new byte[16];
            byte[] iv = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            byte[] startKey;
            using (var sha = SHA256.Create())
            {
                startKey = sha.ComputeHash(Encoding.UTF8.GetBytes("w3c_padding_password"));
            }

            // PBKDF2 的 PRF 是規範固定的 HMAC-SHA-1，與 SHA-256 的 start key 無關。
            byte[] key = OdfEncryption.Pbkdf2(startKey, salt, 50000, 32, "sha1");

            // W3C 填補：前 N-1 個位元組值任意，最後一個位元組為 N。
            int paddingLength = 16 - (plaintext.Length % 16);
            byte[] padded = new byte[plaintext.Length + paddingLength];
            Buffer.BlockCopy(plaintext, 0, padded, 0, plaintext.Length);
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] filler = new byte[paddingLength - 1];
                rng.GetBytes(filler);
                Buffer.BlockCopy(filler, 0, padded, plaintext.Length, filler.Length);
            }
            padded[padded.Length - 1] = (byte)paddingLength;

            byte[] ciphertext;
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.None;
                using var encryptor = aes.CreateEncryptor();
                ciphertext = encryptor.TransformFinalBlock(padded, 0, padded.Length);
            }

            byte[] decrypted = OdfEncryption.DecryptEntry(
                ciphertext,
                "w3c_padding_password",
                OdfEncryption.Aes256AlgorithmUri,
                "PBKDF2",
                32,
                50000,
                salt,
                iv,
                "http://www.w3.org/2000/09/xmldsig#sha256");

            Assert.Equal(plaintext, decrypted);
        }

        /// <summary>
        /// 驗證 Blowfish 以 ODF 傳統 `Blowfish CFB` 模式加密：宣告規範 URI、密文長度與明文相同
        /// （CFB 不填補），且規範的簡短名稱等價可用。
        /// </summary>
        [Fact]
        public void EncryptEntry_Blowfish_UsesCipherFeedbackMode()
        {
            byte[] plaintext = Encoding.UTF8.GetBytes("Blowfish CFB has no padding, so sizes match.");
            byte[] ciphertext = OdfEncryption.EncryptEntry(
                plaintext, "BlowfishPassword", OdfEncryptionAlgorithm.Blowfish, out byte[] iv, out byte[] salt, out _);

            Assert.Equal("urn:oasis:names:tc:opendocument:xmlns:manifest:1.0#blowfish", OdfEncryption.BlowfishAlgorithmUri);
            Assert.Equal(plaintext.Length, ciphertext.Length);
            Assert.Equal(8, iv.Length);

            byte[] decrypted = OdfEncryption.DecryptEntry(
                ciphertext, "BlowfishPassword", OdfEncryption.BlowfishAlgorithmUri, "PBKDF2", 16, OdfEncryption.DefaultPbkdf2IterationCount, salt, iv, "sha1");
            Assert.Equal(plaintext, decrypted);

            // 規範允許以簡短名稱 "Blowfish CFB" 表示同一個演算法。
            byte[] viaShortName = OdfEncryption.DecryptEntry(
                ciphertext, "BlowfishPassword", OdfEncryption.BlowfishAlgorithmName, "PBKDF2", 16, OdfEncryption.DefaultPbkdf2IterationCount, salt, iv, "sha1");
            Assert.Equal(plaintext, viaShortName);
        }

        /// <summary>
        /// 驗證 LibreOffice 傳統加密文件的 manifest 形狀可解密：`Blowfish CFB` 簡短名稱、
        /// `SHA1/1K` 檢查碼、PBKDF2 100,000 次，且 `manifest:key-size` 與
        /// `manifest:start-key-generation` 皆缺席（須套用規範預設 16 位元組與 SHA-1）。
        /// 參數取自 LibreOffice 26.2.4.2 以 ODF 1.0／1.1 設定實際產生的檔案。
        /// </summary>
        [Fact]
        public void DecryptEntry_Blowfish_AcceptsLibreOfficeManifestDefaults()
        {
            byte[] plaintext = Encoding.UTF8.GetBytes("LibreOffice omits key-size and start-key-generation.");
            const string password = "pw123";
            const int iterations = 100_000;

            byte[] salt = new byte[16];
            byte[] iv = new byte[8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            byte[] startKey;
            using (var sha1 = SHA1.Create())
            {
                startKey = sha1.ComputeHash(Encoding.UTF8.GetBytes(password));
            }

            byte[] key = OdfEncryption.Pbkdf2(startKey, salt, iterations, OdfEncryption.BlowfishKeySizeBytes, "sha1");

            var cipher = new Org.BouncyCastle.Crypto.BufferedBlockCipher(
                new Org.BouncyCastle.Crypto.Modes.CfbBlockCipher(
                    new Org.BouncyCastle.Crypto.Engines.BlowfishEngine(), 64));
            cipher.Init(true, new Org.BouncyCastle.Crypto.Parameters.ParametersWithIV(
                new Org.BouncyCastle.Crypto.Parameters.KeyParameter(key), iv));
            byte[] ciphertext = cipher.DoFinal(plaintext);

            // key-size 傳 0、startKeyGenName 傳 null，模擬 LibreOffice 省略這兩項的 manifest。
            byte[] decrypted = OdfEncryption.DecryptEntry(
                ciphertext,
                password,
                OdfEncryption.BlowfishAlgorithmName,
                "PBKDF2",
                0,
                iterations,
                salt,
                iv,
                null);

            Assert.Equal(plaintext, decrypted);
        }

        /// <summary>
        /// 驗證 AES-256-CBC 的 PBKDF2 虛擬亂數函式固定為 HMAC-SHA-1（Part 2 §4.16.7），
        /// 與 `start-key-generation-name` 的 SHA-256 無關。這是與 LibreOffice 互通的前提。
        /// </summary>
        [Fact]
        public void EncryptEntry_Aes256_DerivesKeyWithHmacSha1PseudoRandomFunction()
        {
            byte[] plaintext = Encoding.UTF8.GetBytes("PBKDF2 PRF is HMAC-SHA-1 regardless of the start key digest.");
            const string password = "prf_contract";

            byte[] ciphertext = OdfEncryption.EncryptEntry(
                plaintext, password, OdfEncryptionAlgorithm.Aes256, out byte[] iv, out byte[] salt, out _);

            byte[] startKey;
            using (var sha256 = SHA256.Create())
            {
                startKey = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            }

            // 規範形狀：start key 用 SHA-256，PBKDF2 的 PRF 用 SHA-1。
            byte[] specKey = OdfEncryption.Pbkdf2(
                startKey, salt, OdfEncryption.DefaultPbkdf2IterationCount, OdfEncryption.Aes256KeySizeBytes, "sha1");

            using var aes = Aes.Create();
            aes.Key = specKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            using var decryptor = aes.CreateDecryptor();
            byte[] padded = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);

            int paddingLength = padded[^1];
            Assert.InRange(paddingLength, 1, 16);
            Assert.Equal(plaintext, padded[..^paddingLength]);
        }

        /// <summary>
        /// 驗證早期 OdfKit 版本以 HMAC-SHA-256 為 PBKDF2 虛擬亂數函式產出的封裝仍可解密：
        /// 封裝層在規範 PRF 的 checksum 不符時，會以舊 PRF 再嘗試一次。
        /// </summary>
        [Fact]
        public void Decrypt_Package_AcceptsLegacySha256PseudoRandomFunction()
        {
            const string password = "legacy_prf_password";
            byte[] content = Encoding.UTF8.GetBytes("<content>legacy HMAC-SHA-256 PBKDF2 shape</content>");

            var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", content, "text/xml");

                byte[] deflated = Deflate(content);
                byte[] salt = new byte[16];
                byte[] iv = new byte[16];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(salt);
                    rng.GetBytes(iv);
                }

                byte[] startKey;
                using (var sha256 = SHA256.Create())
                {
                    startKey = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                }

                // 早期形狀：PBKDF2 的 PRF 誤用 HMAC-SHA-256。
                byte[] legacyKey = OdfEncryption.Pbkdf2(startKey, salt, 50_000, OdfEncryption.Aes256KeySizeBytes, "sha256");

                byte[] ciphertext;
                using (var aes = Aes.Create())
                {
                    aes.Key = legacyKey;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    using var encryptor = aes.CreateEncryptor();
                    ciphertext = encryptor.TransformFinalBlock(deflated, 0, deflated.Length);
                }

                OdfPackageEntry entry = package.Entries["content.xml"];
                entry.SetContent(ciphertext);
                entry.EncryptionInfo = new OdfEncryptionInfo
                {
                    ChecksumType = OdfEncryption.Sha256OneKilobyteChecksumUri,
                    Checksum = OdfEncryption.ComputeHash(deflated, OdfEncryption.Sha256OneKilobyteChecksumUri),
                    AlgorithmName = OdfEncryption.Aes256AlgorithmUri,
                    InitialisationVector = iv,
                    KeyDerivationName = "PBKDF2",
                    KeySize = OdfEncryption.Aes256KeySizeBytes,
                    IterationCount = 50_000,
                    Salt = salt,
                    StartKeyGenerationName = "http://www.w3.org/2000/09/xmldsig#sha256",
                    StartKeySize = 32,
                    PlaintextSize = content.Length
                };

                package.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms, true, new OdfLoadOptions { Password = password }))
            using (var stream = package.GetEntryStream("content.xml"))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                Assert.Equal(Encoding.UTF8.GetString(content), reader.ReadToEnd());
            }
        }

        /// <summary>
        /// 驗證早期 OdfKit 版本的 Argon2id 縮寫參數名（`argon2-t`／`-m`／`-p`）與非標準 URI 仍可解密。
        /// </summary>
        [Fact]
        public void DecryptEntry_Argon2id_AcceptsLegacyParameterNames()
        {
            byte[] plaintext = Encoding.UTF8.GetBytes("legacy argon2 parameter names");
            byte[] ciphertext = OdfEncryption.EncryptEntry(
                plaintext, "legacy_argon2", OdfEncryptionAlgorithm.Aes256Gcm, out byte[] iv, out byte[] salt, out _);

            byte[] decrypted = OdfEncryption.DecryptEntry(
                ciphertext,
                "legacy_argon2",
                OdfEncryption.Aes256GcmAlgorithmUri,
                OdfEncryption.Argon2idLegacyDerivationUri,
                32,
                0,
                salt,
                iv,
                "http://www.w3.org/2000/09/xmldsig#sha256",
                "argon2id",
                3,
                65536,
                4);

            Assert.Equal(plaintext, decrypted);
        }

        /// <summary>
        /// 驗證早期 OdfKit 版本寫出的非規範 Blowfish CBC 密文仍可解密：寫入端已改為規範的 8-bit CFB，
        /// 但既有檔案宣告的 `xmldsig-more#blowfish-cbc` 必須保留讀取路徑。
        /// </summary>
        [Fact]
        public void DecryptEntry_Blowfish_AcceptsLegacyCipherBlockChainingCiphertext()
        {
            byte[] plaintext = Encoding.UTF8.GetBytes("legacy blowfish cbc ciphertext written by OdfKit 0.0.1");
            byte[] salt = new byte[16];
            byte[] iv = new byte[8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            byte[] startKey;
            using (var sha = SHA1.Create())
            {
                startKey = sha.ComputeHash(Encoding.UTF8.GetBytes("legacy_blowfish"));
            }

            byte[] key = OdfEncryption.Pbkdf2(startKey, salt, 50000, 16, "sha1");

            var cipher = new Org.BouncyCastle.Crypto.Paddings.PaddedBufferedBlockCipher(
                new Org.BouncyCastle.Crypto.Modes.CbcBlockCipher(new Org.BouncyCastle.Crypto.Engines.BlowfishEngine()),
                new Org.BouncyCastle.Crypto.Paddings.Pkcs7Padding());
            cipher.Init(true, new Org.BouncyCastle.Crypto.Parameters.ParametersWithIV(
                new Org.BouncyCastle.Crypto.Parameters.KeyParameter(key), iv));
            byte[] ciphertext = cipher.DoFinal(plaintext);

            byte[] decrypted = OdfEncryption.DecryptEntry(
                ciphertext,
                "legacy_blowfish",
                OdfEncryption.BlowfishCbcLegacyAlgorithmUri,
                "PBKDF2",
                16,
                50000,
                salt,
                iv,
                "sha1");

            Assert.Equal(plaintext, decrypted);
        }

        /// <summary>
        /// 驗證封裝層仍能解密早期 OdfKit 版本寫出的整份加密封裝：Blowfish CBC 宣告、
        /// `SHA256` 檢查碼型別，且檢查碼以未壓縮明文計算。
        /// </summary>
        [Fact]
        public void Decrypt_Package_AcceptsLegacyOdfKitEncryptionShape()
        {
            const string password = "legacy_package_password";
            byte[] content = Encoding.UTF8.GetBytes("<content>legacy package shape written by OdfKit 0.0.1</content>");

            var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", content, "text/xml");

                byte[] deflated = Deflate(content);

                byte[] salt = new byte[16];
                byte[] iv = new byte[8];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(salt);
                    rng.GetBytes(iv);
                }

                byte[] startKey;
                using (var sha1 = SHA1.Create())
                {
                    startKey = sha1.ComputeHash(Encoding.UTF8.GetBytes(password));
                }

                byte[] key = OdfEncryption.Pbkdf2(startKey, salt, 50000, 16, "sha1");

                var cipher = new Org.BouncyCastle.Crypto.Paddings.PaddedBufferedBlockCipher(
                    new Org.BouncyCastle.Crypto.Modes.CbcBlockCipher(new Org.BouncyCastle.Crypto.Engines.BlowfishEngine()),
                    new Org.BouncyCastle.Crypto.Paddings.Pkcs7Padding());
                cipher.Init(true, new Org.BouncyCastle.Crypto.Parameters.ParametersWithIV(
                    new Org.BouncyCastle.Crypto.Parameters.KeyParameter(key), iv));
                byte[] ciphertext = cipher.DoFinal(deflated);

                byte[] legacyChecksum;
                using (var sha256 = SHA256.Create())
                {
                    legacyChecksum = sha256.ComputeHash(content);
                }

                OdfPackageEntry entry = package.Entries["content.xml"];
                entry.SetContent(ciphertext);
                entry.EncryptionInfo = new OdfEncryptionInfo
                {
                    ChecksumType = "SHA256",
                    Checksum = legacyChecksum,
                    AlgorithmName = OdfEncryption.BlowfishCbcLegacyAlgorithmUri,
                    InitialisationVector = iv,
                    KeyDerivationName = "PBKDF2",
                    KeySize = 16,
                    IterationCount = 50000,
                    Salt = salt,
                    StartKeyGenerationName = "http://www.w3.org/2000/09/xmldsig#sha1",
                    StartKeySize = 20
                };

                package.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms, true, new OdfLoadOptions { Password = password }))
            using (var stream = package.GetEntryStream("content.xml"))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                Assert.Equal(Encoding.UTF8.GetString(content), reader.ReadToEnd());
            }
        }

        /// <summary>
        /// 驗證 1K 檢查碼只涵蓋前 1024 個位元組，而既有的完整摘要型別維持原行為。
        /// </summary>
        [Fact]
        public void ComputeHash_OneKilobyteChecksumTypes_CoverFirstKilobyteOnly()
        {
            byte[] data = new byte[4096];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 251);
            }

            byte[] firstKilobyte = new byte[OdfEncryption.OneKilobyteChecksumLength];
            Buffer.BlockCopy(data, 0, firstKilobyte, 0, firstKilobyte.Length);

            using (var sha256 = SHA256.Create())
            {
                Assert.Equal(
                    sha256.ComputeHash(firstKilobyte),
                    OdfEncryption.ComputeHash(data, OdfEncryption.Sha256OneKilobyteChecksumUri));
                Assert.Equal(
                    sha256.ComputeHash(data),
                    OdfEncryption.ComputeHash(data, "SHA256"));
            }

            using (var sha1 = SHA1.Create())
            {
                Assert.Equal(
                    sha1.ComputeHash(firstKilobyte),
                    OdfEncryption.ComputeHash(data, OdfEncryption.Sha1OneKilobyteChecksumName));
                Assert.Equal(
                    sha1.ComputeHash(firstKilobyte),
                    OdfEncryption.ComputeHash(data, OdfEncryption.Sha1OneKilobyteChecksumUri));
            }

            // 短於 1024 位元組時涵蓋全部內容。
            byte[] shortData = Encoding.UTF8.GetBytes("short");
            using (var sha256 = SHA256.Create())
            {
                Assert.Equal(
                    sha256.ComputeHash(shortData),
                    OdfEncryption.ComputeHash(shortData, OdfEncryption.Sha256OneKilobyteChecksumUri));
            }
        }

        private static byte[] Deflate(byte[] data)
        {
            using var buffer = new MemoryStream();
            using (var deflate = new DeflateStream(buffer, CompressionMode.Compress, true))
            {
                deflate.Write(data, 0, data.Length);
            }

            return buffer.ToArray();
        }

        [Fact]
        public void TestCustomCryptographyProvider_Integration()
        {
            var ms = new MemoryStream();
            string originalContent = "<content>Custom Cryptography Provider Data</content>";
            var customProvider = new MockCryptographyProvider();

            // 1. Save package with custom provider
            using (var package = OdfPackage.Create(ms, true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(originalContent), "text/xml");

                package.SaveOptions.CryptographyProvider = customProvider;
                package.Save();
            }

            // 2. Open and inspect manifest info
            ms.Position = 0;
            using (var package = OdfPackage.Open(ms, true))
            {
                var info = package.FindEntryEncryptionInfo("content.xml");
                Assert.NotNull(info);
                Assert.Equal("custom-rot13", info.AlgorithmName);
            }

            // 3. Open and decrypt using custom provider
            ms.Position = 0;
            var loadOptions = new OdfLoadOptions { CryptographyProvider = customProvider };
            using (var package = OdfPackage.Open(ms, true, loadOptions))
            {
                Assert.True(package.HasEntry("content.xml"));
                using (var stream = package.GetEntryStream("content.xml"))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string content = reader.ReadToEnd();
                    Assert.Equal(originalContent, content);
                }
            }
        }

        [Fact]
        public void TestOpenPgpProvider_ManifestEncryptedKeyRoundtrip()
        {
            var ms = new MemoryStream();
            string originalContent = "<content>OpenPGP Provider Data</content>";
            var provider = new MockOpenPgpCryptographyProvider();

            using (var package = OdfPackage.Create(ms, true))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(originalContent), "text/xml");
                package.SaveOptions.EncryptionAlgorithm = OdfEncryptionAlgorithm.OpenPgp;
                package.SaveOptions.CryptographyProvider = provider;
                package.SaveOptions.OpenPgpRecipients.Add(new OdfOpenPgpRecipient
                {
                    KeyId = "0123456789ABCDEF",
                    Recipient = "測試收件者",
                    PublicKey = [1, 2, 3]
                });
                package.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms, true))
            {
                var info = package.FindEntryEncryptionInfo("content.xml");
                Assert.NotNull(info);
                Assert.Equal(OdfEncryption.OpenPgpAlgorithmUri, info.AlgorithmName);
                var encryptedKey = Assert.Single(info.OpenPgpEncryptedKeys);
                Assert.Equal("0123456789ABCDEF", encryptedKey.KeyId);
                Assert.Equal("測試收件者", encryptedKey.Recipient);
                Assert.Equal(new byte[] { 1, 2, 3 }, encryptedKey.KeyPacket);
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms, true, new OdfLoadOptions { CryptographyProvider = provider }))
            {
                using var stream = package.GetEntryStream("content.xml");
                using var reader = new StreamReader(stream, Encoding.UTF8);
                Assert.Equal(originalContent, reader.ReadToEnd());
            }
        }

        [Fact]
        public void TestOpenPgpWithoutProviderThrows()
        {
            using var ms = new MemoryStream();
            using var package = OdfPackage.Create(ms, true);
            package.SetMimeType("application/vnd.oasis.opendocument.text");
            package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content/>"), "text/xml");
            package.SaveOptions.EncryptionAlgorithm = OdfEncryptionAlgorithm.OpenPgp;
            package.SaveOptions.Password = "password";

            var exception = Assert.Throws<NotSupportedException>(() => package.Save());
            Assert.Equal("OpenPGP 加密必須透過 IOdfCryptographyProvider 實作。", exception.Message);
        }

        private class MockCryptographyProvider : IOdfCryptographyProvider
        {
            public bool CanHandle(OdfEncryptionInfo info)
            {
                return info.AlgorithmName == "custom-rot13";
            }

            public byte[] Decrypt(byte[] ciphertext, OdfEncryptionInfo info, OdfLoadOptions loadOptions)
            {
                byte[] plaintext = new byte[ciphertext.Length];
                for (int i = 0; i < ciphertext.Length; i++)
                {
                    plaintext[i] = (byte)(ciphertext[i] ^ 0x5A);
                }
                return plaintext;
            }

            public byte[] Encrypt(byte[] plaintext, string entryPath, OdfSaveOptions saveOptions, out OdfEncryptionInfo info)
            {
                byte[] ciphertext = new byte[plaintext.Length];
                for (int i = 0; i < plaintext.Length; i++)
                {
                    ciphertext[i] = (byte)(plaintext[i] ^ 0x5A);
                }

                info = new OdfEncryptionInfo
                {
                    AlgorithmName = "custom-rot13",
                    ChecksumType = "SHA256",
                    Checksum = OdfEncryption.ComputeHash(plaintext, "SHA256"),
                    InitialisationVector = new byte[8],
                    KeyDerivationName = "None",
                    KeySize = 0,
                    IterationCount = 0,
                    Salt = new byte[8]
                };

                return ciphertext;
            }
        }

        private class MockOpenPgpCryptographyProvider : IOdfCryptographyProvider
        {
            public bool CanHandle(OdfEncryptionInfo info)
            {
                return info.AlgorithmName == OdfEncryption.OpenPgpAlgorithmUri ||
                    info.OpenPgpEncryptedKeys.Count > 0;
            }

            public byte[] Decrypt(byte[] ciphertext, OdfEncryptionInfo info, OdfLoadOptions loadOptions)
            {
                byte[] plaintext = new byte[ciphertext.Length];
                for (int i = 0; i < ciphertext.Length; i++)
                {
                    plaintext[i] = (byte)(ciphertext[i] ^ 0x33);
                }
                return plaintext;
            }

            public byte[] Encrypt(byte[] plaintext, string entryPath, OdfSaveOptions saveOptions, out OdfEncryptionInfo info)
            {
                byte[] ciphertext = new byte[plaintext.Length];
                for (int i = 0; i < plaintext.Length; i++)
                {
                    ciphertext[i] = (byte)(plaintext[i] ^ 0x33);
                }

                var recipient = saveOptions.OpenPgpRecipients.Single();
                info = new OdfEncryptionInfo
                {
                    AlgorithmName = OdfEncryption.OpenPgpAlgorithmUri,
                    ChecksumType = "SHA256",
                    Checksum = OdfEncryption.ComputeHash(plaintext, "SHA256"),
                    InitialisationVector = [],
                    KeyDerivationName = "OpenPGP",
                    KeySize = 0,
                    IterationCount = 0,
                    Salt = []
                };
                info.OpenPgpEncryptedKeys.Add(new OdfOpenPgpEncryptedKeyInfo
                {
                    KeyId = recipient.KeyId,
                    Recipient = recipient.Recipient,
                    AlgorithmName = "OpenPGP",
                    KeyPacket = recipient.PublicKey
                });

                return ciphertext;
            }
        }

        /// <summary>
        /// 測試當使用已知且支援的 SHA-256 雜湊類型時， <see cref="OdfEncryption.ComputeHash"/> 是否能正確傳回 32 位元組的雜湊值。
        /// </summary>
        /// <param name="checksumType">總和檢查碼類型名稱或 URI</param>
        [Theory]
        [InlineData("SHA256")]
        [InlineData("sha-256")]
        [InlineData("http://www.w3.org/2000/09/xmldsig#sha256")]
        [InlineData("http://www.w3.org/2001/04/xmlenc#sha256")]
        public void ComputeHash_KnownSha256Types_ReturnsThirtyTwoBytes(string checksumType)
        {
            byte[] result = OdfEncryption.ComputeHash([1, 2, 3], checksumType);
            Assert.Equal(32, result.Length);
        }

        /// <summary>
        /// 測試當輸入未知的雜湊類型時， <see cref="OdfEncryption.ComputeHash"/> 是否會擲出 <see cref="NotSupportedException"/> 。
        /// </summary>
        [Fact]
        public void ComputeHash_UnknownType_ThrowsNotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() =>
                OdfEncryption.ComputeHash([1], "sha2561"));
        }

        /// <summary>
        /// 測試當輸入僅為局部匹配的雜湊類型（例如 "sha256extra"）時， <see cref="OdfEncryption.ComputeHash"/> 是否會正確擲出 <see cref="NotSupportedException"/> 。
        /// </summary>
        [Fact]
        public void ComputeHash_UnknownType_ThrowsNotSupportedException_ForPartialMatch()
        {
            Assert.Throws<NotSupportedException>(() =>
                OdfEncryption.ComputeHash([1], "sha256extra"));
        }

        /// <summary>
        /// 測試使用精確的起始金鑰產生演算法名稱（例如結尾為 #sha256、等於 sha256 或 sha-256）進行解密時是否成功，而局部匹配的名稱則應失敗。
        /// </summary>
        [Fact]
        public void DecryptEntry_WithPreciseStartKeyGenName_Succeeds()
        {
            byte[] plaintext = Encoding.UTF8.GetBytes("Test plaintext data for encryption");
            byte[] ciphertext = OdfEncryption.EncryptEntry(plaintext, "MySecretPassword", OdfEncryptionAlgorithm.Aes256, out byte[] iv, out byte[] salt, out byte[] checksum);

            // 標準的 xmldsig#sha256
            byte[] decrypted1 = OdfEncryption.DecryptEntry(
                ciphertext, "MySecretPassword", OdfEncryption.Aes256AlgorithmUri, "PBKDF2", 32, OdfEncryption.DefaultPbkdf2IterationCount, salt, iv, "http://www.w3.org/2000/09/xmldsig#sha256");
            Assert.Equal(plaintext, decrypted1);

            // 完全相等的 sha256
            byte[] decrypted2 = OdfEncryption.DecryptEntry(
                ciphertext, "MySecretPassword", OdfEncryption.Aes256AlgorithmUri, "PBKDF2", 32, OdfEncryption.DefaultPbkdf2IterationCount, salt, iv, "sha256");
            Assert.Equal(plaintext, decrypted2);

            // 結尾為 #sha256 但為其他前綴（例如 xmlenc）
            byte[] decrypted3 = OdfEncryption.DecryptEntry(
                ciphertext, "MySecretPassword", OdfEncryption.Aes256AlgorithmUri, "PBKDF2", 32, OdfEncryption.DefaultPbkdf2IterationCount, salt, iv, "http://www.w3.org/2001/04/xmlenc#sha256");
            Assert.Equal(plaintext, decrypted3);

            // 局部匹配的名稱會衍生出錯誤金鑰。AES-CBC 解密改以 W3C XML Encryption §5.2 的填補規則
            // 移除填補（只看最後一個位元組），因此填補長度落在 1..16 之外時擲出例外；少數情況會通過
            // 填補檢查而解出垃圾資料，此時由封裝層的 checksum 攔截。重試以確保至少觀察到一次擲出。
            bool threw = false;
            for (int i = 0; i < 5; i++)
            {
                byte[] tempCiphertext = OdfEncryption.EncryptEntry(plaintext, "MySecretPassword", OdfEncryptionAlgorithm.Aes256, out byte[] tempIv, out byte[] tempSalt, out _);
                try
                {
                    byte[] wrong = OdfEncryption.DecryptEntry(
                        tempCiphertext, "MySecretPassword", OdfEncryption.Aes256AlgorithmUri, "PBKDF2", 32, OdfEncryption.DefaultPbkdf2IterationCount, tempSalt, tempIv, "sha256extra");
                    Assert.NotEqual(plaintext, wrong);
                }
                catch (Exception)
                {
                    threw = true;
                    break;
                }
            }
            Assert.True(threw, "預期使用錯誤金鑰解密應擲出例外（因填補長度不合法）");
        }

        /// <summary>
        /// 測試 Blowfish 演算法在使用精確的起始金鑰產生演算法名稱（例如結尾為 #sha1、等於 sha1 或 sha-1）進行解密時是否成功，而局部匹配的名稱則應失敗。
        /// </summary>
        [Fact]
        public void DecryptEntry_Blowfish_WithPreciseStartKeyGenName_Succeeds()
        {
            byte[] plaintext = Encoding.UTF8.GetBytes("Test plaintext data for Blowfish");
            byte[] ciphertext = OdfEncryption.EncryptEntry(plaintext, "BlowfishPassword", OdfEncryptionAlgorithm.Blowfish, out byte[] iv, out byte[] salt, out byte[] checksum);

            // 標準的 xmldsig#sha1
            byte[] decrypted1 = OdfEncryption.DecryptEntry(
                ciphertext, "BlowfishPassword", OdfEncryption.BlowfishAlgorithmUri, "PBKDF2", 16, OdfEncryption.DefaultPbkdf2IterationCount, salt, iv, "http://www.w3.org/2000/09/xmldsig#sha1");
            Assert.Equal(plaintext, decrypted1);

            // 完全相等的 sha1
            byte[] decrypted2 = OdfEncryption.DecryptEntry(
                ciphertext, "BlowfishPassword", OdfEncryption.BlowfishAlgorithmUri, "PBKDF2", 16, OdfEncryption.DefaultPbkdf2IterationCount, salt, iv, "sha1");
            Assert.Equal(plaintext, decrypted2);

            // 局部匹配的名稱解密結果應為垃圾資料（與明文不符）
            byte[] decryptedGarbage = OdfEncryption.DecryptEntry(
                ciphertext, "BlowfishPassword", OdfEncryption.BlowfishAlgorithmUri, "PBKDF2", 16, OdfEncryption.DefaultPbkdf2IterationCount, salt, iv, "sha1extra");
            Assert.NotEqual(plaintext, decryptedGarbage);
        }

        // ── OpenPGP 便利層測試 ────────────────────────────────────────────────────

        /// <summary>
        /// 模擬用的 PGP 金鑰提供者，不實際加密，直接傳回金鑰。
        /// </summary>
        private sealed class FakeOpenPgpKeyProvider : IOdfOpenPgpKeyProvider
        {
            /// <inheritdoc />
            public byte[] EncryptSessionKey(byte[] sessionKey, OdfOpenPgpRecipient recipient)
                => sessionKey;

            /// <inheritdoc />
            public byte[] DecryptSessionKey(byte[] encryptedKeyPacket, string keyId)
                => encryptedKeyPacket;
        }

        /// <summary>
        /// 測試 <see cref="OdfOpenPgpCryptographyProvider.CanHandle"/> 在傳入 OpenPGP 加密演算法名稱時，是否正確傳回 <see langword="true"/> 。
        /// </summary>
        [Fact]
        public void OdfOpenPgpCryptographyProvider_CanHandle_ReturnsTrueForOpenPgpEntry()
        {
            var provider = new OdfOpenPgpCryptographyProvider(new FakeOpenPgpKeyProvider());
            var info = new OdfEncryptionInfo
            {
                AlgorithmName = OdfEncryption.OpenPgpAlgorithmUri
            };
            Assert.True(provider.CanHandle(info));
        }

        /// <summary>
        /// 測試 <see cref="OdfOpenPgpCryptographyProvider.CanHandle"/> 在加密金鑰清單不為空時，是否正確傳回 <see langword="true"/> 。
        /// </summary>
        [Fact]
        public void OdfOpenPgpCryptographyProvider_CanHandle_ReturnsTrueWhenEncryptedKeysExist()
        {
            var provider = new OdfOpenPgpCryptographyProvider(new FakeOpenPgpKeyProvider());
            var info = new OdfEncryptionInfo();
            info.OpenPgpEncryptedKeys.Add(new OdfOpenPgpEncryptedKeyInfo
            {
                KeyId = "ABCD1234",
                KeyPacket = [1, 2, 3]
            });
            Assert.True(provider.CanHandle(info));
        }

        /// <summary>
        /// 測試以 <see cref="OdfOpenPgpCryptographyProvider"/> 進行 OpenPGP 加密與解密的完整流程是否能正確還原資料。
        /// </summary>
        [Fact]
        public void OdfOpenPgpCryptographyProvider_EncryptDecrypt_RoundTrip()
        {
            var fakeProvider = new FakeOpenPgpKeyProvider();
            var pgpProvider = new OdfOpenPgpCryptographyProvider(fakeProvider);

            var saveOptions = new OdfSaveOptions
            {
                EncryptionAlgorithm = OdfEncryptionAlgorithm.OpenPgp
            };
            saveOptions.OpenPgpRecipients.Add(new OdfOpenPgpRecipient
            {
                KeyId = "TESTKEY1",
                Recipient = "test@example.com"
            });

            byte[] plaintext = Encoding.UTF8.GetBytes("OdfKit OpenPGP round-trip test");

            // 加密
            byte[] ciphertext = pgpProvider.Encrypt(plaintext, "content/test.xml", saveOptions, out var info);

            Assert.NotEqual(plaintext, ciphertext);
            Assert.Single(info.OpenPgpEncryptedKeys);
            Assert.Equal("TESTKEY1", info.OpenPgpEncryptedKeys[0].KeyId);

            // 解密
            byte[] decrypted = pgpProvider.Decrypt(ciphertext, info, new OdfLoadOptions());
            Assert.Equal(plaintext, decrypted);
        }

        /// <summary>
        /// 測試當有多個 OpenPGP 收件者時，所有加密後的金鑰封包是否皆能正確寫入加密資訊中。
        /// </summary>
        [Fact]
        public void OdfOpenPgpCryptographyProvider_MultipleRecipients_AllKeysWrittenToEncryptionInfo()
        {
            var pgpProvider = new OdfOpenPgpCryptographyProvider(new FakeOpenPgpKeyProvider());
            var saveOptions = new OdfSaveOptions
            {
                EncryptionAlgorithm = OdfEncryptionAlgorithm.OpenPgp
            };
            saveOptions.OpenPgpRecipients.Add(new OdfOpenPgpRecipient { KeyId = "KEY001" });
            saveOptions.OpenPgpRecipients.Add(new OdfOpenPgpRecipient { KeyId = "KEY002" });

            byte[] plaintext = [0x01, 0x02, 0x03];
            pgpProvider.Encrypt(plaintext, "entry.xml", saveOptions, out var info);

            Assert.Equal(2, info.OpenPgpEncryptedKeys.Count);
            Assert.Contains(info.OpenPgpEncryptedKeys, k => k.KeyId == "KEY001");
            Assert.Contains(info.OpenPgpEncryptedKeys, k => k.KeyId == "KEY002");
        }

        /// <summary>
        /// 測試在未提供任何 OpenPGP 收件者時，<see cref="OdfOpenPgpCryptographyProvider.Encrypt"/> 會拒絕輸出不可解密密文。
        /// </summary>
        [Fact]
        public void OdfOpenPgpCryptographyProvider_Encrypt_WithoutRecipients_ThrowsInvalidOperationException()
        {
            var pgpProvider = new OdfOpenPgpCryptographyProvider(new FakeOpenPgpKeyProvider());
            var saveOptions = new OdfSaveOptions
            {
                EncryptionAlgorithm = OdfEncryptionAlgorithm.OpenPgp
            };

            Assert.Throws<InvalidOperationException>(() =>
                pgpProvider.Encrypt([0x01], "entry.xml", saveOptions, out _));
        }

        /// <summary>
        /// 測試當儲存後還原階段解密失敗時，封裝內容仍會回復到儲存前的明文狀態。
        /// </summary>
        [Fact]
        public void SaveToStream_OpenPgpEncryptOnlyProvider_DecryptFailureRestoresPlaintextState()
        {
            using var package = OdfPackage.Create(new MemoryStream(), leaveOpen: true);
            package.SetMimeType("application/vnd.oasis.opendocument.text");
            byte[] original = Encoding.UTF8.GetBytes("<content>still-plain</content>");
            package.WriteEntry("content.xml", original, "text/xml");
            package.WriteEntry("styles.xml", Encoding.UTF8.GetBytes("<styles/>"), "text/xml");
            package.WriteEntry("meta.xml", Encoding.UTF8.GetBytes("<meta/>"), "text/xml");
            package.WriteEntry("settings.xml", Encoding.UTF8.GetBytes("<settings/>"), "text/xml");

            package.SaveOptions.EncryptionAlgorithm = OdfEncryptionAlgorithm.OpenPgp;
            package.SaveOptions.CryptographyProvider = new EncryptOnlyOpenPgpProvider();
            package.SaveOptions.OpenPgpRecipients.Add(new OdfOpenPgpRecipient
            {
                KeyId = "KEY001",
                Recipient = "test@example.com",
                PublicKey = [0x01, 0x02]
            });

            Assert.Throws<CryptographicException>(() => package.SaveToStream(new MemoryStream()));

            byte[] roundtrip = package.ReadEntry("content.xml");
            Assert.Equal(original, roundtrip);
            Assert.Null(package.FindEntryEncryptionInfo("content.xml"));
        }

        /// <summary>
        /// 測試在解密時若無任何可用的私鑰能成功解密金鑰封包，是否正確擲出 <see cref="CryptographicException"/> 。
        /// </summary>
        [Fact]
        public void OdfOpenPgpCryptographyProvider_Decrypt_NoValidKey_ThrowsCryptographicException()
        {
            var provider = new OdfOpenPgpCryptographyProvider(new ThrowingKeyProvider());
            var info = new OdfEncryptionInfo
            {
                AlgorithmName = OdfEncryption.OpenPgpAlgorithmUri,
                InitialisationVector = new byte[16]
            };
            info.OpenPgpEncryptedKeys.Add(new OdfOpenPgpEncryptedKeyInfo
            {
                KeyId = "BADKEY",
                KeyPacket = [1, 2, 3]
            });

            Assert.Throws<CryptographicException>(() =>
                provider.Decrypt(new byte[16], info, new OdfLoadOptions()));
        }

        /// <summary>
        /// 測試多收件者 OpenPGP 解密在第一把金鑰失敗時，仍會嘗試後續收件者並成功解密。
        /// </summary>
        [Fact]
        public void OdfOpenPgpCryptographyProvider_Decrypt_FirstRecipientFails_ContinuesToNextRecipient()
        {
            var provider = new OdfOpenPgpCryptographyProvider(new FirstRecipientCorruptingKeyProvider());
            var saveOptions = new OdfSaveOptions
            {
                EncryptionAlgorithm = OdfEncryptionAlgorithm.OpenPgp
            };
            saveOptions.OpenPgpRecipients.Add(new OdfOpenPgpRecipient { KeyId = "BADKEY", Recipient = "bad@example.com" });
            saveOptions.OpenPgpRecipients.Add(new OdfOpenPgpRecipient { KeyId = "GOODKEY", Recipient = "good@example.com" });

            byte[] plaintext = Encoding.UTF8.GetBytes("fallback recipient decrypt");
            byte[] ciphertext = provider.Encrypt(plaintext, "content.xml", saveOptions, out OdfEncryptionInfo info);

            byte[] decrypted = provider.Decrypt(ciphertext, info, new OdfLoadOptions());
            Assert.Equal(plaintext, decrypted);
        }

        /// <summary>
        /// 模擬一個在任何操作下皆會擲出例外的金鑰提供者。
        /// </summary>
        private sealed class ThrowingKeyProvider : IOdfOpenPgpKeyProvider
        {
            /// <inheritdoc />
            public byte[] EncryptSessionKey(byte[] sessionKey, OdfOpenPgpRecipient recipient)
                => throw new InvalidOperationException("no key");

            /// <inheritdoc />
            public byte[] DecryptSessionKey(byte[] encryptedKeyPacket, string keyId)
                => throw new InvalidOperationException("no private key available");
        }

        private sealed class FirstRecipientCorruptingKeyProvider : IOdfOpenPgpKeyProvider
        {
            public byte[] EncryptSessionKey(byte[] sessionKey, OdfOpenPgpRecipient recipient)
                => sessionKey;

            public byte[] DecryptSessionKey(byte[] encryptedKeyPacket, string keyId)
            {
                if (string.Equals(keyId, "BADKEY", StringComparison.Ordinal))
                    return new byte[encryptedKeyPacket.Length];

                return encryptedKeyPacket;
            }
        }

        private sealed class EncryptOnlyOpenPgpProvider : IOdfCryptographyProvider
        {
            public bool CanHandle(OdfEncryptionInfo info)
            {
                return string.Equals(info.AlgorithmName, OdfEncryption.OpenPgpAlgorithmUri, StringComparison.Ordinal) ||
                    info.OpenPgpEncryptedKeys.Count > 0;
            }

            public byte[] Decrypt(byte[] ciphertext, OdfEncryptionInfo info, OdfLoadOptions loadOptions)
            {
                throw new CryptographicException("decrypt-disabled");
            }

            public byte[] Encrypt(byte[] plaintext, string entryPath, OdfSaveOptions saveOptions, out OdfEncryptionInfo info)
            {
                byte[] ciphertext = new byte[plaintext.Length];
                for (int i = 0; i < plaintext.Length; i++)
                    ciphertext[i] = (byte)(plaintext[i] ^ 0x2F);

                OdfOpenPgpRecipient recipient = saveOptions.OpenPgpRecipients.Single();
                info = new OdfEncryptionInfo
                {
                    AlgorithmName = OdfEncryption.OpenPgpAlgorithmUri,
                    ChecksumType = "SHA256",
                    Checksum = OdfEncryption.ComputeHash(plaintext, "SHA256"),
                    InitialisationVector = [.. new byte[16]],
                    KeyDerivationName = "OpenPGP",
                    KeySize = 32,
                    IterationCount = 1,
                    Salt = [.. new byte[8]]
                };
                info.OpenPgpEncryptedKeys.Add(new OdfOpenPgpEncryptedKeyInfo
                {
                    KeyId = recipient.KeyId,
                    Recipient = recipient.Recipient,
                    AlgorithmName = OdfEncryption.OpenPgpAlgorithmUri,
                    KeyPacket = recipient.PublicKey
                });
                return ciphertext;
            }
        }

        /// <summary>
        /// 測試在 <see cref="OdfLoadOptions"/> 設定 OpenPgpKeyProvider 後，是否會自動建立對應的密碼學提供者執行個體。
        /// </summary>
        [Fact]
        public void OdfLoadOptions_OpenPgpKeyProvider_AutoWiresCryptographyProvider()
        {
            var opts = new OdfLoadOptions
            {
                OpenPgpKeyProvider = new FakeOpenPgpKeyProvider()
            };
            Assert.NotNull(opts.CryptographyProvider);
            Assert.IsType<OdfOpenPgpCryptographyProvider>(opts.CryptographyProvider);
        }

        /// <summary>
        /// 測試在 <see cref="OdfSaveOptions"/> 設定 OpenPgpKeyProvider 後，是否會自動建立對應的密碼學提供者執行個體。
        /// </summary>
        [Fact]
        public void OdfSaveOptions_OpenPgpKeyProvider_AutoWiresCryptographyProvider()
        {
            var opts = new OdfSaveOptions
            {
                OpenPgpKeyProvider = new FakeOpenPgpKeyProvider()
            };
            Assert.NotNull(opts.CryptographyProvider);
            Assert.IsType<OdfOpenPgpCryptographyProvider>(opts.CryptographyProvider);
        }

        /// <summary>
        /// 測試在儲存加密文件時，PBKDF2 反覆運算次數為 <see cref="OdfEncryption.DefaultPbkdf2IterationCount"/>。
        /// </summary>
        [Fact]
        public void Encrypt_PbkdfIterationCount_UsesDefault()
        {
            using var doc = OdfDocument.Create(OdfDocumentKind.Text);
            using var ms = new MemoryStream();
            doc.SaveToStream(ms, new OdfSaveOptions { Password = "test" });
            ms.Position = 0;
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var manifestEntry = zip.GetEntry("META-INF/manifest.xml")!;
            using var sr = new StreamReader(manifestEntry.Open());
            string manifest = sr.ReadToEnd();
            Assert.Contains(
                "manifest:iteration-count=\"" + OdfEncryption.DefaultPbkdf2IterationCount.ToString(CultureInfo.InvariantCulture) + "\"",
                manifest);
        }

        /// <summary>
        /// 測試 AES-256-GCM 與 Argon2id 金鑰衍生的加密及解密完整流程是否能正確還原資料，
        /// 且 manifest.xml 中包含符合 loext 擴充格式的 xml 節點。
        /// </summary>
        [Fact]
        public void Aes256Gcm_Argon2id_RoundTrip_Succeeds()
        {
            using var doc = OdfDocument.Create(OdfDocumentKind.Text);
            using var ms = new MemoryStream();
            var saveOpts = new OdfSaveOptions
            {
                Password = "my_gcm_password",
                EncryptionAlgorithm = OdfEncryptionAlgorithm.Aes256Gcm
            };
            doc.SaveToStream(ms, saveOpts);

            // 驗證 manifest.xml
            ms.Position = 0;
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Read, true))
            {
                var manifestEntry = zip.GetEntry("META-INF/manifest.xml")!;
                using var sr = new StreamReader(manifestEntry.Open());
                string manifest = sr.ReadToEnd();
                Assert.Contains("aes256-gcm", manifest);

                // key-derivation-name 與 loext 參數名稱對標 LibreOffice 的
                // OpenDocument-v1.4+libreoffice-manifest-schema.rng Argon2id 分支。
                Assert.Contains(OdfEncryption.Argon2idDerivationUri, manifest);
                Assert.Contains("loext:argon2-iterations", manifest);
                Assert.Contains("loext:argon2-memory", manifest);
                Assert.Contains("loext:argon2-lanes", manifest);
            }

            // 驗證解密載入
            ms.Position = 0;
            var loadOpts = new OdfLoadOptions { Password = "my_gcm_password" };
            using var loadedDoc = OdfDocument.Load(ms, loadOpts);
            Assert.NotNull(loadedDoc);
            Assert.Equal(OdfDocumentKind.Text, loadedDoc.DocumentKind);
        }

        /// <summary>
        /// 測試 AES-256-GCM 密文遭竄改時，應以在地化密碼學例外拒絕解密。
        /// </summary>
        [Fact]
        public void Aes256Gcm_TamperedCiphertext_ThrowsLocalizedCryptographicException()
        {
            byte[] plaintext = Encoding.UTF8.GetBytes("AES-GCM authentication must reject tampered content.");
            byte[] ciphertext = OdfEncryption.EncryptEntry(
                plaintext,
                "gcm_password",
                OdfEncryptionAlgorithm.Aes256Gcm,
                out byte[] iv,
                out byte[] salt,
                out _);
            ciphertext[ciphertext.Length - 1] ^= 0x7F;

            CryptographicException exception = Assert.Throws<CryptographicException>(() =>
                OdfEncryption.DecryptEntry(
                    ciphertext,
                    "gcm_password",
                    OdfEncryption.Aes256GcmAlgorithmUri,
                    OdfEncryption.Argon2idDerivationUri,
                    32,
                    0,
                    salt,
                    iv,
                    "http://www.w3.org/2000/09/xmldsig#sha256",
                    "argon2id"));

            Assert.Equal(OdfLocalizer.GetMessage("Err_OdfEncryption_GcmDecryptionFailed"), exception.Message);
        }
    }
}

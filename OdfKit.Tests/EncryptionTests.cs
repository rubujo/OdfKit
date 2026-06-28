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
    public class EncryptionTests
    {
        public EncryptionTests()
        {
            OdfLocalizer.DefaultCulture = new CultureInfo("zh-TW");
        }

        [Fact]
        public void TestPbkdf2IterationLimit()
        {
            // Verify direct Pbkdf2 throws CryptographicException
            Assert.Throws<CryptographicException>(() =>
            {
                OdfEncryption.Pbkdf2(new byte[16], new byte[8], 50001, 16, "sha256");
            });

            // Verify direct DecryptEntry throws CryptographicException
            Assert.Throws<CryptographicException>(() =>
            {
                OdfEncryption.DecryptEntry(new byte[16], "password", OdfEncryption.Aes256AlgorithmUri, "PBKDF2", 32, 50001, new byte[16], new byte[16]);
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
                var info = package.GetEntryEncryptionInfo("content.xml");
                Assert.NotNull(info);
                Assert.Equal(OdfEncryption.BlowfishAlgorithmUri, info.AlgorithmName);
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
                var info = package.GetEntryEncryptionInfo("content.xml");
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
                var info = package.GetEntryEncryptionInfo("content.xml");
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
                ciphertext, "MySecretPassword", OdfEncryption.Aes256AlgorithmUri, "PBKDF2", 32, 50000, salt, iv, "http://www.w3.org/2000/09/xmldsig#sha256");
            Assert.Equal(plaintext, decrypted1);

            // 完全相等的 sha256
            byte[] decrypted2 = OdfEncryption.DecryptEntry(
                ciphertext, "MySecretPassword", OdfEncryption.Aes256AlgorithmUri, "PBKDF2", 32, 50000, salt, iv, "sha256");
            Assert.Equal(plaintext, decrypted2);

            // 結尾為 #sha256 但為其他前綴（例如 xmlenc）
            byte[] decrypted3 = OdfEncryption.DecryptEntry(
                ciphertext, "MySecretPassword", OdfEncryption.Aes256AlgorithmUri, "PBKDF2", 32, 50000, salt, iv, "http://www.w3.org/2001/04/xmlenc#sha256");
            Assert.Equal(plaintext, decrypted3);

            // 局部匹配的名稱應解密失敗，大多會擲出例外；若因隨機填補符合 PKCS7 格式，則重試以確保強健性
            bool threw = false;
            for (int i = 0; i < 5; i++)
            {
                byte[] tempCiphertext = OdfEncryption.EncryptEntry(plaintext, "MySecretPassword", OdfEncryptionAlgorithm.Aes256, out byte[] tempIv, out byte[] tempSalt, out _);
                try
                {
                    OdfEncryption.DecryptEntry(
                        tempCiphertext, "MySecretPassword", OdfEncryption.Aes256AlgorithmUri, "PBKDF2", 32, 50000, tempSalt, tempIv, "sha256extra");
                }
                catch (Exception)
                {
                    threw = true;
                    break;
                }
            }
            Assert.True(threw, "預期使用錯誤金鑰解密應擲出例外（因 PKCS7 填補驗證失敗）");
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
                ciphertext, "BlowfishPassword", OdfEncryption.BlowfishAlgorithmUri, "PBKDF2", 16, 50000, salt, iv, "http://www.w3.org/2000/09/xmldsig#sha1");
            Assert.Equal(plaintext, decrypted1);

            // 完全相等的 sha1
            byte[] decrypted2 = OdfEncryption.DecryptEntry(
                ciphertext, "BlowfishPassword", OdfEncryption.BlowfishAlgorithmUri, "PBKDF2", 16, 50000, salt, iv, "sha1");
            Assert.Equal(plaintext, decrypted2);

            // 局部匹配的名稱解密結果應為垃圾資料（與明文不符）
            byte[] decryptedGarbage = OdfEncryption.DecryptEntry(
                ciphertext, "BlowfishPassword", OdfEncryption.BlowfishAlgorithmUri, "PBKDF2", 16, 50000, salt, iv, "sha1extra");
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
        /// 測試在儲存加密文件時， PBKDF2 反覆運算次數是否確實為 50,000 次。
        /// </summary>
        [Fact]
        public void Encrypt_PbkdfIterationCount_Is50000()
        {
            using var doc = OdfDocument.Create(OdfDocumentKind.Text);
            using var ms = new MemoryStream();
            doc.SaveToStream(ms, new OdfSaveOptions { Password = "test" });
            ms.Position = 0;
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var manifestEntry = zip.GetEntry("META-INF/manifest.xml")!;
            using var sr = new StreamReader(manifestEntry.Open());
            string manifest = sr.ReadToEnd();
            Assert.Contains("50000", manifest);
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
                Assert.Contains("argon2id", manifest);
                Assert.Contains("argon2-t", manifest);
                Assert.Contains("argon2-m", manifest);
                Assert.Contains("argon2-p", manifest);
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

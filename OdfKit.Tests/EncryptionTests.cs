using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using OdfKit.Core;

namespace OdfKit.Tests
{
    public class EncryptionTests
    {
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
        public void TestBlowfishKeyLengthValidation()
        {
            var blowfish = new Blowfish();
            Assert.Throws<ArgumentException>(() => blowfish.Initialize(null!));
            Assert.Throws<ArgumentException>(() => blowfish.Initialize(new byte[0]));
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
    }
}

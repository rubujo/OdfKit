using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using OdfKit.Core;

namespace OdfKit.Tests
{
    public class EncryptionTests
    {
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

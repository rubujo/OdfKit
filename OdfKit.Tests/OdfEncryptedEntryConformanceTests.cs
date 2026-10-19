using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Verifies conditional requirements for encrypted manifest entries.
/// 驗證加密 manifest 項目的條件式要求。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public sealed class OdfEncryptedEntryConformanceTests
{
    /// <summary>
    /// Verifies encrypted entries declare a nonnegative plaintext size, including empty plaintext.
    /// 驗證加密項目宣告非負的明文大小，包含空明文。
    /// </summary>
    [Theory]
    [InlineData(OdfVersion.Odf12, true)]
    [InlineData(OdfVersion.Odf13, true)]
    [InlineData(OdfVersion.Odf14, true)]
    [InlineData(OdfVersion.Odf12, false)]
    [InlineData(OdfVersion.Odf13, false)]
    [InlineData(OdfVersion.Odf14, false)]
    public void EncryptedEntryRequiresPlaintextSize(OdfVersion version, bool encrypted)
    {
        foreach (long? size in new long?[] { null, -1, 0, 17 })
        {
            using var stream = new MemoryStream();
            using (OdfPackage writer = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.Text, version, true))
            {
                writer.WriteEntry("payload.bin", new byte[16], "application/octet-stream");
                writer.Save();
            }

            stream.Position = 0;
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
            {
                ZipArchiveEntry entry = archive.GetEntry("META-INF/manifest.xml")!;
                XDocument manifest;
                using (Stream input = entry.Open())
                    manifest = XDocument.Load(input);
                XNamespace ns = OdfNamespaces.Manifest;
                XElement file = manifest.Descendants(ns + "file-entry")
                    .Single(element => (string?)element.Attribute(ns + "full-path") == "payload.bin");
                file.SetAttributeValue(ns + "size", size);
                if (encrypted)
                {
                    file.Add(new XElement(ns + "encryption-data",
                    new XAttribute(ns + "checksum-type", "SHA1/1K"),
                    new XAttribute(ns + "checksum", Convert.ToBase64String(new byte[20])),
                    new XElement(ns + "algorithm",
                        new XAttribute(ns + "algorithm-name", "Blowfish CFB"),
                        new XAttribute(ns + "initialisation-vector", Convert.ToBase64String(new byte[8]))),
                    new XElement(ns + "start-key-generation", new XAttribute(ns + "start-key-generation-name", "SHA1")),
                    new XElement(ns + "key-derivation",
                        new XAttribute(ns + "key-derivation-name", "PBKDF2"),
                        new XAttribute(ns + "iteration-count", 1024),
                        new XAttribute(ns + "salt", Convert.ToBase64String(new byte[16])))));
                }
                entry.Delete();
                using Stream output = archive.CreateEntry("META-INF/manifest.xml").Open();
                manifest.Save(output);
            }

            stream.Position = 0;
            using OdfPackage package = OdfPackage.Open(stream);
            OdfValidationReport report = OdfPackageValidator.Validate(package);
            if (encrypted && size is null or < 0)
                Assert.Contains(report.Issues, issue => issue.RuleId == "ODF0120" && issue.PackagePath == "payload.bin");
            else
                Assert.DoesNotContain(report.Issues, issue => issue.RuleId == "ODF0120" && issue.PackagePath == "payload.bin");
        }
    }
    /// <summary>
    /// Verifies decrypted byte counts agree with the declared size before entry replacement.
    /// 驗證取代項目內容前，解密後位元組數符合宣告大小。
    /// </summary>
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 17)]
    [InlineData(true, 0)]
    [InlineData(true, 17)]
    public void DecryptionChecksDeclaredPlaintextSize(bool customProvider, int length)
    {
        byte[] plaintext = Enumerable.Range(0, length).Select(index => (byte)index).ToArray();
        var provider = customProvider ? new PlaintextTestProvider() : null;
        using var stream = new MemoryStream();
        using (OdfPackage writer = OdfPackage.Create(stream, true))
        {
            writer.SetMimeType("application/vnd.oasis.opendocument.text");
            writer.WriteEntry("payload.bin", plaintext, "application/octet-stream");
            writer.SaveOptions.Password = "password";
            writer.SaveOptions.EncryptionAlgorithm = OdfEncryptionAlgorithm.Blowfish;
            writer.SaveOptions.CryptographyProvider = provider;
            writer.Save();
        }

        foreach (int difference in new[] { -1, 0, 1 })
        {
            stream.Position = 0;
            using OdfPackage package = OdfPackage.Open(stream, true);
            package.LoadOptions.CryptographyProvider = provider;
            OdfPackageEntry entry = package.Entries["payload.bin"];
            OdfEncryptionInfo info = entry.EncryptionInfo!;
            Assert.NotNull(info);
            Assert.Equal(length, info.PlaintextSize);
            info.PlaintextSize = length + difference;
            byte[] ciphertext;
            using (Stream input = entry.OpenReader())
            using (var original = new MemoryStream())
            {
                input.CopyTo(original);
                ciphertext = original.ToArray();
            }

            if (difference == 0)
            {
                OdfEncryption.Decrypt(package, "password");
                Assert.Null(entry.EncryptionInfo);
                using Stream input = entry.OpenReader();
                using var result = new MemoryStream();
                input.CopyTo(result);
                Assert.Equal(plaintext, result.ToArray());
            }
            else
            {
                CryptographicException error = Assert.Throws<CryptographicException>(() => OdfEncryption.Decrypt(package, "password"));
                Assert.Equal(OdfLocalizer.GetMessage("Err_OdfEncryption_PlaintextSizeMismatch", length + difference, length), error.Message);
                Assert.Same(info, entry.EncryptionInfo);
                using Stream input = entry.OpenReader();
                using var result = new MemoryStream();
                input.CopyTo(result);
                Assert.Equal(ciphertext, result.ToArray());
            }
        }
    }

    private sealed class PlaintextTestProvider : IOdfCryptographyProvider
    {
        public bool CanHandle(OdfEncryptionInfo info) => info.AlgorithmName == "test-plaintext";

        public byte[] Decrypt(byte[] ciphertext, OdfEncryptionInfo info, OdfLoadOptions loadOptions) => ciphertext[1..];

        public byte[] Encrypt(byte[] plaintext, string entryPath, OdfSaveOptions saveOptions, out OdfEncryptionInfo info)
        {
            info = new OdfEncryptionInfo
            {
                AlgorithmName = "test-plaintext",
                ChecksumType = "SHA256",
                Checksum = OdfEncryption.ComputeHash(plaintext, "SHA256"),
                PlaintextSize = plaintext.Length == 0 ? null : 99,
                KeyDerivationName = "None"
            };
            byte[] ciphertext = new byte[plaintext.Length + 1];
            plaintext.CopyTo(ciphertext, 1);
            return ciphertext;
        }
    }
}

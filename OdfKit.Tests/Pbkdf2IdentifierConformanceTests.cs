using System.Security.Cryptography;
using System.Text;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Verifies the equivalent PBKDF2 identifiers defined by ODF.
/// 驗證 ODF 定義的等價 PBKDF2 識別名稱。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public sealed class Pbkdf2IdentifierConformanceTests
{
    private const string Identifier = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0#pbkdf2";

    /// <summary>
    /// Verifies decryption accepts the normative IRI as well as the short name.
    /// 驗證解密接受規範 IRI 與簡短名稱。
    /// </summary>
    [Theory]
    [InlineData("PBKDF2")]
    [InlineData(Identifier)]
    public void DecryptionAcceptsEquivalentIdentifiers(string identifier)
    {
        byte[] plaintext = Encoding.UTF8.GetBytes("ODF PBKDF2 identifier equivalence");
        byte[] ciphertext = OdfEncryption.EncryptEntry(plaintext, "password", OdfEncryptionAlgorithm.Blowfish,
            out byte[] iv, out byte[] salt, out _);

        byte[] result = OdfEncryption.DecryptEntry(ciphertext, "password", OdfEncryption.BlowfishAlgorithmUri,
            identifier, 16, OdfEncryption.DefaultPbkdf2IterationCount, salt, iv, "sha1");

        Assert.Equal(plaintext, result);
    }

    /// <summary>
    /// Verifies the normative IRI cannot bypass the iteration limit.
    /// 驗證規範 IRI 無法繞過反覆運算次數上限。
    /// </summary>
    [Fact]
    public void NormativeIdentifierRetainsIterationLimit()
    {
        Assert.Throws<CryptographicException>(() => OdfEncryption.DecryptEntry(new byte[16], "password",
            OdfEncryption.Aes256AlgorithmUri, Identifier, 32, OdfEncryption.MaxPbkdf2IterationCount + 1,
            new byte[16], new byte[16]));
    }

    /// <summary>
    /// Verifies both identifiers reject a zero iteration count while reading a manifest.
    /// 驗證讀取 manifest 時兩種識別名稱都拒絕零次反覆運算。
    /// </summary>
    [Theory]
    [InlineData("PBKDF2")]
    [InlineData(Identifier)]
    public void ManifestRejectsZeroIterations(string identifier)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            $"<manifest:manifest xmlns:manifest='urn:oasis:names:tc:opendocument:xmlns:manifest:1.0'><manifest:file-entry manifest:full-path='content.xml' manifest:media-type='text/xml'><manifest:encryption-data><manifest:key-derivation manifest:key-derivation-name='{identifier}' manifest:iteration-count='0'/></manifest:encryption-data></manifest:file-entry></manifest:manifest>"));
        var context = new OdfManifestLoadContext
        {
            Entries = new() { ["content.xml"] = new OdfPackageEntry("content.xml", Array.Empty<byte>()) },
            LoadOptions = new OdfLoadOptions(),
            Manifest = new(),
            DuplicatePaths = [],
            FileEntryIssues = []
        };

        Assert.Throws<CryptographicException>(() => OdfManifestLoader.Parse(stream, context));
    }

    /// <summary>
    /// Verifies similar IRIs are not mistaken for the normative identifier.
    /// 驗證相似 IRI 不會被誤認為規範識別名稱。
    /// </summary>
    [Theory]
    [InlineData("urn:oasis:names:tc:opendocument:xmlns:manifest:1.0#PBKDF2")]
    [InlineData("urn:oasis:names:tc:opendocument:xmlns:manifest:1.0#pbkdf2-extra")]
    public void DecryptionRejectsDifferentIris(string identifier)
    {
        Assert.Throws<NotSupportedException>(() => OdfEncryption.DecryptEntry(new byte[16], "password",
            OdfEncryption.Aes256AlgorithmUri, identifier, 32, 1024, new byte[16], new byte[16]));
    }
}

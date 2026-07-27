using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Verifies that OdfKit can decrypt password-protected ODF packages produced by other implementations.
/// 驗證 OdfKit 能解密他家實作產生的密碼保護 ODF 封裝。
/// </summary>
/// <remarks>
/// 素材由 LibreOffice 26.2 實機產生並提交至 <c>tests/fixtures/encryption-interop/</c>，因此互通契約
/// 不需要本機 LibreOffice 即可在 CI 驗證。逐項比對的加密參數見同目錄的 <c>manifest.json</c>。
/// </remarks>
[Trait(TestCategories.Kind, TestCategories.Smoke)]
[Trait(TestCategories.Kind, TestCategories.Interop)]
public class EncryptionInteropCorpusTests
{
    private static readonly JsonSerializerOptions ManifestOptions = new() { PropertyNameCaseInsensitive = true };

    public static TheoryData<string> FixtureIds()
    {
        var data = new TheoryData<string>();
        foreach (InteropFixture fixture in LoadManifest().Fixtures)
        {
            data.Add(fixture.Id);
        }

        return data;
    }

    /// <summary>
    /// 驗證每份素材都能以宣告的密碼解密，並取得預期文字。
    /// </summary>
    [Theory]
    [MemberData(nameof(FixtureIds))]
    public void ForeignEncryptedPackageDecryptsToExpectedText(string fixtureId)
    {
        InteropManifest manifest = LoadManifest();
        InteropFixture fixture = manifest.Fixtures.Single(f => f.Id == fixtureId);
        string path = Path.Combine(FixtureDirectory, fixture.Path);

        Assert.True(File.Exists(path), $"缺少互通素材：{fixture.Path}");
        Assert.Equal(fixture.Sha256, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());

        using OdfDocument document = OdfDocument.Load(path, new OdfLoadOptions { Password = manifest.Password });
        Assert.Contains(manifest.ExpectedText, document.ExtractText(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證素材的加密參數與 manifest 宣告一致；參數漂移會讓上面的解密測試失去針對性。
    /// </summary>
    [Theory]
    [MemberData(nameof(FixtureIds))]
    public void ForeignEncryptedPackageDeclaresExpectedEncryptionParameters(string fixtureId)
    {
        InteropManifest manifest = LoadManifest();
        InteropFixture fixture = manifest.Fixtures.Single(f => f.Id == fixtureId);
        string path = Path.Combine(FixtureDirectory, fixture.Path);

        // 整包加密只有單一 encrypted-package 項目；逐項目加密則直接看 content.xml。
        bool wholesome = string.Equals(fixture.Shape, "wholesome", StringComparison.Ordinal);
        string entryName = wholesome ? "encrypted-package" : "content.xml";

        using OdfPackage package = OdfPackage.Open(path);
        OdfEncryptionInfo? info = package.FindEntryEncryptionInfo(entryName);

        Assert.NotNull(info);
        Assert.Equal(fixture.AlgorithmName, info!.AlgorithmName);
        Assert.Equal(fixture.KeyDerivationName, info.KeyDerivationName);
        Assert.Equal(fixture.StartKeyGeneration, info.StartKeyGenerationName);

        // key-size 缺席時 manifest 記為 null，載入後維持 0，由解密流程依演算法套用規範預設。
        Assert.Equal(fixture.KeySize ?? 0, info.KeySize);

        if (wholesome)
        {
            // AEAD tag 已涵蓋完整性，因此不輸出逐項目 checksum。
            Assert.Empty(info.Checksum);
            Assert.Equal(fixture.Argon2!.Iterations.ToString(CultureInfo.InvariantCulture), info.ExtensionProperties["argon2-iterations"]);
            Assert.Equal(fixture.Argon2.MemoryKib.ToString(CultureInfo.InvariantCulture), info.ExtensionProperties["argon2-memory"]);
            Assert.Equal(fixture.Argon2.Lanes.ToString(CultureInfo.InvariantCulture), info.ExtensionProperties["argon2-lanes"]);
        }
        else
        {
            Assert.Equal(fixture.ChecksumType, info.ChecksumType);
            Assert.Equal(fixture.IterationCount, info.IterationCount);
        }
    }

    /// <summary>
    /// 驗證解密後可重新儲存為未加密封裝並保留內容；涵蓋記憶體映射零拷貝與解密內容的互動。
    /// </summary>
    [Theory]
    [MemberData(nameof(FixtureIds))]
    public void ForeignEncryptedPackageSurvivesLoadAndResave(string fixtureId)
    {
        InteropManifest manifest = LoadManifest();
        InteropFixture fixture = manifest.Fixtures.Single(f => f.Id == fixtureId);
        string path = Path.Combine(FixtureDirectory, fixture.Path);
        string resaved = Path.Combine(Path.GetTempPath(), $"odfkit-interop-{Guid.NewGuid():N}.odt");

        try
        {
            using (OdfDocument document = OdfDocument.Load(path, new OdfLoadOptions { Password = manifest.Password }))
            {
                document.Save(resaved);
            }

            using OdfDocument reopened = OdfDocument.Load(resaved);
            Assert.Contains(manifest.ExpectedText, reopened.ExtractText(), StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(resaved))
                File.Delete(resaved);
        }
    }

    private static string FixtureDirectory =>
        Path.Combine(FindRepositoryRoot(), "tests", "fixtures", "encryption-interop");

    private static InteropManifest LoadManifest()
    {
        string manifestPath = Path.Combine(FixtureDirectory, "manifest.json");
        InteropManifest? manifest = JsonSerializer.Deserialize<InteropManifest>(
            File.ReadAllText(manifestPath, Encoding.UTF8), ManifestOptions);

        Assert.NotNull(manifest);
        Assert.NotEmpty(manifest!.Fixtures);
        return manifest;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OdfKit.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("找不到 repository root。");
    }

    private sealed class InteropManifest
    {
        public string Password { get; set; } = string.Empty;

        public string ExpectedText { get; set; } = string.Empty;

        public List<InteropFixture> Fixtures { get; set; } = [];
    }

    private sealed class InteropFixture
    {
        public string Id { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public string? Shape { get; set; }

        public string Sha256 { get; set; } = string.Empty;

        public string AlgorithmName { get; set; } = string.Empty;

        public string ChecksumType { get; set; } = string.Empty;

        public string KeyDerivationName { get; set; } = string.Empty;

        public int IterationCount { get; set; }

        public string? StartKeyGeneration { get; set; }

        public int? KeySize { get; set; }

        public Argon2Parameters? Argon2 { get; set; }
    }

    private sealed class Argon2Parameters
    {
        public int Iterations { get; set; }

        public int MemoryKib { get; set; }

        public int Lanes { get; set; }
    }
}

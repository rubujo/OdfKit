using System.Text.Json;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證共用 JSON 序列化組態的可讀性與 Unicode 行為。
/// </summary>
public class OdfJsonSerializerOptionsTests
{
    /// <summary>
    /// 驗證 HumanReadable 組態保留中文且不產生 \uXXXX 跳脫序列。
    /// </summary>
    [Fact]
    public void HumanReadablePreservesUnicodeLiterals()
    {
        var model = new { message = "加入或修正 content.xml 的 office:version。" };
        string json = JsonSerializer.Serialize(model, OdfJsonSerializerOptions.HumanReadable);

        Assert.Contains("加入或修正", json);
        Assert.DoesNotContain("\\u52a0", json);
        Assert.Contains('\n', json);
    }

    /// <summary>
    /// 驗證 Manifest 組態可往返含中文 notes 的 fixture 中繼資料。
    /// </summary>
    [Fact]
    public void ManifestRoundTripsUnicodeFixtureNotes()
    {
        var document = new ManifestTestDocument
        {
            Fixtures =
            [
                new ManifestTestFixture
                {
                    Id = "unicode-notes",
                    Notes = "驗證 corpus manifest 中文備註",
                },
            ],
        };

        string json = JsonSerializer.Serialize(document, OdfJsonSerializerOptions.Manifest);
        Assert.Contains("驗證 corpus manifest 中文備註", json);
        Assert.DoesNotContain("\\u9a57", json);

        ManifestTestDocument? restored = JsonSerializer.Deserialize<ManifestTestDocument>(json, OdfJsonSerializerOptions.Manifest);
        Assert.NotNull(restored);
        Assert.Equal("驗證 corpus manifest 中文備註", restored.Fixtures[0].Notes);
    }

    /// <summary>
    /// 驗證 Compact 組態為單行且仍保留 Unicode 字面量。
    /// </summary>
    [Fact]
    public void CompactProducesSingleLineUnicodeJson()
    {
        var model = new { label = "蘋果" };
        string json = JsonSerializer.Serialize(model, OdfJsonSerializerOptions.Compact);

        Assert.DoesNotContain('\n', json);
        Assert.Contains("蘋果", json);
        Assert.DoesNotContain("\\u860b", json);
    }

    private sealed class ManifestTestDocument
    {
        public List<ManifestTestFixture> Fixtures { get; set; } = [];
    }

    private sealed class ManifestTestFixture
    {
        public string Id { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;
    }
}

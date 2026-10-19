using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OdfKit.Compliance;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Differential tests of element definitions: every instance was accepted or rejected by Jing against the official schema.
/// 元素定義的差異測試：每個實例都經 Jing 依官方 schema 判定接受或拒絕。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfElementDefinitionConformanceTests
{
    private static readonly Lazy<(string Namespaces, ElementCase[] Cases)> Fixture = new(Load);

    /// <summary>
    /// Gets the group and version pairs covered by the fixture.
    /// 取得固定資料涵蓋的群組與版本組合。
    /// </summary>
    public static TheoryData<string, string> GroupVersions { get; } = BuildGroupVersions();

    /// <summary>
    /// The fixture file is exactly the one recorded in the manifest.
    /// 固定資料檔與清單記錄的完全相同。
    /// </summary>
    [Fact]
    public void FixtureMatchesItsManifest()
    {
        using Stream fixture = Resource("OdfElementCases.jsonl.gz");
        string actual = Convert.ToHexString(SHA256.HashData(ReadAll(fixture))).ToLowerInvariant();
        using StreamReader manifest = new(Resource("OdfElementCases.manifest.txt"), Encoding.UTF8);
        Assert.Equal("fixture-sha256 " + actual, manifest.ReadLine());
        int lines = 0;
        while (manifest.ReadLine() is not null)
            lines++;
        Assert.Equal(Fixture.Value.Cases.Length, lines);
    }

    /// <summary>
    /// Parents, attributes, children and character data that the official schema accepts are accepted, and everything else is rejected.
    /// 官方 schema 接受的父元素、屬性、子元素與字元資料都被接受，其餘則被拒絕。
    /// </summary>
    /// <param name="group">The semantic group identifier. / 語意群組識別碼。</param>
    /// <param name="version">The ODF version label. / ODF 版本標籤。</param>
    [Theory]
    [MemberData(nameof(GroupVersions))]
    public void ElementStructureMatchesTheSchema(string group, string version)
    {
        List<string> failures = [];
        ElementCase[] cases = Cases(group, version);
        Assert.NotEmpty(cases);
        foreach (ElementCase item in cases)
            foreach (OdfComplianceProfile profile in Profiles(version))
            {
                OdfValidationReport report = Validate(item, profile);
                bool schemaRejected = report.Issues.Any(issue => issue.RuleId == "ODF3101");
                if (item.Expected && !report.IsValid)
                    failures.Add($"{item.Label} {item.Name} should be valid: " + string.Join(";", report.Issues.Take(2).Select(issue => issue.RuleId + ":" + issue.Message)));
                else if (!item.Expected && !schemaRejected)
                    failures.Add($"{item.Label} {item.Name} should be rejected by the schema");
            }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Select(failure => failure.Length > 300 ? failure[..300] : failure).Distinct().Take(10)));
    }

    private static TheoryData<string, string> BuildGroupVersions()
    {
        TheoryData<string, string> data = [];
        foreach ((string group, string version) in Fixture.Value.Cases.Select(item => (item.Group, item.Version)).Distinct().OrderBy(pair => pair.Group, StringComparer.Ordinal).ThenBy(pair => pair.Version, StringComparer.Ordinal))
            data.Add(group, version);
        return data;
    }

    private static ElementCase[] Cases(string group, string version) => Fixture.Value.Cases.Where(item => item.Group == group && item.Version == version).ToArray();

    private static OdfValidationReport Validate(ElementCase item, OdfComplianceProfile profile)
    {
        if (item.Kind == "base")
            return ValidatePackage(item, profile);
        string extension = item.Kind switch
        {
            "spreadsheet" => "x.fods",
            "presentation" => "x.fodp",
            "graphics" => "x.fodg",
            "chart" => "x.fodc",
            "image" => "x.fodi",
            _ => "x.fodt"
        };
        string xml = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document{Fixture.Value.Namespaces} office:mimetype=\"application/vnd.oasis.opendocument.{item.Kind}\" office:version=\"{item.Version}\">{item.Body}</office:document>";
        using MemoryStream input = new(Encoding.UTF8.GetBytes(xml));
        return OdfValidator.Validate(input, extension, profile);
    }

    private static OdfValidationReport ValidatePackage(ElementCase item, OdfComplianceProfile profile)
    {
        string content = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document-content{Fixture.Value.Namespaces} office:version=\"{item.Version}\">{item.Body}</office:document-content>";
        string manifest = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"{item.Version}\"><manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.base\" manifest:version=\"{item.Version}\"/><manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\"/></manifest:manifest>";
        using MemoryStream zip = new();
        using (ZipArchive archive = new(zip, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "mimetype", "application/vnd.oasis.opendocument.base", CompressionLevel.NoCompression);
            Write(archive, "META-INF/manifest.xml", manifest, CompressionLevel.Optimal);
            Write(archive, "content.xml", content, CompressionLevel.Optimal);
        }
        zip.Position = 0;
        using OdfPackage package = OdfPackage.Open(zip);
        return OdfPackageValidator.Validate(package, profile);
    }

    private static void Write(ZipArchive archive, string name, string text, CompressionLevel level)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, level);
        using Stream stream = entry.Open();
        byte[] bytes = new UTF8Encoding(false).GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static Stream Resource(string name)
    {
        Stream? resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        Assert.NotNull(resource);
        return resource;
    }

    private static byte[] ReadAll(Stream stream)
    {
        using MemoryStream copy = new();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static (string Namespaces, ElementCase[] Cases) Load()
    {
        using Stream resource = Resource("OdfElementCases.jsonl.gz");
        using GZipStream gzip = new(resource, CompressionMode.Decompress);
        using StreamReader reader = new(gzip, Encoding.UTF8);
        string namespaces = JsonDocument.Parse(reader.ReadLine()!).RootElement.GetProperty("namespaces").GetString()!;
        List<ElementCase> cases = [];
        while (reader.ReadLine() is { } line)
        {
            JsonElement row = JsonDocument.Parse(line).RootElement;
            cases.Add(new ElementCase(row[0].GetString()!, row[1].GetString()!, row[2].GetString()!, row[3].GetString()!, row[4].GetString()!, row[5].GetBoolean(), row[6].GetString()!));
        }
        return (namespaces, cases.ToArray());
    }

    private static OdfComplianceProfile[] Profiles(string version) => version switch
    {
        "1.2" => [OdfComplianceProfiles.OasisOdf12Strict, OdfComplianceProfiles.OasisOdf12Extended],
        "1.3" => [OdfComplianceProfiles.OasisOdf13Strict, OdfComplianceProfiles.OasisOdf13Extended],
        _ => [OdfComplianceProfiles.OasisOdf14Strict, OdfComplianceProfiles.OasisOdf14Extended]
    };

    private sealed record ElementCase(string Group, string Version, string Label, string Name, string Kind, bool Expected, string Body);
}

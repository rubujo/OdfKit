using System.Security.Cryptography;
using System.Text.Json;
using OdfKit.Collaboration;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 JSON Collaboration clean-room fixture manifest 契約。
/// </summary>
public sealed class CollaborationFixtureManifestTests
{
    private static readonly string[] DocumentedTdfOperations =
    [
        "delete",
        "move",
        "addParagraph",
        "splitParagraph",
        "mergeParagraph",
        "addText",
        "addTab",
        "addLineBreak",
        "addField",
        "updateField",
        "addTable",
        "addRows",
        "addCells",
        "addColumn",
        "deleteColumns",
        "addListStyle",
        "addHeaderFooter",
        "deleteHeaderFooterContent",
        "addNote",
        "documentLayout",
        "addFontDecl",
        "format",
        "addStyle",
        "changeStyle",
        "deleteStyle",
        "addDrawing",
    ];

    /// <summary>
    /// 驗證 manifest schema、來源邊界、路徑與 SHA-256。
    /// </summary>
    [Fact]
    public void Manifest_DeclaresCleanRoomCoverageAndValidHashes()
    {
        string fixtureRoot = GetCollaborationFixtureRoot();
        using JsonDocument manifest = LoadManifest();
        JsonElement root = manifest.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Contains("do not copy TDF Java source", root.GetProperty("cleanRoomPolicy").GetString(), StringComparison.Ordinal);
        Assert.Equal("https://tdf.github.io/odftoolkit/odfdom/operations/operations.html", root.GetProperty("sourceUrl").GetString());

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var coveredOperations = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement fixture in root.GetProperty("fixtures").EnumerateArray())
        {
            string id = RequiredString(fixture, "id");
            string relativePath = RequiredString(fixture, "path");
            string sourceType = RequiredString(fixture, "sourceType");
            string sourceUrl = RequiredString(fixture, "sourceUrl");
            string license = RequiredString(fixture, "license");
            string primaryOperation = RequiredString(fixture, "primaryOperation");
            string semanticStatus = RequiredString(fixture, "semanticStatus");
            string expectedReplay = RequiredString(fixture, "expectedReplay");
            string wireShape = RequiredString(fixture, "wireShape");
            string sha256 = RequiredString(fixture, "sha256");

            Assert.True(ids.Add(id), "Duplicate fixture id: " + id);
            Assert.True(paths.Add(relativePath), "Duplicate fixture path: " + relativePath);
            Assert.False(string.IsNullOrWhiteSpace(sourceType));
            Assert.False(string.IsNullOrWhiteSpace(license));
            Assert.False(string.IsNullOrWhiteSpace(primaryOperation));
            Assert.False(string.IsNullOrWhiteSpace(semanticStatus));
            Assert.False(string.IsNullOrWhiteSpace(expectedReplay));
            Assert.False(string.IsNullOrWhiteSpace(wireShape));
            Assert.DoesNotContain("github.com/tdf/odftoolkit/blob", sourceUrl, StringComparison.OrdinalIgnoreCase);

            string fullPath = Path.GetFullPath(Path.Combine(fixtureRoot, relativePath));
            Assert.StartsWith(fixtureRoot, fullPath, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(fullPath), "Missing collaboration fixture: " + relativePath);
            Assert.Equal(sha256, ComputeSha256(fullPath));

            if (string.Equals(sourceType, "tdf-public-docs", StringComparison.Ordinal))
            {
                coveredOperations.Add(primaryOperation);
            }

            Assert.True(fixture.TryGetProperty("expectedOperations", out JsonElement operations));
            Assert.Equal(JsonValueKind.Array, operations.ValueKind);
            Assert.True(operations.GetArrayLength() > 0);
            Assert.True(fixture.TryGetProperty("expectedReport", out JsonElement report));
            Assert.Equal(JsonValueKind.Object, report.ValueKind);
        }

        foreach (string operation in DocumentedTdfOperations)
        {
            Assert.Contains(operation, coveredOperations);
        }
    }

    /// <summary>
    /// 驗證所有 fixture 的 parse、replay、safety 與 strict 期望。
    /// </summary>
    [Fact]
    public void Fixtures_HonorManifestReplayAndSafetyExpectations()
    {
        string fixtureRoot = GetCollaborationFixtureRoot();
        using JsonDocument manifest = LoadManifest();
        foreach (JsonElement fixture in manifest.RootElement.GetProperty("fixtures").EnumerateArray())
        {
            string relativePath = RequiredString(fixture, "path");
            string id = RequiredString(fixture, "id");
            string semanticStatus = RequiredString(fixture, "semanticStatus");
            string json = File.ReadAllText(Path.Combine(fixtureRoot, relativePath));

            if (string.Equals(semanticStatus, "parse-safety", StringComparison.Ordinal))
            {
                Assert.Throws<JsonException>(() => OdtOperationLog.Parse(json, CreateSafetyProbeOptions(relativePath)));
                continue;
            }

            if (string.Equals(semanticStatus, "strict-unsupported", StringComparison.Ordinal))
            {
                var strictOptions = new OdtOperationCompatibilityOptions
                {
                    UnsupportedOperationPolicy = OdtUnsupportedOperationPolicy.Throw,
                };
                using TextDocument document = TextDocument.Create();
                Assert.Throws<NotSupportedException>(() => OdtOperationsImporter.Merge(document, json, strictOptions));
                continue;
            }

            OdtOperationLog log = OdtOperationLog.Parse(json, OdtOperationCompatibilityOptions.CreateTdfCompatibility());
            string[] expectedOperations = ExpectedOperations(fixture);
            foreach (OdtOperation operation in log.Operations)
            {
                Assert.Contains(operation.Name, expectedOperations);
            }

            if (string.Equals(semanticStatus, "roundtrip", StringComparison.Ordinal))
            {
                string serialized = log.Serialize(OdtOperationCompatibilityOptions.CreateTdfCompatibility());
                Assert.Contains("tdfFuture", serialized, StringComparison.Ordinal);
            }

            using TextDocument merged = OdtOperationsImporter.Merge(
                json,
                OdtOperationCompatibilityOptions.CreateTdfCompatibility(),
                out OdtOperationImportReport report);
            (int replayed, int ignored, int unsupported) = ExpectedReport(fixture);
            Assert.True(report.ReplayedCount == replayed, id + " replayed count mismatch.");
            Assert.True(report.IgnoredCount == ignored, id + " ignored count mismatch.");
            Assert.True(report.UnsupportedCount == unsupported, id + " unsupported count mismatch.");
            if (string.Equals(semanticStatus, "replay-safety", StringComparison.Ordinal))
            {
                Assert.False(string.IsNullOrWhiteSpace(report.SafetyLimitHitReason));
            }
        }
    }

    private static JsonDocument LoadManifest() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(GetCollaborationFixtureRoot(), "manifest.json")));

    private static string GetCollaborationFixtureRoot() =>
        Path.GetFullPath(Path.Combine(FindRepositoryRoot(), "tests", "fixtures", "collaboration"));

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

        throw new DirectoryNotFoundException("找不到 OdfKit repo root。");
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        Assert.True(element.TryGetProperty(propertyName, out JsonElement value), "Missing manifest property: " + propertyName);
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        string? text = value.GetString();
        Assert.False(string.IsNullOrWhiteSpace(text));
        return text!;
    }

    private static string ComputeSha256(string path)
    {
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(NormalizeLineEndings(File.ReadAllBytes(path)));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Strips CR bytes immediately preceding LF so the hash is independent of the checkout's
    /// <c>.gitattributes</c> <c>eol</c> normalization (repo declares <c>*.json text eol=crlf</c>).
    /// 移除緊接在 LF 前的 CR 位元組，使雜湊值不受 checkout 時 <c>.gitattributes</c> 的
    /// <c>eol</c> 正規化影響（repo 對 <c>*.json</c> 宣告 <c>eol=crlf</c>）。
    /// </summary>
    private static byte[] NormalizeLineEndings(byte[] raw)
    {
        using var output = new MemoryStream(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            if (raw[i] == (byte)'\r' && i + 1 < raw.Length && raw[i + 1] == (byte)'\n')
                continue;
            output.WriteByte(raw[i]);
        }
        return output.ToArray();
    }

    private static OdtOperationCompatibilityOptions CreateSafetyProbeOptions(string relativePath)
    {
        var options = new OdtOperationCompatibilityOptions();
        if (relativePath.Contains("operation-count", StringComparison.Ordinal))
        {
            options.Safety = new OdtOperationSafetyOptions { MaxOperationCount = 1 };
        }
        else if (relativePath.Contains("large-attrs", StringComparison.Ordinal))
        {
            options.Safety = new OdtOperationSafetyOptions { MaxAttributesLength = 16 };
        }

        return options;
    }

    private static string[] ExpectedOperations(JsonElement fixture)
        => fixture.GetProperty("expectedOperations")
            .EnumerateArray()
            .Select(operation => operation.GetString()!)
            .ToArray();

    private static (int Replayed, int Ignored, int Unsupported) ExpectedReport(JsonElement fixture)
    {
        JsonElement report = fixture.GetProperty("expectedReport");
        return (
            report.GetProperty("replayed").GetInt32(),
            report.GetProperty("ignored").GetInt32(),
            report.GetProperty("unsupported").GetInt32());
    }
}

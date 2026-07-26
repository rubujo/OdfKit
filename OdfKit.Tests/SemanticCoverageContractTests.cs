using System.Text.Json;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Verifies the auditable semantic API evidence contract.
/// 驗證可稽核的語意 API 證據契約。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Smoke)]
public class SemanticCoverageContractTests
{
    private static readonly string[] RequiredFormats =
        ["ODT", "ODS", "ODP", "ODG", "ODC", "ODB", "ODF", "ODI"];
    private static readonly string[] RequiredOperations =
        ["Create", "Get", "Find", "Set", "Update", "Remove", "Clear", "RoundTrip", "Interop"];
    private static readonly string[] RequiredQualityDimensions =
        ["ExistingDocument", "UnknownContentPreservation", "LegacyVersions", "DowngradeDiagnostics", "InvalidInput"];

    /// <summary>
    /// Verifies every ODF document format has evidence-backed semantic families and operations.
    /// 驗證每種 ODF 文件格式都具備證據支撐的語意族群與操作。
    /// </summary>
    [Fact]
    public void Manifest_CoversEveryPrimaryFormatAndOperation()
    {
        string root = FindRepositoryRoot();
        string path = Path.Combine(root, "docs", "semantic-coverage.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement manifest = document.RootElement;

        Assert.True(IsSchemaVersionSupported(manifest.GetProperty("schemaVersion").GetInt32()));
        Assert.Equal("1.4", manifest.GetProperty("odfVersion").GetString());
        Assert.Equal(
            "normalize-to-1.4-preserve-unknown",
            manifest.GetProperty("legacyVersionPolicy").GetString());
        JsonElement legacyEvidence = manifest.GetProperty("legacyVersionEvidence");
        Assert.Equal(
            ["1.0", "1.1", "1.2", "1.3"],
            legacyEvidence.GetProperty("versions").EnumerateArray().Select(item => item.GetString()!).ToArray());
        Assert.Equal(
            ["ODG", "ODP", "ODS", "ODT"],
            legacyEvidence.GetProperty("formats").EnumerateArray().Select(item => item.GetString()!).Order().ToArray());
        string legacyTestPath = legacyEvidence.GetProperty("test").GetString()!;
        string legacySymbol = legacyEvidence.GetProperty("symbol").GetString()!;
        Assert.Contains(
            legacySymbol,
            File.ReadAllText(Path.Combine(root, legacyTestPath)),
            StringComparison.Ordinal);
        JsonElement mutationEvidence = manifest.GetProperty("mutationEvidence");
        string mutationTestPath = mutationEvidence.GetProperty("test").GetString()!;
        string mutationTestSource = File.ReadAllText(Path.Combine(root, mutationTestPath));
        string[] mutationSymbols =
            [.. mutationEvidence.GetProperty("randomOperationSequences").EnumerateArray().Select(item => item.GetString()!)];
        Assert.Equal(4, mutationSymbols.Length);
        foreach (string mutationSymbol in mutationSymbols)
        {
            Assert.Contains(mutationSymbol, mutationTestSource, StringComparison.Ordinal);
        }
        Assert.True(mutationEvidence.GetProperty("repeatedSaveLoad").GetBoolean());
        Assert.True(File.Exists(Path.Combine(root, mutationEvidence.GetProperty("corpusDifferentialScript").GetString()!)));
        Assert.True(File.Exists(Path.Combine(root, mutationEvidence.GetProperty("corpusManifest").GetString()!)));

        JsonElement[] families = [.. manifest.GetProperty("families").EnumerateArray()];
        JsonElement[] qualityEvidence = [.. manifest.GetProperty("qualityEvidence").EnumerateArray()];
        foreach (JsonElement evidence in qualityEvidence)
        {
            string dimension = evidence.GetProperty("dimension").GetString()!;
            Assert.Contains(dimension, RequiredQualityDimensions);
            string testPath = evidence.GetProperty("test").GetString()!;
            string symbol = evidence.GetProperty("symbol").GetString()!;
            string testSource = File.ReadAllText(Path.Combine(root, testPath));
            Assert.Contains(symbol, testSource, StringComparison.Ordinal);
            string[] formats =
                [.. evidence.GetProperty("formats").EnumerateArray().Select(format => format.GetString()!)];
            Assert.NotEmpty(formats);
            Assert.All(formats, format => Assert.Contains(format, RequiredFormats));
        }

        foreach (string format in RequiredFormats)
        {
            Assert.Contains(families, family => family.GetProperty("format").GetString() == format);
        }

        foreach (JsonElement family in families)
        {
            string familyFormat = family.GetProperty("format").GetString()!;
            foreach (string dimension in RequiredQualityDimensions)
            {
                Assert.Contains(
                    qualityEvidence,
                    evidence => evidence.GetProperty("dimension").GetString() == dimension &&
                        evidence.GetProperty("formats").EnumerateArray()
                            .Any(format => format.GetString() == familyFormat));
            }

            Assert.False(family.TryGetProperty("status", out _));
            string[] familyTopics =
                [.. family.GetProperty("topics").EnumerateArray().Select(topic => topic.GetString()!)];
            Assert.NotEmpty(familyTopics);
            Assert.Equal(familyTopics.Length, familyTopics.Distinct(StringComparer.Ordinal).Count());
            Assert.DoesNotContain(familyTopics, string.IsNullOrWhiteSpace);
            Assert.NotEmpty(family.GetProperty("specification").EnumerateArray());
            Assert.False(string.IsNullOrWhiteSpace(family.GetProperty("limitations").GetString()));

            JsonElement operations = family.GetProperty("operations");
            foreach (string operation in RequiredOperations)
            {
                string? status = operations.GetProperty(operation).GetString();
                Assert.True(
                    IsOperationStatusAllowed(operation, status),
                    $"Invalid operation status: {operation} -> {status}");
            }

            foreach (string evidenceGroup in new[] { "implementation", "tests", "interop" })
            {
                JsonElement[] evidence = [.. family.GetProperty(evidenceGroup).EnumerateArray()];
                Assert.NotEmpty(evidence);
                foreach (JsonElement evidencePath in evidence)
                {
                    Assert.True(
                        File.Exists(Path.Combine(root, evidencePath.GetString()!)),
                        $"Missing {evidenceGroup} evidence: {evidencePath.GetString()}");
                }
            }

            HashSet<string> coveredOperations = [];
            Dictionary<string, HashSet<string>> topicOperations = familyTopics.ToDictionary(
                topic => topic,
                _ => new HashSet<string>(StringComparer.Ordinal),
                StringComparer.Ordinal);
            HashSet<string> focusedTopics = [];
            string[] interopPaths =
                [.. family.GetProperty("interop").EnumerateArray().Select(path => path.GetString()!)];
            foreach (JsonElement evidence in family.GetProperty("operationEvidence").EnumerateArray())
            {
                string testPath = evidence.GetProperty("test").GetString()!;
                string symbol = evidence.GetProperty("symbol").GetString()!;
                string source = File.ReadAllText(Path.Combine(root, testPath));
                Assert.Contains(symbol, source, StringComparison.Ordinal);
                string[] evidenceTopics =
                    [.. evidence.GetProperty("topics").EnumerateArray().Select(topic => topic.GetString()!)];
                Assert.NotEmpty(evidenceTopics);
                foreach (string topic in evidenceTopics)
                {
                    Assert.Contains(topic, familyTopics);
                    if (familyTopics.Length == 1 || evidenceTopics.Length < familyTopics.Length)
                    {
                        focusedTopics.Add(topic);
                    }
                }

                foreach (JsonElement operation in evidence.GetProperty("operations").EnumerateArray())
                {
                    string operationName = operation.GetString()!;
                    Assert.Contains(operationName, RequiredOperations);
                    if (operationName == "Interop")
                    {
                        Assert.Contains(testPath, interopPaths);
                    }
                    coveredOperations.Add(operationName);
                    foreach (string topic in evidenceTopics)
                    {
                        topicOperations[topic].Add(operationName);
                    }
                }
            }

            Assert.Equal(RequiredOperations.Order(), coveredOperations.Order());
            foreach (string topic in familyTopics)
            {
                Assert.Contains(topic, focusedTopics);
                string[] applicableOperations =
                [
                    .. RequiredOperations.Where(
                        operation => operations.GetProperty(operation).GetString() != "not-applicable"),
                ];
                Assert.Equal(applicableOperations.Order(), topicOperations[topic].Order());
            }
        }

        string provenancePath = Path.Combine(root, "docs", "provenance", "semantic-api-provenance.json");
        using JsonDocument provenanceDocument = JsonDocument.Parse(File.ReadAllText(provenancePath));
        JsonElement provenance = provenanceDocument.RootElement;
        Assert.Equal(1, provenance.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(
            "clean-room-specification-and-observation-only",
            provenance.GetProperty("policy").GetString());
        Assert.NotEmpty(provenance.GetProperty("forbiddenSources").EnumerateArray());
        JsonElement[] provenanceFamilies = [.. provenance.GetProperty("families").EnumerateArray()];
        Assert.Equal(
            families.Select(family => family.GetProperty("id").GetString()).Order(),
            provenanceFamilies.Select(family => family.GetProperty("id").GetString()).Order());
        foreach (JsonElement record in provenanceFamilies)
        {
            Assert.NotEmpty(record.GetProperty("specificationSources").EnumerateArray());
            Assert.NotEmpty(record.GetProperty("fixtureSources").EnumerateArray());
            Assert.NotEmpty(record.GetProperty("behaviorObservations").EnumerateArray());
            Assert.False(string.IsNullOrWhiteSpace(record.GetProperty("implementationBoundary").GetString()));
            foreach (JsonElement fixture in record.GetProperty("fixtureSources").EnumerateArray())
            {
                Assert.True(File.Exists(Path.Combine(root, fixture.GetString()!)));
            }
        }
    }

    /// <summary>
    /// Rejects legacy and cross-dimension semantic operation statuses.
    /// 拒絕舊版及跨維度的語意操作狀態。
    /// </summary>
    /// <param name="operation">The operation name. / 操作名稱。</param>
    /// <param name="status">The status value. / 狀態值。</param>
    [Theory]
    [InlineData("Create", "complete")]
    [InlineData("Create", "tested")]
    [InlineData("Create", "interop-tested")]
    [InlineData("Interop", "complete")]
    [InlineData("Interop", "tested")]
    [InlineData("Interop", "verified")]
    public void OperationStatusContract_RejectsLegacyAndCrossDimensionValues(string operation, string status)
    {
        Assert.False(IsOperationStatusAllowed(operation, status));
    }

    /// <summary>
    /// Rejects legacy semantic coverage schema versions.
    /// 拒絕舊版 semantic coverage schema 版本。
    /// </summary>
    /// <param name="schemaVersion">The schema version. / Schema 版本。</param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SchemaVersionContract_RejectsLegacyVersions(int schemaVersion)
    {
        Assert.False(IsSchemaVersionSupported(schemaVersion));
    }

    private static bool IsSchemaVersionSupported(int schemaVersion) => schemaVersion == 4;

    private static bool IsOperationStatusAllowed(string operation, string? status)
    {
        string[] allowedStatuses = operation == "Interop"
            ? ["interop-tested", "not-applicable"]
            : ["verified", "not-applicable"];
        return allowedStatuses.Contains(status, StringComparer.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OdfKit.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root could not be located.");
    }
}

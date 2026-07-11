using System.Text.Json;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Verifies the auditable semantic API completion contract.
/// 驗證可稽核的語意 API 完成契約。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Smoke)]
public class SemanticCoverageContractTests
{
    private static readonly string[] RequiredFormats = ["ODT", "ODS", "ODP", "ODG"];
    private static readonly string[] RequiredOperations =
        ["Create", "Get", "Find", "Set", "Update", "Remove", "Clear", "RoundTrip", "Interop"];

    /// <summary>
    /// Verifies every primary format has complete semantic families and evidence.
    /// 驗證每個主要格式都具備完成的語意族群與證據。
    /// </summary>
    [Fact]
    public void Manifest_CoversEveryPrimaryFormatAndOperation()
    {
        string root = FindRepositoryRoot();
        string path = Path.Combine(root, "docs", "semantic-coverage.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement manifest = document.RootElement;

        Assert.Equal(1, manifest.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("1.4", manifest.GetProperty("odfVersion").GetString());
        Assert.Equal(
            "normalize-to-1.4-preserve-unknown",
            manifest.GetProperty("legacyVersionPolicy").GetString());
        JsonElement legacyEvidence = manifest.GetProperty("legacyVersionEvidence");
        Assert.Equal(
            ["1.1", "1.2", "1.3"],
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

        JsonElement[] families = [.. manifest.GetProperty("families").EnumerateArray()];
        foreach (string format in RequiredFormats)
        {
            Assert.Contains(families, family => family.GetProperty("format").GetString() == format);
        }

        foreach (JsonElement family in families)
        {
            Assert.Equal("complete", family.GetProperty("status").GetString());
            Assert.NotEmpty(family.GetProperty("topics").EnumerateArray());
            Assert.NotEmpty(family.GetProperty("specification").EnumerateArray());
            Assert.False(string.IsNullOrWhiteSpace(family.GetProperty("limitations").GetString()));

            JsonElement operations = family.GetProperty("operations");
            foreach (string operation in RequiredOperations)
            {
                string? status = operations.GetProperty(operation).GetString();
                string[] allowedStatuses = operation == "Interop"
                    ? ["tested", "not-applicable"]
                    : ["complete", "not-applicable"];
                Assert.Contains(status, allowedStatuses);
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
            foreach (JsonElement evidence in family.GetProperty("operationEvidence").EnumerateArray())
            {
                string testPath = evidence.GetProperty("test").GetString()!;
                string symbol = evidence.GetProperty("symbol").GetString()!;
                string source = File.ReadAllText(Path.Combine(root, testPath));
                Assert.Contains(symbol, source, StringComparison.Ordinal);
                foreach (JsonElement operation in evidence.GetProperty("operations").EnumerateArray())
                {
                    coveredOperations.Add(operation.GetString()!);
                }
            }

            Assert.Equal(RequiredOperations.Order(), coveredOperations.Order());
        }
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

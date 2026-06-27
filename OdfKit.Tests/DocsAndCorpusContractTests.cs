using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OdfKit.Cli;
using OdfKit.Compliance;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 ODF Toolkit 對標文件與 corpus manifest 已宣告必要契約。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Smoke)]
public class DocsAndCorpusContractTests
{
    /// <summary>
    /// 驗證 parity 文件列出外部 validator 啟用方式與 mismatch 規則。
    /// </summary>
    [Fact]
    public void OdfToolkitParityDocumentDeclaresExternalBaselineContract()
    {
        string repoRoot = FindRepositoryRoot();
        string parity = File.ReadAllText(Path.Combine(repoRoot, "docs", "odf-toolkit-parity.md"));

        Assert.Contains("ODF Toolkit", parity, StringComparison.Ordinal);
        Assert.Contains("ODF Validator", parity, StringComparison.Ordinal);
        Assert.Contains("ODFKIT_ODFVALIDATOR_JAR", parity, StringComparison.Ordinal);
        Assert.Contains("--baseline odf-validator", parity, StringComparison.Ordinal);
        Assert.Contains("validate-corpus", parity, StringComparison.Ordinal);
        Assert.Contains("baselineMismatchCount", parity, StringComparison.Ordinal);
        Assert.Contains("documented exception", parity, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("schema-specific child collection", parity, StringComparison.Ordinal);
        Assert.Contains("child relation coverage", parity, StringComparison.Ordinal);
        Assert.Contains("ODFDOM-style sample traversal", parity, StringComparison.Ordinal);
        Assert.Contains("presentation page", parity, StringComparison.Ordinal);
        Assert.Contains("MathML formula object", parity, StringComparison.Ordinal);
        Assert.Contains("odf-official-corpus-sources.md", parity, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證官方 corpus 來源文件宣告 baseline 來源與命名邊界。
    /// </summary>
    [Fact]
    public void OfficialCorpusSourcesDocumentDeclaresBaselineSources()
    {
        string repoRoot = FindRepositoryRoot();
        string sources = File.ReadAllText(Path.Combine(repoRoot, "docs", "odf-official-corpus-sources.md"));

        Assert.Contains("https://odftoolkit.org/", sources, StringComparison.Ordinal);
        Assert.Contains("https://odftoolkit.org/conformance/ODFValidator.html", sources, StringComparison.Ordinal);
        Assert.Contains("https://github.com/tdf/odftoolkit", sources, StringComparison.Ordinal);
        Assert.Contains("https://github.com/openpreserve/odf-validator", sources, StringComparison.Ordinal);
        Assert.Contains("--baseline odf-validator", sources, StringComparison.Ordinal);
        Assert.Contains("Initialize-OdfExternalCorpus.ps1", sources, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證 profile 來源文件列出所有內建 profile 的權威與驗證狀態。
    /// </summary>
    [Fact]
    public void ProfileSourcesDocumentDeclaresBuiltInProfileVerificationStatus()
    {
        string repoRoot = FindRepositoryRoot();
        string document = File.ReadAllText(Path.Combine(repoRoot, "docs", "odf-profile-sources.md"));

        foreach (OdfComplianceProfile profile in OdfComplianceProfiles.BuiltIn)
        {
            Assert.Contains(profile.Id, document, StringComparison.Ordinal);
            Assert.Contains(profile.AuthorityLevel.ToString(), document, StringComparison.Ordinal);
            Assert.Contains(profile.VerificationStatus.ToString(), document, StringComparison.Ordinal);
        }

        Assert.Contains("NeedsActiveSource", document, StringComparison.Ordinal);
        Assert.Contains("CompatibilityOnly", document, StringComparison.Ordinal);
        Assert.Contains("不得在文件中標示為 official、verified 或 normative", document, StringComparison.Ordinal);
        Assert.Contains("all-known", document, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證 corpus manifest 宣告小型可提交 corpus 與外部 corpus 路徑規則。
    /// </summary>
    [Fact]
    public void CorpusManifestDeclaresFixtureMetadataContract()
    {
        string repoRoot = FindRepositoryRoot();
        string manifest = File.ReadAllText(Path.Combine(repoRoot, "docs", "corpus-manifest.md"));

        Assert.Contains("ODFKIT_PARITY_CORPUS_ROOT", manifest, StringComparison.Ordinal);
        Assert.Contains("license", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expected", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("roundTrip", manifest, StringComparison.Ordinal);
        Assert.Contains("validate-corpus", manifest, StringComparison.Ordinal);
        Assert.Contains("tests/fixtures/corpus/manifest.json", manifest, StringComparison.Ordinal);
        Assert.Contains("docs/examples/external-corpus/manifest.json", manifest, StringComparison.Ordinal);
        Assert.Contains("repo-generated-minimal-flat-text", manifest, StringComparison.Ordinal);
        Assert.Contains("generated-format-minimal", manifest, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證外部 corpus 範本宣告必要欄位與 baseline exception 格式。
    /// </summary>
    [Fact]
    public void ExternalCorpusTemplatesDeclareRequiredShape()
    {
        string repoRoot = FindRepositoryRoot();
        string manifestPath = Path.Combine(repoRoot, "docs", "examples", "external-corpus", "manifest.json");
        string exceptionsPath = Path.Combine(repoRoot, "docs", "examples", "external-corpus", "baseline-exceptions.json");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        using JsonDocument exceptions = JsonDocument.Parse(File.ReadAllText(exceptionsPath));

        JsonElement fixture = manifest.RootElement.GetProperty("fixtures")[0];
        Assert.Equal("external-review-required", fixture.GetProperty("license").GetString());
        Assert.Equal("ODF Validator sample", fixture.GetProperty("source").GetString());
        Assert.Equal(
            "https://odftoolkit.org/conformance/ODFValidator.html",
            fixture.GetProperty("sourceUri").GetString());
        Assert.Equal("semantic-equivalent", fixture.GetProperty("roundTrip").GetString());
        Assert.Equal("odf-validator", exceptions.RootElement.GetProperty("exceptions")[0].GetProperty("baseline").GetString());
    }

    /// <summary>
    /// 驗證外部 corpus 範本可由 CLI metadata-only 模式檢查。
    /// </summary>
    [Fact]
    public void ExternalCorpusTemplatesCanBeMetadataValidatedByCli()
    {
        string repoRoot = FindRepositoryRoot();
        string manifestPath = Path.Combine(repoRoot, "docs", "examples", "external-corpus", "manifest.json");
        string exceptionsPath = Path.Combine(repoRoot, "docs", "examples", "external-corpus", "baseline-exceptions.json");
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = OdfKitCli.Run(
            [
                "validate-corpus",
                manifestPath,
                "--metadata-only",
                "--format",
                "json",
                "--baseline-exceptions",
                exceptionsPath
            ],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        using JsonDocument json = JsonDocument.Parse(output.ToString());
        JsonElement summary = json.RootElement.GetProperty("summary");
        Assert.True(summary.GetProperty("metadataOnly").GetBoolean());
        Assert.Equal(2, summary.GetProperty("fixtureCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("baselineExceptionCount").GetInt32());
    }

    /// <summary>
    /// 驗證 corpus CI 腳本與 GitHub Actions 入口存在。
    /// </summary>
    [Fact]
    public void CorpusCiEntryPointsExist()
    {
        string repoRoot = FindRepositoryRoot();
        string scriptPath = Path.Combine(repoRoot, "eng", "Test-OdfCorpus.ps1");
        string initializeScriptPath = Path.Combine(repoRoot, "eng", "Initialize-OdfExternalCorpus.ps1");
        string workflowPath = Path.Combine(repoRoot, ".github", "workflows", "odf-corpus.yml");
        string script = File.ReadAllText(scriptPath);
        string initializeScript = File.ReadAllText(initializeScriptPath);
        string workflow = File.ReadAllText(workflowPath);

        Assert.Contains("validate-corpus", script, StringComparison.Ordinal);
        Assert.Contains("ODFKIT_PARITY_CORPUS_ROOT", script, StringComparison.Ordinal);
        Assert.Contains("ODFKIT_ODFVALIDATOR_JAR", script, StringComparison.Ordinal);
        Assert.Contains("--metadata-only", script, StringComparison.Ordinal);
        Assert.Contains("manifest.json", initializeScript, StringComparison.Ordinal);
        Assert.Contains("baseline-exceptions.json", initializeScript, StringComparison.Ordinal);
        Assert.Contains("Test-OdfCorpus.ps1", workflow, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證快速 CI 與測試策略文件使用 trait 分類，而不是硬編碼測試類別清單。
    /// </summary>
    [Fact]
    public void CiSmokeTestsUseTraitCategoryFilter()
    {
        string repoRoot = FindRepositoryRoot();
        string workflow = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "ci.yml"));
        string strategy = File.ReadAllText(Path.Combine(repoRoot, "docs", "testing-strategy.md"));
        string categories = File.ReadAllText(Path.Combine(repoRoot, "OdfKit.Tests", "TestCategories.cs"));

        Assert.Contains("Category=Smoke", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("FullyQualifiedName~OdfKit.Tests.", workflow, StringComparison.Ordinal);
        Assert.Contains("TestCategories", strategy, StringComparison.Ordinal);
        Assert.Contains("--filter Category=Smoke", strategy, StringComparison.Ordinal);
        Assert.Contains("public const string Smoke = \"Smoke\";", categories, StringComparison.Ordinal);
        Assert.Contains("public const string Interop = \"Interop\";", categories, StringComparison.Ordinal);
        Assert.Contains("public const string Corpus = \"Corpus\";", categories, StringComparison.Ordinal);
        Assert.Contains("public const string Compliance = \"Compliance\";", categories, StringComparison.Ordinal);
        Assert.Contains("public const string Scenario = \"Scenario\";", categories, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證 typed DOM coverage artifact 腳本與 GitHub Actions 入口存在。
    /// </summary>
    [Fact]
    public void TypedDomCoverageArtifactEntryPointsExist()
    {
        string repoRoot = FindRepositoryRoot();
        string scriptPath = Path.Combine(repoRoot, "eng", "Test-OdfTypedDomCoverage.ps1");
        string workflowPath = Path.Combine(repoRoot, ".github", "workflows", "typed-dom-coverage.yml");
        string script = File.ReadAllText(scriptPath);
        string workflow = File.ReadAllText(workflowPath);

        Assert.Contains("typed-dom-coverage", script, StringComparison.Ordinal);
        Assert.Contains("odf-typed-dom-coverage.json", script, StringComparison.Ordinal);
        Assert.Contains("schemaElementCount", script, StringComparison.Ordinal);
        Assert.Contains("Test-OdfTypedDomCoverage.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("upload-artifact", workflow, StringComparison.Ordinal);
        Assert.Contains("odf-typed-dom-coverage", workflow, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證大型產生式成品與同步資源表已記錄來源與維護規則。
    /// </summary>
    [Fact]
    public void GeneratedArtifactProvenanceDeclaresMaintenanceRules()
    {
        string repoRoot = FindRepositoryRoot();
        string provenance = File.ReadAllText(Path.Combine(repoRoot, "docs", "provenance", "README.md"));
        string localizer = File.ReadAllText(Path.Combine(repoRoot, "OdfKit", "Compliance", "OdfLocalizer.Exceptions.cs"));
        string domWrapper = File.ReadAllText(Path.Combine(repoRoot, "OdfKit", "DOM", "Generated", "AnimationAnimateElement.g.cs"));
        string schemaProvider = File.ReadAllText(Path.Combine(repoRoot, "OdfKit", "Compliance", "Generated", "Odf14OfficialSchemaProvider.g.cs"));

        Assert.Contains("OdfKit/DOM/Generated/*.g.cs", provenance, StringComparison.Ordinal);
        Assert.Contains("OdfKit/Compliance/Generated/Odf*OfficialSchemaProvider.g.cs", provenance, StringComparison.Ordinal);
        Assert.Contains("OdfKit/Compliance/OdfLocalizer.Exceptions.cs", provenance, StringComparison.Ordinal);
        Assert.Contains("不可手動編輯", provenance, StringComparison.Ordinal);
        Assert.Contains("不可只手動修改單一文化", localizer, StringComparison.Ordinal);
        Assert.Contains("<auto-generated", domWrapper, StringComparison.Ordinal);
        Assert.Contains("<auto-generated", schemaProvider, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證 repo 內可提交 corpus manifest 可由 CLI 直接執行。
    /// </summary>
    [Fact]
    public void RepoCorpusManifestCanBeValidatedByCli()
    {
        string repoRoot = FindRepositoryRoot();
        string manifestPath = Path.Combine(repoRoot, "tests", "fixtures", "corpus", "manifest.json");
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = OdfKitCli.Run(["validate-corpus", manifestPath, "--format", "json"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        using JsonDocument json = JsonDocument.Parse(output.ToString());
        JsonElement summary = json.RootElement.GetProperty("summary");
        JsonElement fixture = json.RootElement.GetProperty("fixtures")[0];
        int fixtureCount = summary.GetProperty("fixtureCount").GetInt32();
        Assert.True(fixtureCount >= 200, $"corpus fixtureCount 應 ≥ 200，實際為 {fixtureCount}。");
        Assert.Equal(fixtureCount, summary.GetProperty("passedCount").GetInt32());
        Assert.Equal(fixtureCount - 3, summary.GetProperty("validCount").GetInt32());
        Assert.Equal(3, summary.GetProperty("invalidCount").GetInt32());
        Assert.Equal("repo-generated-minimal-flat-text", fixture.GetProperty("id").GetString());
        Assert.True(fixture.GetProperty("kindMatches").GetBoolean());
        Assert.True(fixture.GetProperty("versionMatches").GetBoolean());
        Assert.Contains(
            json.RootElement.GetProperty("fixtures").EnumerateArray(),
            item => item.GetProperty("id").GetString() == "repo-generated-page-layout-interleave-flat-text" &&
                item.GetProperty("passed").GetBoolean());
        Assert.Contains(
            json.RootElement.GetProperty("fixtures").EnumerateArray(),
            item => item.GetProperty("id").GetString() == "repo-generated-page-layout-interleave-duplicate-flat-text" &&
                item.GetProperty("expected").GetString() == "invalid" &&
                item.GetProperty("passed").GetBoolean());
        Assert.Contains(
            json.RootElement.GetProperty("fixtures").EnumerateArray(),
            item => item.GetProperty("id").GetString() == "repo-generated-mathml-formula" &&
                item.GetProperty("passed").GetBoolean());
        Assert.Contains(
            json.RootElement.GetProperty("fixtures").EnumerateArray(),
            item => item.GetProperty("id").GetString() == "repo-generated-odf14-decorative-image" &&
                item.GetProperty("expected").GetString() == "valid" &&
                item.GetProperty("passed").GetBoolean());
        Assert.Contains(
            json.RootElement.GetProperty("fixtures").EnumerateArray(),
            item => item.GetProperty("id").GetString() == "repo-generated-odf14-table-in-shape" &&
                item.GetProperty("expected").GetString() == "valid" &&
                item.GetProperty("passed").GetBoolean());
        Assert.Contains(
            json.RootElement.GetProperty("fixtures").EnumerateArray(),
            item => item.GetProperty("id").GetString() == "repo-generated-odf14-zero-based-list" &&
                item.GetProperty("expected").GetString() == "valid" &&
                item.GetProperty("passed").GetBoolean());
        Assert.Contains(
            json.RootElement.GetProperty("fixtures").EnumerateArray(),
            item => item.GetProperty("id").GetString() == "repo-generated-odf14-invalid-decorative-bad-value" &&
                item.GetProperty("expected").GetString() == "invalid" &&
                item.GetProperty("passed").GetBoolean());
    }

    /// <summary>
    /// 驗證 <c>docs/odf-format-support.md</c> 主矩陣表中，每一列標示為 <c>complete</c> 的
    /// High-level API 欄位，其 Test evidence 欄位皆非空，避免文件狀態與測試證據脫節
    /// （Workstream A 文件契約檢查；僅檢查證據欄位存在，不驗證測試實際內容）。
    /// </summary>
    [Fact]
    public void FormatSupportMatrix_CompleteRowsDeclareNonEmptyTestEvidence()
    {
        string repoRoot = FindRepositoryRoot();
        string[] lines = File.ReadAllLines(Path.Combine(repoRoot, "docs", "odf-format-support.md"));

        var rows = new List<(string Extension, string HighLevelApi, string TestEvidence)>();
        bool inMatrixSection = false;
        foreach (string line in lines)
        {
            if (line.StartsWith("## 矩陣", StringComparison.Ordinal))
            {
                inMatrixSection = true;
                continue;
            }

            if (inMatrixSection && line.StartsWith("## ", StringComparison.Ordinal))
            {
                break;
            }

            if (!inMatrixSection || !line.TrimStart().StartsWith("| `.", StringComparison.Ordinal))
            {
                continue;
            }

            string[] cells = line.Split('|');
            // 欄位順序：(空白) Extension MIME Kind Detect Create Load Save Validate Round-trip HighLevelApi TestEvidence (空白)
            Assert.True(cells.Length >= 12, $"矩陣資料列欄位數不足，無法解析：{line}");
            rows.Add((cells[1].Trim(), cells[10].Trim(), cells[11].Trim()));
        }

        Assert.True(rows.Count >= 24, $"預期矩陣至少含 24 個 extension 資料列，實際解析到 {rows.Count} 列。");

        foreach ((string extension, string highLevelApi, string testEvidence) in rows)
        {
            if (string.Equals(highLevelApi, "complete", StringComparison.Ordinal))
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(testEvidence),
                    $"{extension} 的 High-level API 標示為 complete，但 Test evidence 欄位為空。");
            }
        }
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
}

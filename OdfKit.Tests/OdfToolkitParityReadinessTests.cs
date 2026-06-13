using System;
using System.IO;
using System.Text.Json;
using OdfKit.Cli;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 ODF Toolkit 對標文件與 corpus manifest 已宣告必要契約。
/// </summary>
public class OdfToolkitParityReadinessTests
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
        Assert.Contains("repo-generated-minimal-flat-text", manifest, StringComparison.Ordinal);
        Assert.Contains("generated-format-minimal", manifest, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證 corpus CI 腳本與 GitHub Actions 入口存在。
    /// </summary>
    [Fact]
    public void CorpusCiEntryPointsExist()
    {
        string repoRoot = FindRepositoryRoot();
        string scriptPath = Path.Combine(repoRoot, "eng", "Test-OdfCorpus.ps1");
        string workflowPath = Path.Combine(repoRoot, ".github", "workflows", "odf-corpus.yml");
        string script = File.ReadAllText(scriptPath);
        string workflow = File.ReadAllText(workflowPath);

        Assert.Contains("validate-corpus", script, StringComparison.Ordinal);
        Assert.Contains("ODFKIT_PARITY_CORPUS_ROOT", script, StringComparison.Ordinal);
        Assert.Contains("ODFKIT_ODFVALIDATOR_JAR", script, StringComparison.Ordinal);
        Assert.Contains("Test-OdfCorpus.ps1", workflow, StringComparison.Ordinal);
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
        Assert.Equal(8, summary.GetProperty("fixtureCount").GetInt32());
        Assert.Equal(8, summary.GetProperty("passedCount").GetInt32());
        Assert.Equal("repo-generated-minimal-flat-text", fixture.GetProperty("id").GetString());
        Assert.True(fixture.GetProperty("kindMatches").GetBoolean());
        Assert.True(fixture.GetProperty("versionMatches").GetBoolean());
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

using System.IO;
using System.Text.Json;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 ODF 1.4 規格覆蓋契約、API reference 與 typed DOM audit 入口。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfCoverageContractTests
{
    /// <summary>
    /// 驗證 ODF 1.4 覆蓋契約明確區分規格覆蓋與高階 API 深度。
    /// </summary>
    [Fact]
    public void Odf14CoverageContract_DeclaresLayeredCoverageModel()
    {
        string repoRoot = FindRepoRoot();
        string document = File.ReadAllText(Path.Combine(repoRoot, "docs", "odf14-coverage-contract.md"));

        Assert.Contains("schema coverage", document);
        Assert.Contains("package lifecycle", document);
        Assert.Contains("high-level facade", document);
        Assert.Contains("interop behavior", document);
        Assert.Contains("typed-dom-coverage", document);
        Assert.Contains("main", document);
        Assert.DoesNotContain("短期 gate", document);
    }

    /// <summary>
    /// 驗證 API reference 已提供四個高使用率工作流的永久文件。
    /// </summary>
    [Fact]
    public void ApiReference_DocumentsHighValueWorkflows()
    {
        string repoRoot = FindRepoRoot();
        string referenceRoot = Path.Combine(repoRoot, "docs", "reference");
        string index = File.ReadAllText(Path.Combine(referenceRoot, "index.md"));
        string spreadsheet = File.ReadAllText(Path.Combine(referenceRoot, "spreadsheet-data.md"));
        string charts = File.ReadAllText(Path.Combine(referenceRoot, "charts.md"));
        string templates = File.ReadAllText(Path.Combine(referenceRoot, "templates.md"));
        string interop = File.ReadAllText(Path.Combine(referenceRoot, "interop.md"));

        Assert.Contains("Spreadsheet data", index);
        Assert.Contains("Chart", index);
        Assert.Contains("Template", index);
        Assert.Contains("Interop", index);
        Assert.Contains("ValidateObjectBinding", spreadsheet);
        Assert.Contains("UpsertObjects", spreadsheet);
        Assert.Contains("InsertChartFromRange", charts);
        Assert.Contains("GetEmbeddedChartDocument", charts);
        Assert.Contains("TemplateBinder.Bind", templates);
        Assert.Contains("OdfTemplateBindReport", templates);
        Assert.Contains("OdfPracticalCompatibilityValidator", interop);
        Assert.Contains("LibreOffice", interop);
    }

    /// <summary>
    /// 驗證 ODF 1.4 coverage status 文件連回 audit 與 typed DOM 追蹤入口。
    /// </summary>
    [Fact]
    public void Odf14CoverageStatus_DocumentsAuditEntrypoints()
    {
        string repoRoot = FindRepoRoot();
        string document = File.ReadAllText(Path.Combine(repoRoot, "docs", "odf14-coverage-status.md"));

        Assert.Contains("OdfTypedDomCoverage.Build()", document);
        Assert.Contains("typed-dom-coverage", document);
        Assert.Contains("High-level facade", document);
        Assert.Contains("odf14-coverage-contract.md", document);
    }

    /// <summary>
    /// 驗證 v0.0.1 完滿條件綁定 main 持續證據，而非發行里程碑。
    /// </summary>
    [Fact]
    public void ContinuousV001Contract_DoesNotDependOnReleaseMilestone()
    {
        string repoRoot = FindRepoRoot();
        string quality = File.ReadAllText(Path.Combine(repoRoot, "docs", "product-quality-gates.md"));
        string version = File.ReadAllText(Path.Combine(repoRoot, "docs", "version-delivery.md"));
        string changelog = File.ReadAllText(Path.Combine(repoRoot, "CHANGELOG.md"));

        Assert.Contains("每次 `main` 變更", quality);
        Assert.Contains("持續維護", version);
        Assert.Contains("持續完滿", changelog);
        Assert.DoesNotContain("**C. 發版前**", quality);
        Assert.False(File.Exists(Path.Combine(repoRoot, "docs", "odf14-coverage-roadmap.md")));
        Assert.False(File.Exists(Path.Combine(repoRoot, "docs", "api-reference-roadmap.md")));
    }

    /// <summary>
    /// Verifies that the external RELAX NG workflow uses immutable, hash-verified tool caches and blocking parity gates.
    /// 驗證外部 RELAX NG 工作流程使用不可變且經雜湊驗證的工具快取與阻擋式對標閘門。
    /// </summary>
    [Fact]
    public void ExternalValidatorCi_UsesImmutableVerifiedCache()
    {
        string repoRoot = FindRepoRoot();
        string manifestPath = Path.Combine(repoRoot, "eng", "external-tools.json");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement validator = manifest.RootElement.GetProperty("odfValidator");
        JsonElement jing = manifest.RootElement.GetProperty("jing");
        string sha256 = validator.GetProperty("sha256").GetString()!;
        string jingSha256 = jing.GetProperty("sha256").GetString()!;
        string workflow = File.ReadAllText(
            Path.Combine(repoRoot, ".github", "workflows", "odf-external-baseline.yml"));
        string installer = File.ReadAllText(Path.Combine(repoRoot, "eng", "Install-OdfValidator.ps1"));
        string jingInstaller = File.ReadAllText(Path.Combine(repoRoot, "eng", "Install-Jing.ps1"));
        string jingBaseline = File.ReadAllText(Path.Combine(repoRoot, "eng", "Test-OdfRelaxNgBaseline.ps1"));
        string corpusScript = File.ReadAllText(Path.Combine(repoRoot, "eng", "Test-OdfCorpus.ps1"));

        Assert.Equal("0.13.0", validator.GetProperty("version").GetString());
        Assert.Equal(64, sha256.Length);
        Assert.Equal("20241231", jing.GetProperty("version").GetString());
        Assert.Equal(64, jingSha256.Length);
        Assert.Contains("steps.validator.outputs.source", workflow);
        Assert.Contains("steps.validator.outputs.cache_revision", workflow);
        Assert.Contains("steps.validator.outputs.version", workflow);
        Assert.Contains("steps.validator.outputs.sha256", workflow);
        Assert.Contains("steps.jing.outputs.source", workflow);
        Assert.Contains("steps.jing.outputs.cache_revision", workflow);
        Assert.Contains("steps.jing.outputs.version", workflow);
        Assert.Contains("steps.jing.outputs.sha256", workflow);
        Assert.DoesNotContain("restore-keys", workflow);
        Assert.Contains("Get-FileHash", installer);
        Assert.Contains("Get-FileHash", jingInstaller);
        Assert.Contains("binFiles", jingInstaller);
        Assert.Contains("System.IO.Compression.ZipFile", jingBaseline);
        Assert.Contains("ArgumentList.Add(\"-i\")", jingBaseline);
        Assert.Contains("FormulaTemplate\", \"FlatFormula", jingBaseline);
        Assert.Contains("OpenDocument-schema-v1.0-os.rng", jingBaseline);
        Assert.Contains("OpenDocument-schema-v1.1.rng", jingBaseline);
        Assert.Contains("OpenDocument-v1.4-schema.rng", jingBaseline);
        Assert.Contains("InternalBaselineVersions", corpusScript);
        Assert.Contains("InternalBaselineExcludedKinds", corpusScript);
        Assert.Contains("InternalBaselinePackageOnly", corpusScript);
        Assert.Contains("-InternalBaselineVersions '1.0', '1.1', '1.2', '1.3', '1.4'", workflow);
        Assert.Contains("-InternalBaselineExcludedKinds 'Database', 'Formula', 'FormulaTemplate'", workflow);
        Assert.Contains("Test-OdfRelaxNgBaseline.ps1", workflow);
        Assert.Contains("OdfCorpusGenerator", workflow);
        Assert.Contains("ValidateWithOdfValidator_RealJar_DetectsValidAndInvalidDocuments", workflow);
        Assert.DoesNotContain("continue-on-error", workflow);
        Assert.Contains("-SkipBuild", workflow);
        Assert.Contains("-SkipInternalValidation", workflow);
    }

    /// <summary>
    /// 驗證 cookbook 保留高使用率工作流的 API 名稱，避免文件漂移。
    /// </summary>
    [Fact]
    public void Cookbook_ReferencesCurrentHighValueWorkflowApis()
    {
        string repoRoot = FindRepoRoot();
        string document = File.ReadAllText(Path.Combine(repoRoot, "docs", "cookbook.md"));

        Assert.Contains("ValidateObjectBinding", document);
        Assert.Contains("UpsertObjects", document);
        Assert.Contains("InsertChartFromRange", document);
        Assert.Contains("GetEmbeddedChartDocument", document);
        Assert.Contains("TemplateBinder.Bind", document);
    }

    /// <summary>
    /// 驗證 typed DOM coverage audit 可穩定輸出 schema 與 wrapper 摘要。
    /// </summary>
    [Fact]
    public void TypedDomCoverageAudit_ReportsSchemaAndWrapperCounts()
    {
        OdfTypedDomCoverageReport report = OdfTypedDomCoverage.Build();

        Assert.Equal("1.4", report.SchemaVersion);
        Assert.True(report.SchemaElementCount > 100);
        Assert.True(report.SchemaAttributeCount > 100);
        Assert.True(report.TypedElementCount > 0);
        Assert.NotEmpty(report.Elements);
        Assert.NotEmpty(report.AttributeDatatypeCoverage);
    }

    /// <summary>
    /// 驗證 ODF 1.4 schema provider 與 typed DOM audit 維持可比較的最低覆蓋摘要。
    /// </summary>
    [Fact]
    public void TypedDomCoverageAudit_HasStableOdf14ElementAndAttributeCounts()
    {
        OdfTypedDomCoverageReport report = OdfTypedDomCoverage.Build();

        Assert.True(report.SchemaElementCount >= 599);
        Assert.True(report.SchemaAttributeCount >= 1299);
        Assert.True(report.TypedElementCount <= report.SchemaElementCount);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                File.Exists(Path.Combine(directory.FullName, "OdfKit", "OdfKit.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Cannot locate repository root.");
    }
}

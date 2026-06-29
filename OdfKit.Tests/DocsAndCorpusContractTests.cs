using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    /// 驗證 ODFDOM 官方 sample parity 外部 corpus 範本具備來源、授權、雜湊欄位並可通過 metadata gate。
    /// </summary>
    [Fact]
    public void ExternalOdfDomSampleCorpusTemplateCanBeMetadataValidatedByCli()
    {
        string repoRoot = FindRepositoryRoot();
        string manifestPath = Path.Combine(repoRoot, "docs", "examples", "odfdom-sample-corpus", "manifest.json");
        string exceptionsPath = Path.Combine(repoRoot, "docs", "examples", "odfdom-sample-corpus", "baseline-exceptions.json");
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
        Assert.Equal(0, summary.GetProperty("baselineExceptionCount").GetInt32());

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        foreach (JsonElement fixture in manifest.RootElement.GetProperty("fixtures").EnumerateArray())
        {
            Assert.Equal("ODF Toolkit ODFDOM sample", fixture.GetProperty("source").GetString());
            Assert.Equal("external-review-required", fixture.GetProperty("license").GetString());
            Assert.Contains("github.com/tdf/odftoolkit", fixture.GetProperty("sourceUri").GetString(), StringComparison.Ordinal);
            Assert.Matches("^[0-9a-f]{64}$", fixture.GetProperty("sha256").GetString());
            Assert.Contains("Replace", fixture.GetProperty("notes").GetString(), StringComparison.Ordinal);
        }
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
        Assert.Contains("odfdom-sample-corpus", initializeScript, StringComparison.Ordinal);
        Assert.Contains("ValidateSet", initializeScript, StringComparison.Ordinal);
        Assert.Contains("manifest.json", initializeScript, StringComparison.Ordinal);
        Assert.Contains("baseline-exceptions.json", initializeScript, StringComparison.Ordinal);
        Assert.Contains("Test-OdfCorpus.ps1", workflow, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證快速 CI 使用 trait 分類並保留 Smoke shard，避免單一測試主機承載整批測試。
    /// </summary>
    [Fact]
    public void CiSmokeTestsUseTraitCategoryFilter()
    {
        string repoRoot = FindRepositoryRoot();
        string workflow = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "ci.yml"));
        string categories = File.ReadAllText(Path.Combine(repoRoot, "OdfKit.Tests", "TestCategories.cs"));
        string roundTripMatrix = File.ReadAllText(Path.Combine(repoRoot, "OdfKit.Tests", "PackageRoundTripMatrixTests.cs"));

        Assert.Contains("Category=Smoke", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("FullyQualifiedName~OdfKit.Tests.", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("- name: Run smoke tests\r\n", workflow, StringComparison.Ordinal);
        Assert.Contains("Run smoke tests (docs)", workflow, StringComparison.Ordinal);
        Assert.Contains("Run smoke tests (api)", workflow, StringComparison.Ordinal);
        Assert.Contains("Run smoke tests (package-entries)", workflow, StringComparison.Ordinal);
        Assert.Contains("Run smoke tests (package-roundtrip-core)", workflow, StringComparison.Ordinal);
        Assert.Contains("Run smoke tests (package-roundtrip-embedded)", workflow, StringComparison.Ordinal);
        Assert.Contains("Run smoke tests (package-roundtrip-preservation)", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("$testFilter = 'Category=Smoke&FullyQualifiedName~PackageRoundTripTests'", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("Run smoke tests (package-roundtrip-minimal)", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("MinimalSupportedFormatRoundTrips", workflow, StringComparison.Ordinal);
        Assert.Contains("[Fact(Explicit = true)]", roundTripMatrix, StringComparison.Ordinal);
        Assert.Contains("Run smoke tests (vertical-slice)", workflow, StringComparison.Ordinal);
        Assert.Contains("Run smoke tests (core-security)", workflow, StringComparison.Ordinal);
        Assert.Contains("public const string Smoke = \"Smoke\";", categories, StringComparison.Ordinal);
        Assert.Contains("public const string Interop = \"Interop\";", categories, StringComparison.Ordinal);
        Assert.Contains("public const string Corpus = \"Corpus\";", categories, StringComparison.Ordinal);
        Assert.Contains("public const string Compliance = \"Compliance\";", categories, StringComparison.Ordinal);
        Assert.Contains("public const string Scenario = \"Scenario\";", categories, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證高負載壓力測試保留在 Stress 分層，不會被快速 Smoke CI 撈入。
    /// </summary>
    [Fact]
    public void StressTestsRemainOutsideSmokeTier()
    {
        string repoRoot = FindRepositoryRoot();
        string empiricalStress = File.ReadAllText(Path.Combine(repoRoot, "OdfKit.Tests", "EmpiricalStressTests.cs"));
        string coreStress = File.ReadAllText(Path.Combine(repoRoot, "OdfKit.Tests", "OdfCoreStressTests.cs"));

        Assert.Contains("TestCategories.Stress", empiricalStress, StringComparison.Ordinal);
        Assert.Contains("TestCategories.Performance", empiricalStress, StringComparison.Ordinal);
        Assert.Contains("TestCategories.Stress", coreStress, StringComparison.Ordinal);
        Assert.DoesNotContain("TestCategories.Smoke", empiricalStress, StringComparison.Ordinal);
        Assert.DoesNotContain("TestCategories.Smoke", coreStress, StringComparison.Ordinal);
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
    /// 驗證 trimming 煙霧測試入口已文件化，且 CI 入口會監看工具本身與核心程式碼。
    /// </summary>
    [Fact]
    public void TrimSmokeEntryPointsAreDocumentedAndPathScoped()
    {
        string repoRoot = FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(repoRoot, "eng", "Test-TrimSmoke.ps1"));
        string workflow = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "trim-smoke.yml"));
        string toolsReadme = File.ReadAllText(Path.Combine(repoRoot, "tools", "README.md"));
        string trimProject = File.ReadAllText(Path.Combine(repoRoot, "tools", "OdfKit.TrimSmoke", "OdfKit.TrimSmoke.csproj"));
        string openPgpProvider = File.ReadAllText(Path.Combine(repoRoot, "OdfKit", "Core", "OdfBouncyCastleOpenPgpProvider.cs"));

        Assert.Contains("PublishTrimmed", script, StringComparison.Ordinal);
        Assert.Contains("OdfKit.TrimSmoke.exe", script, StringComparison.Ordinal);
        Assert.Contains("tools/OdfKit.TrimSmoke/**", workflow, StringComparison.Ordinal);
        Assert.Contains("eng/Test-TrimSmoke.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("workflow_dispatch", workflow, StringComparison.Ordinal);
        Assert.Contains("Test-TrimSmoke.ps1 -Configuration Release", toolsReadme, StringComparison.Ordinal);
        Assert.Contains("<TrimmerRootAssembly Include=\"BouncyCastle.Cryptography\" />", trimProject, StringComparison.Ordinal);
        Assert.Contains("<NuGetAudit>false</NuGetAudit>", trimProject, StringComparison.Ordinal);
        Assert.Contains("<NoWarn>$(NoWarn);IL2104</NoWarn>", trimProject, StringComparison.Ordinal);
        Assert.Contains("RequiresUnreferencedCode", openPgpProvider, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證 ODF policy automation 入口已文件化，且 CI 入口以 Policy trait 分層。
    /// </summary>
    [Fact]
    public void PolicyAutomationEntryPointsAreDocumentedAndPathScoped()
    {
        string repoRoot = FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(repoRoot, "eng", "Test-OdfPolicy.ps1"));
        string workflow = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "odf-policy.yml"));
        string engReadme = File.ReadAllText(Path.Combine(repoRoot, "eng", "README.md"));
        string interopCorpus = File.ReadAllText(Path.Combine(repoRoot, "docs", "interop-corpus.md"));
        string categories = File.ReadAllText(Path.Combine(repoRoot, "OdfKit.Tests", "TestCategories.cs"));
        string securityBoundaryTests = File.ReadAllText(Path.Combine(repoRoot, "OdfKit.Tests", "OdfSecurityBoundaryTests.cs"));
        string securityComplianceTests = File.ReadAllText(Path.Combine(repoRoot, "OdfKit.Tests", "SecurityComplianceTests.cs"));

        Assert.Contains("Category=Policy", script, StringComparison.Ordinal);
        Assert.Contains("Test-OdfPolicy.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("OdfKit.Tests/**", workflow, StringComparison.Ordinal);
        Assert.Contains("workflow_dispatch", workflow, StringComparison.Ordinal);
        Assert.Contains("Test-OdfPolicy.ps1", engReadme, StringComparison.Ordinal);
        Assert.Contains("Category=Policy", engReadme, StringComparison.Ordinal);
        Assert.Contains("ODF policy", interopCorpus, StringComparison.Ordinal);
        Assert.Contains("public const string Policy = \"Policy\";", categories, StringComparison.Ordinal);
        Assert.Contains("TestCategories.Policy", securityBoundaryTests, StringComparison.Ordinal);
        Assert.Contains("TestCategories.Policy", securityComplianceTests, StringComparison.Ordinal);
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
    /// 驗證 validate-corpus CLI 訊息已覆蓋全部支援語言，且非英文文化不是英文佔位。
    /// </summary>
    [Fact]
    public void CliCorpusDiagnosticsAreLocalizedForAllSupportedCultures()
    {
        string repoRoot = FindRepositoryRoot();
        string localizer = File.ReadAllText(Path.Combine(repoRoot, "OdfKit", "Compliance", "OdfLocalizer.Exceptions.cs"));
        string[] keys =
        [
            "Cli_InvalidJsonFile",
            "Cli_UnhandledError",
            "Cli_BaselineExceptionBaselineInvalid",
            "Cli_BaselineExceptionDoesNotMatchFixture",
            "Cli_BaselineExceptionDuplicate",
            "Cli_BaselineExceptionEntriesMustBeObjects",
            "Cli_BaselineExceptionRequiresBooleanProperty",
            "Cli_BaselineExceptionRequiresProperty",
            "Cli_BaselineExceptionsRequiresExceptionsArray",
            "Cli_CorpusFixtureDuplicateId",
            "Cli_CorpusFixtureDuplicatePath",
            "Cli_CorpusFixtureEntriesMustBeObjects",
            "Cli_CorpusFixtureExpectedInvalid",
            "Cli_CorpusFixtureNotFound",
            "Cli_CorpusFixturePathEscapesRoot",
            "Cli_CorpusFixturePathMustBeRelative",
            "Cli_CorpusFixtureRequiresProperty",
            "Cli_CorpusFixtureRoundTripInvalid",
            "Cli_CorpusFixtureSha256Invalid",
            "Cli_CorpusFixtureUnknownProfile",
            "Cli_CorpusManifestRequiresAtLeastOneFixture",
            "Cli_CorpusManifestRequiresFixturesArray",
            "Cli_ExternalCorpusFixtureRequiresAbsoluteSourceUri",
        ];

        foreach (string key in keys)
        {
            Assert.Equal(12, Regex.Matches(localizer, "\\[\"" + Regex.Escape(key) + "\"\\]").Count);
        }

        Assert.Contains("Die baseline exception baseline", localizer, StringComparison.Ordinal);
        Assert.Contains("La baseline d'une exception", localizer, StringComparison.Ordinal);
        Assert.Contains("baseline exception의 baseline", localizer, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證新增的公式與 OpenPGP 診斷訊息可由全部支援語言查出，而不是回退為 key 名稱。
    /// </summary>
    [Fact]
    public void FormulaAndOpenPgpDiagnosticsAreLocalizedForAllSupportedCultures()
    {
        string[] cultures = ["en", "zh-TW", "de", "fr", "nl", "nb", "pt", "it", "sk", "da", "ms", "ko"];
        string[] keys =
        [
            "Diag_OdfFormulaSupport_ParseFailed",
            "Diag_OdfFormulaSupport_UnsupportedFunction",
            "Err_OdfBouncyCastleOpenPgpProvider_UnsupportedPublicKeyAlgorithm",
        ];

        foreach (string cultureName in cultures)
        {
            var culture = new System.Globalization.CultureInfo(cultureName);
            foreach (string key in keys)
            {
                string message = OdfLocalizer.GetMessage(key, culture, "XLOOKUP");

                Assert.NotEqual(key, message);
                Assert.Contains("XLOOKUP", message, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// 驗證公式評估、schema pattern validator 與 JSON Collaboration 的 clean-room 來源索引已宣告證據與不可複製來源。
    /// </summary>
    [Fact]
    public void CleanRoomSourceIndexDeclaresFormulaAndSchemaPatternEvidence()
    {
        string repoRoot = FindRepositoryRoot();
        string sourceIndex = File.ReadAllText(Path.Combine(repoRoot, "docs", "provenance", "clean-room-source-index.md"));
        string provenance = File.ReadAllText(Path.Combine(repoRoot, "docs", "provenance", "README.md"));
        string docsIndex = File.ReadAllText(Path.Combine(repoRoot, "docs", "index.md"));
        string formulaTests = File.ReadAllText(Path.Combine(repoRoot, "OdfKit.Tests", "OpenFormulaSupportTests.cs"));
        string complianceTests = File.ReadAllText(Path.Combine(repoRoot, "OdfKit.Tests", "ComplianceTests.cs"));

        Assert.Contains("DefaultFormulaEvaluator.*", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("OdfSchemaPatternValidator.*", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("OdfKit.Extensions.Collaboration", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("OASIS OpenDocument v1.4 Part 4 OpenFormula", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("RELAX NG Specification", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("W3C XML Schema Part 2 Datatypes", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("TDF ODF Toolkit 公開文件", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("reference JSON", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("Managed conversion fidelity 來源", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("OdfSvgExporter.cs", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("OdfRtfImporter.cs", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("OdpToPptxConverter.cs", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("PptxToOdpConverter.cs", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("OpenFormulaSupportTests", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("FormulaEvaluatorStressTests", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("FormulaTranslationStressTests", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("ComplianceTests.SchemaPatternValidator*", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("CollaborationOperationsTests", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("tests/fixtures/collaboration/manifest.json", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("ManagedSvgExportTests.SvgExporterSplitsFullEnhancedPathEllipseArcs", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("ManagedTextExportTests.RtfImporterConvertsSectionAndColumnBreaksToSoftPageBreaks", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("ManagedPptxConversionTests.PptxConvertersPreserveBasicObjectAnimations", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("CorpusComplianceTests", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("eng/Test-OdfCorpus.ps1", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("不複製 LibreOffice", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("Java ODF Toolkit", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("OT", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("CRDT", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("golden / regression", sourceIndex, StringComparison.Ordinal);
        Assert.Contains("clean-room-source-index.md", provenance, StringComparison.Ordinal);
        Assert.Contains("clean-room-source-index.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("managed conversion fidelity", docsIndex, StringComparison.Ordinal);
        Assert.Contains("SpreadsheetFormulaRoundTripPreservesUnsupportedFormula", formulaTests, StringComparison.Ordinal);
        Assert.Contains("LibreOfficeEasterSundayEvaluatesToDateSerial", formulaTests, StringComparison.Ordinal);
        Assert.Contains("LibreOfficeIsOmittedEvaluatesByArgumentCount", formulaTests, StringComparison.Ordinal);
        Assert.Contains("SchemaPatternValidatorHandlesInterleaveChildrenOutOfOrder", complianceTests, StringComparison.Ordinal);
        Assert.Contains("SchemaPatternValidatorHandlesTextDataAndValueNodes", complianceTests, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證 ODS 串流文件明確區分嚴格順序模式與交錯緩衝便利路徑。
    /// </summary>
    [Fact]
    public void OdsStreamWriterCookbookDeclaresBufferedSwitchToSheetBoundary()
    {
        string repoRoot = FindRepositoryRoot();
        string cookbook = File.ReadAllText(Path.Combine(repoRoot, "docs", "cookbook.md"));

        Assert.Contains("嚴格順序寫入模式", cookbook, StringComparison.Ordinal);
        Assert.Contains("WriteStartSheet", cookbook, StringComparison.Ordinal);
        Assert.Contains("WriteEndSheet", cookbook, StringComparison.Ordinal);
        Assert.Contains("低記憶體輸出", cookbook, StringComparison.Ordinal);
        Assert.Contains("SwitchToSheet", cookbook, StringComparison.Ordinal);
        Assert.Contains("暫存緩衝", cookbook, StringComparison.Ordinal);
        Assert.Contains("不屬於純串流模式", cookbook, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證 cookbook 宣告四個高階複雜文件場景與 JSON Collaboration 相容入口。
    /// </summary>
    [Fact]
    public void CookbookDeclaresComplexDocumentAndCollaborationScenarios()
    {
        string repoRoot = FindRepositoryRoot();
        string cookbook = File.ReadAllText(Path.Combine(repoRoot, "docs", "cookbook.md"));

        Assert.Contains("年度報告（ODT）", cookbook, StringComparison.Ordinal);
        Assert.Contains("財務模型（ODS）", cookbook, StringComparison.Ordinal);
        Assert.Contains("商業簡報（ODP）", cookbook, StringComparison.Ordinal);
        Assert.Contains("流程圖／架構圖（ODG）", cookbook, StringComparison.Ordinal);
        Assert.Contains("SpreadsheetDocument.Builder()", cookbook, StringComparison.Ordinal);
        Assert.Contains(".WithMetadata(metadata => metadata.Title(\"財務模型\").Author(\"OdfKit\"))", cookbook, StringComparison.Ordinal);
        Assert.Contains("OdtOperationCompatibilityOptions", cookbook, StringComparison.Ordinal);
        Assert.Contains("CreateTdfCompatibility", cookbook, StringComparison.Ordinal);
        Assert.Contains("OdtOperationImportReport", cookbook, StringComparison.Ordinal);
        Assert.Contains("TextDocument.Builder()", cookbook, StringComparison.Ordinal);
        Assert.Contains("WithStyles", cookbook, StringComparison.Ordinal);
        Assert.Contains("OdfStyleSet.BusinessReport", cookbook, StringComparison.Ordinal);
        Assert.Contains("public static OdfStyleSet FromTheme", File.ReadAllText(Path.Combine(repoRoot, "OdfKit", "Styles", "OdfStyleSet.cs")), StringComparison.Ordinal);
        Assert.Contains("OdfLayoutPreset", cookbook, StringComparison.Ordinal);
        Assert.Contains("WithLayoutPreset", cookbook, StringComparison.Ordinal);
        Assert.Contains("AddCoverPage", cookbook, StringComparison.Ordinal);
        Assert.Contains("AddTableOfContents", cookbook, StringComparison.Ordinal);
        Assert.Contains("WithPageSetup", cookbook, StringComparison.Ordinal);
        Assert.Contains("BackgroundColor", cookbook, StringComparison.Ordinal);
        Assert.Contains("AddSection", cookbook, StringComparison.Ordinal);
        Assert.Contains("AddImage", cookbook, StringComparison.Ordinal);
        Assert.Contains("AddNamedRange", cookbook, StringComparison.Ordinal);
        Assert.Contains("AddFormulaColumn", cookbook, StringComparison.Ordinal);
        Assert.Contains("SetFormulaRange", cookbook, StringComparison.Ordinal);
        Assert.Contains("AddDecimalValidation", cookbook, StringComparison.Ordinal);
        Assert.Contains("AddDataBarFormat", cookbook, StringComparison.Ordinal);
        Assert.Contains("AddPivotTable", cookbook, StringComparison.Ordinal);
        Assert.Contains("WithMasterPage", cookbook, StringComparison.Ordinal);
        Assert.Contains("OdfDesignTheme.Flowchart", cookbook, StringComparison.Ordinal);
        Assert.Contains("WithTheme", cookbook, StringComparison.Ordinal);
        Assert.Contains("AddTitleSlide", cookbook, StringComparison.Ordinal);
        Assert.Contains("AddTwoColumnSlide", cookbook, StringComparison.Ordinal);
        Assert.Contains("AddChartSlide", cookbook, StringComparison.Ordinal);
        Assert.Contains("AddLayer", cookbook, StringComparison.Ordinal);
        Assert.Contains("AddConnector", cookbook, StringComparison.Ordinal);
        Assert.Contains("AddGroup", cookbook, StringComparison.Ordinal);
        Assert.Contains("SaveAsSvg", cookbook, StringComparison.Ordinal);
        Assert.Contains("OdfChartDataLabelPreset", cookbook, StringComparison.Ordinal);
        Assert.Contains("WithDataLabels", cookbook, StringComparison.Ordinal);
        Assert.Contains("WithChartPaletteColors", File.ReadAllText(Path.Combine(repoRoot, "OdfKit", "Styles", "OdfStyleSet.cs")), StringComparison.Ordinal);
        Assert.Contains("OdfLayoutPreset", File.ReadAllText(Path.Combine(repoRoot, "OdfKit", "Styles", "OdfLayoutPreset.cs")), StringComparison.Ordinal);
        Assert.Contains("FindFirst", cookbook, StringComparison.Ordinal);
        Assert.Contains("FindAll", cookbook, StringComparison.Ordinal);
        Assert.Contains("WithChild", cookbook, StringComparison.Ordinal);
        Assert.Contains("ReplaceFirst", cookbook, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證 GitHub Release nupkg 文件提供可重現 restore 的 local feed 範本。
    /// </summary>
    [Fact]
    public void GitHubReleasePublishingDocumentDeclaresLocalFeedRestoreContract()
    {
        string repoRoot = FindRepositoryRoot();
        string release = File.ReadAllText(Path.Combine(repoRoot, "docs", "github-release-publishing.md"));

        Assert.Contains("nuget.config", release, StringComparison.Ordinal);
        Assert.Contains("odfkit-github-release", release, StringComparison.Ordinal);
        Assert.Contains("dotnet restore --configfile nuget.config", release, StringComparison.Ordinal);
        Assert.Contains("RestoreAdditionalProjectSources", release, StringComparison.Ordinal);
        Assert.Contains("nuget.org", release, StringComparison.Ordinal);
        Assert.Contains("非目前目標", release, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證 JSON Collaboration 文件邊界與 clean-room 策略沒有再被列為純 non-goal。
    /// </summary>
    [Fact]
    public void JsonCollaborationScopeIsExtensionScopedCompatibilitySubset()
    {
        string repoRoot = FindRepositoryRoot();
        string nonGoals = File.ReadAllText(Path.Combine(repoRoot, "docs", "udx-non-goals.md"));

        Assert.Contains("extension-scoped compatibility subset", nonGoals, StringComparison.Ordinal);
        Assert.Contains("OdfKit.Extensions.Collaboration", nonGoals, StringComparison.Ordinal);
        Assert.Contains("{ \"changes\": [...] }", nonGoals, StringComparison.Ordinal);
        Assert.Contains("addLineBreak", nonGoals, StringComparison.Ordinal);
        Assert.Contains("format", nonGoals, StringComparison.Ordinal);
        Assert.Contains("format` range", nonGoals, StringComparison.Ordinal);
        Assert.Contains("前景色、背景色", nonGoals, StringComparison.Ordinal);
        Assert.Contains("small-caps", nonGoals, StringComparison.Ordinal);
        Assert.Contains("上標／下標", nonGoals, StringComparison.Ordinal);
        Assert.Contains("addStyle", nonGoals, StringComparison.Ordinal);
        Assert.Contains("delete", nonGoals, StringComparison.Ordinal);
        Assert.Contains("splitParagraph", nonGoals, StringComparison.Ordinal);
        Assert.Contains("mergeParagraph", nonGoals, StringComparison.Ordinal);
        Assert.Contains("addListStyle", nonGoals, StringComparison.Ordinal);
        Assert.Contains("addTable", nonGoals, StringComparison.Ordinal);
        Assert.Contains("addRows", nonGoals, StringComparison.Ordinal);
        Assert.Contains("addCells", nonGoals, StringComparison.Ordinal);
        Assert.Contains("OT", nonGoals, StringComparison.Ordinal);
        Assert.Contains("CRDT", nonGoals, StringComparison.Ordinal);
        Assert.Contains("clean-room", nonGoals, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證真實 LibreOffice 互通測試只由專用入口明確啟用。
    /// </summary>
    [Fact]
    public void LibreOfficeInteropTestsRequireDedicatedRunFlag()
    {
        string repoRoot = FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(repoRoot, "eng", "Test-LibreOfficeInterop.ps1"));
        string matrix = File.ReadAllText(Path.Combine(repoRoot, "docs", "libreoffice-interop-matrix.md"));

        Assert.Contains("ODFKIT_RUN_LIBREOFFICE_INTEROP", script, StringComparison.Ordinal);
        Assert.Contains("ODFKIT_RUN_LIBREOFFICE_INTEROP=1", matrix, StringComparison.Ordinal);
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
        Assert.Equal(fixtureCount - 7, summary.GetProperty("validCount").GetInt32());
        Assert.Equal(7, summary.GetProperty("invalidCount").GetInt32());
        Assert.True(summary.GetProperty("sha256CheckedCount").GetInt32() >= 4);
        Assert.Equal(0, summary.GetProperty("sha256MismatchCount").GetInt32());
        Assert.Equal("repo-generated-minimal-flat-text", fixture.GetProperty("id").GetString());
        Assert.True(fixture.GetProperty("kindMatches").GetBoolean());
        Assert.True(fixture.GetProperty("versionMatches").GetBoolean());
        Assert.Contains(
            json.RootElement.GetProperty("fixtures").EnumerateArray(),
            item => item.GetProperty("id").GetString() == "repo-generated-realistic-review-memo-flat-text" &&
                item.GetProperty("expected").GetString() == "valid" &&
                item.GetProperty("passed").GetBoolean());
        Assert.Contains(
            json.RootElement.GetProperty("fixtures").EnumerateArray(),
            item => item.GetProperty("id").GetString() == "repo-generated-invalid-table-inside-paragraph-flat-text" &&
                item.GetProperty("expected").GetString() == "invalid" &&
                item.GetProperty("passed").GetBoolean());
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
        Assert.Contains(
            json.RootElement.GetProperty("fixtures").EnumerateArray(),
            item => item.GetProperty("id").GetString() == "repo-generated-complex-annual-report" &&
                item.GetProperty("expected").GetString() == "invalid" &&
                item.GetProperty("sha256Matches").GetBoolean() &&
                item.GetProperty("passed").GetBoolean());
        Assert.Contains(
            json.RootElement.GetProperty("fixtures").EnumerateArray(),
            item => item.GetProperty("id").GetString() == "repo-generated-complex-financial-model" &&
                item.GetProperty("expected").GetString() == "invalid" &&
                item.GetProperty("sha256Matches").GetBoolean() &&
                item.GetProperty("passed").GetBoolean());
        Assert.Contains(
            json.RootElement.GetProperty("fixtures").EnumerateArray(),
            item => item.GetProperty("id").GetString() == "repo-generated-complex-business-deck" &&
                item.GetProperty("expected").GetString() == "valid" &&
                item.GetProperty("sha256Matches").GetBoolean() &&
                item.GetProperty("passed").GetBoolean());
        Assert.Contains(
            json.RootElement.GetProperty("fixtures").EnumerateArray(),
            item => item.GetProperty("id").GetString() == "repo-generated-complex-flow-diagram" &&
                item.GetProperty("expected").GetString() == "invalid" &&
                item.GetProperty("sha256Matches").GetBoolean() &&
                item.GetProperty("passed").GetBoolean());
    }

    /// <summary>
    /// 驗證 JSON Collaboration fixture manifest 具備 clean-room 來源、授權與 SHA-256。
    /// </summary>
    [Fact]
    public void CollaborationFixtureManifestDeclaresSourceLicenseAndHash()
    {
        string repoRoot = FindRepositoryRoot();
        string manifestPath = Path.Combine(repoRoot, "tests", "fixtures", "collaboration", "manifest.json");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement fixture = manifest.RootElement.GetProperty("fixtures")[0];
        string path = fixture.GetProperty("path").GetString() ?? string.Empty;
        string expectedHash = fixture.GetProperty("sha256").GetString() ?? string.Empty;
        string fullPath = Path.Combine(Path.GetDirectoryName(manifestPath)!, path);

        Assert.Equal("repo-generated-tdf-subset-envelope", fixture.GetProperty("id").GetString());
        Assert.Equal("generated-no-copyright", fixture.GetProperty("license").GetString());
        Assert.Equal("tdf-changes-envelope", fixture.GetProperty("wireShape").GetString());
        Assert.StartsWith("https://github.com/tdf/odftoolkit/", fixture.GetProperty("sourceUri").GetString(), StringComparison.Ordinal);
        Assert.Equal(expectedHash, ComputeSha256(fullPath));

        string json = File.ReadAllText(fullPath);
        Assert.Contains("\"changes\"", json, StringComparison.Ordinal);
        Assert.Contains("addParagraph", json, StringComparison.Ordinal);
        Assert.Contains("addText", json, StringComparison.Ordinal);
        Assert.Contains("delete", json, StringComparison.Ordinal);
        Assert.Contains("splitParagraph", json, StringComparison.Ordinal);
        Assert.Contains("mergeParagraph", json, StringComparison.Ordinal);
        Assert.Contains("addLineBreak", json, StringComparison.Ordinal);
        Assert.Contains("format", json, StringComparison.Ordinal);
        Assert.Contains("addListStyle", json, StringComparison.Ordinal);
        Assert.Contains("addTable", json, StringComparison.Ordinal);
        Assert.Contains("addRows", json, StringComparison.Ordinal);
        Assert.Contains("addCells", json, StringComparison.Ordinal);
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

    /// <summary>
    /// 驗證格式支援文件不再把 Formula 最小語意編輯 helper 誤列為後續追蹤項目。
    /// </summary>
    [Fact]
    public void FormatSupportDocs_FormulaSemanticEditingHelpersAreMarkedComplete()
    {
        string repoRoot = FindRepositoryRoot();
        string support = File.ReadAllText(Path.Combine(repoRoot, "docs", "odf-format-support.md"));
        string tests = File.ReadAllText(Path.Combine(repoRoot, "OdfKit.Tests", "FormulaHighLevelApiTests.cs"));

        Assert.Contains("Formula 已具備 `FindFirst`／`FindAll`／`WithChild`／`ReplaceFirst`", support, StringComparison.Ordinal);
        Assert.Contains("最小「尋找→取得→更新」語意編輯 helper", support, StringComparison.Ordinal);
        Assert.DoesNotContain("續列為獨立追蹤的後續深度工作", support, StringComparison.Ordinal);
        Assert.Contains("OdfMathToken_FindAndWithChild_WorksForNestedStructure", tests, StringComparison.Ordinal);
        Assert.Contains("FormulaTokenSemanticEditing_FindReplaceAndSetMathRow_Persists", tests, StringComparison.Ordinal);
        Assert.Contains("ReplaceFirst_ReplacesNestedFraction", tests, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證格式支援文件明確區分 CNS 11643 全字庫骨架支援與官方語意相容聲明。
    /// </summary>
    [Fact]
    public void FormatSupportDocs_Cns11643FontSupportBoundaryIsConservative()
    {
        string repoRoot = FindRepositoryRoot();
        string support = File.ReadAllText(Path.Combine(repoRoot, "docs", "odf-format-support.md"));

        Assert.Contains("Unicode 平面分段", support, StringComparison.Ordinal);
        Assert.Contains("`TextDocument.ApplyCjkFontFallback()`", support, StringComparison.Ordinal);
        Assert.Contains("`OdfParagraph.AddCns11643Text(...)`", support, StringComparison.Ordinal);
        Assert.Contains("`TW-Kai-Ext-B-98_1`", support, StringComparison.Ordinal);
        Assert.Contains("`TW-Song-Ext-B-98_1`", support, StringComparison.Ordinal);
        Assert.Contains("OdfKit 不內建政府字型", support, StringComparison.Ordinal);
        Assert.Contains("不應宣稱完整", support, StringComparison.Ordinal);
        Assert.Contains("CNS 11643 官方語意相容或認證", support, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證大型測試檔必須具備分類，避免測試套件膨脹後失去分層治理。
    /// </summary>
    [Fact]
    public void LargeTestFilesDeclareCategoryTrait()
    {
        const int largeTestFileLineThreshold = 700;
        string repoRoot = FindRepositoryRoot();
        string testRoot = Path.Combine(repoRoot, "OdfKit.Tests");
        string[] violations = Directory.EnumerateFiles(testRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !path.Contains($"{Path.DirectorySeparatorChar}MockSoffice{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(path => new
            {
                RelativePath = Path.GetRelativePath(repoRoot, path),
                Text = File.ReadAllText(path),
                LineCount = File.ReadLines(path).Count()
            })
            .Where(file => file.LineCount >= largeTestFileLineThreshold &&
                (file.Text.Contains("[Fact", StringComparison.Ordinal) ||
                    file.Text.Contains("[Theory", StringComparison.Ordinal)) &&
                !file.Text.Contains("[Trait(TestCategories.Kind", StringComparison.Ordinal))
            .Select(file => $"{file.RelativePath} ({file.LineCount} 行)")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    /// <summary>
    /// 驗證超大型測試檔至少已採用 partial 類別拆分，避免單一檔案持續吸收無關情境。
    /// </summary>
    [Fact]
    public void ExtraLargeTestFilesUsePartialClasses()
    {
        const int extraLargeTestFileLineThreshold = 3000;
        string repoRoot = FindRepositoryRoot();
        string testRoot = Path.Combine(repoRoot, "OdfKit.Tests");
        string[] violations = Directory.EnumerateFiles(testRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !path.Contains($"{Path.DirectorySeparatorChar}MockSoffice{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(path => new
            {
                RelativePath = Path.GetRelativePath(repoRoot, path),
                Text = File.ReadAllText(path),
                LineCount = File.ReadLines(path).Count()
            })
            .Where(file => file.LineCount >= extraLargeTestFileLineThreshold &&
                (file.Text.Contains("[Fact", StringComparison.Ordinal) ||
                    file.Text.Contains("[Theory", StringComparison.Ordinal)) &&
                !file.Text.Contains("partial class", StringComparison.Ordinal))
            .Select(file => $"{file.RelativePath} ({file.LineCount} 行)")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
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

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        byte[] hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

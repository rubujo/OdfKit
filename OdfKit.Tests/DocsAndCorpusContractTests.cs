using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using OdfKit.Cli;
using OdfKit.Compliance;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 ODF Toolkit 對標文件與 corpus manifest 已宣告必要契約。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Smoke)]
public class DocsAndCorpusContractTests
{
    /// <summary>
    /// Verifies the documented package resource budgets match the public loading defaults.
    /// 驗證文件所載封裝資源預算符合公開載入預設值。
    /// </summary>
    [Fact]
    public void SecurityLimitsDocumentMatchesLoadOptionDefaults()
    {
        string repoRoot = FindRepositoryRoot();
        string securityLimits = File.ReadAllText(Path.Combine(repoRoot, "docs", "security-limits.md"));
        var options = new OdfLoadOptions();

        Assert.Equal(5000, options.MaxZipEntries);
        Assert.Equal(500L * 1024 * 1024, options.MaxEntrySize);
        Assert.Equal(1024L * 1024 * 1024, options.MaxTotalUncompressedSize);
        Assert.Equal(1024L * 1024 * 1024, options.MaxPackageSize);
        Assert.Equal(64L * 1024 * 1024, options.MaxXmlCharactersInDocument);
        Assert.Contains("| ZIP 項目數 | 5,000 |", securityLimits, StringComparison.Ordinal);
        Assert.Contains("| 單一項目解壓縮大小 | 500 MiB |", securityLimits, StringComparison.Ordinal);
        Assert.Contains("| 全封裝解壓縮總量 | 1 GiB |", securityLimits, StringComparison.Ordinal);
        Assert.Contains("| 不可搜尋輸入原始大小 | 1 GiB |", securityLimits, StringComparison.Ordinal);
        Assert.Contains("| 單一 XML 文件字元數 | 64 MiB |", securityLimits, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies evidence claims use schema v2 and dimension-specific levels.
    /// 驗證證據宣稱使用 schema v2 與各維度專屬層級。
    /// </summary>
    [Fact]
    public void EvidenceClaimsUseDimensionSpecificVerifiedLevels()
    {
        string repoRoot = FindRepositoryRoot();
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(repoRoot, "docs", "claims.json")));
        JsonElement manifest = document.RootElement;

        Assert.True(IsEvidenceClaimsSchemaVersionSupported(manifest.GetProperty("schemaVersion").GetInt32()));
        foreach (JsonElement claim in manifest.GetProperty("claims").EnumerateArray())
        {
            string dimension = claim.GetProperty("dimension").GetString()!;
            string level = claim.GetProperty("level").GetString()!;
            Assert.True(
                IsEvidenceClaimLevelAllowed(dimension, level),
                $"Invalid evidence claim level: {dimension} -> {level}");
        }
    }

    /// <summary>
    /// Rejects legacy and cross-dimension evidence claim levels.
    /// 拒絕舊版及跨維度的證據宣稱層級。
    /// </summary>
    /// <param name="dimension">The evidence dimension. / 證據維度。</param>
    /// <param name="level">The evidence level. / 證據層級。</param>
    [Theory]
    [InlineData("PackageFidelity", "complete")]
    [InlineData("PackageFidelity", "semantic-contract-verified")]
    [InlineData("SemanticApiDepth", "semantic-facade-complete")]
    [InlineData("SemanticApiDepth", "round-trip-verified")]
    [InlineData("InteropEvidence", "tested")]
    [InlineData("InteropEvidence", "semantic-contract-verified")]
    public void EvidenceClaimLevelContractRejectsLegacyAndCrossDimensionValues(string dimension, string level)
    {
        Assert.False(IsEvidenceClaimLevelAllowed(dimension, level));
    }

    /// <summary>
    /// Rejects the legacy evidence claims schema version.
    /// 拒絕舊版證據宣稱 schema 版本。
    /// </summary>
    [Fact]
    public void EvidenceClaimsSchemaVersionContractRejectsLegacyVersion()
    {
        Assert.False(IsEvidenceClaimsSchemaVersionSupported(1));
    }

    private static bool IsEvidenceClaimLevelAllowed(string dimension, string level)
    {
        return dimension switch
        {
            "PackageFidelity" => level == "round-trip-verified",
            "SemanticApiDepth" => level == "semantic-contract-verified",
            "InteropEvidence" => level == "interop-tested",
            _ => false,
        };
    }

    private static bool IsEvidenceClaimsSchemaVersionSupported(int schemaVersion) => schemaVersion == 2;

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
    /// 驗證 ODFDOM 官方 sample parity 外部 corpus 已釘選至具體上游版本，具備已審核授權與雜湊欄位，並可通過 metadata gate。
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
        Assert.Equal(4, summary.GetProperty("fixtureCount").GetInt32());
        Assert.Equal(0, summary.GetProperty("baselineExceptionCount").GetInt32());

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        foreach (JsonElement fixture in manifest.RootElement.GetProperty("fixtures").EnumerateArray())
        {
            Assert.Equal("ODF Toolkit ODFDOM sample", fixture.GetProperty("source").GetString());
            Assert.Equal("Apache-2.0", fixture.GetProperty("license").GetString());
            Assert.Contains(
                "github.com/tdf/odftoolkit/blob/b926a6134a2fee782076500dfc02c47c2d651cff",
                fixture.GetProperty("sourceUri").GetString(),
                StringComparison.Ordinal);
            Assert.Matches("^[0-9a-f]{64}$", fixture.GetProperty("sha256").GetString());
            Assert.Contains("Pinned baseline: tdf/odftoolkit release v0.13.0", fixture.GetProperty("notes").GetString(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// 驗證 validate-corpus CLI 訊息已覆蓋全部支援語言，且非英文文化不是英文佔位。
    /// </summary>
    [Fact]
    public void CliCorpusDiagnosticsAreLocalizedForAllSupportedCultures()
    {
        string repoRoot = FindRepositoryRoot();
        // 例外字典已按語系拆檔；契約掃描需彙整全部 OdfLocalizer.Exceptions*.cs。
        string localizer = string.Concat(
            Directory.EnumerateFiles(
                    Path.Combine(repoRoot, "OdfKit", "Compliance"),
                    "OdfLocalizer.Exceptions*.cs",
                    SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
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
            Assert.Equal(17, Regex.Count(localizer, "\\[\"" + Regex.Escape(key) + "\"\\]"));
        }

        Assert.Contains("Die baseline exception baseline", localizer, StringComparison.Ordinal);
        Assert.Contains("La baseline d'une exception", localizer, StringComparison.Ordinal);
        Assert.Contains("baseline exception의 baseline", localizer, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證可維護性文件與在地化拆分、歷史腳本歸檔、一行式 summary 閘門已就緒。
    /// </summary>
    [Fact]
    public void MaintainabilityDocsAndExceptionCultureSplitsArePresent()
    {
        string repoRoot = FindRepositoryRoot();
        string maintainability = File.ReadAllText(Path.Combine(repoRoot, "docs", "maintainability.md"));
        string docsIndex = File.ReadAllText(Path.Combine(repoRoot, "docs", "index.md"));
        string engReadme = File.ReadAllText(Path.Combine(repoRoot, "eng", "README.md"));
        string historicalReadme = File.ReadAllText(Path.Combine(repoRoot, "eng", "historical-refactor", "README.md"));
        string oneLineScript = File.ReadAllText(Path.Combine(repoRoot, "eng", "Test-OneLineXmlSummary.ps1"));
        string keyParityScript = File.ReadAllText(Path.Combine(repoRoot, "eng", "Test-LocalizerKeyParity.ps1"));
        string addLocalizerKeyScript = File.ReadAllText(Path.Combine(repoRoot, "eng", "Add-LocalizerKey.ps1"));
        string publicApiBaselineScript = File.ReadAllText(Path.Combine(repoRoot, "eng", "Generate-PublicApiBaseline.ps1"));
        string publicApiReadme = File.ReadAllText(Path.Combine(repoRoot, "OdfKit", "PublicAPI", "README.md"));
        string mailMergeMain = File.ReadAllText(Path.Combine(repoRoot, "OdfKit", "Text", "OdfStreamingMailMerge.cs"));
        string mailMergeSegments = File.ReadAllText(Path.Combine(repoRoot, "OdfKit", "Text", "OdfStreamingMailMerge.Segments.cs"));
        string mailMergeCache = File.ReadAllText(Path.Combine(repoRoot, "OdfKit", "Text", "OdfStreamingMailMerge.ExpressionCache.cs"));

        string[] cultures = ["en", "zh-TW", "de", "fr", "nl", "nb", "pt", "it", "sk", "da", "ms", "ko"];
        foreach (string culture in cultures)
        {
            string path = Path.Combine(repoRoot, "OdfKit", "Compliance", "OdfLocalizer.Exceptions." + culture + ".cs");
            Assert.True(File.Exists(path), "缺少語系例外字典：" + culture);
        }

        string[] publicApiTfms = ["net10.0", "netstandard2.0"];
        foreach (string tfm in publicApiTfms)
        {
            string shipped = Path.Combine(repoRoot, "OdfKit", "PublicAPI", tfm, "PublicAPI.Shipped.txt");
            string unshipped = Path.Combine(repoRoot, "OdfKit", "PublicAPI", tfm, "PublicAPI.Unshipped.txt");
            Assert.True(File.Exists(shipped), "缺少 PublicAPI.Shipped.txt：" + tfm);
            Assert.True(File.Exists(unshipped), "缺少 PublicAPI.Unshipped.txt：" + tfm);
            Assert.True(
                File.ReadAllLines(unshipped).Length > 100,
                "PublicAPI.Unshipped.txt 基線過短：" + tfm);
        }

        Assert.Contains("maintainability.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("historical-refactor", engReadme, StringComparison.Ordinal);
        Assert.Contains("Test-LocalizerKeyParity.ps1", engReadme, StringComparison.Ordinal);
        Assert.Contains("Generate-PublicApiBaseline.ps1", engReadme, StringComparison.Ordinal);
        Assert.Contains("預設不要重跑", historicalReadme, StringComparison.Ordinal);
        Assert.Contains("FailOnIssues", oneLineScript, StringComparison.Ordinal);
        Assert.Contains("FailOnIssues", keyParityScript, StringComparison.Ordinal);
        Assert.Contains("Add-LocalizerKey", addLocalizerKeyScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exceptions.", addLocalizerKeyScript, StringComparison.Ordinal);
        Assert.Contains("RS0016", publicApiBaselineScript, StringComparison.Ordinal);
        Assert.Contains("PublicApiAnalyzers", publicApiReadme, StringComparison.Ordinal);
        Assert.Contains("partial class OdfStreamingMailMerge", mailMergeMain, StringComparison.Ordinal);
        Assert.Contains("TemplateSegment", mailMergeSegments, StringComparison.Ordinal);
        Assert.Contains("MailMergeExpressionCache", mailMergeCache, StringComparison.Ordinal);
        Assert.DoesNotContain("小於 1MB", mailMergeMain, StringComparison.Ordinal);
        Assert.Contains("Partial", maintainability, StringComparison.Ordinal);
        Assert.Contains("PublicApiAnalyzers", maintainability, StringComparison.Ordinal);
        Assert.Contains("Test-LocalizerKeyParity", maintainability, StringComparison.Ordinal);
        Assert.Contains("Add-LocalizerKey", maintainability, StringComparison.Ordinal);
        Assert.Contains("EnablePackageValidation", maintainability, StringComparison.Ordinal);
        Assert.Contains("i18n", maintainability, StringComparison.Ordinal);

        string agents = File.ReadAllText(Path.Combine(repoRoot, "AGENTS.md"));
        string ciYml = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "ci.yml"));
        string ciCd = File.ReadAllText(Path.Combine(repoRoot, "docs", "ci-cd.md"));
        string packageProps = File.ReadAllText(Path.Combine(repoRoot, "eng", "OdfKit.Package.props"));
        // OdfKit.csproj 自 M-2 起以 <Import Project="..\eng\OdfKit.Package.props" /> 取得
        // EnablePackageValidation 等中繼資料，不再內嵌於本檔；改讀「本檔＋其 Import 鏈」的
        // 原始文字合併結果，讓下方 Assert.Contains 仍能偵測「兩處都真的沒有該屬性」的退化。
        string odfKitCsproj = CsprojImportResolver.ReadProjectTextWithImports(repoRoot, @"OdfKit\OdfKit.csproj");
        string genJsonScript = File.ReadAllText(Path.Combine(repoRoot, "eng", "Generate-LocalizerExceptionsFromJson.ps1"));
        string optionalParamsDoc = File.ReadAllText(Path.Combine(repoRoot, "docs", "public-api-optional-parameters.md"));
        string collaboratorsDoc = File.ReadAllText(Path.Combine(repoRoot, "docs", "architecture-collaborators.md"));
        string archiveMain = File.ReadAllText(Path.Combine(repoRoot, "OdfKit", "Core", "OdfPackageArchiveWriter.cs"));
        string tableSparse = File.ReadAllText(Path.Combine(repoRoot, "OdfKit", "DOM", "OdfElementContentModel.Table.Sparse.cs"));

        foreach (string culture in cultures)
        {
            string jsonPath = Path.Combine(repoRoot, "OdfKit", "Compliance", "i18n", "exceptions." + culture + ".json");
            Assert.True(File.Exists(jsonPath), "缺少 i18n JSON：" + culture);
        }

        Assert.Contains("docs/maintainability.md", agents, StringComparison.Ordinal);
        Assert.Contains("OdfKit/PublicAPI/README.md", agents, StringComparison.Ordinal);
        Assert.Contains("OdfKit/Compliance/i18n/README.md", agents, StringComparison.Ordinal);
        Assert.Contains("docs/public-api-optional-parameters.md", agents, StringComparison.Ordinal);
        Assert.Contains("architecture-collaborators", maintainability, StringComparison.Ordinal);
        Assert.Contains("maintainability:", ciYml, StringComparison.Ordinal);
        Assert.Contains("Test-LocalizerKeyParity.ps1", ciYml, StringComparison.Ordinal);
        Assert.Contains("Generate-LocalizerExceptionsFromJson.ps1", ciYml, StringComparison.Ordinal);
        Assert.Contains("maintainability", ciCd, StringComparison.Ordinal);
        Assert.Contains("EnablePackageValidation", packageProps, StringComparison.Ordinal);
        Assert.Contains("EnablePackageValidation", odfKitCsproj, StringComparison.Ordinal);
        Assert.Contains("VerifyOnly", genJsonScript, StringComparison.Ordinal);
        Assert.Contains("RS0026", optionalParamsDoc, StringComparison.Ordinal);
        Assert.Contains("OdfPackageArchiveWriter", collaboratorsDoc, StringComparison.Ordinal);
        Assert.Contains("partial class OdfPackageArchiveWriter", archiveMain, StringComparison.Ordinal);
        Assert.Contains("TablePageState", tableSparse, StringComparison.Ordinal);
        Assert.Contains("architecture-collaborators.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("public-api-optional-parameters.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Schema 與流通性", maintainability, StringComparison.Ordinal);
        Assert.Contains("非目標", maintainability, StringComparison.Ordinal);
        Assert.Contains("ODF 1.0～1.4", agents, StringComparison.Ordinal);
        Assert.DoesNotContain("Schema 可選套件", maintainability, StringComparison.Ordinal);
        // 歷史 Split 腳本不得再留在 eng/ 根目錄（應在 historical-refactor/）。
        Assert.Empty(
            Directory.EnumerateFiles(Path.Combine(repoRoot, "eng"), "Split-*.ps1", SearchOption.TopDirectoryOnly));
    }

    /// <summary>
    /// 驗證 17 語系例外字典的訊息鍵集合與 en 完全對等（i18n 鍵值對等閘門）。
    /// </summary>
    [Fact]
    public void ExceptionDictionaryKeysAreParityAcrossCultures()
    {
        string repoRoot = FindRepositoryRoot();
        string[] cultures = ["en", "zh-TW", "de", "fr", "nl", "nb", "pt", "it", "sk", "da", "ms", "ko", "ja", "es", "cs", "pl", "pt-BR"];
        var keyPattern = new Regex(
            "\\[\"((?:Err|Warn|Cli|Diag|Rule)_[^\"]+)\"\\]",
            RegexOptions.Compiled);

        Dictionary<string, HashSet<string>> keysByCulture = new(StringComparer.Ordinal);
        foreach (string culture in cultures)
        {
            string path = Path.Combine(repoRoot, "OdfKit", "Compliance", "OdfLocalizer.Exceptions." + culture + ".cs");
            string text = File.ReadAllText(path);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in keyPattern.Matches(text))
            {
                keys.Add(match.Groups[1].Value);
            }

            Assert.NotEmpty(keys);
            keysByCulture[culture] = keys;
        }

        HashSet<string> baseline = keysByCulture["en"];
        foreach (string culture in cultures)
        {
            if (culture == "en")
            {
                continue;
            }

            HashSet<string> current = keysByCulture[culture];
            List<string> missing = baseline
                .Where(key => !current.Contains(key))
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();
            List<string> extra = current
                .Where(key => !baseline.Contains(key))
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();

            Assert.True(
                missing.Count == 0 && extra.Count == 0,
                "語系 " + culture + " 與 en 鍵值不對等。缺少：" + string.Join(", ", missing.Take(10)) +
                "；多餘：" + string.Join(", ", extra.Take(10)));
        }
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
    /// 驗證產品原始碼中直接使用的本地化訊息鍵值皆已註冊於全部支援語言。
    /// </summary>
    [Fact]
    public void LiteralLocalizerMessageKeysResolveForAllSupportedCultures()
    {
        string repoRoot = FindRepositoryRoot();
        string[] sourceRoots =
        [
            Path.Combine(repoRoot, "OdfKit"),
            .. Directory.GetDirectories(repoRoot, "OdfKit.Extensions.*"),
            Path.Combine(repoRoot, "tools")
        ];
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        var keyPattern = new Regex("OdfLocalizer\\.GetMessage\\(\\s*\"([^\"]+)\"", RegexOptions.Compiled);
        foreach (string sourceRoot in sourceRoots)
        {
            if (!Directory.Exists(sourceRoot))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                    file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                string source = File.ReadAllText(file);
                foreach (Match match in keyPattern.Matches(source))
                {
                    keys.Add(match.Groups[1].Value);
                }
            }
        }

        Assert.NotEmpty(keys);

        string[] cultures = ["en", "zh-TW", "de", "fr", "nl", "nb", "pt", "it", "sk", "da", "ms", "ko"];
        object?[] args = ["A", "B", "C", "D"];
        foreach (string cultureName in cultures)
        {
            var culture = new System.Globalization.CultureInfo(cultureName);
            foreach (string key in keys)
            {
                string message = OdfLocalizer.GetMessage(key, culture, args);

                Assert.NotEqual(key, message);
            }
        }
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
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        int expectedInvalidCount = manifest.RootElement.GetProperty("fixtures").EnumerateArray()
            .Count(item => string.Equals(item.GetProperty("expected").GetString(), "invalid", StringComparison.Ordinal));
        int expectedValidCount = fixtureCount - expectedInvalidCount;
        Assert.True(fixtureCount >= 200, $"corpus fixtureCount 應 ≥ 200，實際為 {fixtureCount}。");
        Assert.Equal(fixtureCount, summary.GetProperty("passedCount").GetInt32());
        Assert.Equal(expectedValidCount, summary.GetProperty("validCount").GetInt32());
        Assert.Equal(expectedInvalidCount, summary.GetProperty("invalidCount").GetInt32());
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
                item.GetProperty("expected").GetString() == "valid" &&
                item.GetProperty("sha256Matches").GetBoolean() &&
                item.GetProperty("passed").GetBoolean());
        Assert.Contains(
            json.RootElement.GetProperty("fixtures").EnumerateArray(),
            item => item.GetProperty("id").GetString() == "repo-generated-complex-financial-model" &&
                item.GetProperty("expected").GetString() == "valid" &&
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
                item.GetProperty("expected").GetString() == "valid" &&
                item.GetProperty("sha256Matches").GetBoolean() &&
                item.GetProperty("passed").GetBoolean());
    }

    /// <summary>
    /// 驗證 JSON Collaboration fixture manifest 具備 clean-room 來源、授權、SHA-256 與完整 operation 覆蓋。
    /// </summary>
    [Fact]
    public void CollaborationFixtureManifestDeclaresSourceLicenseAndHash()
    {
        string repoRoot = FindRepositoryRoot();
        string manifestPath = Path.Combine(repoRoot, "tests", "fixtures", "collaboration", "manifest.json");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement root = manifest.RootElement;
        JsonElement fixtures = root.GetProperty("fixtures");
        JsonElement fixture = manifest.RootElement.GetProperty("fixtures")[0];
        string path = fixture.GetProperty("path").GetString() ?? string.Empty;
        string expectedHash = fixture.GetProperty("sha256").GetString() ?? string.Empty;
        string fullPath = Path.Combine(Path.GetDirectoryName(manifestPath)!, path);

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Contains("do not copy TDF Java source", root.GetProperty("cleanRoomPolicy").GetString(), StringComparison.Ordinal);
        Assert.Equal("repo-generated-tdf-subset-envelope", fixture.GetProperty("id").GetString());
        Assert.Equal("repo-generated", fixture.GetProperty("sourceType").GetString());
        Assert.Equal("TDF changes envelope with a representative implemented subset.", fixture.GetProperty("wireShape").GetString());
        Assert.Equal("https://tdf.github.io/odftoolkit/odfdom/operations/operations.html", fixture.GetProperty("sourceUrl").GetString());
        Assert.Equal(expectedHash, ComputeSha256(fullPath));
        Assert.Contains(fixtures.EnumerateArray(), item => item.GetProperty("path").GetString() == "repo-generated/unknown-fields-roundtrip.json");
        Assert.Contains(fixtures.EnumerateArray(), item => item.GetProperty("semanticStatus").GetString() == "parse-safety");
        Assert.Contains(fixtures.EnumerateArray(), item => item.GetProperty("semanticStatus").GetString() == "strict-unsupported");

        string json = File.ReadAllText(fullPath);
        Assert.Contains("\"changes\"", json, StringComparison.Ordinal);
        string[] documentedOperations =
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
        foreach (string operation in documentedOperations)
        {
            Assert.Contains(
                fixtures.EnumerateArray(),
                item => item.GetProperty("sourceType").GetString() == "tdf-public-docs" &&
                    item.GetProperty("primaryOperation").GetString() == operation);
        }
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

    private static Dictionary<string, string[]> ParseProfileSourceRows(string document)
    {
        Dictionary<string, string[]> rows = [];
        foreach (string line in document.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            Match match = Regex.Match(line, @"^\| `(?<id>[^`]+)` \| (?<rest>.+) \|$");
            if (!match.Success)
            {
                continue;
            }

            string[] columns = match.Groups["rest"].Value
                .Split(" | ")
                .Select(column => column.Trim().Trim('`'))
                .ToArray();

            rows.Add(match.Groups["id"].Value, columns);
        }

        return rows;
    }

    private static string FormatVersionRange(OdfVersionRange range)
    {
        if (range.Minimum == OdfVersion.Odf10 && range.Maximum == OdfVersion.Odf14)
        {
            return "all-known";
        }

        if (range.Minimum == range.Maximum)
        {
            return range.Minimum switch
            {
                OdfVersion.Odf10 => "1.0",
                OdfVersion.Odf11 => "1.1",
                OdfVersion.Odf12 => "1.2",
                OdfVersion.Odf13 => "1.3",
                OdfVersion.Odf14 => "1.4",
                _ => range.Minimum.ToString()
            };
        }

        return FormatVersionRange(OdfVersionRange.Exact(range.Minimum)) + "-" +
            FormatVersionRange(OdfVersionRange.Exact(range.Maximum));
    }

    private static string ComputeSha256(string path)
    {
        byte[] hash = SHA256.HashData(NormalizeLineEndings(File.ReadAllBytes(path)));
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
}

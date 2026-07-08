using System.IO;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 ODF 1.4 規格覆蓋路線圖與 typed DOM audit 入口。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfCoverageRoadmapTests
{
    /// <summary>
    /// 驗證 ODF 1.4 覆蓋路線圖明確區分規格覆蓋與高階 API 深度。
    /// </summary>
    [Fact]
    public void Odf14CoverageRoadmap_DeclaresLayeredCoverageModel()
    {
        string repoRoot = FindRepoRoot();
        string document = File.ReadAllText(Path.Combine(repoRoot, "docs", "odf14-coverage-roadmap.md"));

        Assert.Contains("schema coverage", document);
        Assert.Contains("package lifecycle", document);
        Assert.Contains("high-level facade", document);
        Assert.Contains("interop behavior", document);
        Assert.Contains("typed-dom-coverage", document);
    }

    /// <summary>
    /// 驗證 API reference roadmap 鎖定目前成熟度收斂的三個高使用率工作流。
    /// </summary>
    [Fact]
    public void ApiReferenceRoadmap_TracksHighValueWorkflowDocs()
    {
        string repoRoot = FindRepoRoot();
        string document = File.ReadAllText(Path.Combine(repoRoot, "docs", "api-reference-roadmap.md"));

        Assert.Contains("Spreadsheet data workflows", document);
        Assert.Contains("Chart workflows", document);
        Assert.Contains("Template workflows", document);
        Assert.Contains("Compatibility Notes", document);
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

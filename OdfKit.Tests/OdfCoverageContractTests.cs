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
    /// 驗證 typed DOM coverage audit 可穩定輸出 schema 與 wrapper 摘要。
    /// </summary>
    [Fact]
    public void TypedDomCoverageAuditReportsSchemaAndWrapperCounts()
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
    public void TypedDomCoverageAuditHasStableOdf14ElementAndAttributeCounts()
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

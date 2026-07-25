using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 NuGet 發佈前的必要套件中繼資料。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Smoke)]
public class PackageReadinessTests
{
    /// <summary>
    /// 驗證核心套件包含發佈所需的 NuGet 中繼資料與封裝檔案。
    /// </summary>
    [Fact]
    public void CoreProjectDeclaresRequiredPackageMetadata()
    {
        string repoRoot = FindRepositoryRoot();
        const string projectRelativePath = @"OdfKit\OdfKit.csproj";
        string projectPath = Path.Combine(repoRoot, "OdfKit", "OdfKit.csproj");

        // 中繼資料（PackageLicenseExpression、RepositoryUrl、IncludeSymbols 等）自 M-2 起改由
        // eng/OdfKit.Package.props 以 <Import> 提供，不再內嵌於本檔；GetValue 因此改為求值
        // 有效屬性（本檔＋其 Import 鏈），而非只讀本檔第一個 PropertyGroup 的扁平 XML。
        Assert.Equal("OdfKit", GetValue(repoRoot, projectRelativePath, "PackageId"));
        Assert.Equal("CC0-1.0", GetValue(repoRoot, projectRelativePath, "PackageLicenseExpression"));
        Assert.Equal("README.md", GetValue(repoRoot, projectRelativePath, "PackageReadmeFile"));
        Assert.Equal("https://github.com/rubujo/OdfKit", GetValue(repoRoot, projectRelativePath, "PackageProjectUrl"));
        Assert.Equal("https://github.com/rubujo/OdfKit", GetValue(repoRoot, projectRelativePath, "RepositoryUrl"));
        Assert.Equal("git", GetValue(repoRoot, projectRelativePath, "RepositoryType"));
        Assert.Equal("true", GetValue(repoRoot, projectRelativePath, "IncludeSymbols"));
        Assert.Equal("snupkg", GetValue(repoRoot, projectRelativePath, "SymbolPackageFormat"));
        Assert.Equal("true", GetValue(repoRoot, projectRelativePath, "PublishRepositoryUrl"));

        string description = GetValue(repoRoot, projectRelativePath, "Description");
        Assert.Contains("ODF", description, StringComparison.Ordinal);
        Assert.Contains("OpenDocument", description, StringComparison.Ordinal);

        string tags = GetValue(repoRoot, projectRelativePath, "PackageTags");
        Assert.Contains("ODF", tags, StringComparison.Ordinal);
        Assert.Contains("ODT", tags, StringComparison.Ordinal);
        Assert.Contains("ODS", tags, StringComparison.Ordinal);

        XDocument project = XDocument.Load(projectPath);
        AssertFileIsPacked(project, @"..\README.md", @"\");
        AssertFileIsPacked(project, @"..\LICENSE", @"\");
        AssertFileIsPacked(project, @"..\THIRD-PARTY-NOTICES.md", @"\");
        Assert.True(File.Exists(Path.Combine(repoRoot, "README.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "LICENSE")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "THIRD-PARTY-NOTICES.md")));
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

        throw new InvalidOperationException("找不到 OdfKit repository 根目錄。");
    }

    private static string GetValue(string repoRoot, string projectRelativePath, string elementName)
    {
        // 刻意保留「找不到就丟例外」語意：即使屬性搬進 Import 鏈中的任何一份檔案，只要真的
        // 從整條鏈消失（真實退化），CsprojImportResolver 仍回傳 null，這裡仍會擲出例外並讓
        // 呼叫端測試失敗，而不是無論如何都通過的假測試。
        return CsprojImportResolver.GetEffectivePropertyValue(repoRoot, projectRelativePath, elementName)
            ?? throw new InvalidOperationException("缺少 " + elementName + " 套件中繼資料。");
    }

    private static void AssertFileIsPacked(XDocument project, string include, string packagePath)
    {
        bool found = project.Descendants("None").Any(element =>
            string.Equals(element.Attribute("Include")?.Value, include, StringComparison.Ordinal) &&
            string.Equals(element.Attribute("Pack")?.Value, "true", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(element.Attribute("PackagePath")?.Value, packagePath, StringComparison.Ordinal));

        Assert.True(found, include + " 必須加入 NuGet 封裝。");
    }
}

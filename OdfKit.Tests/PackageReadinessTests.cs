using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 NuGet 發佈前的必要套件中繼資料。
/// </summary>
public class PackageReadinessTests
{
    /// <summary>
    /// 驗證核心套件包含發佈所需的 NuGet 中繼資料與封裝檔案。
    /// </summary>
    [Fact]
    public void CoreProjectDeclaresRequiredPackageMetadata()
    {
        string repoRoot = FindRepositoryRoot();
        string projectPath = Path.Combine(repoRoot, "OdfKit", "OdfKit.csproj");

        XDocument project = XDocument.Load(projectPath);
        XElement propertyGroup = project.Root?.Elements("PropertyGroup").FirstOrDefault()
            ?? throw new InvalidOperationException("找不到主要 PropertyGroup。");

        Assert.Equal("OdfKit", GetValue(propertyGroup, "PackageId"));
        Assert.Equal("CC0-1.0", GetValue(propertyGroup, "PackageLicenseExpression"));
        Assert.Equal("README.md", GetValue(propertyGroup, "PackageReadmeFile"));
        Assert.Equal("git", GetValue(propertyGroup, "RepositoryType"));
        Assert.Equal("true", GetValue(propertyGroup, "IncludeSymbols"));
        Assert.Equal("snupkg", GetValue(propertyGroup, "SymbolPackageFormat"));
        Assert.Equal("true", GetValue(propertyGroup, "PublishRepositoryUrl"));

        string description = GetValue(propertyGroup, "Description");
        Assert.Contains("ODF", description, StringComparison.Ordinal);
        Assert.Contains("OpenDocument", description, StringComparison.Ordinal);

        string tags = GetValue(propertyGroup, "PackageTags");
        Assert.Contains("ODF", tags, StringComparison.Ordinal);
        Assert.Contains("ODT", tags, StringComparison.Ordinal);
        Assert.Contains("ODS", tags, StringComparison.Ordinal);

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

    private static string GetValue(XElement propertyGroup, string elementName)
    {
        return propertyGroup.Element(elementName)?.Value
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

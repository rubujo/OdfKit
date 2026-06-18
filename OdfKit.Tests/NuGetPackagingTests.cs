using System.Xml.Linq;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證可發佈 NuGet 專案之雙 TFM 與套件中繼資料（REL-1）。
/// </summary>
public class NuGetPackagingTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly (string PackageId, string ProjectRelativePath)[] PackableProjects =
    [
        ("OdfKit", "OdfKit/OdfKit.csproj"),
        ("OdfKit.Extensions.Html", "OdfKit.Extensions.Html/OdfKit.Extensions.Html.csproj"),
        ("OdfKit.Extensions.Imaging", "OdfKit.Extensions.Imaging/OdfKit.Extensions.Imaging.csproj"),
        ("OdfKit.Extensions.Ooxml", "OdfKit.Extensions.Ooxml/OdfKit.Extensions.Ooxml.csproj"),
        ("OdfKit.Extensions.Pdf", "OdfKit.Extensions.Pdf/OdfKit.Extensions.Pdf.csproj"),
        ("OdfKit.Extensions.Rendering", "OdfKit.Extensions.Rendering/OdfKit.Extensions.Rendering.csproj"),
    ];

    /// <summary>
    /// 驗證所有可發佈專案皆宣告 net10.0 與 netstandard2.0 雙目標框架。
    /// </summary>
    [Theory]
    [MemberData(nameof(PackableProjectPaths))]
    public void PackableProject_DeclaresDualTargetFrameworks(string packageId, string projectRelativePath)
    {
        Assert.False(string.IsNullOrWhiteSpace(packageId));
        string tfms = ReadProperty(projectRelativePath, "TargetFrameworks")
            ?? throw new InvalidOperationException($"找不到 TargetFrameworks：{projectRelativePath}");

        Assert.Contains("net10.0", tfms, StringComparison.Ordinal);
        Assert.Contains("netstandard2.0", tfms, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證核心套件具備 README 與授權中繼資料。
    /// </summary>
    [Fact]
    public void OdfKit_CorePackage_HasReadmeAndLicenseMetadata()
    {
        Assert.Equal("OdfKit", ReadProperty("OdfKit/OdfKit.csproj", "PackageId"));
        Assert.Equal("CC0-1.0", ReadProperty("OdfKit/OdfKit.csproj", "PackageLicenseExpression"));
        Assert.Equal("README.md", ReadProperty("OdfKit/OdfKit.csproj", "PackageReadmeFile"));
    }

    public static TheoryData<string, string> PackableProjectPaths()
    {
        var data = new TheoryData<string, string>();
        foreach (var (packageId, path) in PackableProjects)
        {
            data.Add(packageId, path);
        }

        return data;
    }

    private static string ReadProperty(string projectRelativePath, string propertyName)
    {
        string projectPath = Path.Combine(RepoRoot, projectRelativePath);
        XDocument document = XDocument.Load(projectPath);
        XNamespace msbuild = document.Root!.Name.Namespace;

        foreach (XElement group in document.Root.Elements(msbuild + "PropertyGroup"))
        {
            XElement? element = group.Element(msbuild + propertyName);
            if (element is not null && !string.IsNullOrWhiteSpace(element.Value))
            {
                return element.Value.Trim();
            }
        }

        return string.Empty;
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "OdfKit", "OdfKit.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("找不到 OdfKit repo 根目錄。");
    }
}

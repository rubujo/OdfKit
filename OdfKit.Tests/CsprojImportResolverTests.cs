using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證測試用 MSBuild Import 解析器的跨平台路徑行為。
/// </summary>
public class CsprojImportResolverTests
{
    /// <summary>
    /// 驗證 Windows 風格的專案與 Import 相對路徑可在所有支援平台解析。
    /// </summary>
    [Fact]
    public void WindowsStyleRelativePathsResolveOnAllPlatforms()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "odfkit-csproj-import-" + Guid.NewGuid().ToString("N"));
        string projectDirectory = Path.Combine(tempRoot, "src");
        string propsDirectory = Path.Combine(tempRoot, "eng");

        try
        {
            Directory.CreateDirectory(projectDirectory);
            Directory.CreateDirectory(propsDirectory);
            File.WriteAllText(
                Path.Combine(projectDirectory, "Test.csproj"),
                """
                <Project>
                  <Import Project="..\eng\Test.Package.props" />
                </Project>
                """);
            File.WriteAllText(
                Path.Combine(propsDirectory, "Test.Package.props"),
                """
                <Project>
                  <PropertyGroup>
                    <PackageId>CrossPlatform.Package</PackageId>
                  </PropertyGroup>
                </Project>
                """);

            string? packageId = CsprojImportResolver.GetEffectivePropertyValue(
                tempRoot,
                @"src\Test.csproj",
                "PackageId");

            Assert.Equal("CrossPlatform.Package", packageId);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}

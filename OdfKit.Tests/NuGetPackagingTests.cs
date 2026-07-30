using System.Xml.Linq;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Verifies dual-targeting and package metadata for publishable NuGet projects.
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
        ("OdfKit.Extensions.Rdf", "OdfKit.Extensions.Rdf/OdfKit.Extensions.Rdf.csproj"),
        ("OdfKit.Extensions.Collaboration", "OdfKit.Extensions.Collaboration/OdfKit.Extensions.Collaboration.csproj"),
    ];

    /// <summary>
    /// 驗證所有可發佈專案皆宣告 net10.0 與 netstandard2.0 雙目標框架。
    /// </summary>
    [Theory]
    [MemberData(nameof(PackableProjectPaths))]
    public void PackableProjectDeclaresDualTargetFrameworks(string packageId, string projectRelativePath)
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
    public void OdfKitCorePackageHasReadmeAndLicenseMetadata()
    {
        Assert.Equal("OdfKit", ReadProperty("OdfKit/OdfKit.csproj", "PackageId"));
        Assert.Equal("CC0-1.0", ReadProperty("OdfKit/OdfKit.csproj", "PackageLicenseExpression"));
        Assert.Equal("README.md", ReadProperty("OdfKit/OdfKit.csproj", "PackageReadmeFile"));
    }

    /// <summary>
    /// Verifies that the Imaging package explicitly carries native assets for Linux, Windows, and macOS.
    /// 驗證 Imaging 套件明確攜帶 Linux、Windows 與 macOS 原生資產相依。
    /// </summary>
    [Fact]
    public void ImagingPackageDeclaresAllDesktopNativeAssets()
    {
        string projectPath = Path.Combine(
            RepoRoot,
            "OdfKit.Extensions.Imaging",
            "OdfKit.Extensions.Imaging.csproj");
        XDocument document = XDocument.Load(projectPath);
        XNamespace msbuild = document.Root!.Name.Namespace;
        string[] packageIds = document
            .Descendants(msbuild + "PackageReference")
            .Select(static element => (string?)element.Attribute("Include"))
            .Where(static id => id is not null)
            .Cast<string>()
            .ToArray();

        Assert.Contains("SkiaSharp.NativeAssets.Linux", packageIds);
        Assert.Contains("SkiaSharp.NativeAssets.Win32", packageIds);
        Assert.Contains("SkiaSharp.NativeAssets.macOS", packageIds);
    }

    /// <summary>
    /// Verifies that NuGet CI packs once and tests the same immutable snapshot on four desktop runners.
    /// 驗證 NuGet CI 僅封裝一次，並在四種桌面 runner 測試同一份不可變快照。
    /// </summary>
    [Fact]
    public void NuGetWorkflowUsesSinglePackAndFourPlatformConsumerMatrix()
    {
        string workflow = File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", "nuget-pack.yml"));
        string setupAction = File.ReadAllText(
            Path.Combine(RepoRoot, ".github", "actions", "setup-dotnet-odfkit", "action.yml"));
        string packScript = File.ReadAllText(Path.Combine(RepoRoot, "eng", "Test-NuGetPack.ps1"));

        Assert.Contains("pack-contract:", workflow, StringComparison.Ordinal);
        Assert.Contains("needs: pack-contract", workflow, StringComparison.Ordinal);
        Assert.Contains("runner: ubuntu-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("runner: windows-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("runner: windows-11-arm", workflow, StringComparison.Ordinal);
        Assert.Contains("runner: macos-15", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: actions/upload-artifact@", workflow, StringComparison.Ordinal);
        Assert.Contains("uses: actions/download-artifact@", workflow, StringComparison.Ordinal);
        Assert.Contains("-GenerateHashManifest", workflow, StringComparison.Ordinal);
        Assert.Contains("VerifyHashManifest = $true", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("schedule:", workflow, StringComparison.Ordinal);
        // NuGet cache 由 workflow 直接呼叫 cache-odfkit：actions/cache 的 post 步驟在多層
        // composite 下會取得最外層 composite 的 inputs（actions/runner#2030），被 setup action
        // 巢狀包裝時儲存會靜默失敗。
        Assert.Contains("key: nuget-${{ runner.os }}-v1", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("uses: ./.github/actions/cache-odfkit", setupAction, StringComparison.Ordinal);
        Assert.DoesNotContain("uses: actions/cache", setupAction, StringComparison.Ordinal);
        Assert.DoesNotContain("runner.arch", setupAction, StringComparison.Ordinal);
        Assert.DoesNotContain("nuget-fingerprint", setupAction, StringComparison.Ordinal);
        Assert.DoesNotContain("matrix.rid", setupAction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SHA256SUMS", packScript, StringComparison.Ordinal);
        Assert.Contains("RuntimeInformation.OSArchitecture", packScript, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the Windows x64 package gate runs the net48 consumer against all published packages.
    /// 驗證 Windows x64 套件閘門會以 net48 consumer 執行全部可發佈套件。
    /// </summary>
    [Fact]
    public void NetFramework48ConsumerIsIntegratedWithPackageGateAndWorkflow()
    {
        string projectPath = Path.Combine(
            RepoRoot,
            "tests",
            "OdfKit.NetFramework48Smoke",
            "OdfKit.NetFramework48Smoke.csproj");
        XDocument project = XDocument.Load(projectPath);
        XNamespace msbuild = project.Root!.Name.Namespace;
        string? targetFramework = project
            .Descendants(msbuild + "TargetFramework")
            .Select(static element => element.Value)
            .SingleOrDefault();
        string[] packageIds = project
            .Descendants(msbuild + "PackageReference")
            .Select(static element => (string?)element.Attribute("Include"))
            .Where(static id => id is not null)
            .Cast<string>()
            .ToArray();

        Assert.Equal("net48", targetFramework);
        foreach ((string packageId, _) in PackableProjects)
        {
            Assert.Contains(packageId, packageIds);
        }

        string smokeScript = File.ReadAllText(Path.Combine(RepoRoot, "eng", "Test-NetFramework48Smoke.ps1"));
        string packScript = File.ReadAllText(Path.Combine(RepoRoot, "eng", "Test-NuGetPack.ps1"));
        string workflow = File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", "nuget-pack.yml"));
        Assert.Contains("UseLocalPackages=true", smokeScript, StringComparison.Ordinal);
        Assert.Contains("Test-NetFramework48Smoke.ps1", packScript, StringComparison.Ordinal);
        Assert.Contains("net48: true", workflow, StringComparison.Ordinal);
        Assert.Contains("matrix.net48", workflow, StringComparison.Ordinal);
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
        // 部分屬性（例如 OdfKit.csproj 的 PackageLicenseExpression）自 M-2 起改由
        // <Import Project="..\eng\OdfKit.Package.props" /> 提供，不再內嵌於各專案檔本身；
        // 因此改為求值有效屬性（本檔＋其 Import 鏈），而非只掃描本檔自己的 PropertyGroup。
        // 若屬性從整條鏈消失（真實退化），此處仍回傳空字串，讓呼叫端 Assert.Equal/Contains 失敗。
        string? value = CsprojImportResolver.GetEffectivePropertyValue(RepoRoot, projectRelativePath, propertyName);
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
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

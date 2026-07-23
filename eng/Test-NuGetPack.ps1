#Requires -Version 7.0
<#
.SYNOPSIS
    驗證 OdfKit NuGet 封裝結構與 net8.0、net10.0、net48 消費端煙霧建置（REL-1）。
.PARAMETER Configuration
    建置組態，預設 Release。
.PARAMETER OutputDirectory
    封裝輸出目錄，預設 artifacts/nuget。
.PARAMETER SkipPack
    略過封裝，使用既有 artifacts/nuget。
.PARAMETER SkipConsumerSmoke
    略過消費端煙霧測試，只驗證封裝契約。
.PARAMETER ConsumerSmokeDirectory
    消費端 smoke 的 artifacts 子目錄名稱。
.PARAMETER SkipNetFrameworkSmoke
    略過 Windows net48 CLR 消費端煙霧；用於非 x64 Windows runner。
.PARAMETER GenerateHashManifest
    為封裝產物建立 SHA256SUMS manifest。
.PARAMETER VerifyHashManifest
    在使用既有封裝前驗證 SHA256SUMS manifest。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$OutputDirectory = "artifacts/nuget",
    [switch]$SkipPack,
    [switch]$SkipConsumerSmoke,
    [string]$ConsumerSmokeDirectory = "nuget-consumer-smoke",
    [switch]$SkipNetFrameworkSmoke,
    [switch]$GenerateHashManifest,
    [switch]$VerifyHashManifest
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoRoot = Split-Path -Parent $PSScriptRoot
$packageVersion = & (Join-Path $PSScriptRoot "Get-PackageVersion.ps1")
$outDir = Join-Path $repoRoot $OutputDirectory
$hashManifestPath = Join-Path $outDir "SHA256SUMS"
$previousNugetPackages = $env:NUGET_PACKAGES

$expectedPackages = @(
    @{ Id = "OdfKit"; Assembly = "OdfKit.dll"; Tfms = @("net10.0", "netstandard2.0"); Consumer = $true; RequireSnupkg = $true },
    @{ Id = "OdfKit.Extensions.Html"; Assembly = "OdfKit.Extensions.Html.dll"; Tfms = @("net10.0", "netstandard2.0"); Consumer = $true; RequireSnupkg = $false },
    @{ Id = "OdfKit.Extensions.Imaging"; Assembly = "OdfKit.Extensions.Imaging.dll"; Tfms = @("net10.0", "netstandard2.0"); Consumer = $true; RequireSnupkg = $false },
    @{ Id = "OdfKit.Extensions.Ooxml"; Assembly = "OdfKit.Extensions.Ooxml.dll"; Tfms = @("net10.0", "netstandard2.0"); Consumer = $true; RequireSnupkg = $false },
    @{ Id = "OdfKit.Extensions.Pdf"; Assembly = "OdfKit.Extensions.Pdf.dll"; Tfms = @("net10.0", "netstandard2.0"); Consumer = $true; RequireSnupkg = $false },
    @{ Id = "OdfKit.Extensions.Rendering"; Assembly = "OdfKit.Extensions.Rendering.dll"; Tfms = @("net10.0", "netstandard2.0"); Consumer = $true; RequireSnupkg = $false },
    @{ Id = "OdfKit.Extensions.Rdf"; Assembly = "OdfKit.Extensions.Rdf.dll"; Tfms = @("net10.0", "netstandard2.0"); Consumer = $true; RequireSnupkg = $false },
    @{ Id = "OdfKit.Extensions.Collaboration"; Assembly = "OdfKit.Extensions.Collaboration.dll"; Tfms = @("net10.0", "netstandard2.0"); Consumer = $true; RequireSnupkg = $false },
    @{ Id = "OdfKit.Extensions.Scripting"; Assembly = "OdfKit.Extensions.Scripting.dll"; Tfms = @("net10.0", "netstandard2.0"); Consumer = $true; RequireSnupkg = $false },
    @{ Id = "OdfKit.WebFonts.Abstractions"; Assembly = "OdfKit.WebFonts.Abstractions.dll"; Tfms = @("net10.0", "netstandard2.0"); Consumer = $true; RequireSnupkg = $true },
    @{ Id = "OdfKit.WebFonts.Encoding.Legacy"; Assembly = "OdfKit.WebFonts.Encoding.Legacy.dll"; Tfms = @("net10.0", "netstandard2.0"); Consumer = $true; RequireSnupkg = $true },
    @{ Id = "OdfKit.WebFonts.Data.SqlServer"; Assembly = "OdfKit.WebFonts.Data.SqlServer.dll"; Tfms = @("net10.0", "netstandard2.0"); Consumer = $true; RequireSnupkg = $true },
    @{ Id = "OdfKit.WebFonts.OpenType"; Assembly = "OdfKit.WebFonts.OpenType.dll"; Tfms = @("net10.0", "netstandard2.0"); Consumer = $true; RequireSnupkg = $true },
    @{ Id = "OdfKit.WebFonts.Build"; Tool = $true; Tfms = @(); Consumer = $false; RequireSnupkg = $true },
    @{ Id = "OdfKit.WebFonts.Worker"; Assembly = "OdfKit.WebFonts.Worker.dll"; Tfms = @("net10.0"); Consumer = $false; RequireSnupkg = $true },
    @{ Id = "OdfKit.WebFonts.Profiles"; Assembly = "OdfKit.WebFonts.Profiles.dll"; Tfms = @("net10.0", "netstandard2.0"); Consumer = $true; RequireSnupkg = $true },
    @{ Id = "OdfKit.WebFonts.Windows"; Assembly = "OdfKit.WebFonts.Windows.dll"; Tfms = @("net10.0", "netstandard2.0"); Consumer = $true; RequireSnupkg = $true },
    @{ Id = "OdfKit.WebFonts.Hosting.AspNetCore"; Assembly = "OdfKit.WebFonts.Hosting.AspNetCore.dll"; Tfms = @("net10.0"); Consumer = $false; RequireSnupkg = $true },
    @{ Id = "OdfKit.WebFonts.Hosting.SystemWeb"; Assembly = "OdfKit.WebFonts.Hosting.SystemWeb.dll"; Tfms = @("net48"); Consumer = $false; RequireSnupkg = $true },
    @{ Id = "OdfKit.Extensions.Html.WebFonts"; Assembly = "OdfKit.Extensions.Html.WebFonts.dll"; Tfms = @("net10.0", "netstandard2.0"); Consumer = $true; RequireSnupkg = $true }
)

$allowedPrereleaseDependencies = @(
    "CSharpMath"
)

function Get-NuGetPackageFiles {
    return @(Get-ChildItem -LiteralPath $outDir -File |
        Where-Object { $_.Extension -in @(".nupkg", ".snupkg") } |
        Sort-Object Name)
}

function Write-NuGetPackageHashManifest {
    $packageFiles = Get-NuGetPackageFiles
    if ($packageFiles.Count -eq 0) {
        throw "沒有可建立雜湊 manifest 的 NuGet 封裝。"
    }

    $lines = foreach ($packageFile in $packageFiles) {
        $hash = (Get-FileHash -LiteralPath $packageFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $($packageFile.Name)"
    }
    $lines | Set-Content -LiteralPath $hashManifestPath -Encoding utf8
    Write-Host "OK：已建立 NuGet SHA-256 manifest：$hashManifestPath"
}

function Test-NuGetPackageHashManifest {
    if (-not (Test-Path -LiteralPath $hashManifestPath -PathType Leaf)) {
        throw "缺少 NuGet SHA-256 manifest：$hashManifestPath"
    }

    $manifestNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($line in Get-Content -LiteralPath $hashManifestPath) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }
        if ($line -notmatch '^(?<hash>[0-9a-fA-F]{64})  (?<name>[^\\/]+\.(?:nupkg|snupkg))$') {
            throw "無效的 NuGet SHA-256 manifest 行：$line"
        }

        $fileName = $Matches.name
        $expectedHash = $Matches.hash
        if (-not $manifestNames.Add($fileName)) {
            throw "NuGet SHA-256 manifest 含重複檔名：$fileName"
        }

        $packagePath = Join-Path $outDir $fileName
        if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
            throw "NuGet SHA-256 manifest 指向不存在的檔案：$fileName"
        }

        $actualHash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash
        if (-not [string]::Equals($actualHash, $expectedHash, [StringComparison]::OrdinalIgnoreCase)) {
            throw "NuGet 封裝 SHA-256 不符：$fileName"
        }
    }

    $packageNames = @(Get-NuGetPackageFiles | ForEach-Object { $_.Name })
    if ($manifestNames.Count -ne $packageNames.Count) {
        throw "NuGet SHA-256 manifest 與封裝檔案數量不一致。"
    }
    foreach ($packageName in $packageNames) {
        if (-not $manifestNames.Contains($packageName)) {
            throw "NuGet SHA-256 manifest 未涵蓋封裝：$packageName"
        }
    }

    Write-Host "OK：NuGet SHA-256 manifest 驗證通過。"
}

Push-Location $repoRoot
try {
    if (-not $SkipPack) {
        & (Join-Path $PSScriptRoot "Pack-NuGet.ps1") -Configuration $Configuration -OutputDirectory $OutputDirectory
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    if ($VerifyHashManifest) {
        Test-NuGetPackageHashManifest
    }

    foreach ($pkg in $expectedPackages) {
        $nupkgPath = Join-Path $outDir "$($pkg.Id).$packageVersion.nupkg"
        if (-not (Test-Path -LiteralPath $nupkgPath)) {
            throw "缺少套件：$nupkgPath"
        }

        $zip = [System.IO.Compression.ZipFile]::OpenRead($nupkgPath)
        try {
            foreach ($tfm in $pkg.Tfms) {
                $entryPath = "lib/$tfm/$($pkg.Assembly)"
                $entry = $zip.Entries | Where-Object { $_.FullName -eq $entryPath }
                if (-not $entry) {
                    throw "套件 $($pkg.Id) 缺少 $entryPath"
                }
            }

            if ($pkg.Tool) {
                $toolSettings = $zip.Entries | Where-Object { $_.FullName -eq "tools/net10.0/any/DotnetToolSettings.xml" }
                $toolAssembly = $zip.Entries | Where-Object { $_.FullName -like "tools/net10.0/any/*.dll" } | Select-Object -First 1
                if (-not $toolSettings -or -not $toolAssembly) {
                    throw "工具套件 $($pkg.Id) 缺少 net10.0 tool payload"
                }
            }

            $nuspec = $zip.Entries | Where-Object { $_.FullName -like "*.nuspec" } | Select-Object -First 1
            if (-not $nuspec) {
                throw "套件 $($pkg.Id) 缺少 nuspec"
            }

            $stream = $nuspec.Open()
            try {
                $reader = [System.IO.StreamReader]::new($stream)
                try {
                    [xml]$nuspecXml = $reader.ReadToEnd()
                }
                finally {
                    $reader.Dispose()
                }
            }
            finally {
                $stream.Dispose()
            }

            $dependencies = $nuspecXml.package.metadata.dependencies.group.dependency + $nuspecXml.package.metadata.dependencies.dependency
            $dependencyIds = @($dependencies | Where-Object { $null -ne $_ } | ForEach-Object { [string]$_.id })
            if ($pkg.Id -like '*WebFonts*') {
                $readme = $zip.Entries | Where-Object { $_.FullName -eq 'README.md' }
                if (-not $readme -or [string]$nuspecXml.package.metadata.readme -ne 'README.md') {
                    throw "套件 $($pkg.Id) 缺少 NuGet README.md 或 readme metadata"
                }

                if ($pkg.Id -ne 'OdfKit.WebFonts.Build') {
                    $forbiddenPayload = $zip.Entries | Where-Object {
                        $_.FullName -match '^(?:runtimes/|tools/)' `
                            -or $_.FullName -match '\.(?:exe|dll\.config|py|pyc|pyd|node|so|dylib)$'
                    } | Select-Object -First 1
                    if ($forbiddenPayload) {
                        throw "受控 WebFont 套件含外部工具或原生 payload：$($forbiddenPayload.FullName)"
                    }
                }
            }
            foreach ($dependency in $dependencies) {
                if ($null -eq $dependency) {
                    continue
                }

                $dependencyId = [string]$dependency.id
                $dependencyVersion = [string]$dependency.version
                if ($dependencyVersion -match '-' -and $allowedPrereleaseDependencies -notcontains $dependencyId) {
                    throw "套件 $($pkg.Id) 含未允許的 prerelease 相依：$dependencyId $dependencyVersion"
                }
            }

            if ($pkg.Id -eq "OdfKit.Extensions.Imaging") {
                foreach ($nativeDependency in @(
                    "SkiaSharp.NativeAssets.Linux",
                    "SkiaSharp.NativeAssets.Win32",
                    "SkiaSharp.NativeAssets.macOS")) {
                    if ($dependencyIds -notcontains $nativeDependency) {
                        throw "套件 $($pkg.Id) 缺少跨平台原生相依：$nativeDependency"
                    }
                }
            }
        }
        finally {
            $zip.Dispose()
        }

        if ($pkg.RequireSnupkg) {
            $snupkg = Join-Path $outDir "$($pkg.Id).$packageVersion.snupkg"
            if (-not (Test-Path -LiteralPath $snupkg)) {
                throw "缺少符號套件：$snupkg"
            }
        }

        Write-Host "OK：$($pkg.Id) 套件結構"
    }

    if ($GenerateHashManifest) {
        Write-NuGetPackageHashManifest
    }

    if ($SkipConsumerSmoke) {
        Write-Host "REL-1 NuGet 封裝契約驗收通過；已略過消費端煙霧測試。"
        return
    }

    $artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts"))
    $smokeDir = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot $ConsumerSmokeDirectory))
    $artifactsPrefix = $artifactsRoot.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $smokeDir.StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "拒絕清理 artifacts 之外的消費端煙霧目錄：$smokeDir"
    }
    if (Test-Path -LiteralPath $smokeDir) {
        Remove-Item -LiteralPath $smokeDir -Recurse -Force
    }

    dotnet new console -n NuGetConsumerSmoke -o $smokeDir -f net8.0 --force
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $env:NUGET_PACKAGES = Join-Path $smokeDir ".packages"
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="odfkit-local" value="$outDir" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
"@ | Set-Content -LiteralPath (Join-Path $smokeDir "NuGet.Config") -Encoding utf8

    foreach ($pkg in $expectedPackages | Where-Object { $_.Consumer }) {
        dotnet add $smokeDir package $pkg.Id --version $packageVersion --no-restore
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    dotnet restore $smokeDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    @"
using OdfKit.Text;
using OdfKit.Collaboration;
using OdfKit.Conversion;
using OdfKit.Export;
using OdfKit.Extensions.Imaging;
using OdfKit.Extensions.Rdf;
using OdfKit.Extensions.Rendering;
using OdfKit.Extensions.Scripting;
using OdfKit.WebFonts;
using OdfKit.WebFonts.Encoding.Legacy;
using OdfKit.WebFonts.Profiles;
using OdfKit.WebFonts.Windows;
using System.Runtime.InteropServices;

using var doc = TextDocument.Create();
doc.AddParagraph("NuGet smoke");
_ = doc.Scripting().Capabilities;
_ = new OdfHtmlExportOptions();
_ = typeof(OdfToXlsxConverter);
_ = new OdfPdfRenderer();
_ = typeof(LocalProcessBackend);
_ = OdfRdfGraphUris.ResolveSubjectUri("content.xml");
_ = new OdtOperationCompatibilityOptions();
WebFontTextSequence webFontSequence = WebFontTextSequence.Create("邉\U000E0110\U000F0000");
if (webFontSequence.UnicodeScalars.Count != 3)
{
    throw new InvalidOperationException("WebFont sequence smoke failed.");
}
_ = new Big5CharacterMappingProvider();
_ = typeof(JsonCharacterMappingProvider);
_ = typeof(WindowsEudcFontSourceResolver);

var measured = OdfTextMeasurer.MeasureWidth("OdfKit", "Arial", 12);
if (measured.ToCentimeters() <= 0)
{
    throw new InvalidOperationException("Imaging native runtime smoke failed.");
}

Console.WriteLine($"runtime={RuntimeInformation.RuntimeIdentifier}; os-arch={RuntimeInformation.OSArchitecture}; process-arch={RuntimeInformation.ProcessArchitecture}");
Console.WriteLine("ok");
"@ | Set-Content -LiteralPath (Join-Path $smokeDir "Program.cs") -Encoding utf8

    dotnet build $smokeDir -c $Configuration
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet (Join-Path $smokeDir "bin/$Configuration/net8.0/NuGetConsumerSmoke.dll")
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $net10SmokeDir = Join-Path $smokeDir "net10-webfonts"
    dotnet new console -n WebFontNet10ConsumerSmoke -o $net10SmokeDir -f net10.0 --force
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    foreach ($packageId in @(
        "OdfKit.WebFonts.OpenType",
        "OdfKit.WebFonts.Worker",
        "OdfKit.WebFonts.Hosting.AspNetCore")) {
        dotnet add $net10SmokeDir package $packageId --version $packageVersion --no-restore
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    dotnet restore $net10SmokeDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    @"
using OdfKit.WebFonts.Hosting.AspNetCore;
using OdfKit.WebFonts.OpenType;
using OdfKit.WebFonts.Worker;
using OdfKit.WebFonts;
using Microsoft.Extensions.DependencyInjection;

var workerOptions = new WebFontWorkerOptions
{
    CacheLockRetryDelay = TimeSpan.FromMilliseconds(25),
    MaxCacheLockRetryDelay = TimeSpan.FromMilliseconds(250)
};
if (workerOptions.MaxCacheLockRetryDelay <= workerOptions.CacheLockRetryDelay)
{
    throw new InvalidOperationException("WebFont Worker package API smoke failed.");
}

_ = new ManagedOpenTypeWebFontEngineOptions();
_ = new OdfWebFontOptions();
_ = new OdfWebFontGenerationRequest();
var generationOptions = new OdfWebFontGenerationOptions
{
    AuthorizationPolicyName = "webfont-generation",
    RateLimiterPolicyName = "webfont-generation"
};
generationOptions.AllowedFaces.Add(new WebFontFaceIdentity
{
    FontSourceId = "consumer-smoke",
    SourceSha256 = new string('a', 64)
});
generationOptions.AllowedProfileIds.Add("consumer-v1");
IServiceCollection services = new ServiceCollection();
services.AddOdfWebFontGeneration(
    _ => throw new NotSupportedException(),
    options =>
    {
        options.AuthorizationPolicyName = generationOptions.AuthorizationPolicyName;
        options.RateLimiterPolicyName = generationOptions.RateLimiterPolicyName;
        options.AllowedFaces.Add(generationOptions.AllowedFaces.Single());
        options.AllowedProfileIds.Add(generationOptions.AllowedProfileIds.Single());
    },
    _ => { });
Console.WriteLine("WebFont net10 package consumer smoke passed.");
"@ | Set-Content -LiteralPath (Join-Path $net10SmokeDir "Program.cs") -Encoding utf8

    dotnet build $net10SmokeDir -c $Configuration
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet (Join-Path $net10SmokeDir "bin/$Configuration/net10.0/WebFontNet10ConsumerSmoke.dll")
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    if ($IsWindows -and -not $SkipNetFrameworkSmoke) {
        & (Join-Path $PSScriptRoot "Test-NetFramework48Smoke.ps1") `
            -Configuration $Configuration `
            -PackageDirectory $outDir `
            -PackageVersion $packageVersion
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    else {
        Write-Host "非 Windows x64 net48 runner，略過 net48 CLR consumer smoke。"
    }

    Write-Host ""
    Write-Host "REL-1 NuGet 封裝驗收通過。"
}
finally {
    $env:NUGET_PACKAGES = $previousNugetPackages
    Pop-Location
}

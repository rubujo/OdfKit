#Requires -Version 7.0
<#
.SYNOPSIS
    驗證 OdfKit NuGet 封裝結構與 net8.0 消費端煙霧建置（REL-1）。
.PARAMETER Configuration
    建置組態，預設 Release。
.PARAMETER SkipPack
    略過封裝，使用既有 artifacts/nuget。
.PARAMETER SkipConsumerSmoke
    略過消費端煙霧測試，只驗證封裝契約。
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
    [switch]$SkipPack,
    [switch]$SkipConsumerSmoke,
    [switch]$SkipNetFrameworkSmoke,
    [switch]$GenerateHashManifest,
    [switch]$VerifyHashManifest
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoRoot = Split-Path -Parent $PSScriptRoot
$packageVersion = & (Join-Path $PSScriptRoot "Get-PackageVersion.ps1")
$outDir = Join-Path $repoRoot "artifacts/nuget"
$hashManifestPath = Join-Path $outDir "SHA256SUMS"
$expectedTfms = @("net10.0", "netstandard2.0")
$previousNugetPackages = $env:NUGET_PACKAGES

$expectedPackages = @(
    @{ Id = "OdfKit"; Assembly = "OdfKit.dll"; RequireSnupkg = $true },
    @{ Id = "OdfKit.Extensions.Html"; Assembly = "OdfKit.Extensions.Html.dll"; RequireSnupkg = $false },
    @{ Id = "OdfKit.Extensions.Imaging"; Assembly = "OdfKit.Extensions.Imaging.dll"; RequireSnupkg = $false },
    @{ Id = "OdfKit.Extensions.Ooxml"; Assembly = "OdfKit.Extensions.Ooxml.dll"; RequireSnupkg = $false },
    @{ Id = "OdfKit.Extensions.Pdf"; Assembly = "OdfKit.Extensions.Pdf.dll"; RequireSnupkg = $false },
    @{ Id = "OdfKit.Extensions.Rendering"; Assembly = "OdfKit.Extensions.Rendering.dll"; RequireSnupkg = $false },
    @{ Id = "OdfKit.Extensions.Rdf"; Assembly = "OdfKit.Extensions.Rdf.dll"; RequireSnupkg = $false },
    @{ Id = "OdfKit.Extensions.Collaboration"; Assembly = "OdfKit.Extensions.Collaboration.dll"; RequireSnupkg = $false }
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
        & (Join-Path $PSScriptRoot "Pack-NuGet.ps1") -Configuration $Configuration
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
            foreach ($tfm in $expectedTfms) {
                $entryPath = "lib/$tfm/$($pkg.Assembly)"
                $entry = $zip.Entries | Where-Object { $_.FullName -eq $entryPath }
                if (-not $entry) {
                    throw "套件 $($pkg.Id) 缺少 $entryPath"
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

        Write-Host "OK：$($pkg.Id) 雙 TFM 結構"
    }

    if ($GenerateHashManifest) {
        Write-NuGetPackageHashManifest
    }

    if ($SkipConsumerSmoke) {
        Write-Host "REL-1 NuGet 封裝契約驗收通過；已略過消費端煙霧測試。"
        return
    }

    $artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts"))
    $smokeDir = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot "nuget-consumer-smoke"))
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

    foreach ($pkg in $expectedPackages) {
        dotnet add $smokeDir package $pkg.Id --version $packageVersion
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    @"
using OdfKit.Text;
using OdfKit.Collaboration;
using OdfKit.Conversion;
using OdfKit.Export;
using OdfKit.Extensions.Imaging;
using OdfKit.Extensions.Rdf;
using OdfKit.Extensions.Rendering;
using System.Runtime.InteropServices;

using var doc = TextDocument.Create();
doc.AddParagraph("NuGet smoke");
_ = new OdfHtmlExportOptions();
_ = typeof(OdfToXlsxConverter);
_ = new OdfPdfRenderer();
_ = typeof(LocalProcessBackend);
_ = OdfRdfGraphUris.ResolveSubjectUri("content.xml");
_ = new OdtOperationCompatibilityOptions();

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

    dotnet run --project $smokeDir -c $Configuration --no-build
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

#Requires -Version 7.0
<#
.SYNOPSIS
    驗證 OdfKit NuGet 封裝結構與 net8.0 消費端煙霧建置（REL-1）。
.PARAMETER Configuration
    建置組態，預設 Release。
.PARAMETER SkipPack
    略過封裝，使用既有 artifacts/nuget。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [switch]$SkipPack
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoRoot = Split-Path -Parent $PSScriptRoot
$packageVersion = & (Join-Path $PSScriptRoot "Get-PackageVersion.ps1")
$outDir = Join-Path $repoRoot "artifacts/nuget"
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

Push-Location $repoRoot
try {
    if (-not $SkipPack) {
        & (Join-Path $PSScriptRoot "Pack-NuGet.ps1") -Configuration $Configuration
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
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
                foreach ($nativeDependency in @("SkiaSharp.NativeAssets.Linux", "SkiaSharp.NativeAssets.Win32")) {
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

    $smokeDir = Join-Path $repoRoot "artifacts/nuget-consumer-smoke"
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

Console.WriteLine("ok");
"@ | Set-Content -LiteralPath (Join-Path $smokeDir "Program.cs") -Encoding utf8

    dotnet build $smokeDir -c $Configuration
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet run --project $smokeDir -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host ""
    Write-Host "REL-1 NuGet 封裝驗收通過。"
}
finally {
    $env:NUGET_PACKAGES = $previousNugetPackages
    Pop-Location
}

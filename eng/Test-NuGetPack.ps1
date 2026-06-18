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

$expectedPackages = @(
    @{ Id = "OdfKit"; Assembly = "OdfKit.dll"; RequireSnupkg = $true },
    @{ Id = "OdfKit.Extensions.Html"; Assembly = "OdfKit.Extensions.Html.dll"; RequireSnupkg = $false },
    @{ Id = "OdfKit.Extensions.Imaging"; Assembly = "OdfKit.Extensions.Imaging.dll"; RequireSnupkg = $false },
    @{ Id = "OdfKit.Extensions.Ooxml"; Assembly = "OdfKit.Extensions.Ooxml.dll"; RequireSnupkg = $false },
    @{ Id = "OdfKit.Extensions.Pdf"; Assembly = "OdfKit.Extensions.Pdf.dll"; RequireSnupkg = $false },
    @{ Id = "OdfKit.Extensions.Rendering"; Assembly = "OdfKit.Extensions.Rendering.dll"; RequireSnupkg = $false }
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

    dotnet add $smokeDir package OdfKit --version $packageVersion --source $outDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    @"
using OdfKit.Text;

using var doc = TextDocument.Create();
doc.AddParagraph("NuGet smoke");
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
    Pop-Location
}
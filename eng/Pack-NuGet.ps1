#Requires -Version 7.0
<#
.SYNOPSIS
    建置並封裝所有可發佈的 OdfKit NuGet 套件（REL-1）。
.PARAMETER Configuration
    建置組態，預設 Release。
.PARAMETER OutputDirectory
    輸出目錄，預設 artifacts/nuget。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$OutputDirectory = "artifacts/nuget"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repoRoot $OutputDirectory

$packableProjects = @(
    "OdfKit/OdfKit.csproj",
    "OdfKit.Extensions.Html/OdfKit.Extensions.Html.csproj",
    "OdfKit.Extensions.Imaging/OdfKit.Extensions.Imaging.csproj",
    "OdfKit.Extensions.Ooxml/OdfKit.Extensions.Ooxml.csproj",
    "OdfKit.Extensions.Pdf/OdfKit.Extensions.Pdf.csproj",
    "OdfKit.Extensions.Rendering/OdfKit.Extensions.Rendering.csproj",
    "OdfKit.Extensions.Rdf/OdfKit.Extensions.Rdf.csproj",
    "OdfKit.Extensions.Collaboration/OdfKit.Extensions.Collaboration.csproj",
    "OdfKit.WebFonts.Abstractions/OdfKit.WebFonts.Abstractions.csproj",
    "OdfKit.WebFonts.Encoding.Legacy/OdfKit.WebFonts.Encoding.Legacy.csproj",
    "OdfKit.WebFonts.Data.SqlServer/OdfKit.WebFonts.Data.SqlServer.csproj",
    "OdfKit.WebFonts.OpenType/OdfKit.WebFonts.OpenType.csproj",
    "OdfKit.WebFonts.Build/OdfKit.WebFonts.Build.csproj",
    "OdfKit.WebFonts.Worker/OdfKit.WebFonts.Worker.csproj",
    "OdfKit.WebFonts.Profiles/OdfKit.WebFonts.Profiles.csproj",
    "OdfKit.WebFonts.Hosting.AspNetCore/OdfKit.WebFonts.Hosting.AspNetCore.csproj",
    "OdfKit.WebFonts.Hosting.SystemWeb/OdfKit.WebFonts.Hosting.SystemWeb.csproj",
    "OdfKit.Extensions.Html.WebFonts/OdfKit.Extensions.Html.WebFonts.csproj"
)

if (Test-Path -LiteralPath $outDir) {
    Remove-Item -LiteralPath $outDir -Recurse -Force
}

New-Item -ItemType Directory -Path $outDir -Force | Out-Null

Push-Location $repoRoot
try {
    Write-Host "還原 NuGet 相依…"
    foreach ($relative in $packableProjects) {
        dotnet restore $relative
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    foreach ($relative in $packableProjects) {
        $project = Join-Path $repoRoot $relative
        if (-not (Test-Path -LiteralPath $project)) {
            throw "找不到專案：$relative"
        }

        Write-Host "封裝：$relative"
        dotnet pack $project -c $Configuration -o $outDir --no-restore
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    Write-Host ""
    Write-Host "輸出目錄：$outDir"
    Get-ChildItem -LiteralPath $outDir -Filter *.nupkg | ForEach-Object { Write-Host "  $($_.Name)" }
}
finally {
    Pop-Location
}

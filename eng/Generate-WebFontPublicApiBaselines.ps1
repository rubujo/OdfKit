#Requires -Version 7.0
[CmdletBinding()]
param([switch]$Verify)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projects = @(
    'OdfKit.WebFonts.Abstractions/OdfKit.WebFonts.Abstractions.csproj',
    'OdfKit.WebFonts.Encoding.Legacy/OdfKit.WebFonts.Encoding.Legacy.csproj',
    'OdfKit.WebFonts.Data.SqlServer/OdfKit.WebFonts.Data.SqlServer.csproj',
    'OdfKit.WebFonts.OpenType/OdfKit.WebFonts.OpenType.csproj',
    'OdfKit.WebFonts.Build/OdfKit.WebFonts.Build.csproj',
    'OdfKit.WebFonts.Worker/OdfKit.WebFonts.Worker.csproj',
    'OdfKit.WebFonts.Sidecar/OdfKit.WebFonts.Sidecar.csproj',
    'OdfKit.WebFonts.Profiles/OdfKit.WebFonts.Profiles.csproj',
    'OdfKit.WebFonts.Windows/OdfKit.WebFonts.Windows.csproj',
    'OdfKit.WebFonts.Hosting.AspNetCore/OdfKit.WebFonts.Hosting.AspNetCore.csproj',
    'OdfKit.WebFonts.Hosting.SystemWeb/OdfKit.WebFonts.Hosting.SystemWeb.csproj',
    'OdfKit.Extensions.Html.WebFonts/OdfKit.Extensions.Html.WebFonts.csproj'
)

foreach ($project in $projects) {
    $content = Get-Content -LiteralPath (Join-Path $repoRoot $project) -Raw
    $match = [regex]::Match($content, '<TargetFrameworks?>([^<]+)</TargetFrameworks?>')
    if (-not $match.Success) {
        throw "找不到 TargetFramework：$project"
    }

    $parameters = @{
        Project = $project
        Frameworks = @($match.Groups[1].Value -split ';')
        Verify = $Verify
    }
    & (Join-Path $PSScriptRoot 'Generate-PublicApiBaseline.ps1') @parameters
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

Write-Host 'PASS：WebFont 專案已透過共同 Public API 基線機制完成。'

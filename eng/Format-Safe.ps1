#Requires -Version 7.0
<#
.SYNOPSIS
    安全格式化：避免全方案 dotnet format 污染 OdfKit.Tests（雙 TFM + analyzer 修正）。
.PARAMETER IncludeTests
    一併格式化測試專案（僅 whitespace，不執行 analyzer 程式碼修正）。
.PARAMETER VerifyOnly
    僅驗證格式與衝突標記，不寫入變更。
#>
param(
    [switch]$IncludeTests,
    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

$libraryProjects = @(
    'OdfKit/OdfKit.csproj',
    'OdfKit.Extensions.Html/OdfKit.Extensions.Html.csproj',
    'OdfKit.Extensions.Imaging/OdfKit.Extensions.Imaging.csproj',
    'OdfKit.Extensions.Ooxml/OdfKit.Extensions.Ooxml.csproj',
    'OdfKit.Extensions.Pdf/OdfKit.Extensions.Pdf.csproj',
    'OdfKit.Extensions.Rendering/OdfKit.Extensions.Rendering.csproj',
    'tools/OdfKit.Cli/OdfKit.Cli.csproj',
    'tools/OdfSchemaGenerator/OdfSchemaGenerator.csproj'
)

$verifyArg = if ($VerifyOnly) { '--verify-no-changes' } else { $null }

foreach ($relative in $libraryProjects) {
    $project = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $project)) {
        throw "找不到專案：$relative"
    }

    Write-Host "格式化：$relative"
    if ($verifyArg) {
        dotnet format $project $verifyArg --verbosity quiet
    }
    else {
        dotnet format $project --verbosity quiet
    }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if ($IncludeTests) {
    $testsProject = Join-Path $root 'OdfKit.Tests/OdfKit.Tests.csproj'
    Write-Host '格式化測試專案（僅 whitespace，跳過 analyzer 修正）…'
    if ($verifyArg) {
        dotnet format whitespace $testsProject $verifyArg --verbosity quiet
    }
    else {
        dotnet format whitespace $testsProject --verbosity quiet
    }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

& (Join-Path $PSScriptRoot 'Test-MergeConflictMarkers.ps1') -Root $root
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host 'Format-Safe 完成。'
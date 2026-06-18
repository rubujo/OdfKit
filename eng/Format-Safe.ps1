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

# OdfKit 主函式庫（621 檔）跑完整 dotnet format 會觸發大量 analyzer 修正，本機可達數十分鐘；僅 whitespace。
$whitespaceOnlyProjects = @(
    'OdfKit/OdfKit.csproj'
)

$libraryProjects = @(
    'OdfKit.Extensions.Html/OdfKit.Extensions.Html.csproj',
    'OdfKit.Extensions.Imaging/OdfKit.Extensions.Imaging.csproj',
    'OdfKit.Extensions.Ooxml/OdfKit.Extensions.Ooxml.csproj',
    'OdfKit.Extensions.Pdf/OdfKit.Extensions.Pdf.csproj',
    'OdfKit.Extensions.Rendering/OdfKit.Extensions.Rendering.csproj',
    'OdfKit.Extensions.Rdf/OdfKit.Extensions.Rdf.csproj',
    'tools/OdfKit.Cli/OdfKit.Cli.csproj',
    'tools/OdfSchemaGenerator/OdfSchemaGenerator.csproj'
)

$verifyArg = if ($VerifyOnly) { '--verify-no-changes' } else { $null }

function Invoke-DotNetFormat {
    param(
        [string]$ProjectPath,
        [string]$Relative,
        [switch]$WhitespaceOnly
    )

    $formatCmd = if ($WhitespaceOnly) { 'whitespace' } else { $null }
    Write-Host "格式化：$Relative$(if ($WhitespaceOnly) { '（僅 whitespace）' })"

    $args = @('format')
    if ($formatCmd) { $args += $formatCmd }
    $args += $ProjectPath, '--no-restore', '--verbosity', 'quiet'
    if ($verifyArg) { $args += $verifyArg }

    dotnet @args
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

foreach ($relative in $whitespaceOnlyProjects) {
    $project = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $project)) {
        throw "找不到專案：$relative"
    }

    Invoke-DotNetFormat -ProjectPath $project -Relative $relative -WhitespaceOnly
}

foreach ($relative in $libraryProjects) {
    $project = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $project)) {
        throw "找不到專案：$relative"
    }

    Invoke-DotNetFormat -ProjectPath $project -Relative $relative
}

if ($IncludeTests) {
    $testsProject = Join-Path $root 'OdfKit.Tests/OdfKit.Tests.csproj'
    Write-Host '格式化測試專案（僅 whitespace，跳過 analyzer 修正）…'
    Invoke-DotNetFormat -ProjectPath $testsProject -Relative 'OdfKit.Tests/OdfKit.Tests.csproj' -WhitespaceOnly
}

& (Join-Path $PSScriptRoot 'Test-MergeConflictMarkers.ps1') -Root $root
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host 'Format-Safe 完成。'
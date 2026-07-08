#Requires -Version 7.0
<#
.SYNOPSIS
    執行 Microsoft Office GUI / COM ODF 煙霧驗收。
.DESCRIPTION
    檢查 Windows 與 Word / Excel / PowerPoint COM，並執行 OfficeGuiSmokeTests。
    測試會開啟代表性 ODT / ODS / ODP fixture，驗證 Office 可讀取主要內容。
.PARAMETER Configuration
    建置組態，預設 Release。
.PARAMETER Framework
    測試目標框架，預設 net8.0。
.PARAMETER RequireEnvironment
    若環境不完整則以 exit 1 結束；預設略過（exit 0）。
.PARAMETER NoBuild
    略過建置，直接執行測試。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Framework = "net8.0",
    [switch]$RequireEnvironment,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $repoRoot "OdfKit.Tests/OdfKit.Tests.csproj"
$fixtureRoot = Join-Path $repoRoot "tests/fixtures/corpus/generated/complex"

function Test-ProgIdAvailable {
    param([string]$ProgId)

    if (-not $IsWindows) {
        return $false
    }

    try {
        $type = [Type]::GetTypeFromProgID($ProgId)
        return $null -ne $type
    }
    catch {
        return $false
    }
}

function Get-EnvironmentIssues {
    $issues = New-Object System.Collections.Generic.List[string]

    if (-not $IsWindows) {
        [void]$issues.Add("非 Windows 平台（Office COM 不可用）")
    }

    foreach ($progId in @("Word.Application", "Excel.Application", "PowerPoint.Application")) {
        if (-not (Test-ProgIdAvailable -ProgId $progId)) {
            [void]$issues.Add("找不到 $progId COM")
        }
    }

    foreach ($fileName in @("complex-annual-report.odt", "complex-financial-model.ods", "complex-business-deck.odp")) {
        $path = Join-Path $fixtureRoot $fileName
        if (-not (Test-Path -LiteralPath $path)) {
            [void]$issues.Add("找不到 fixture：$path")
        }
    }

    return $issues
}

Push-Location $repoRoot
try {
    $issues = Get-EnvironmentIssues
    if ($issues.Count -gt 0) {
        $message = "Office GUI 煙霧驗收環境不完整，測試將略過：`n- " + ($issues -join "`n- ")
        if ($RequireEnvironment) {
            throw $message
        }

        Write-Host $message
        exit 0
    }

    Write-Host "Office GUI 煙霧驗收環境就緒；執行 OfficeGuiSmokeTests。"

    if (-not $NoBuild) {
        dotnet build $testProject -c $Configuration --framework $Framework
    }

    $testArgs = @(
        "test",
        $testProject,
        "-c", $Configuration,
        "--framework", $Framework,
        "--filter", "FullyQualifiedName~OfficeGuiSmokeTests",
        "--no-restore"
    )

    if ($NoBuild) {
        $testArgs += "--no-build"
    }

    dotnet @testArgs
}
finally {
    Pop-Location
}

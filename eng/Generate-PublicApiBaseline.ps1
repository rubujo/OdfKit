#Requires -Version 7.0
<#
.SYNOPSIS
    產生或更新 OdfKit 公開 API 基線（PublicAPI.Unshipped.txt）。
.DESCRIPTION
    依 Microsoft.CodeAnalysis.PublicApiAnalyzers（.NET 執行階段／Azure SDK 等同款）慣例，
    以 RS0016 code fix 寫入各 TFM 的 PublicAPI.Unshipped.txt。

    0.x 期間 API 留在 Unshipped；發佈 1.0 時再整批移入 PublicAPI.Shipped.txt。
    雙 TFM（net10.0／netstandard2.0）會分別暫時鎖定 TargetFrameworks 後產生，
    避免 dotnet format 只寫入第一個 TFM。

.PARAMETER Configuration
    建置組態，預設 Release。
.PARAMETER Frameworks
    要更新的 TFM 清單，預設 net10.0 與 netstandard2.0。
.PARAMETER Verify
    產生後以 CI analyzer 模式重建，確認 RS0016／RS0017 無錯誤。
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Project = 'OdfKit/OdfKit.csproj',
    [string[]]$Frameworks = @('net10.0', 'netstandard2.0'),
    [switch]$Verify
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot $Project
$publicApiRoot = Join-Path (Split-Path -Parent $project) 'PublicAPI'
$previousBaselineMode = $env:ODFKIT_PUBLICAPI_BASELINE
$previousCi = $env:CI

if (-not (Test-Path $project)) {
    throw "找不到專案：$project"
}

$env:ODFKIT_PUBLICAPI_BASELINE = '1'
$env:CI = 'true'

$utf8Bom = New-Object System.Text.UTF8Encoding $true
$csprojOriginal = [System.IO.File]::ReadAllText($project)

try {
    foreach ($tfm in $Frameworks) {
        $tfmDir = Join-Path $publicApiRoot $tfm
        if (-not (Test-Path $tfmDir)) {
            New-Item -ItemType Directory -Path $tfmDir | Out-Null
        }

        $shipped = Join-Path $tfmDir 'PublicAPI.Shipped.txt'
        $unshipped = Join-Path $tfmDir 'PublicAPI.Unshipped.txt'
        if (-not (Test-Path $shipped)) {
            [System.IO.File]::WriteAllText($shipped, "#nullable enable`r`n", $utf8Bom)
        }
        # 清空 Unshipped 後由 code fix 完整回填，避免殘留過期簽章。
        [System.IO.File]::WriteAllText($unshipped, "#nullable enable`r`n", $utf8Bom)

        Write-Host "產生 PublicAPI 基線：$tfm …"
        $frameworkMatch = [regex]::Match(
            $csprojOriginal,
            '<TargetFrameworks?>([^<]+)</TargetFrameworks?>')
        if (-not $frameworkMatch.Success) {
            throw "找不到 TargetFramework：$Project"
        }

        $withoutFramework = $csprojOriginal.Remove($frameworkMatch.Index, $frameworkMatch.Length)
        $patched = $withoutFramework.Insert(
            $frameworkMatch.Index,
            "<TargetFramework>$tfm</TargetFramework>")
        [System.IO.File]::WriteAllText($project, $patched, $utf8Bom)

        & dotnet restore $project `
            -p:NuGetAudit=false `
            -p:WarningsNotAsErrors=NU1510 `
            --nologo
        if ($LASTEXITCODE -ne 0) {
            throw "還原失敗：$Project / $tfm"
        }

        & dotnet format analyzers $project `
            --diagnostics RS0016 `
            --severity warn `
            --include-generated `
            --verbosity minimal `
            --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet format analyzers 失敗（$tfm，exit $LASTEXITCODE）。"
        }

        $lineCount = @(Get-Content -LiteralPath $unshipped).Count
        Write-Host "  $tfm Unshipped 行數：$lineCount"
        if ($lineCount -lt 2) {
            throw "$tfm PublicAPI.Unshipped.txt 行數異常偏低（$lineCount），請檢查 analyzer 是否生效。"
        }

        # 還原雙 TFM，再處理下一個
        [System.IO.File]::WriteAllText($project, $csprojOriginal, $utf8Bom)
    }
}
finally {
    [System.IO.File]::WriteAllText($project, $csprojOriginal, $utf8Bom)
    $env:ODFKIT_PUBLICAPI_BASELINE = $previousBaselineMode
    $env:CI = $previousCi
}

if ($Verify) {
    try {
        Write-Host '驗證建置（無 BASELINE 模式，CI analyzer 開啟）…'
        $env:CI = 'true'
        & dotnet restore $project `
            -p:NuGetAudit=false `
            -p:WarningsNotAsErrors=NU1510 `
            --nologo
        if ($LASTEXITCODE -ne 0) {
            throw "驗證前還原失敗：$Project"
        }

        & dotnet build $project `
            -c $Configuration `
            --nologo `
            --no-restore `
            /p:RunAnalyzersDuringBuild=true
        if ($LASTEXITCODE -ne 0) {
            throw "驗證建置失敗。若僅剩 RS0026／RS0027，請確認 .editorconfig 已將其設為 suggestion。"
        }
        Write-Host '驗證建置通過。'
    }
    finally {
        $env:CI = $previousCi
    }
}

Write-Host "完成。請檢視 $Project 的 PublicAPI 基線後提交。"
Write-Host '提示：0.x 維持 Unshipped；1.0 發佈時再移入 Shipped。'

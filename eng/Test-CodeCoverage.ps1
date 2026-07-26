#Requires -Version 7.0
<#
.SYNOPSIS
驗證 Cobertura 報表的全專案 line 與 branch coverage 最低門檻。
.PARAMETER SearchRoot
含 coverage.cobertura.xml 的測試結果根目錄。
.PARAMETER MinimumLineRate
最低行覆蓋率，0 到 1；預設 0.88。
.PARAMETER MinimumBranchRate
最低分支覆蓋率，0 到 1；預設 0.57。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SearchRoot,
    [ValidateRange(0, 1)]
    [double]$MinimumLineRate = 0.88,
    [ValidateRange(0, 1)]
    [double]$MinimumBranchRate = 0.57
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$resolvedRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $SearchRoot))
if (-not (Test-Path -LiteralPath $resolvedRoot -PathType Container)) {
    throw "Coverage 搜尋目錄不存在：$resolvedRoot"
}

$reports = @(Get-ChildItem -LiteralPath $resolvedRoot -Recurse -File -Filter "coverage.cobertura.xml")
if ($reports.Count -eq 0) {
    throw "找不到 Cobertura coverage 報表：$resolvedRoot"
}

$measurements = @{}
foreach ($report in $reports) {
    [xml]$coverage = Get-Content -LiteralPath $report.FullName -Raw
    $lineRate = [double]::Parse(
        [string]$coverage.coverage."line-rate",
        [Globalization.CultureInfo]::InvariantCulture)
    $branchRate = [double]::Parse(
        [string]$coverage.coverage."branch-rate",
        [Globalization.CultureInfo]::InvariantCulture)
    $key = "$lineRate|$branchRate|$($coverage.coverage.'lines-valid')|$($coverage.coverage.'branches-valid')"
    $measurements[$key] = [pscustomobject]@{
        LineRate = $lineRate
        BranchRate = $branchRate
    }
}

if ($measurements.Count -ne 1) {
    throw "同一 TFM 的 Cobertura 報表含不一致量測結果，無法建立單一 coverage gate。"
}

$measurement = @($measurements.Values)[0]
if ($measurement.LineRate -lt $MinimumLineRate) {
    throw ("行覆蓋率 {0:P2} 低於最低門檻 {1:P2}。" -f $measurement.LineRate, $MinimumLineRate)
}
if ($measurement.BranchRate -lt $MinimumBranchRate) {
    throw ("分支覆蓋率 {0:P2} 低於最低門檻 {1:P2}。" -f $measurement.BranchRate, $MinimumBranchRate)
}

Write-Host ("OK：coverage gate 通過，line {0:P2}（最低 {1:P2}）、branch {2:P2}（最低 {3:P2}）。" -f `
        $measurement.LineRate, $MinimumLineRate, $measurement.BranchRate, $MinimumBranchRate)

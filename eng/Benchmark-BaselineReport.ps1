#Requires -Version 7.0
<#
.SYNOPSIS
    Runs the stable BenchmarkDotNet profile and writes a Markdown baseline report.
.DESCRIPTION
    The report captures the command, profile settings, regression-gate baseline
    metadata, and BenchmarkDotNet GitHub summaries. It is intended for PR notes,
    release checks, and local performance investigations.
.PARAMETER Configuration
    Build configuration. Defaults to Release.
.PARAMETER Filter
    BenchmarkDotNet filter passed to Benchmark-Stable.ps1.
.PARAMETER OutputPath
    Markdown report path. Defaults to artifacts/performance/baseline-report.md.
.PARAMETER IterationTime
    Desired iteration time in milliseconds.
.PARAMETER MinIterationCount
    Minimum measured iterations.
.PARAMETER MaxIterationCount
    Maximum measured iterations.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Filter = "*",
    [string]$OutputPath = "artifacts/performance/baseline-report.md",
    [int]$IterationTime = 250,
    [int]$MinIterationCount = 9,
    [int]$MaxIterationCount = 15
)

$ErrorActionPreference = "Stop"

if ($MaxIterationCount -le $MinIterationCount) {
    throw "MaxIterationCount must be greater than MinIterationCount."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$stableScript = Join-Path $PSScriptRoot "Benchmark-Stable.ps1"
$baselinePath = Join-Path $PSScriptRoot "baselines/performance-baselines.json"
$artifactRoot = Join-Path $repoRoot "artifacts/performance/benchmarkdotnet"
$artifactBase = Join-Path $repoRoot "artifacts/performance"
$outputFullPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath
}
else {
    Join-Path $repoRoot $OutputPath
}

$resolvedArtifactRoot = [System.IO.Path]::GetFullPath($artifactRoot)
$resolvedArtifactBase = [System.IO.Path]::GetFullPath($artifactBase)
if (-not $resolvedArtifactRoot.StartsWith($resolvedArtifactBase, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean benchmark artifacts outside artifacts/performance."
}

if (Test-Path $artifactRoot) {
    Remove-Item -LiteralPath $artifactRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $outputFullPath) -Force | Out-Null

& $stableScript `
    -Configuration $Configuration `
    -Filter $Filter `
    -IterationTime $IterationTime `
    -MinIterationCount $MinIterationCount `
    -MaxIterationCount $MaxIterationCount `
    -Artifacts $artifactRoot

$reportFiles = Get-ChildItem -Path (Join-Path $artifactRoot "results") -Filter "*-report-github.md" -ErrorAction SilentlyContinue |
    Sort-Object Name

if ($reportFiles.Count -eq 0) {
    throw "No BenchmarkDotNet GitHub report files were produced."
}

$baseline = Get-Content -Path $baselinePath -Raw | ConvertFrom-Json
$generatedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz"

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("# OdfKit Performance Baseline Report")
$lines.Add("")
$lines.Add("Generated: $generatedAt")
$lines.Add("")
$lines.Add("## Command")
$lines.Add("")
$lines.Add('```powershell')
$lines.Add(('pwsh eng/Benchmark-Stable.ps1 -Configuration {0} -Filter "{1}" -IterationTime {2} -MinIterationCount {3} -MaxIterationCount {4}' -f $Configuration, $Filter, $IterationTime, $MinIterationCount, $MaxIterationCount))
$lines.Add('```')
$lines.Add("")
$lines.Add("## Profile")
$lines.Add("")
$lines.Add("| Setting | Value |")
$lines.Add("|---------|-------|")
$lines.Add(('| Configuration | `{0}` |' -f $Configuration))
$lines.Add(('| Filter | `{0}` |' -f $Filter))
$lines.Add(('| Iteration time | `{0} ms` |' -f $IterationTime))
$lines.Add(('| Iterations | `{0}-{1}` |' -f $MinIterationCount, $MaxIterationCount))
$lines.Add(('| Artifacts | `{0}` |' -f $artifactRoot))
$lines.Add("")
$lines.Add("## Regression Gate")
$lines.Add("")
$lines.Add("| Benchmark | Baseline mean | Tolerance |")
$lines.Add("|-----------|---------------|-----------|")

foreach ($property in $baseline.benchmarks.PSObject.Properties) {
    $entry = $property.Value
    $mean = [math]::Round(([double]$entry.meanNanoseconds) / 1000.0, 2)
    $tolerance = [math]::Round(([double]$entry.toleranceRatio) * 100, 0)
    $lines.Add(('| `{0}` | `{1} us` | `+{2}%` |' -f $property.Name, $mean, $tolerance))
}

$lines.Add("")
$lines.Add("## BenchmarkDotNet Summaries")

foreach ($report in $reportFiles) {
    $lines.Add("")
    $lines.Add("### $($report.BaseName -replace '-report-github$', '')")
    $lines.Add("")
    $lines.AddRange([string[]](Get-Content -Path $report.FullName))
}

Set-Content -Path $outputFullPath -Value $lines -Encoding UTF8
Write-Host "已產生效能基準報告：$outputFullPath"

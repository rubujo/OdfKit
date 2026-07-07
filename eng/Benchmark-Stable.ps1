#Requires -Version 7.0
<#
.SYNOPSIS
    Runs BenchmarkDotNet with a longer, time-based profile for stable local measurements.
.DESCRIPTION
    This script is intended for local performance investigations and release checks.
    It uses BenchmarkDotNet iteration-time guidance instead of a tiny fixed
    iteration count, which reduces short-iteration noise for benchmarks such as
    OdsStreamWriterBenchmarks.WriteRows.
.PARAMETER Configuration
    Build configuration. Defaults to Release.
.PARAMETER Filter
    BenchmarkDotNet filter. Defaults to all benchmarks.
.PARAMETER IterationTime
    Desired iteration time in milliseconds. Defaults to 250.
.PARAMETER MinIterationCount
    Minimum measured iterations. Defaults to 9.
.PARAMETER MaxIterationCount
    Maximum measured iterations. Defaults to 15.
.PARAMETER Artifacts
    Optional BenchmarkDotNet artifacts directory.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Filter = "*",
    [int]$IterationTime = 250,
    [int]$MinIterationCount = 9,
    [int]$MaxIterationCount = 15,
    [string]$Artifacts
)

$ErrorActionPreference = "Stop"

if ($IterationTime -lt 1) {
    throw "IterationTime must be at least 1 ms."
}

if ($MinIterationCount -lt 1) {
    throw "MinIterationCount must be at least 1."
}

if ($MaxIterationCount -le $MinIterationCount) {
    throw "MaxIterationCount must be greater than MinIterationCount."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$benchmarkProject = Join-Path $repoRoot "OdfKit.Benchmarks/OdfKit.Benchmarks.csproj"

$arguments = @(
    "run",
    "--project", $benchmarkProject,
    "-c", $Configuration,
    "--",
    "--filter", $Filter,
    "--job", "Medium",
    "--memory",
    "--exceptions",
    "--iterationTime", $IterationTime.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    "--minIterationCount", $MinIterationCount.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    "--maxIterationCount", $MaxIterationCount.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    "--join"
)

if (-not [string]::IsNullOrWhiteSpace($Artifacts)) {
    $arguments += @("--artifacts", $Artifacts)
}

Push-Location $repoRoot
try {
    Write-Host "執行穩定 BenchmarkDotNet profile..."
    Write-Host "  Filter: $Filter"
    Write-Host "  IterationTime: $IterationTime ms"
    Write-Host "  Iterations: $MinIterationCount-$MaxIterationCount"
    dotnet @arguments
}
finally {
    Pop-Location
}

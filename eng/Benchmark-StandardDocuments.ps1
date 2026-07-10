#Requires -Version 7.0
<#
.SYNOPSIS
    Runs the standard ODS, ODT, and ODP performance scenarios.
.DESCRIPTION
    Builds the benchmark executable and runs each large scenario in an isolated child process.
    The JSON output records elapsed time, allocated bytes, peak working set, package size,
    uncompressed XML size, and a deterministic semantic checksum.
.PARAMETER Configuration
    Build configuration. Defaults to Release.
.PARAMETER OutputPath
    JSON output path. Defaults to artifacts/performance/standard-documents.json.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$OutputPath = "artifacts/performance/standard-documents.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "OdfKit.Benchmarks/OdfKit.Benchmarks.csproj"
$outputFullPath = if ([IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }

Push-Location $repoRoot
try {
    dotnet build $project -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Benchmark project build failed with exit code $LASTEXITCODE." }
    $dll = Join-Path $repoRoot "OdfKit.Benchmarks/bin/$Configuration/net10.0/OdfKit.Benchmarks.dll"
    $json = dotnet $dll --manual-standard
    if ($LASTEXITCODE -ne 0) { throw "Standard benchmark runner failed with exit code $LASTEXITCODE." }
    $json | ConvertFrom-Json | Out-Null
    New-Item -ItemType Directory -Path (Split-Path -Parent $outputFullPath) -Force | Out-Null
    Set-Content -LiteralPath $outputFullPath -Value $json -Encoding utf8
    Write-Host "已產生三格式標準效能報告：$outputFullPath"
}
finally {
    Pop-Location
}

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
.PARAMETER ArtifactName
    Stable artifact identity recorded in the report metadata.
.PARAMETER NoRestore
    Builds with the existing restored dependency graph without contacting package sources.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$OutputPath = "artifacts/performance/standard-documents.json",
    [string]$ArtifactName = "standard-document-performance-local",
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "OdfKit.Benchmarks/OdfKit.Benchmarks.csproj"
$outputFullPath = if ([IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }

function Get-ProcessorDescription {
    if (-not [string]::IsNullOrWhiteSpace($env:PROCESSOR_IDENTIFIER)) {
        return $env:PROCESSOR_IDENTIFIER.Trim()
    }
    if (Test-Path -LiteralPath '/proc/cpuinfo') {
        $modelLine = Get-Content -LiteralPath '/proc/cpuinfo' | Where-Object { $_ -match '^model name\s*:' } | Select-Object -First 1
        if ($modelLine -match '^model name\s*:\s*(.+)$') { return $Matches[1].Trim() }
    }
    if ([Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::OSX)) {
        $model = & sysctl -n machdep.cpu.brand_string 2>$null
        if (-not [string]::IsNullOrWhiteSpace($model)) { return $model.Trim() }
    }
    return "Unknown $([Runtime.InteropServices.RuntimeInformation]::OSArchitecture) processor"
}

Push-Location $repoRoot
try {
    $buildArguments = @("build", $project, "-c", $Configuration)
    if ($NoRestore) {
        $buildArguments += "--no-restore"
    }
    dotnet @buildArguments
    if ($LASTEXITCODE -ne 0) { throw "Benchmark project build failed with exit code $LASTEXITCODE." }
    $dll = Join-Path $repoRoot "OdfKit.Benchmarks/bin/$Configuration/net10.0/OdfKit.Benchmarks.dll"
    $json = dotnet $dll --manual-standard
    if ($LASTEXITCODE -ne 0) { throw "Standard benchmark runner failed with exit code $LASTEXITCODE." }
    $measurements = @($json | ConvertFrom-Json)
    $commitSha = (git rev-parse HEAD 2>$null | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($commitSha)) { $commitSha = "unknown" }
    $report = [ordered]@{
        schemaVersion = 2
        metadata = [ordered]@{
            commitSha = $commitSha
            measuredAtUtc = [DateTimeOffset]::UtcNow.ToString("O", [Globalization.CultureInfo]::InvariantCulture)
            workflowRunId = $(if ($env:GITHUB_RUN_ID) { $env:GITHUB_RUN_ID } else { "local" })
            workflowRunAttempt = $(if ($env:GITHUB_RUN_ATTEMPT) { $env:GITHUB_RUN_ATTEMPT } else { "local" })
            runnerOs = $(if ($env:RUNNER_OS) { $env:RUNNER_OS } else { [Environment]::OSVersion.ToString() })
            runnerArchitecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
            runtimeVersion = $measurements[0].RuntimeVersion
            processorDescription = Get-ProcessorDescription
            artifactName = $ArtifactName
        }
        measurements = $measurements
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $outputFullPath) -Force | Out-Null
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outputFullPath -Encoding utf8
    Write-Host "已產生三格式標準效能報告：$outputFullPath"
}
finally {
    Pop-Location
}

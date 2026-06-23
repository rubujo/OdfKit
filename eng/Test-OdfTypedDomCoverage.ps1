[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Framework = "net10.0",
    [string]$OutputPath = "artifacts/typed-dom-coverage/odf-typed-dom-coverage.json"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    dotnet restore
    dotnet build -c $Configuration --no-restore

    $resolvedOutputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
    $outputDirectory = Split-Path -Parent $resolvedOutputPath
    if (-not (Test-Path -LiteralPath $outputDirectory)) {
        New-Item -ItemType Directory -Path $outputDirectory | Out-Null
    }

    $report = dotnet run `
        --project tools/OdfKit.Cli `
        --framework $Framework `
        --configuration $Configuration `
        --no-build `
        -- `
        typed-dom-coverage `
        --format json

    $report | Set-Content -LiteralPath $resolvedOutputPath -Encoding UTF8
    $json = Get-Content -LiteralPath $resolvedOutputPath -Raw | ConvertFrom-Json
    if ($json.summary.schemaElementCount -lt 550) {
        throw "Typed DOM schema element count is below release floor: $($json.summary.schemaElementCount)"
    }

    if ($json.summary.typedElementCount -lt 550) {
        throw "Typed DOM typed element count is below release floor: $($json.summary.typedElementCount)"
    }

    if ($json.summary.schemaAttributeCount -lt 100) {
        throw "Typed DOM schema attribute count is below release floor: $($json.summary.schemaAttributeCount)"
    }

    Write-Host "Wrote typed DOM coverage report: $resolvedOutputPath"
}
finally {
    Pop-Location
}

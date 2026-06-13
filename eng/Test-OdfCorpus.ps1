[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Framework = "net10.0",
    [string]$InternalManifest = "tests/fixtures/corpus/manifest.json",
    [string]$ExternalRoot = $env:ODFKIT_PARITY_CORPUS_ROOT,
    [string]$ExternalManifest = "",
    [string]$BaselineJar = $env:ODFKIT_ODFVALIDATOR_JAR,
    [string]$BaselineExceptions = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    dotnet build -c $Configuration

    $commonArgs = @(
        "run",
        "--project",
        "tools/OdfKit.Cli",
        "--framework",
        $Framework,
        "--configuration",
        $Configuration,
        "--no-build",
        "--",
        "validate-corpus"
    )

    dotnet @commonArgs $InternalManifest --format json

    if (-not [string]::IsNullOrWhiteSpace($ExternalRoot)) {
        $manifestPath = if ([string]::IsNullOrWhiteSpace($ExternalManifest)) {
            Join-Path $ExternalRoot "manifest.json"
        }
        else {
            $ExternalManifest
        }

        if (-not (Test-Path -LiteralPath $manifestPath)) {
            throw "External corpus manifest not found: $manifestPath"
        }

        $metadataArgs = @($manifestPath, "--metadata-only", "--format", "json")
        if (-not [string]::IsNullOrWhiteSpace($BaselineExceptions)) {
            $metadataArgs += @("--baseline-exceptions", $BaselineExceptions)
        }

        dotnet @commonArgs @metadataArgs

        $externalArgs = @($manifestPath, "--root", $ExternalRoot, "--format", "json")
        if (-not [string]::IsNullOrWhiteSpace($BaselineJar)) {
            $externalArgs += @("--baseline", "odf-validator", "--baseline-jar", $BaselineJar)
        }

        if (-not [string]::IsNullOrWhiteSpace($BaselineExceptions)) {
            $externalArgs += @("--baseline-exceptions", $BaselineExceptions)
        }

        dotnet @commonArgs @externalArgs
    }
    else {
        Write-Host "ODFKIT_PARITY_CORPUS_ROOT is not set; skipping external corpus validation."
    }
}
finally {
    Pop-Location
}

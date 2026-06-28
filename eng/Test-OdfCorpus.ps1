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

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList
    )

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "命令失敗（exit code $LASTEXITCODE）：$FilePath $($ArgumentList -join ' ')"
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    Invoke-NativeCommand "dotnet" @("restore")
    Invoke-NativeCommand "dotnet" @("build", "-c", $Configuration, "--no-restore")

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

    Invoke-NativeCommand "dotnet" ($commonArgs + @($InternalManifest, "--format", "json"))

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

        Invoke-NativeCommand "dotnet" ($commonArgs + $metadataArgs)

        $externalArgs = @($manifestPath, "--root", $ExternalRoot, "--format", "json")
        if (-not [string]::IsNullOrWhiteSpace($BaselineJar)) {
            $externalArgs += @("--baseline", "odf-validator", "--baseline-jar", $BaselineJar)
        }

        if (-not [string]::IsNullOrWhiteSpace($BaselineExceptions)) {
            $externalArgs += @("--baseline-exceptions", $BaselineExceptions)
        }

        Invoke-NativeCommand "dotnet" ($commonArgs + $externalArgs)
    }
    else {
        Write-Host "ODFKIT_PARITY_CORPUS_ROOT is not set; skipping external corpus validation."
    }
}
finally {
    Pop-Location
}

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Framework = "net10.0",
    [string]$InternalManifest = "tests/fixtures/corpus/manifest.json",
    [string]$ExternalRoot = $env:ODFKIT_PARITY_CORPUS_ROOT,
    [string]$ExternalManifest = "",
    [string]$BaselineJar = $env:ODFKIT_ODFVALIDATOR_JAR,
    [string]$BaselineExceptions = "",
    [string]$InternalBaselineJar = "",
    [ValidateRange(1, [int]::MaxValue)]
    [int]$InternalBaselineTimeoutMilliseconds = 120000,
    [string[]]$InternalBaselineVersions = @("1.0", "1.1", "1.2", "1.3", "1.4"),
    [string[]]$InternalBaselineExcludedKinds = @(),
    [switch]$InternalBaselinePackageOnly,
    [switch]$SkipBuild,
    [switch]$SkipInternalValidation
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

function New-VersionFilteredManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ManifestPath,

        [Parameter(Mandatory = $true)]
        [string[]]$Versions,

        [string[]]$ExcludedKinds = @(),

        [switch]$PackageOnly
    )

    $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    $fixtures = @($manifest.fixtures | Where-Object { $Versions -contains $_.version })
    if ($PackageOnly) {
        $fixtures = @($fixtures | Where-Object { -not $_.kind.StartsWith("Flat", [StringComparison]::Ordinal) })
    }
    if ($ExcludedKinds.Count -gt 0) {
        $fixtures = @($fixtures | Where-Object {
            -not ($ExcludedKinds -contains [string]$_.kind)
        })
    }
    if ($fixtures.Count -eq 0) {
        throw "Corpus manifest does not contain fixtures for ODF version(s): $($Versions -join ', ')"
    }

    $temporaryPath = Join-Path ([System.IO.Path]::GetTempPath()) `
        ("odfkit-corpus-" + [Guid]::NewGuid().ToString("N") + ".json")
    [pscustomobject]@{ fixtures = $fixtures } |
        ConvertTo-Json -Depth 20 |
        Set-Content -LiteralPath $temporaryPath -Encoding utf8
    return $temporaryPath
}

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    if (-not $SkipBuild) {
        Invoke-NativeCommand "dotnet" @("restore")
        Invoke-NativeCommand "dotnet" @("build", "-c", $Configuration, "--no-restore")
    }

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

    if (-not $SkipInternalValidation) {
        Invoke-NativeCommand "dotnet" ($commonArgs + @($InternalManifest, "--format", "json"))
    }

    if (-not [string]::IsNullOrWhiteSpace($InternalBaselineJar)) {
        if (-not (Test-Path -LiteralPath $InternalBaselineJar -PathType Leaf)) {
            throw "Internal baseline JAR not found: $InternalBaselineJar"
        }

        $filteredManifest = New-VersionFilteredManifest `
            -ManifestPath $InternalManifest `
            -Versions $InternalBaselineVersions `
            -ExcludedKinds $InternalBaselineExcludedKinds `
            -PackageOnly:$InternalBaselinePackageOnly
        try {
            $internalRoot = Split-Path -Parent (Resolve-Path -LiteralPath $InternalManifest)
            $baselineArgs = @(
                $filteredManifest,
                "--root",
                $internalRoot,
                "--format",
                "json",
                "--baseline",
                "odf-validator",
                "--baseline-jar",
                $InternalBaselineJar,
                "--baseline-timeout-ms",
                $InternalBaselineTimeoutMilliseconds.ToString([Globalization.CultureInfo]::InvariantCulture)
            )
            Invoke-NativeCommand "dotnet" ($commonArgs + $baselineArgs)
        }
        finally {
            Remove-Item -LiteralPath $filteredManifest -Force -ErrorAction SilentlyContinue
        }
    }

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

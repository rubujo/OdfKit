[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DestinationRoot,

    [string]$ManifestPath = "eng/external-tools.json"
)

$ErrorActionPreference = "Stop"

function Test-ExpectedHash {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedSha256
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }

    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    return [string]::Equals($actual, $ExpectedSha256, [StringComparison]::OrdinalIgnoreCase)
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$resolvedManifest = if ([System.IO.Path]::IsPathRooted($ManifestPath)) {
    $ManifestPath
}
else {
    Join-Path $repoRoot $ManifestPath
}

$schema = (Get-Content -LiteralPath $resolvedManifest -Raw | ConvertFrom-Json).libreOfficeManifestSchema
$schemaPath = Join-Path $DestinationRoot $schema.fileName

if (Test-Path -LiteralPath $DestinationRoot -PathType Container) {
    if (Test-ExpectedHash -Path $schemaPath -ExpectedSha256 $schema.sha256) {
        Write-Host "Using verified LibreOffice manifest schema $($schema.version) from the immutable tool cache."
        Write-Output $schemaPath
        return
    }

    if (Test-Path -LiteralPath $schemaPath -PathType Leaf) {
        $actual = (Get-FileHash -LiteralPath $schemaPath -Algorithm SHA256).Hash.ToLowerInvariant()
        throw "Cached LibreOffice manifest schema SHA-256 mismatch. Expected $($schema.sha256), received $actual."
    }

    throw "Cached LibreOffice manifest schema installation is incomplete: $schemaPath"
}

New-Item -ItemType Directory -Path $DestinationRoot | Out-Null
$temporaryPath = Join-Path $DestinationRoot ($schema.fileName + "." + [Guid]::NewGuid().ToString("N") + ".tmp")
try {
    Invoke-WebRequest -UseBasicParsing -Uri $schema.uri -OutFile $temporaryPath
    if (-not (Test-ExpectedHash -Path $temporaryPath -ExpectedSha256 $schema.sha256)) {
        $actual = (Get-FileHash -LiteralPath $temporaryPath -Algorithm SHA256).Hash.ToLowerInvariant()
        throw "LibreOffice manifest schema SHA-256 mismatch. Expected $($schema.sha256), received $actual."
    }

    Move-Item -LiteralPath $temporaryPath -Destination $schemaPath
}
finally {
    Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
}

Write-Host "Downloaded and verified LibreOffice manifest schema $($schema.version)."
Write-Output $schemaPath

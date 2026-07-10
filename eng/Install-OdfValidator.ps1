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

$tool = (Get-Content -LiteralPath $resolvedManifest -Raw | ConvertFrom-Json).odfValidator
$jarPath = Join-Path $DestinationRoot $tool.fileName

if (Test-ExpectedHash -Path $jarPath -ExpectedSha256 $tool.sha256) {
    Write-Host "Using verified ODF Validator $($tool.version) from the immutable tool cache."
    Write-Output $jarPath
    return
}

if (Test-Path -LiteralPath $jarPath -PathType Leaf) {
    $actual = (Get-FileHash -LiteralPath $jarPath -Algorithm SHA256).Hash.ToLowerInvariant()
    throw "Cached ODF Validator SHA-256 mismatch. Expected $($tool.sha256), received $actual. Increment cacheRevision only after investigating the cache entry."
}
New-Item -ItemType Directory -Path $DestinationRoot -Force | Out-Null

$temporaryPath = Join-Path $DestinationRoot ($tool.fileName + "." + [Guid]::NewGuid().ToString("N") + ".tmp")
try {
    Invoke-WebRequest -UseBasicParsing -Uri $tool.uri -OutFile $temporaryPath
    if (-not (Test-ExpectedHash -Path $temporaryPath -ExpectedSha256 $tool.sha256)) {
        $actual = (Get-FileHash -LiteralPath $temporaryPath -Algorithm SHA256).Hash.ToLowerInvariant()
        throw "ODF Validator SHA-256 mismatch. Expected $($tool.sha256), received $actual."
    }

    Move-Item -LiteralPath $temporaryPath -Destination $jarPath
}
finally {
    Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
}

Write-Host "Downloaded and verified ODF Validator $($tool.version)."
Write-Output $jarPath

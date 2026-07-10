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

function Assert-VerifiedBinFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [pscustomobject]$ExpectedFiles
    )

    foreach ($property in $ExpectedFiles.PSObject.Properties) {
        $path = Join-Path (Join-Path $Root "bin") $property.Name
        if (-not (Test-ExpectedHash -Path $path -ExpectedSha256 $property.Value)) {
            if (Test-Path -LiteralPath $path -PathType Leaf) {
                $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
                throw "Cached Jing file SHA-256 mismatch for $($property.Name). Expected $($property.Value), received $actual."
            }

            throw "Cached Jing installation is incomplete: $path"
        }
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$resolvedManifest = if ([System.IO.Path]::IsPathRooted($ManifestPath)) {
    $ManifestPath
}
else {
    Join-Path $repoRoot $ManifestPath
}

$tool = (Get-Content -LiteralPath $resolvedManifest -Raw | ConvertFrom-Json).jing
$archivePath = Join-Path $DestinationRoot $tool.archiveFileName
$jingPath = Join-Path (Join-Path $DestinationRoot "bin") "jing.jar"

if (Test-Path -LiteralPath $DestinationRoot -PathType Container) {
    if (-not (Test-ExpectedHash -Path $archivePath -ExpectedSha256 $tool.sha256)) {
        if (Test-Path -LiteralPath $archivePath -PathType Leaf) {
            $actual = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
            throw "Cached Jing archive SHA-256 mismatch. Expected $($tool.sha256), received $actual."
        }

        throw "Cached Jing installation is incomplete: $archivePath"
    }

    Assert-VerifiedBinFiles -Root $DestinationRoot -ExpectedFiles $tool.binFiles
    Write-Host "Using verified Jing $($tool.version) from the immutable tool cache."
    Write-Output $jingPath
    return
}

New-Item -ItemType Directory -Path $DestinationRoot | Out-Null
$temporaryArchive = Join-Path $DestinationRoot ($tool.archiveFileName + "." + [Guid]::NewGuid().ToString("N") + ".tmp")
$temporaryRoot = Join-Path $DestinationRoot ("extract-" + [Guid]::NewGuid().ToString("N"))
try {
    Invoke-WebRequest -UseBasicParsing -Uri $tool.uri -OutFile $temporaryArchive
    if (-not (Test-ExpectedHash -Path $temporaryArchive -ExpectedSha256 $tool.sha256)) {
        $actual = (Get-FileHash -LiteralPath $temporaryArchive -Algorithm SHA256).Hash.ToLowerInvariant()
        throw "Jing archive SHA-256 mismatch. Expected $($tool.sha256), received $actual."
    }

    Expand-Archive -LiteralPath $temporaryArchive -DestinationPath $temporaryRoot
    $extractedBin = Get-ChildItem -LiteralPath $temporaryRoot -Directory |
        ForEach-Object { Join-Path $_.FullName "bin" } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Container } |
        Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($extractedBin)) {
        throw "Jing archive does not contain the expected release bin directory."
    }

    Move-Item -LiteralPath $extractedBin -Destination (Join-Path $DestinationRoot "bin")
    Move-Item -LiteralPath $temporaryArchive -Destination $archivePath
    Assert-VerifiedBinFiles -Root $DestinationRoot -ExpectedFiles $tool.binFiles
}
finally {
    Remove-Item -LiteralPath $temporaryArchive -Force -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $temporaryRoot -PathType Container) {
        $destinationFullPath = [System.IO.Path]::GetFullPath($DestinationRoot).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
        $temporaryFullPath = [System.IO.Path]::GetFullPath($temporaryRoot)
        if ($temporaryFullPath.StartsWith($destinationFullPath, [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
        }
    }
}

Write-Host "Downloaded and verified Jing $($tool.version)."
Write-Output $jingPath

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$DestinationRoot,
    [string]$Manifest = "docs/examples/oasis-odf-tc-corpus/manifest.json"
)
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = if ([IO.Path]::IsPathRooted($Manifest)) { $Manifest } else { Join-Path $repoRoot $Manifest }
$data = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$root = [IO.Path]::GetFullPath($DestinationRoot)
$prefix = $root.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
foreach ($fixture in $data.fixtures) {
    $target = [IO.Path]::GetFullPath((Join-Path $root $fixture.path))
    if (-not $target.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw "Corpus path escapes root." }
    if ($fixture.sha256 -notmatch '^[0-9a-f]{64}$' -or
        $fixture.sourceUri -notmatch '^https://github.com/oasis-tcs/odf-tc/blob/[0-9a-f]{40}/') {
        throw "OASIS fixture must have an immutable source and SHA-256: $($fixture.id)"
    }
    if (Test-Path -LiteralPath $target -PathType Leaf) {
        if ((Get-FileHash -LiteralPath $target).Hash -ne $fixture.sha256) { throw "Cached OASIS fixture SHA-256 mismatch: $target" }
        continue
    }
    New-Item -ItemType Directory -Force (Split-Path -Parent $target) | Out-Null
    $temporary = $target + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    try {
        $uri = $fixture.sourceUri.Replace('https://github.com/', 'https://raw.githubusercontent.com/').Replace('/blob/', '/')
        Invoke-WebRequest -Uri $uri -OutFile $temporary
        if ((Get-FileHash -LiteralPath $temporary).Hash -ne $fixture.sha256) { throw "Downloaded OASIS fixture SHA-256 mismatch: $($fixture.id)" }
        Move-Item -LiteralPath $temporary -Destination $target
    }
    finally { Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue }
}
Write-Host "Verified $($data.fixtures.Count) pinned OASIS odf-tc fixtures."

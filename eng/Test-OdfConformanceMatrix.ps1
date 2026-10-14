#Requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$directory = Join-Path $root 'docs/odf-conformance-matrix'
$metadata = Get-Content (Join-Path $directory 'sources.json') -Raw | ConvertFrom-Json
$audits = Get-Content (Join-Path $root 'docs/odf-conformance-audits.json') -Raw | ConvertFrom-Json
if ($metadata.fullConformanceClaim -ne $false -or $audits.fullConformanceClaim -ne $false) {
    throw 'The matrix must not imply full conformance.'
}

$clauses = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
$counts = @{}
foreach ($version in @('1.0', '1.1', '1.2', '1.3', '1.4')) {
    foreach ($row in (Import-Csv (Join-Path $directory "odf-$version.csv"))) {
        $source = @($metadata.sources | Where-Object { $_.version -eq $version -and $_.part -eq $row.part })
        if ($row.version -cne $version -or $row.id -cne "ODF-$version/$($row.part)/$($row.clause)" -or
            $source.Count -ne 1 -or -not $row.source.StartsWith($source[0].uri + '#', [StringComparison]::Ordinal) -or
            [string]::IsNullOrWhiteSpace($row.title) -or $row.title -match '\.{3,}' -or
            $row.status -notin @('evidence-gap', 'structure-only', 'partial-formula-profile', 'informative') -or
            -not $clauses.TryAdd($row.id, $row)) {
            throw "Invalid or duplicate matrix row: $($row.id)"
        }
        $key = "$version/$($row.part)"
        $counts[$key] = 1 + $counts[$key]
    }
}
if ($clauses.Count -ne $metadata.clauseCount -or $metadata.sources.Count -ne 14 -or $metadata.schemas.Count -ne 13) {
    throw 'Publication or clause count mismatch.'
}
foreach ($source in $metadata.sources) {
    if ($source.sha256 -notmatch '^[0-9a-f]{64}$' -or $source.uri -notlike 'https://docs.oasis-open.org/*' -or
        $counts["$($source.version)/$($source.part)"] -ne $source.clauseCount) {
        throw "Invalid source lock: $($source.file)"
    }
}
foreach ($schema in $metadata.schemas) {
    $path = [IO.Path]::GetFullPath((Join-Path $root $schema.path))
    if (-not $path.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Schema path escapes the repository: $($schema.path)"
    }
    # Decode bytes rather than ReadAllText, which strips a UTF-8 BOM. Only CRLF
    # checkout conversion is permitted; all other byte changes still fail.
    $normalized = [Text.Encoding]::UTF8.GetBytes([Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($path)).Replace("`r`n", "`n"))
    $hasher = [Security.Cryptography.SHA256]::Create()
    try {
        $actualHash = [Convert]::ToHexString($hasher.ComputeHash($normalized)).ToLowerInvariant()
    } finally {
        $hasher.Dispose()
    }
    if ($actualHash -cne $schema.sha256) {
        throw "Schema changed; review and rebuild the matrix: $($schema.path)"
    }
}

$ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($audit in $audits.requirements) {
    if (-not $ids.Add($audit.id) -or $audit.clauses.Count -eq 0 -or
        $audit.status -notin @('fixed', 'partial', 'validator-gap', 'evidence-gap', 'security-exclusion', 'out-of-sdk-scope') -or
        [string]::IsNullOrWhiteSpace($audit.assessment) -or
        ($audit.status -eq 'fixed' -and $audit.tests.Count -eq 0) -or
        ($audit.status -in @('validator-gap', 'evidence-gap') -and [string]::IsNullOrWhiteSpace($audit.nextAction))) {
        throw "Incomplete audit: $($audit.id)"
    }
    foreach ($clause in $audit.clauses) {
        if (-not $clauses.ContainsKey($clause) -or $audit.id -cnotin ($clauses[$clause].audits -split ';')) {
            throw "Missing audit association: $($audit.id) / $clause"
        }
    }
    foreach ($relative in @($audit.implementation) + @($audit.tests | ForEach-Object path)) {
        $path = [IO.Path]::GetFullPath((Join-Path $root $relative))
        if (-not $path.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing audit evidence: $relative"
        }
    }
    foreach ($test in $audit.tests) {
        $source = Get-Content (Join-Path $root $test.path) -Raw
        if ($source -notmatch ('\b' + [regex]::Escape($test.symbol) + '\s*\(')) {
            throw "Missing test symbol: $($test.path) / $($test.symbol)"
        }
    }
}
foreach ($row in $clauses.Values) {
    foreach ($id in ($row.audits -split ';' | Where-Object { $_ })) {
        if (-not $ids.Contains($id)) { throw "Unknown audit: $id" }
    }
}
foreach ($status in $metadata.statusCounts.PSObject.Properties) {
    if (@($clauses.Values | Where-Object status -CEQ $status.Name).Count -ne $status.Value) {
        throw "Status count mismatch: $($status.Name)"
    }
}
Write-Host "Conformance matrix contracts passed: $($clauses.Count) clauses; $($ids.Count) reviewed requirements; no full conformance claim."

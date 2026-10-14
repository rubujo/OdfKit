[CmdletBinding()]
param()
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$tool = (Get-Content (Join-Path $root 'eng/external-tools.json') -Raw | ConvertFrom-Json).opfOdfValidator
if ($tool.sha256 -notmatch '^[0-9a-f]{64}$' -or $tool.minimumJavaVersion -ne '11' -or
    $tool.uri -ne "https://github.com/openpreserve/odf-validator/releases/download/v$($tool.version)/$($tool.fileName)") {
    throw "OPF metadata must pin the reviewed release and hash."
}
$manifest = Get-Content (Join-Path $root 'docs/examples/oasis-odf-tc-corpus/manifest.json') -Raw | ConvertFrom-Json
$inventory = Get-Content (Join-Path $root 'docs/oasis-odf-tc-inventory.json') -Raw | ConvertFrom-Json
if ($manifest.revision -notmatch '^[0-9a-f]{40}$' -or $manifest.revision -cne $inventory.revision -or
    $inventory.documentCount -ne $inventory.documents.Count -or
    $inventory.toolCount -ne $inventory.tools.Count) { throw "OASIS inventory revision/count mismatch." }
$ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($fixture in $manifest.fixtures) {
    if (-not $ids.Add($fixture.id) -or -not $paths.Add($fixture.path)) { throw "Duplicate OASIS fixture id/path." }
    $sourcePath = 'src/test/resources/odf1.3/testfiles/' + [IO.Path]::GetFileName($fixture.path)
    $source = @($inventory.documents | Where-Object { $_.path -ceq $sourcePath })
    $expected = if ($source.Count -eq 1 -and $source[0].decision -eq 'adopted-negative') { 'invalid' } else { 'valid' }
    if ($source.Count -ne 1 -or $source[0].decision -notin @('adopted', 'adopted-negative') -or $source[0].sha256 -cne $fixture.sha256 -or
        $fixture.sha256 -notmatch '^[0-9a-f]{64}$' -or $fixture.version -ne '1.3' -or $fixture.expected -ne $expected -or
        $fixture.license -ne 'OASIS-TC-IPR-policy-download-only' -or
        [Uri]::UnescapeDataString($fixture.sourceUri) -cne "https://github.com/oasis-tcs/odf-tc/blob/$($manifest.revision)/$sourcePath") {
        throw "OASIS fixture differs from the reviewed inventory: $($fixture.id)"
    }
}
$kinds = @($manifest.fixtures.kind | Sort-Object -Unique)
if (@($inventory.documents | Where-Object { $_.decision -in @('adopted', 'adopted-negative') }).Count -ne $ids.Count -or
    @('Text', 'Spreadsheet', 'Presentation', 'Graphics' | Where-Object { $_ -notin $kinds }).Count -gt 0 -or
    @($manifest.fixtures.expected | Sort-Object -Unique).Count -ne 2) { throw "OASIS adopted corpus is incomplete." }
$corpus = Get-Content (Join-Path $root 'tests/fixtures/corpus/manifest.json') -Raw | ConvertFrom-Json
$exceptions = Get-Content (Join-Path $root 'docs/opf-baseline-exceptions.json') -Raw | ConvertFrom-Json
if ($exceptions.validator -ne 'opf-odf-validator' -or $exceptions.validatorVersion -ne $tool.version) { throw "OPF exception version mismatch." }
$exceptionIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($entry in $exceptions.exceptions) {
    $fixture = @($corpus.fixtures | Where-Object { $_.id -ceq $entry.fixtureId })
    if (-not $exceptionIds.Add($entry.fixtureId) -or $fixture.Count -ne 1 -or
        $entry.expected -ne $fixture[0].expected -or $entry.actual -eq $entry.expected -or
        $entry.actual -notin @('valid', 'invalid') -or $entry.errorSignatures.Count -eq 0 -or
        [string]::IsNullOrWhiteSpace($entry.reason)) { throw "Invalid OPF exception: $($entry.fixtureId)" }
}
Write-Host "External source contracts passed: $($ids.Count) OASIS fixtures; $($exceptionIds.Count) scoped OPF exceptions."

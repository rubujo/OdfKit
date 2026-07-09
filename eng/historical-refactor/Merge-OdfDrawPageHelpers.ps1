#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$dir = Join-Path $PSScriptRoot '..\OdfKit\Drawing'
$corePath = Join-Path $dir 'OdfDrawPage.cs'
$helpersPath = Join-Path $dir 'OdfDrawPage.Helpers.cs'

if (-not (Test-Path -LiteralPath $helpersPath)) {
    Write-Host 'OdfDrawPage.Helpers.cs not found; nothing to merge.'
    exit 0
}

$coreLines = [System.Collections.Generic.List[string]]@(Get-Content -LiteralPath $corePath -Encoding UTF8)
$helpersLines = [System.Collections.Generic.List[string]]@(Get-Content -LiteralPath $helpersPath -Encoding UTF8)

$regionStart = -1
for ($i = 0; $i -lt $helpersLines.Count; $i++) {
    if ($helpersLines[$i] -match '#region Drawing Helpers') {
        $regionStart = $i
        break
    }
}
if ($regionStart -lt 0) {
    throw 'Could not find #region Drawing Helpers in OdfDrawPage.Helpers.cs'
}

$regionEnd = -1
for ($i = $regionStart + 1; $i -lt $helpersLines.Count; $i++) {
    if ($helpersLines[$i] -match '#endregion') {
        $regionEnd = $i
        break
    }
}
if ($regionEnd -lt 0) {
    throw 'Could not find #endregion in OdfDrawPage.Helpers.cs'
}

$regionBlock = $helpersLines[$regionStart..$regionEnd]

if ($coreLines[$coreLines.Count - 1] -ne '}') {
    throw 'Expected OdfDrawPage.cs to end with }'
}
$coreLines.RemoveAt($coreLines.Count - 1)

foreach ($line in $regionBlock) { [void]$coreLines.Add($line) }
[void]$coreLines.Add('')
[void]$coreLines.Add('}')

Set-Content -LiteralPath $corePath -Value $coreLines -Encoding UTF8
Remove-Item -LiteralPath $helpersPath -Force

Write-Host "Merged Helpers into OdfDrawPage.cs ($($coreLines.Count) lines)"
Write-Host 'Removed OdfDrawPage.Helpers.cs'
Write-Host 'Done.'
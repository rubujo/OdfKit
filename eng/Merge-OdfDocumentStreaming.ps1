#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$kit = Join-Path $PSScriptRoot '..\OdfKit'

function Get-RegionBlock {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [string]$RegionMarker
    )
    $regionStart = -1
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -match [regex]::Escape($RegionMarker)) {
            $regionStart = $i
            break
        }
    }
    if ($regionStart -lt 0) { throw "Could not find region: $RegionMarker" }

    $regionEnd = -1
    for ($i = $regionStart + 1; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -match '#endregion') {
            $regionEnd = $i
            break
        }
    }
    if ($regionEnd -lt 0) { throw "Could not find #endregion after $RegionMarker" }
    return ,$Lines[$regionStart..$regionEnd]
}

$corePath = Join-Path $kit 'Core\OdfDocument.Lifecycle.cs'
$sourcePath = Join-Path $kit 'Core\OdfDocument.Streaming.cs'

if (-not (Test-Path -LiteralPath $sourcePath)) {
    Write-Host 'OdfDocument.Streaming.cs not found; nothing to merge.'
    exit 0
}

$coreLines = [System.Collections.Generic.List[string]]@(Get-Content -LiteralPath $corePath -Encoding UTF8)
$sourceLines = [System.Collections.Generic.List[string]]@(Get-Content -LiteralPath $sourcePath -Encoding UTF8)
$block = Get-RegionBlock -Lines $sourceLines -RegionMarker '#region Web Streaming APIs'

if ($coreLines[$coreLines.Count - 1] -ne '}') {
    throw 'Expected OdfDocument.Lifecycle.cs to end with }'
}
$coreLines.RemoveAt($coreLines.Count - 1)
[void]$coreLines.Add('')
foreach ($line in $block) { [void]$coreLines.Add($line) }
[void]$coreLines.Add('')
[void]$coreLines.Add('}')

Set-Content -LiteralPath $corePath -Value $coreLines -Encoding UTF8
Remove-Item -LiteralPath $sourcePath -Force
Write-Host "Merged Streaming into Lifecycle ($($coreLines.Count) lines)"
Write-Host 'Done.'
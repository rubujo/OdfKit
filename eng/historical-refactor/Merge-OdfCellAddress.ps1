#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$dir = Join-Path $PSScriptRoot '..\OdfKit\Spreadsheet'
$corePath = Join-Path $dir 'OdfCellAddress.cs'
$parsingPath = Join-Path $dir 'OdfCellAddress.Parsing.cs'
$formattingPath = Join-Path $dir 'OdfCellAddress.Formatting.cs'

function Get-RegionBlock {
    param([System.Collections.Generic.List[string]]$Lines, [string]$RegionMarker)
    $regionStart = -1
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -match [regex]::Escape($RegionMarker)) {
            $regionStart = $i
            break
        }
    }
    if ($regionStart -lt 0) {
        throw "Could not find $RegionMarker"
    }

    $regionEnd = -1
    for ($i = $regionStart + 1; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -match '#endregion') {
            $regionEnd = $i
            break
        }
    }
    if ($regionEnd -lt 0) {
        throw "Could not find #endregion after $RegionMarker"
    }

    return $Lines[$regionStart..$regionEnd]
}

$coreLines = [System.Collections.Generic.List[string]]@(Get-Content -LiteralPath $corePath -Encoding UTF8)
$parsingLines = [System.Collections.Generic.List[string]]@(Get-Content -LiteralPath $parsingPath -Encoding UTF8)
$formattingLines = [System.Collections.Generic.List[string]]@(Get-Content -LiteralPath $formattingPath -Encoding UTF8)

# 改為單檔 struct（移除 partial）
for ($i = 0; $i -lt $coreLines.Count; $i++) {
    if ($coreLines[$i] -match 'readonly partial struct OdfCellAddress') {
        $coreLines[$i] = $coreLines[$i] -replace 'partial struct', 'struct'
        break
    }
}

# 補上 Formatting 所需的 using
if ($coreLines -notcontains 'using System.Text;') {
    $usingIndex = 0
    for ($i = 0; $i -lt $coreLines.Count; $i++) {
        if ($coreLines[$i] -match '^using ') { $usingIndex = $i }
    }
    $coreLines.Insert($usingIndex + 1, 'using System.Text;')
}

$parsingBlock = Get-RegionBlock -Lines $parsingLines -RegionMarker '#region Address Parsing'
$formattingBlock = Get-RegionBlock -Lines $formattingLines -RegionMarker '#region Address Formatting'

if ($coreLines[$coreLines.Count - 1] -ne '}') {
    throw 'Expected OdfCellAddress.cs to end with }'
}
$coreLines.RemoveAt($coreLines.Count - 1)

foreach ($line in $parsingBlock) { [void]$coreLines.Add($line) }
[void]$coreLines.Add('')
foreach ($line in $formattingBlock) { [void]$coreLines.Add($line) }
[void]$coreLines.Add('')
[void]$coreLines.Add('}')

Set-Content -LiteralPath $corePath -Value $coreLines -Encoding UTF8
Remove-Item -LiteralPath $parsingPath -Force
Remove-Item -LiteralPath $formattingPath -Force

Write-Host "Merged OdfCellAddress partials into OdfCellAddress.cs ($($coreLines.Count) lines)"
Write-Host 'Removed OdfCellAddress.Parsing.cs and OdfCellAddress.Formatting.cs'
Write-Host 'Done.'
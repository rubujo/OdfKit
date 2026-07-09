#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$domDir = Join-Path $PSScriptRoot '..\OdfKit\DOM'
$corePath = Join-Path $domDir 'OdfTypedDomCoverage.cs'
$propPath = Join-Path $domDir 'OdfTypedDomCoverage.PropertyTypes.cs'

$coreLines = [System.Collections.Generic.List[string]]@(Get-Content -LiteralPath $corePath -Encoding UTF8)
$propLines = [System.Collections.Generic.List[string]]@(Get-Content -LiteralPath $propPath -Encoding UTF8)

# 移除 partial 關鍵字
for ($i = 0; $i -lt $coreLines.Count; $i++) {
    if ($coreLines[$i] -match 'static partial class OdfTypedDomCoverage') {
        $coreLines[$i] = $coreLines[$i] -replace 'partial class', 'class'
        break
    }
}

# 取出 PropertyTypes 內所有 #region 區塊（略過 namespace / class 包裝）
$regionStart = -1
for ($i = 0; $i -lt $propLines.Count; $i++) {
    if ($propLines[$i] -match '#region Property Type Resolution') {
        $regionStart = $i
        break
    }
}
if ($regionStart -lt 0) { throw 'Could not find Property Type Resolution region' }

$body = $propLines[$regionStart..($propLines.Count - 2)]

if ($coreLines[$coreLines.Count - 1] -ne '}') {
    throw 'Expected OdfTypedDomCoverage.cs to end with }'
}
$coreLines.RemoveAt($coreLines.Count - 1)
[void]$coreLines.Add('')
foreach ($line in $body) { [void]$coreLines.Add($line) }
[void]$coreLines.Add('')
[void]$coreLines.Add('}')

Set-Content -LiteralPath $corePath -Value $coreLines -Encoding UTF8
Remove-Item -LiteralPath $propPath -Force

Write-Host "Merged PropertyTypes into OdfTypedDomCoverage.cs ($($coreLines.Count) lines)"
Write-Host 'Removed OdfTypedDomCoverage.PropertyTypes.cs'
Write-Host 'Done.'
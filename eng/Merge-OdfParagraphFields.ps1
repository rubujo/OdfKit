#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$textDir = Join-Path $PSScriptRoot '..\OdfKit\Text'
$corePath = Join-Path $textDir 'OdfParagraph.cs'
$fieldsPath = Join-Path $textDir 'OdfParagraph.Fields.cs'

if (-not (Test-Path -LiteralPath $fieldsPath)) {
    Write-Host 'OdfParagraph.Fields.cs not found; nothing to merge.'
    exit 0
}

$coreLines = [System.Collections.Generic.List[string]]@(Get-Content -Path $corePath -Encoding UTF8)
$fieldsLines = [System.Collections.Generic.List[string]]@(Get-Content -Path $fieldsPath -Encoding UTF8)

# 取出 Fields partial 內 #region Fields & References 區塊（略過外層 class 包裝）
$regionStart = -1
for ($i = 0; $i -lt $fieldsLines.Count; $i++) {
    if ($fieldsLines[$i] -match '#region Fields') {
        $regionStart = $i
        break
    }
}
if ($regionStart -lt 0) {
    throw 'Could not find #region Fields & References in OdfParagraph.Fields.cs'
}

$regionEnd = -1
for ($i = $regionStart + 1; $i -lt $fieldsLines.Count; $i++) {
    if ($fieldsLines[$i] -match '#endregion') {
        $regionEnd = $i
        break
    }
}
if ($regionEnd -lt 0) {
    throw 'Could not find #endregion in OdfParagraph.Fields.cs'
}

$regionBlock = $fieldsLines[$regionStart..$regionEnd]

# 移除 core 結尾 class 的 '}'
if ($coreLines[$coreLines.Count - 1] -ne '}') {
    throw 'Expected OdfParagraph.cs to end with }'
}
$coreLines.RemoveAt($coreLines.Count - 1)

foreach ($line in $regionBlock) { [void]$coreLines.Add($line) }
[void]$coreLines.Add('')
[void]$coreLines.Add('}')

Set-Content -Path $corePath -Value $coreLines -Encoding UTF8
Remove-Item -Path $fieldsPath -Force
Write-Host "Merged Fields into OdfParagraph.cs ($($coreLines.Count) lines)"
Write-Host 'Removed OdfParagraph.Fields.cs'
Write-Host 'Done.'
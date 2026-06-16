#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$sheetDir = Join-Path $PSScriptRoot '..\OdfKit\Spreadsheet'
$sourcePath = Join-Path $sheetDir 'OdfCellAddress.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$blocks = @(
    @{ Start = 125; End = 312; File = 'OdfCellAddress.Parsing.cs'; Region = 'Address Parsing' }
    @{ Start = 314; End = 446; File = 'OdfCellAddress.Formatting.cs'; Region = 'Address Formatting & Structural Shift' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.Core')
    if ($Text -match 'StringBuilder') { [void]$needed.Add('System.Text') }

    $order = @('System', 'System.Text', 'OdfKit.Core')
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-PartialFile {
    param([string]$Path, [string]$RegionName, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    $text = $BodyLines -join "`n"
    foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
    $out.Add('')
    $out.Add('namespace OdfKit.Spreadsheet;')
    $out.Add('')
    $out.Add('public readonly partial struct OdfCellAddress')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$core = $lines[0..122] | ForEach-Object {
    $_ -replace '^public readonly struct OdfCellAddress', 'public readonly partial struct OdfCellAddress'
}
$core += '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfCellAddress.cs: $($core.Count) lines"

foreach ($block in $blocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    Write-PartialFile -Path (Join-Path $sheetDir $block.File) -RegionName $block.Region -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

Write-Host 'Done.'
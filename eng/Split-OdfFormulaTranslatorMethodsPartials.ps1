#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$formulaDir = Join-Path $PSScriptRoot '..\OdfKit\Formula'
$sourcePath = Join-Path $formulaDir 'OdfFormulaTranslator.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$files = @(
    @{ Start = 13; End = 319; File = 'OdfFormulaTranslator.Tokenize.cs'; Region = 'Tokenization' }
    @{ Start = 321; End = 454; File = 'OdfFormulaTranslator.Conversion.cs'; Region = 'Formula Conversion' }
    @{ Start = 456; End = ($lineCount - 1); File = 'OdfFormulaTranslator.Offset.cs'; Region = 'Formula Offset' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    if ($Text -match 'List<|Dictionary<|HashSet<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'StringBuilder|Encoding\.') { [void]$needed.Add('System.Text') }
    if ($Text -match 'OdfCell|Spreadsheet') { [void]$needed.Add('OdfKit.Spreadsheet') }

    $order = @('System', 'System.Collections.Generic', 'System.Text', 'OdfKit.Spreadsheet')
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
    $out.Add('namespace OdfKit.Formula;')
    $out.Add('')
    $out.Add('public static partial class OdfFormulaTranslator')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

foreach ($block in $files) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    Write-PartialFile -Path (Join-Path $formulaDir $block.File) -RegionName $block.Region -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

Remove-Item -Path $sourcePath -Force
Write-Host 'Removed OdfFormulaTranslator.cs'
Write-Host 'Done.'
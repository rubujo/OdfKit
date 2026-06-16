#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$formulaDir = Join-Path $PSScriptRoot '..\OdfKit\Formula'
$sourcePath = Join-Path $formulaDir 'DefaultFormulaEvaluator.Helpers.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$sectionStarts = @(95, 632, 1026, 1324)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'List<|Dictionary<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'AstNode|IOdfFormulaEvaluator') { [void]$needed.Add('OdfKit.Formula.AST') }
    if ($Text -match 'OdfCell|SpreadsheetDocument') { [void]$needed.Add('OdfKit.Spreadsheet') }

    $order = @('System', 'System.Collections.Generic', 'OdfKit.DOM', 'OdfKit.Formula.AST', 'OdfKit.Spreadsheet')
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-PartialFile {
    param(
        [string]$Path,
        [string]$RegionName,
        [string[]]$BodyLines
    )
    $out = [System.Collections.Generic.List[string]]::new()
    $text = $BodyLines -join "`n"
    foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
    $out.Add('')
    $out.Add('namespace OdfKit.Formula;')
    $out.Add('')
    $out.Add('public partial class DefaultFormulaEvaluator')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    $out.AddRange($BodyLines)
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$coreEnd = $sectionStarts[0] - 2
$coreBody = $lines[13..$coreEnd]
Write-PartialFile -Path $sourcePath -RegionName 'Helpers & Coercion' -BodyLines $coreBody
Write-Host "Core DefaultFormulaEvaluator.Helpers.cs: $($coreBody.Count) body lines"

$files = @(
    @{ Start = 95; File = 'DefaultFormulaEvaluator.Helpers.Statistical.cs'; Region = 'Helpers & Coercion - Statistical' }
    @{ Start = 632; File = 'DefaultFormulaEvaluator.Helpers.Lookup.cs'; Region = 'Helpers & Coercion - Lookup' }
    @{ Start = 1026; File = 'DefaultFormulaEvaluator.Helpers.DateTime.cs'; Region = 'Helpers & Coercion - DateTime' }
    @{ Start = 1324; File = 'DefaultFormulaEvaluator.Helpers.Financial.cs'; Region = 'Helpers & Coercion - Financial' }
)
for ($i = 0; $i -lt $files.Count; $i++) {
    $start = $files[$i].Start - 1
    $end = if ($i + 1 -lt $files.Count) { $files[$i + 1].Start - 2 } else { $lines.Count - 3 }
    $block = $lines[$start..$end]
    Write-PartialFile -Path (Join-Path $formulaDir $files[$i].File) -RegionName $files[$i].Region -BodyLines $block
    Write-Host "  $($files[$i].File): $($block.Count) body lines"
}

Write-Host 'Done.'
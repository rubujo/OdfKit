#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$formulaDir = Join-Path $PSScriptRoot '..\OdfKit\Formula'
$sourcePath = Join-Path $formulaDir 'DefaultFormulaEvaluator.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$typeBlocks = @(
    @{ Start = 10; End = 49; File = 'OdfFormulaErrorType.cs'; IsCore = $false }
    @{ Start = 51; End = 112; File = 'OdfFormulaError.cs'; IsCore = $false }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    if ($Text -match 'List<|Dictionary<|HashSet<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'OdfNode|OdfNamespaces|OdfPackage|OdfKitDiagnostics') { [void]$needed.Add('OdfKit.Core') }
    if ($Text -match 'AstNode|IOdfFormulaEvaluator') { [void]$needed.Add('OdfKit.Formula.AST') }
    if ($Text -match 'OdfCellAddress|OdfTableSheet') { [void]$needed.Add('OdfKit.Spreadsheet') }
    if ($Text -match 'OdfNode') { [void]$needed.Add('OdfKit.DOM') }

    $order = @('System', 'System.Collections.Generic', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Formula.AST', 'OdfKit.Spreadsheet')
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-TypeFile {
    param([string]$Path, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    $text = $BodyLines -join "`n"
    foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
    $out.Add('')
    $out.Add('namespace OdfKit.Formula;')
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

function Write-PartialFile {
    param([string]$Path, [string]$RegionName, [string[]]$BodyLines)
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
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

foreach ($block in $typeBlocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    Write-TypeFile -Path (Join-Path $formulaDir $block.File) -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

$dispatchBody = $lines[287..474]
Write-PartialFile -Path (Join-Path $formulaDir 'DefaultFormulaEvaluator.FunctionDispatch.cs') -RegionName 'Function Dispatch' -BodyLines $dispatchBody
Write-Host "  DefaultFormulaEvaluator.FunctionDispatch.cs: $($dispatchBody.Count) body lines"

$coreHeader = @(
    'using System;',
    'using System.Collections.Generic;',
    'using OdfKit.Core;',
    'using OdfKit.DOM;',
    'using OdfKit.Formula.AST;',
    'using OdfKit.Spreadsheet;',
    '',
    'namespace OdfKit.Formula;',
    '',
    '/// <summary>',
    '/// 預設的 ODF 公式評估器實現。',
    '/// </summary>',
    'public partial class DefaultFormulaEvaluator : IOdfFormulaEvaluator',
    '{'
)
$coreBody = $lines[118..285]
$core = $coreHeader + $coreBody + '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core DefaultFormulaEvaluator.cs: $($core.Count) lines"
Write-Host 'Done.'
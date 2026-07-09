#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$formulaDir = Join-Path $PSScriptRoot '..\OdfKit\Formula'
$sourcePath = Join-Path $formulaDir 'DefaultFormulaEvaluator.InternalTypes.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$typeBlocks = @(
    @{ Start = 10; End = 88; File = 'FormulaCriteriaMatcher.cs' }
    @{ Start = 90; End = $lineCount; File = 'OdfDomEvaluationContext.cs' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'List<|Dictionary<|HashSet<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'OdfNamespaces|OdfKitDiagnostics') { [void]$needed.Add('OdfKit.Core') }
    if ($Text -match 'OdfCellAddress|OdfSpreadsheetLimits|OdfTableSheet') { [void]$needed.Add('OdfKit.Spreadsheet') }
    if ($Text -match 'OdfFormulaError|AstNode|IOdfFormulaEvaluator') { [void]$needed.Add('OdfKit.Formula.AST') }
    if ($Text -match 'OdfNamespaces|OdfNode') { [void]$needed.Add('OdfKit.DOM') }

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

foreach ($block in $typeBlocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    Write-TypeFile -Path (Join-Path $formulaDir $block.File) -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

Remove-Item -Path $sourcePath -Force
Write-Host 'Removed DefaultFormulaEvaluator.InternalTypes.cs'
Write-Host 'Done.'
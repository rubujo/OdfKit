#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$formulaDir = Join-Path $PSScriptRoot '..\OdfKit\Formula'
$sourcePath = Join-Path $formulaDir 'OdfFormulaSupport.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$typeBlocks = @(
    @{ Start = 7; End = 21; File = 'OdfFormulaSupportLevel.cs' }
    @{ Start = 23; End = 42; File = 'OdfFormulaDiagnosticSeverity.cs' }
    @{ Start = 44; End = 76; File = 'OdfFormulaFunctionInfo.cs' }
    @{ Start = 78; End = 117; File = 'OdfFormulaDiagnostic.cs' }
    @{ Start = 119; End = 208; File = 'OdfFormulaAnalysis.cs' }
    @{ Start = 210; End = $lineCount; File = 'OdfFormulaSupport.cs' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    if ($Text -match 'List<|Dictionary<|HashSet<|IReadOnlyList<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'AstNode|OdfFormulaAst|FormulaNode|OdfKit\.Formula\.AST') { [void]$needed.Add('OdfKit.Formula.AST') }

    $order = @('System', 'System.Collections.Generic', 'OdfKit.Formula.AST')
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-TypeFile {
    param([string]$Path, [string[]]$BodyLines, [switch]$IsCoreReplacement)
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
    $path = Join-Path $formulaDir $block.File
    Write-TypeFile -Path $path -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

Write-Host 'Done.'
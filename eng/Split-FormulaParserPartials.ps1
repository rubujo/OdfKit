#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$formulaDir = Join-Path $PSScriptRoot '..\OdfKit\Formula'
$sourcePath = Join-Path $formulaDir 'FormulaParser.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$typeBlocks = @(
    @{ Start = 8; End = 62; File = 'FormulaTokenType.cs' }
    @{ Start = 64; End = 92; File = 'FormulaParserToken.cs' }
    @{ Start = 94; End = 291; File = 'FormulaTokenizer.cs' }
    @{ Start = 293; End = $lineCount; File = 'FormulaParser.cs'; IsCore = $true }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    if ($Text -match 'List<|Dictionary<|HashSet<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'AstNode|OdfFormulaError') { [void]$needed.Add('OdfKit.Formula.AST') }
    if ($Text -match 'OdfCell|Spreadsheet') { [void]$needed.Add('OdfKit.Spreadsheet') }

    $order = @('System', 'System.Collections.Generic', 'OdfKit.Formula.AST', 'OdfKit.Spreadsheet')
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-TypeFile {
    param([string]$Path, [string[]]$BodyLines, [switch]$IsCore)
    $out = [System.Collections.Generic.List[string]]::new()
    if ($IsCore) {
        foreach ($line in $lines[0..5]) { $out.Add($line) }
        $out.Add('')
        foreach ($line in $BodyLines) { $out.Add($line) }
    }
    else {
        $text = $BodyLines -join "`n"
        foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
        $out.Add('')
        $out.Add('namespace OdfKit.Formula;')
        $out.Add('')
        foreach ($line in $BodyLines) { $out.Add($line) }
    }
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

foreach ($block in $typeBlocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    $path = Join-Path $formulaDir $block.File
    if ($block.IsCore) {
        Write-TypeFile -Path $path -BodyLines $body -IsCore
        Write-Host "Core $($block.File): $($body.Count) body lines"
    }
    else {
        Write-TypeFile -Path $path -BodyLines $body
        Write-Host "  $($block.File): $($body.Count) body lines"
    }
}

Write-Host 'Done.'